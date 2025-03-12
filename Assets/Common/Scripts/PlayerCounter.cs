using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerCounter : NetworkBehaviour
{
    public TextMeshProUGUI playerCountText;

    // Variable de rede con permisos
    private NetworkVariable<int> playerCount = new NetworkVariable<int>(
        default, 
        NetworkVariableReadPermission.Everyone, // Clientes poden ler
        NetworkVariableWritePermission.Server  // Soamente servidor pode escribir
    );
public override void OnNetworkSpawn()
{
    if (IsServer)
    {
        Debug.Log("[Servidor] Spawneando PlayerCounter...");
        GetComponent<NetworkObject>().Spawn();
        
        NetworkManager.Singleton.OnClientConnectedCallback += UpdatePlayerCount;
        NetworkManager.Singleton.OnClientDisconnectCallback += UpdatePlayerCount;
    }

    playerCount.OnValueChanged += OnPlayerCountChanged;
}

    private void UpdatePlayerCount(ulong clientId)
    {
        if (IsServer) 
        {
            playerCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
            Debug.Log($"[Servidor] Jugadores conectados: {playerCount.Value}");
        }
    }

    private void OnPlayerCountChanged(int oldValue, int newValue)
    {
        Debug.Log($"[Cliente {NetworkManager.Singleton.LocalClientId}] Cambio detectado: {oldValue} -> {newValue}");

        if (playerCountText != null)
        {
            playerCountText.text = "Jugadores conectados: " + newValue;
        }
    }

    public override void OnDestroy()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= UpdatePlayerCount;
            NetworkManager.Singleton.OnClientDisconnectCallback -= UpdatePlayerCount;
        }

        playerCount.OnValueChanged -= OnPlayerCountChanged;
    }
}
