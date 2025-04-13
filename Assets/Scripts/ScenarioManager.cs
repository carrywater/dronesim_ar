using UnityEngine;
using Unity.Netcode;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Manages the selection and transitions of delivery scenarios.
    /// This script is responsible for randomly selecting a scenario when both cubes are picked up
    /// and handling the scenario transitions.
    /// </summary>
    public class ScenarioManager : NetworkBehaviour
    {
        // Networked scenario variable
        private NetworkVariable<DeliveryScenario> activeScenario = new NetworkVariable<DeliveryScenario>(
            DeliveryScenario.NoDisturbance,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Reference to the DroneManager
        private DroneManager droneManager;

        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[ScenarioManager]";

        private void Start()
        {
            // Find the DroneManager in the scene
            droneManager = FindObjectOfType<DroneManager>();
            if (droneManager == null)
            {
                Debug.LogError($"{logPrefix} DroneManager not found in scene!");
                return;
            }

            // Subscribe to scenario changes
            activeScenario.OnValueChanged += OnScenarioChanged;
            
            LogInfo($"Initialized ScenarioManager. IsServer: {IsServer}, IsClient: {IsClient}");
        }

        private void OnDestroy()
        {
            // Unsubscribe from scenario changes
            activeScenario.OnValueChanged -= OnScenarioChanged;
        }

        private void OnScenarioChanged(DeliveryScenario previousValue, DeliveryScenario newValue)
        {
            LogInfo($"Scenario changed from {previousValue} to {newValue}");
            
            // Notify the DroneManager of the scenario change
            if (droneManager != null)
            {
                droneManager.OnScenarioChanged(newValue);
            }
        }

        /// <summary>
        /// Selects a random scenario and triggers the delivery
        /// </summary>
        public void SelectRandomScenario()
        {
            if (!IsServer) return;
            
            // Select a random scenario
            DeliveryScenario[] scenarios = System.Enum.GetValues(typeof(DeliveryScenario)) as DeliveryScenario[];
            DeliveryScenario randomScenario = scenarios[Random.Range(0, scenarios.Length)];
            
            LogInfo($"Selected random scenario: {randomScenario}");
            
            // Set the active scenario
            activeScenario.Value = randomScenario;
            
            // Start the delivery with the selected scenario
            droneManager.StartDelivery(randomScenario);
        }

        /// <summary>
        /// Gets the current active scenario
        /// </summary>
        public DeliveryScenario GetActiveScenario()
        {
            return activeScenario.Value;
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