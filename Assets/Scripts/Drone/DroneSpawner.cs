using UnityEngine;
using Unity.Netcode;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Solely responsible for spawning the drone in the environment.
    /// </summary>
    public class DroneSpawner : NetworkBehaviour
    {
        [Header("Drone Settings")]
        [SerializeField] private GameObject dronePrefab;
        [SerializeField] private float spawnHeight = 5f;
        [SerializeField] private float spawnXOffset = 2f;
        
        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[DroneSpawner]";
        
        // Spawned drone reference
        private GameObject spawnedDrone;

        /// <summary>
        /// Spawns the drone at a fixed position
        /// </summary>
        public void SpawnDrone()
        {
            if (!IsServer) return;
            
            LogInfo("Spawning drone...");
            
            // Calculate spawn position
            Vector3 spawnPosition = new Vector3(spawnXOffset, spawnHeight, 0f);
            
            // Spawn the drone
            spawnedDrone = Instantiate(dronePrefab, spawnPosition, Quaternion.identity);
            
            LogInfo($"Drone spawned at {spawnPosition}");
        }
        
        /// <summary>
        /// Gets the spawned drone
        /// </summary>
        public GameObject GetSpawnedDrone()
        {
            return spawnedDrone;
        }
        
        #region Logging Methods
        private void LogInfo(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"{logPrefix} {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning($"{logPrefix} {message}");
            }
        }

        private void LogError(string message)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"{logPrefix} {message}");
            }
        }
        #endregion
    }
} 