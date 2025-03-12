using UnityEngine;
using Unity.Netcode;

public class PickUpObject : NetworkBehaviour
{
    public Transform holdPoint;
    private NetworkVariable<NetworkObjectReference> heldObjectRef = new NetworkVariable<NetworkObjectReference>();

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[Cliente {NetworkManager.Singleton.LocalClientId}] Pulsado E");
            
            if (heldObjectRef.Value.TryGet(out NetworkObject heldObject))
            {
                Debug.Log($"[Cliente {NetworkManager.Singleton.LocalClientId}] Intentando soltar {heldObject.name}");
                DropObjectServerRpc();
            }
            else
            {
                Debug.Log($"[Cliente {NetworkManager.Singleton.LocalClientId}] Intentando recoger objeto");
                TryPickUp();
            }
        }
    }

    private void LateUpdate()
{
    if (heldObjectRef.Value.TryGet(out NetworkObject heldObj) && holdPoint != null)
    {
        heldObj.transform.position = holdPoint.position;
        heldObj.transform.rotation = holdPoint.rotation;
    }
}

    private void TryPickUp()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.8f);
        foreach (var collider in colliders)
        {
            if (collider.CompareTag("Pickup") && collider.TryGetComponent(out NetworkObject netObj))
            {
                if (!netObj.IsOwnedByServer) // Se xa ten dono, non se pode recoller
                {
                    Debug.Log($"[DEBUG] {netObj.name} ya tiene dueño.");
                    return;
                }

                PickUpObjectServerRpc(netObj);
                break;
            }
        }
    }

[ServerRpc(RequireOwnership = false)]
private void PickUpObjectServerRpc(NetworkObjectReference netObjRef, ServerRpcParams rpcParams = default)
{
    if (netObjRef.TryGet(out NetworkObject netObj))
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (netObj.IsOwnedByServer)
        {
            netObj.ChangeOwnership(clientId);
        }

        Rigidbody rb = netObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; 
            rb.useGravity = false; 
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider pickupCollider = netObj.GetComponent<Collider>();
        Collider playerCollider = GetComponent<Collider>();

        if (pickupCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(pickupCollider, playerCollider, true);
        }

        // FixedJoint para fixar obxeto ao holdPoint
        FixedJoint joint = netObj.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = holdPoint.GetComponent<Rigidbody>();
        joint.breakForce = Mathf.Infinity; // Evitar que se solte do holdpoint
        joint.breakTorque = Mathf.Infinity;

        heldObjectRef.Value = netObj;
        MoveObjectToPlayerClientRpc(netObj.NetworkObjectId, clientId);
    }
}



    [ClientRpc]
private void MoveObjectToPlayerClientRpc(ulong objectId, ulong clientId)
{
    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
    {
        // Buscar xogador por clientId
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Transform playerTransform = client.PlayerObject.transform;
            PickUpObject playerPickUpScript = playerTransform.GetComponent<PickUpObject>();

            if (playerPickUpScript != null && playerPickUpScript.holdPoint != null)
            {
                Debug.Log($"[Cliente {clientId}] Moviendo objeto a la posición del HoldPoint del jugador {clientId}");
                
                playerPickUpScript.heldObjectRef.Value = netObj;
            }
            else
            {
                Debug.LogError($"[Cliente {clientId}] No se encontró HoldPoint en el jugador.");
            }
        }
    }
}


[ServerRpc(RequireOwnership = false)]
private void DropObjectServerRpc(ServerRpcParams rpcParams = default)
{
    ulong senderClientId = rpcParams.Receive.SenderClientId;
    Debug.Log($"[Servidor] Cliente {senderClientId} intenta soltar el objeto.");

    if (heldObjectRef.Value.TryGet(out NetworkObject netObj))
    {
        Collider pickupCollider = netObj.GetComponent<Collider>();
        Collider playerCollider = GetComponent<Collider>();

        if (pickupCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(pickupCollider, playerCollider, false);
        }

        Rigidbody rb = netObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Reactivar fisica
            rb.useGravity = true;
        }

        FixedJoint joint = netObj.GetComponent<FixedJoint>();
        if (joint != null)
        {
            Destroy(joint);
        }

        netObj.TryRemoveParent();
        netObj.transform.position = transform.position + transform.forward * 1.5f; // Soltar obxeto frente ao xogador
        netObj.RemoveOwnership();

        Debug.Log($"[Servidor] Cliente {senderClientId} ha soltado {netObj.name}");

        heldObjectRef.Value = default;
    }
}



    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (holdPoint == null)
        {
            holdPoint = transform.Find("HoldPoint");
            
            if (holdPoint == null)
            {
                Debug.LogError("[ERROR] No se encontró HoldPoint. Asegúrate de que el prefab del jugador tiene un objeto llamado 'HoldPoint'.");
            }
            else
            {
                Debug.Log($"[DEBUG] HoldPoint asignado correctamente en {gameObject.name}");
            }
        }
    }
}
