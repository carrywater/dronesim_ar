using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Handles all navigation-related functionality for the drone.
    /// This script is responsible for navigating the drone to various destinations
    /// using NavMesh and communicating with the DroneManager for state changes.
    /// </summary>
    public class DroneNavigator : NetworkBehaviour
    {
        [Header("Navigation Settings")]
        [SerializeField] private float arrivalThreshold = 0.5f;
        [SerializeField] private float updateInterval = 0.5f;
        
        [Header("References")]
        [SerializeField] private Transform droneTransform;
        [SerializeField] private NavMeshAgent navAgent;
        
        // Reference to the DroneManager
        private DroneManager droneManager;
        
        // Navigation state
        private Vector3 currentDestination;
        private float lastUpdateTime;
        private bool isNavigating;
        
        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[DroneNavigator]";

        private void Start()
        {
            // Get components if not set
            if (droneTransform == null) droneTransform = transform;
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
            
            // Find the DroneManager
            droneManager = GetComponent<DroneManager>();
            if (droneManager == null)
            {
                LogError("DroneManager not found on the same GameObject!");
                return;
            }
            
            LogInfo($"Initialized DroneNavigator. IsServer: {IsServer}, IsClient: {IsClient}");
        }

        private void Update()
        {
            if (!IsServer) return;
            
            // Check if we've reached the destination
            if (isNavigating && navAgent != null && !navAgent.pathPending)
            {
                float distanceToTarget = Vector3.Distance(droneTransform.position, currentDestination);
                
                // Check if we've reached the destination
                if (distanceToTarget <= arrivalThreshold)
                {
                    LogInfo($"Reached destination: {currentDestination}");
                    isNavigating = false;
                    
                    // Notify the DroneManager that we've reached the destination
                    if (droneManager != null)
                    {
                        droneManager.OnDestinationReached();
                    }
                }
                
                // Periodically update the navigation
                if (Time.time - lastUpdateTime >= updateInterval)
                {
                    lastUpdateTime = Time.time;
                    UpdateNavigation();
                }
            }
        }

        /// <summary>
        /// Sets the destination for the drone to navigate to
        /// </summary>
        /// <param name="destination">The destination position</param>
        public void SetDestination(Vector3 destination)
        {
            if (!IsServer) return;
            
            LogInfo($"Setting destination to: {destination}");
            currentDestination = destination;
            isNavigating = true;
            lastUpdateTime = Time.time;
            
            UpdateNavigation();
        }

        /// <summary>
        /// Updates the navigation to the current destination
        /// </summary>
        private void UpdateNavigation()
        {
            if (navAgent != null)
            {
                navAgent.SetDestination(currentDestination);
            }
            else
            {
                LogError("NavAgent is null!");
            }
        }

        /// <summary>
        /// Stops the navigation
        /// </summary>
        public void StopNavigation()
        {
            if (!IsServer) return;
            
            LogInfo("Stopping navigation");
            isNavigating = false;
            
            if (navAgent != null)
            {
                navAgent.isStopped = true;
            }
        }

        /// <summary>
        /// Resumes the navigation
        /// </summary>
        public void ResumeNavigation()
        {
            if (!IsServer) return;
            
            LogInfo("Resuming navigation");
            isNavigating = true;
            
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                UpdateNavigation();
            }
        }

        /// <summary>
        /// Gets the current destination
        /// </summary>
        public Vector3 GetCurrentDestination()
        {
            return currentDestination;
        }

        /// <summary>
        /// Checks if the drone is currently navigating
        /// </summary>
        public bool IsNavigating()
        {
            return isNavigating;
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