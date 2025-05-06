using UnityEngine;
using Unity.Netcode;                    
using Meta.XR.MRUtilityKit;                 // MRUK namespace

[RequireComponent(typeof(FindSpawnPositions))]
public class DroneSpawner : NetworkBehaviour
{
    [Header("Prefab & settings")]
    public GameObject dronePrefab;

    FindSpawnPositions finder;
    NetworkObject spawnedDrone;

    void Awake()
    {
        finder = GetComponent<FindSpawnPositions>();
        if (finder != null)
        {
            // Ensure only one drone is spawned and the correct prefab is used
            finder.SpawnAmount = 1;
            finder.SpawnObject = dronePrefab;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return; // Only the server handles network spawning
        // The FindSpawnPositions component will automatically attempt to spawn
        // once MRUK finishes loading the scene (via RegisterSceneLoadedCallback inside its Start method).
    }

    void Update()
    {
        if (!IsServer || spawnedDrone != null) return;

        // Find the first child instantiated by FindSpawnPositions that has a NetworkObject.
        foreach (Transform child in transform)
        {
            var netObj = child.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (!netObj.IsSpawned)
                {
                    netObj.Spawn(true); // Spawn with ownership to server
                }
                spawnedDrone = netObj; // Cache reference to avoid repeated checks
                break;
            }
        }
    }

    // No additional helper methods required; all spawn logic is handled by FindSpawnPositions.
}