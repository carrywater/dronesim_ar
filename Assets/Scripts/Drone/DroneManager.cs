using Unity.Netcode;
using UnityEngine;
using System;
using UnityEngine.AI;

// Import the namespace containing our enums
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Acts as the central coordinator for drone behavior.
    /// Controls the drone's state and behaviors based on the scenario,
    /// and delegates specific tasks to specialized components.
    /// </summary>
    public class DroneManager : NetworkBehaviour
    {
        [Header("Drone Settings")]
        [SerializeField] private float confirmationTimeout = 10f;
        [SerializeField] private float guidanceTimeout = 15f;
        
        // References to specialized components
        [SerializeField] private DroneNavigator navigator;
        [SerializeField] private DroneVisuals visuals;
        
        // Networked state variables
        private NetworkVariable<DroneState> currentState = new NetworkVariable<DroneState>(
            DroneState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        private NetworkVariable<DeliveryScenario> activeScenario = new NetworkVariable<DeliveryScenario>(
            DeliveryScenario.NoDisturbance,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        // Local state
        private float stateTimer;
        private Vector3 targetLandingPosition;
        
        // Event for state changes
        public delegate void StateChangedHandler(DroneState newState);
        public event StateChangedHandler OnStateChanged;
        
        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[DroneManager]";

        private void Start()
        {
            // Get components if not set
            if (navigator == null) navigator = GetComponent<DroneNavigator>();
            if (visuals == null) visuals = GetComponent<DroneVisuals>();
            
            // Subscribe to state changes
            currentState.OnValueChanged += OnDroneStateChanged;
            activeScenario.OnValueChanged += OnScenarioChanged;
            
            LogInfo($"Initialized DroneManager. IsServer: {IsServer}, IsClient: {IsClient}");
        }

        private void OnDestroy()
        {
            // Unsubscribe from state changes
            currentState.OnValueChanged -= OnDroneStateChanged;
            activeScenario.OnValueChanged -= OnScenarioChanged;
        }

        private void Update()
        {
            if (!IsServer) return;
            
            // Handle state-specific behavior
            switch (currentState.Value)
            {
                case DroneState.FlightToRecipient:
                    HandleFlightToRecipient();
                    break;
                case DroneState.Landing_Autonomous:
                    HandleAutonomousLanding();
                    break;
                case DroneState.Landing_Confirmation:
                    HandleConfirmationLanding();
                    break;
                case DroneState.Landing_Guidance:
                    HandleGuidanceLanding();
                    break;
                case DroneState.LandingInProgress:
                    HandleLandingInProgress();
                    break;
            }
        }

        private void OnDroneStateChanged(DroneState previousValue, DroneState newValue)
        {
            LogStateTransition(previousValue, newValue);
            
            // Reset state timer when state changes
            stateTimer = 0f;
            
            // Trigger the state changed event
            OnStateChanged?.Invoke(newValue);
            
            // Handle state transitions
            switch (newValue)
            {
                case DroneState.SessionStart:
                    HandleSessionStart();
                    break;
                case DroneState.FlightToRecipient:
                    StartFlightToRecipient();
                    break;
                case DroneState.EvaluateScenario:
                    EvaluateScenario();
                    break;
                case DroneState.Landing_Autonomous:
                    LogInfo("Starting autonomous landing sequence");
                    break;
                case DroneState.Landing_Confirmation:
                    LogInfo("Waiting for user confirmation gesture");
                    break;
                case DroneState.Landing_Guidance:
                    LogInfo("Waiting for user to point to landing location");
                    break;
                case DroneState.LandingInProgress:
                    LogInfo("Landing sequence in progress");
                    break;
                case DroneState.LandingSuccess:
                    LogInfo("Landing completed successfully");
                    break;
            }
        }

        private void OnScenarioChanged(DeliveryScenario previousValue, DeliveryScenario newValue)
        {
            LogInfo($"Scenario changed from {previousValue} to {newValue}");
        }

        private void HandleSessionStart()
        {
            if (!IsServer) return;
            
            LogInfo("Handling session start");
            // Initialize the session
            currentState.Value = DroneState.FlightToRecipient;
        }

        private void StartFlightToRecipient()
        {
            if (!IsServer) return;
            
            LogInfo("Starting flight to recipient");
            
            // Find the recipient cube
            var recipientCube = FindObjectOfType<CubeInteraction>();
            if (recipientCube != null && recipientCube.objectType == "Recipient")
            {
                // Set the destination to the recipient's position
                if (navigator != null)
                {
                    navigator.SetDestination(recipientCube.transform.position);
                }
                else
                {
                    LogError("DroneNavigator not found!");
                }
            }
            else
            {
                LogError("Recipient cube not found!");
            }
        }

        private void HandleFlightToRecipient()
        {
            // The DroneNavigator will notify us when we've reached the destination
            // via the OnDestinationReached method
        }

        private void HandleAutonomousLanding()
        {
            // Autonomous landing doesn't require user input
            // Just proceed to landing
            currentState.Value = DroneState.LandingInProgress;
        }

        private void HandleConfirmationLanding()
        {
            // Wait for user confirmation
            stateTimer += Time.deltaTime;
            
            // If timeout, proceed with landing anyway
            if (stateTimer >= confirmationTimeout)
            {
                LogWarning("Confirmation timeout, proceeding with landing");
                currentState.Value = DroneState.LandingInProgress;
            }
        }

        private void HandleGuidanceLanding()
        {
            // Wait for user to point to landing location
            stateTimer += Time.deltaTime;
            
            // If timeout, use current position as landing spot
            if (stateTimer >= guidanceTimeout)
            {
                LogWarning("Guidance timeout, using current position as landing spot");
                targetLandingPosition = transform.position;
                currentState.Value = DroneState.LandingInProgress;
            }
        }

        private void HandleLandingInProgress()
        {
            // Move towards landing position
            if (navigator != null)
            {
                navigator.SetDestination(targetLandingPosition);
            }
            else
            {
                LogError("DroneNavigator not found!");
            }
        }

        /// <summary>
        /// Called when the drone has reached its destination
        /// </summary>
        public void OnDestinationReached()
        {
            if (!IsServer) return;
            
            LogInfo("Destination reached");
            
            // Check the current state to determine what to do next
            switch (currentState.Value)
            {
                case DroneState.FlightToRecipient:
                    // We've reached the recipient, evaluate the scenario
                    currentState.Value = DroneState.EvaluateScenario;
                    break;
                case DroneState.LandingInProgress:
                    // We've reached the landing position
                    currentState.Value = DroneState.LandingSuccess;
                    break;
            }
        }

        /// <summary>
        /// Initiates a new delivery with the specified scenario
        /// </summary>
        /// <param name="scenario">The delivery scenario to use</param>
        public void StartDelivery(DeliveryScenario scenario)
        {
            if (!IsServer) return;
            
            LogInfo($"Starting delivery with scenario: {scenario}");
            activeScenario.Value = scenario;
            currentState.Value = DroneState.SessionStart;
        }

        /// <summary>
        /// Confirms the landing when in Landing_Confirmation state
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ConfirmLandingServerRpc()
        {
            if (!IsServer) return;
            
            LogInfo("Landing confirmation received");
            if (currentState.Value == DroneState.Landing_Confirmation)
            {
                currentState.Value = DroneState.LandingInProgress;
            }
            else
            {
                LogWarning($"Received landing confirmation while in state: {currentState.Value}");
            }
        }

        /// <summary>
        /// Sets the landing point when in Landing_Guidance state
        /// </summary>
        /// <param name="position">The target landing position</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetLandingPointServerRpc(Vector3 position)
        {
            if (!IsServer) return;
            
            LogInfo($"Landing point set to: {position}");
            if (currentState.Value == DroneState.Landing_Guidance)
            {
                targetLandingPosition = position;
                currentState.Value = DroneState.LandingInProgress;
            }
            else
            {
                LogWarning($"Received landing point while in state: {currentState.Value}");
            }
        }

        /// <summary>
        /// Called when the scenario changes
        /// </summary>
        /// <param name="newScenario">The new scenario</param>
        public void OnScenarioChanged(DeliveryScenario newScenario)
        {
            if (!IsServer) return;
            
            LogInfo($"Scenario changed to: {newScenario}");
            activeScenario.Value = newScenario;
        }

        /// <summary>
        /// Gets the current state of the drone
        /// </summary>
        public DroneState GetCurrentState()
        {
            return currentState.Value;
        }

        /// <summary>
        /// Gets the current scenario
        /// </summary>
        public DeliveryScenario GetCurrentScenario()
        {
            return activeScenario.Value;
        }

        private void EvaluateScenario()
        {
            if (!IsServer) return;

            LogInfo($"Evaluating scenario: {activeScenario.Value}");

            // Determine landing behavior based on current scenario
            switch (activeScenario.Value)
            {
                case DeliveryScenario.NoDisturbance:
                    LogInfo("Selected autonomous landing (no disturbance)");
                    currentState.Value = DroneState.Landing_Autonomous;
                    break;
                case DeliveryScenario.ModerateDisturbance:
                    LogInfo("Selected confirmation landing (moderate disturbance)");
                    currentState.Value = DroneState.Landing_Confirmation;
                    break;
                case DeliveryScenario.MajorDisturbance:
                    LogInfo("Selected guidance landing (major disturbance)");
                    currentState.Value = DroneState.Landing_Guidance;
                    break;
            }
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

        private void LogStateTransition(DroneState fromState, DroneState toState)
        {
            if (enableDebugLogging)
            {
                string clientInfo = IsServer ? "[Server]" : IsClient ? "[Client]" : "[Local]";
                Debug.Log($"{logPrefix} {clientInfo} State transition: {fromState} → {toState}");
            }
        }
        #endregion
    }
} 