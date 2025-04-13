using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Solely responsible for spawning the drone in the environment.
    /// This script finds a suitable spawn location using AR surface detection
    /// and instantiates the drone prefab at that location.
    /// </summary>
    public class DroneSpawner : NetworkBehaviour
    {
        [Header("Drone Settings")]
        [SerializeField] private GameObject dronePrefab;
        [SerializeField] private float spawnHeight = 5f;
        [SerializeField] private float navMeshCheckRadius = 1f;
        [SerializeField] private LayerMask surfaceLayer;
        
        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[DroneSpawner]";
        
        // Spawned drone reference
        private GameObject spawnedDrone;

        /// <summary>
        /// Spawns the drone above the largest surface in the environment
        /// </summary>
        public void SpawnDrone()
        {
            if (!IsServer) return;
            
            LogInfo("Spawning drone...");
            
            // Find the largest surface using AR surface detection
            Vector3 spawnPosition = FindLargestSurface();
            
            // Adjust spawn position to be above the surface
            spawnPosition.y += spawnHeight;
            
            // Check if the position is on the NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPosition, out hit, navMeshCheckRadius, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }
            else
            {
                LogWarning("Could not find valid NavMesh position, using original position");
            }
            
            // Spawn the drone
            spawnedDrone = Instantiate(dronePrefab, spawnPosition, Quaternion.identity);
            
            LogInfo($"Drone spawned at {spawnPosition}");
        }
        
        /// <summary>
        /// Finds the largest surface in the environment using AR surface detection
        /// </summary>
        private Vector3 FindLargestSurface()
        {
            // This is a placeholder for actual AR surface detection
            // In a real implementation, you would use Meta SDK or AR Foundation
            // to find the largest surface in the environment
            
            // For now, we'll just use a raycast to find a surface
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.transform.position, Vector3.down, out hit, 100f, surfaceLayer))
            {
                return hit.point;
            }
            
            // If no surface is found, use a default position
            LogWarning("No surface found, using default position");
            return new Vector3(0, 0, 0);
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