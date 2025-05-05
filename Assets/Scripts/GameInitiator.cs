using UnityEngine;
using Unity.Netcode;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Handles the initialization of the game when both cubes are picked up.
    /// This script is responsible for starting the game sequence and managing the cubes.
    /// </summary>
    public class GameInitiator : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CubeInteraction recipientCube;
        [SerializeField] private CubeInteraction bystanderCube;
        [SerializeField] private DroneSpawner droneSpawner;
        
        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[GameInitiator]";

        private void Start()
        {
            if (!IsServer) return;

            if (droneSpawner == null)
            {
                Debug.LogError("[GameInitiator] DroneSpawner not assigned!");
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Check if both cubes are picked up
            if (recipientCube != null && bystanderCube != null &&
                recipientCube.IsCurrentlyPickedUp() && bystanderCube.IsCurrentlyPickedUp())
            {
                StartGame();
            }
        }

        private void StartGame()
        {
            if (!IsServer) return;

            LogInfo("Starting game...");

            // Hide the cubes
            recipientCube.gameObject.SetActive(false);
            bystanderCube.gameObject.SetActive(false);

            // Spawn the drone
            droneSpawner.SpawnDrone();

            LogInfo("Game started successfully");
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