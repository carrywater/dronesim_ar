using UnityEngine;
using Unity.Netcode;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Manages visual feedback for the drone based on its state.
    /// This script is responsible for controlling lights, animations, and other visual cues
    /// that indicate the drone's current state and behavior.
    /// </summary>
    public class DroneVisuals : NetworkBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private Light statusLight;
        [SerializeField] private ParticleSystem rotorParticles;
        [SerializeField] private Animator droneAnimator;
        
        [Header("Color Settings")]
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color flyingColor = Color.blue;
        [SerializeField] private Color confirmationColor = Color.yellow;
        [SerializeField] private Color guidanceColor = Color.red;
        [SerializeField] private Color landingColor = Color.green;
        
        [Header("Animation Settings")]
        [SerializeField] private float blinkRate = 1f;
        [SerializeField] private float wobbleAmount = 0.1f;
        [SerializeField] private float wobbleSpeed = 2f;
        
        // Reference to the DroneManager
        private DroneManager droneManager;
        
        // Visual state
        private DroneState currentState;
        private float blinkTimer;
        private bool isBlinking;
        private Vector3 originalPosition;
        
        // Logging
        [SerializeField] private bool enableDebugLogging = true;
        private string logPrefix = "[DroneVisuals]";

        private void Start()
        {
            // Get components if not set
            if (statusLight == null) statusLight = GetComponentInChildren<Light>();
            if (rotorParticles == null) rotorParticles = GetComponentInChildren<ParticleSystem>();
            if (droneAnimator == null) droneAnimator = GetComponent<Animator>();
            
            // Find the DroneManager
            droneManager = GetComponent<DroneManager>();
            if (droneManager == null)
            {
                LogError("DroneManager not found on the same GameObject!");
                return;
            }
            
            // Store original position for wobble effect
            originalPosition = transform.position;
            
            // Subscribe to state changes
            droneManager.OnStateChanged += OnDroneStateChanged;
            
            LogInfo($"Initialized DroneVisuals. IsServer: {IsServer}, IsClient: {IsClient}");
        }

        private void OnDestroy()
        {
            // Unsubscribe from state changes
            if (droneManager != null)
            {
                droneManager.OnStateChanged -= OnDroneStateChanged;
            }
        }

        private void Update()
        {
            // Update visual effects based on current state
            UpdateVisualEffects();
        }

        private void OnDroneStateChanged(DroneState newState)
        {
            LogInfo($"Drone state changed to: {newState}");
            currentState = newState;
            
            // Update visuals based on new state
            UpdateVisualsForState(newState);
        }

        private void UpdateVisualsForState(DroneState state)
        {
            // Set light color based on state
            if (statusLight != null)
            {
                switch (state)
                {
                    case DroneState.Idle:
                        statusLight.color = idleColor;
                        break;
                    case DroneState.FlightToRecipient:
                    case DroneState.SessionStart:
                        statusLight.color = flyingColor;
                        break;
                    case DroneState.Landing_Confirmation:
                        statusLight.color = confirmationColor;
                        isBlinking = true;
                        break;
                    case DroneState.Landing_Guidance:
                        statusLight.color = guidanceColor;
                        isBlinking = true;
                        break;
                    case DroneState.LandingInProgress:
                    case DroneState.LandingSuccess:
                        statusLight.color = landingColor;
                        break;
                    default:
                        statusLight.color = idleColor;
                        break;
                }
            }
            
            // Control rotor particles
            if (rotorParticles != null)
            {
                if (state == DroneState.Idle)
                {
                    rotorParticles.Stop();
                }
                else
                {
                    rotorParticles.Play();
                }
            }
            
            // Trigger animations
            if (droneAnimator != null)
            {
                switch (state)
                {
                    case DroneState.Idle:
                        droneAnimator.SetTrigger("Idle");
                        break;
                    case DroneState.FlightToRecipient:
                        droneAnimator.SetTrigger("Fly");
                        break;
                    case DroneState.Landing_Confirmation:
                        droneAnimator.SetTrigger("Hover");
                        break;
                    case DroneState.Landing_Guidance:
                        droneAnimator.SetTrigger("Hover");
                        break;
                    case DroneState.LandingInProgress:
                        droneAnimator.SetTrigger("Land");
                        break;
                    case DroneState.LandingSuccess:
                        droneAnimator.SetTrigger("LandComplete");
                        break;
                }
            }
        }

        private void UpdateVisualEffects()
        {
            // Handle blinking effect
            if (isBlinking)
            {
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= blinkRate)
                {
                    blinkTimer = 0f;
                    if (statusLight != null)
                    {
                        statusLight.enabled = !statusLight.enabled;
                    }
                }
            }
            else
            {
                if (statusLight != null)
                {
                    statusLight.enabled = true;
                }
            }
            
            // Handle wobble effect for hovering
            if (currentState == DroneState.Landing_Confirmation || currentState == DroneState.Landing_Guidance)
            {
                float wobble = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount;
                transform.position = originalPosition + new Vector3(0, wobble, 0);
            }
            else
            {
                transform.position = originalPosition;
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
        #endregion
    }
} 