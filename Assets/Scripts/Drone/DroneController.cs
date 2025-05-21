using UnityEngine;
using System;
using System.Collections;
using System.Linq;

/// <summary>
/// Core drone controller that manages both flight states and movement execution.
/// 
/// Responsibilities:
/// - Manage flight states (Idle, Hover, Cruise, Landing, Abort)
/// - Execute and track movement
/// - Provide state change events
/// - Validate state transitions
/// 
/// Rules of Use:
/// - Only use public transition methods (TransitionTo*)
/// - Subscribe to events for reliable notification
/// - Initialize before use with Configure method
/// </summary>
public class DroneController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _hoverSpeed = 2f;
    [Range(0.1f, 5f)]
    [SerializeField] private float _cruiseSpeed = 3f;
    [Range(0.1f, 5f)]
    [SerializeField] private float _landingSpeed = 2f;
    [Range(0.1f, 5f)]
    [SerializeField] private float _abortSpeed = 4f;
    
    [Header("Height Settings")]
    [Range(1f, 10f)]
    [SerializeField] private float _hoverHeight = 6f;
    [Range(5f, 15f)]
    [SerializeField] private float _abortHeight = 8f;
    
    [Header("Movement Smoothing")]
    [Range(0.1f, 2f)]
    [SerializeField] private float _accelerationTime = 0.5f;
    [Range(0.1f, 2f)]
    [SerializeField] private float _decelerationTime = 0.8f;
    [Range(0.05f, 0.5f)]
    [SerializeField] private float _arrivalThreshold = 0.1f;
    [Range(0.05f, 0.5f)]
    [SerializeField] private float _speedThreshold = 0.1f;

    [Header("References")]
    [SerializeField] private Transform _droneOffset;

    #region State Management
    
    /// <summary>
    /// Flight state enum defining all possible drone states
    /// </summary>
    public enum FlightState 
    { 
        Idle,           // Motors off, on ground
        Spawning,       // Initial positioning
        Hover,          // Steady hover at hover height
        Cruising,       // Moving horizontally
        Descending,     // Moving downward for landing
        LandingAbort,   // Returning to hover after landing abort
        Aborting,       // Emergency abort (rising and despawning)
        Despawning      // Final cleanup before destruction
    }
    
    // Current state
    private FlightState _currentState = FlightState.Idle;
    public FlightState CurrentState => _currentState;
    
    // State transition events
    public event Action<FlightState> OnStateEnter;
    public event Action<FlightState> OnStateExit;
    public event Action<FlightState> OnStateComplete;
    
    // Component events
    public event Action<bool> OnTrackingStateChanged;
    public event Action<bool> OnStabilizationStateChanged;
    
    // Movement state
    private Vector3 _targetPosition;
    private Vector3 _currentVelocity = Vector3.zero;
    private bool _isMoving = false;
    private bool _isInitialized = false;
    
    #endregion
    
    #region Unity Lifecycle Methods
    
    private void Awake()
    {
        ValidateComponents();
    }
    
    private void Update()
    {
        if (!_isMoving || _droneOffset == null) return;
        
        // Execute movement based on target position
        UpdateMovement();
        
        // Check if we've reached the target
        CheckMovementCompletion();
    }
    
    private void ValidateComponents()
    {
        if (_droneOffset == null && transform.childCount > 0)
        {
            _droneOffset = transform.GetChild(0);
            Debug.Log("Auto-assigned drone offset to first child");
        }
        
        if (_droneOffset == null)
        {
            Debug.LogError("DroneController requires a drone offset transform!");
        }
    }
    
    #endregion
    
    #region Configuration
    
    /// <summary>
    /// Configure the drone with essential flight parameters
    /// </summary>
    public void Configure(float hoverHeight, float hoverSpeed, float cruiseSpeed, 
                          float abortHeight, float landingSpeed, float abortSpeed,
                          float accelerationTime, float decelerationTime)
    {
        _hoverHeight = hoverHeight;
        _hoverSpeed = hoverSpeed;
        _cruiseSpeed = cruiseSpeed;
        _abortHeight = abortHeight;
        _landingSpeed = landingSpeed;
        _abortSpeed = abortSpeed;
        _accelerationTime = accelerationTime;
        _decelerationTime = decelerationTime;
        
        _isInitialized = true;
        Debug.Log("DroneController: Configured successfully");
    }
    
    #endregion
    
    #region State Transition Methods

    /// <summary>
    /// Start drone in idle state at the specified position
    /// </summary>
    public void SpawnAt(Vector3 position)
    {
        if (!_isInitialized)
        {
            Debug.LogError("DroneController: Cannot spawn drone - not initialized!");
            return;
        }
        
        if (_isMoving)
        {
            Debug.LogWarning("DroneController: Cannot spawn while moving");
            return;
        }
        
        // Set initial position
        SetPosition(position);
        
        // Transition to Spawning state
        TransitionToState(FlightState.Spawning);
    }

    /// <summary>
    /// Transition to hover state
    /// </summary>
    public void TransitionToHover()
    {
        if (!IsValidTransition(FlightState.Hover))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Hover");
            return;
        }
        
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        
        TransitionToState(FlightState.Hover);
        StartMovement();
    }
    
    /// <summary>
    /// Start cruising to a target position
    /// </summary>
    public void TransitionToCruise(Vector3 targetPosition)
    {
        if (!IsValidTransition(FlightState.Cruising))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Cruising");
            return;
        }
        
        Vector3 localPosition = transform.InverseTransformPoint(targetPosition);
        _targetPosition = new Vector3(localPosition.x, _hoverHeight, localPosition.z);
        
        TransitionToState(FlightState.Cruising);
        StartMovement();
    }

    /// <summary>
    /// Start descending to land at the target position
    /// </summary>
    public void TransitionToLanding(Vector3 landingPosition)
    {
        if (!IsValidTransition(FlightState.Descending))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Descending");
            return;
        }
        
        Vector3 localPosition = transform.InverseTransformPoint(landingPosition);
        // First align horizontally with landing spot
        Vector3 horizontalTarget = new Vector3(localPosition.x, _droneOffset.localPosition.y, localPosition.z);
        _targetPosition = horizontalTarget;
        
        TransitionToState(FlightState.Descending);
        StartMovement();
        
        // When horizontal alignment is complete, begin descent
        StartCoroutine(SequentialMovement(
            () => IsAtTargetPosition(horizontalTarget, 0.1f), 
            () => 
            {
                _targetPosition = new Vector3(localPosition.x, 0.1f, localPosition.z);
                StartMovement();
            }
        ));
    }

    /// <summary>
    /// Abort landing and return to hover
    /// </summary>
    public void TransitionToLandingAbort()
    {
        if (!IsValidTransition(FlightState.LandingAbort))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to LandingAbort");
            return;
        }
        
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        
        TransitionToState(FlightState.LandingAbort);
        StartMovement();
    }

    /// <summary>
    /// Begin mission abort sequence
    /// </summary>
    public void TransitionToAbort()
    {
        if (!IsValidTransition(FlightState.Aborting))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Aborting");
            return;
        }
        
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _abortHeight, currentPos.z);
        
        TransitionToState(FlightState.Aborting);
        StartMovement();
    }

    /// <summary>
    /// Begin despawning sequence
    /// </summary>
    public void TransitionToDespawn()
    {
        if (!IsValidTransition(FlightState.Despawning))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Despawning");
            return;
        }
        
        TransitionToState(FlightState.Despawning);
        StartCoroutine(DespawnSequence());
    }
    
    #endregion
    
    #region Movement Methods
    
    /// <summary>
    /// Set the drone's position immediately (for initialization)
    /// </summary>
    public void SetPosition(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        _droneOffset.localPosition = localPosition;
        _targetPosition = localPosition;
        _currentVelocity = Vector3.zero;
        _isMoving = false;
    }
    
    /// <summary>
    /// Stop all movement immediately
    /// </summary>
    public void StopMovement()
    {
        _isMoving = false;
        _currentVelocity = Vector3.zero;
    }
    
    /// <summary>
    /// Check if we have arrived at the target position and are no longer moving
    /// </summary>
    public bool IsMovementComplete()
    {
        return !_isMoving;
    }
    
    /// <summary>
    /// Check if we're at the target position
    /// </summary>
    public bool IsAtTargetPosition(Vector3 target, float threshold = 0.1f)
    {
        if (_droneOffset == null) return false;
        return Vector3.Distance(_droneOffset.localPosition, target) < threshold;
    }
    
    /// <summary>
    /// Get the current local position
    /// </summary>
    public Vector3 GetCurrentPosition()
    {
        return _droneOffset != null ? _droneOffset.localPosition : Vector3.zero;
    }

    /// <summary>
    /// Get the current movement velocity
    /// </summary>
    public Vector3 GetCurrentVelocity()
    {
        return _currentVelocity;
    }
    
    #endregion
    
    #region Private Methods
    
    private void TransitionToState(FlightState newState)
    {
        // Exit current state
        OnStateExit?.Invoke(_currentState);
        
        // Update state
        FlightState oldState = _currentState;
        _currentState = newState;
        
        // Enter new state
        OnStateEnter?.Invoke(newState);
        
        Debug.Log($"DroneController: Transitioned from {oldState} to {newState}");
    }
    
    private bool IsValidTransition(FlightState targetState)
    {
        if (!_isInitialized)
        {
            Debug.LogError("DroneController: Cannot transition - not initialized!");
            return false;
        }
        
        if (_isMoving && targetState != FlightState.Aborting) // Aborting can interrupt
        {
            Debug.LogWarning("DroneController: Cannot transition while moving");
            return false;
        }
        
        // State-specific validation
        switch (_currentState)
        {
            case FlightState.Idle:
                return targetState == FlightState.Spawning;
                
            case FlightState.Spawning:
                return targetState == FlightState.Hover || targetState == FlightState.Aborting;
                
            case FlightState.Hover:
                return targetState == FlightState.Cruising || 
                       targetState == FlightState.Descending || 
                       targetState == FlightState.Aborting;
                
            case FlightState.Cruising:
                return targetState == FlightState.Hover || 
                       targetState == FlightState.Descending || 
                       targetState == FlightState.Aborting;
                
            case FlightState.Descending:
                return targetState == FlightState.LandingAbort || 
                       targetState == FlightState.Idle || 
                       targetState == FlightState.Aborting;
                
            case FlightState.LandingAbort:
                return targetState == FlightState.Hover || 
                       targetState == FlightState.Aborting;
                
            case FlightState.Aborting:
                return targetState == FlightState.Despawning;
                
            case FlightState.Despawning:
                return false; // No valid transitions from despawning
                
            default:
                return false;
        }
    }
    
    private void StartMovement()
    {
        _isMoving = true;
        
        // Calculate smooth time based on movement type and distance
        float distance = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        float speed = GetSpeedForState(_currentState);
        float smoothTime = CalculateSmoothTime(distance, speed);
        
        Debug.Log($"Starting movement to {_targetPosition}, distance: {distance:F2}m, speed: {speed:F2}m/s, smooth time: {smoothTime:F2}s");
    }
    
    private void UpdateMovement()
    {
        // Calculate smooth time based on current parameters
        float smoothTime = CalculateSmoothTime(
            Vector3.Distance(_droneOffset.localPosition, _targetPosition),
            GetSpeedForState(_currentState)
        );
        
        // Update position with smoothing
        _droneOffset.localPosition = Vector3.SmoothDamp(
            _droneOffset.localPosition,
            _targetPosition,
            ref _currentVelocity,
            smoothTime
        );
    }
    
    private void CheckMovementCompletion()
    {
        // Calculate distance to target and current speed
        float distanceToTarget = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        float currentSpeed = _currentVelocity.magnitude;
        
        // Consider movement complete if we're close enough AND moving slowly enough
        if (distanceToTarget <= _arrivalThreshold && currentSpeed <= _speedThreshold)
        {
            _isMoving = false;
            OnStateComplete?.Invoke(_currentState);
            
            // Handle state-specific completion
            switch (_currentState)
            {
                case FlightState.Spawning:
                    TransitionToState(FlightState.Hover);
                    break;
                    
                case FlightState.Cruising:
                    TransitionToState(FlightState.Hover);
                    break;
                    
                case FlightState.Descending:
                    TransitionToState(FlightState.Idle);
                    break;
                    
                case FlightState.LandingAbort:
                    TransitionToState(FlightState.Hover);
                    break;
                    
                case FlightState.Aborting:
                    TransitionToState(FlightState.Despawning);
                    break;
            }
            
            Debug.Log($"Movement complete: {_currentState}, position: {_droneOffset.localPosition:F2}");
        }
    }
    
    private float CalculateSmoothTime(float distance, float speed)
    {
        // Base smooth time calculation
        float timeToTarget = distance / speed;
        
        // Different states need different smoothing characteristics
        switch (_currentState)
        {
            case FlightState.Hover:
            case FlightState.Aborting:
                // Vertical movements use acceleration time
                return Mathf.Max(0.1f, timeToTarget * _accelerationTime);
            
            case FlightState.Cruising:
                // Horizontal movements need more precision near the target
                return Mathf.Max(0.1f, timeToTarget * _decelerationTime);
            
            case FlightState.Descending:
                // Landing needs to be extra smooth and careful
                return Mathf.Max(0.2f, timeToTarget * (_decelerationTime * 1.5f));
            
            default:
                return 0.5f;
        }
    }
    
    private float GetSpeedForState(FlightState state)
    {
        switch (state)
        {
            case FlightState.Hover: return _hoverSpeed;
            case FlightState.Cruising: return _cruiseSpeed;
            case FlightState.Descending: return _landingSpeed;
            case FlightState.Aborting: return _abortSpeed;
            default: return 1.0f;
        }
    }
    
    private IEnumerator SequentialMovement(Func<bool> completionCheck, Action nextAction)
    {
        // Wait for the first movement to complete
        yield return new WaitUntil(completionCheck);
        
        // Execute the next action
        nextAction?.Invoke();
    }
    
    private IEnumerator DespawnSequence()
    {
        // Wait a moment before destroying
        yield return new WaitForSeconds(0.5f);
        
        // Destroy the drone
        Destroy(gameObject);
    }
    
    #endregion
}

[System.Serializable]
public class LegConfig
{
    [Tooltip("Enable this leg for animation")] public bool enabled = false;
    public Transform legTransform;
    [Tooltip("Which axis to rotate around (X = Red, Y = Green, Z = Blue)")]
    public enum RotationAxis { X, Y, Z }
    public RotationAxis rotationAxis = RotationAxis.X;
    [Tooltip("Angle to rotate when extended (positive = clockwise, negative = counter-clockwise)")]
    [Range(-90f, 90f)]
    public float extendedAngle = -30f;
    [Tooltip("Whether to invert the rotation direction")]
    public bool invertDirection = false;
    public Vector3 GetRotationAxisVector()
    {
        switch (rotationAxis)
        {
            case RotationAxis.X: return Vector3.right;
            case RotationAxis.Y: return Vector3.up;
            case RotationAxis.Z: return Vector3.forward;
            default: return Vector3.right;
        }
    }
}