   using UnityEngine;
   using Unity.Netcode;

   public class PanelAutoNetworkSpawner : MonoBehaviour
   {
       private NetworkObject _netObj;

       private void Awake()
       {
           _netObj = GetComponent<NetworkObject>();
           // Only the Host/Server should call .Spawn() on the MRUK instance
           if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
               && _netObj != null && !_netObj.IsSpawned)
           {
               // Give ownership to the first client if you like:
               // _netObj.SpawnWithOwnership(NetworkManager.Singleton.ConnectedClientsList[0].ClientId);

               // Or just server‐authoritative:
               _netObj.Spawn();
               Debug.Log("[PanelAutoNetworkSpawner] Server spawned panel as a NetworkObject");
           }
       }
   }