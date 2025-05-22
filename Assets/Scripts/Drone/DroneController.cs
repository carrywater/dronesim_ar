using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using Utils;

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

    [Header("References")]
    [SerializeField] private Transform _droneOffset;

    #region State Management
    
    /// <summary>
    /// Flight state enum defining all possible drone states
    /// </summary>
    public enum FlightState 
    { 
        Idle,
        Spawning,
        Hover,
        HoveringAtPosition,
        Cruising,
        Descending,
        LandingAbort,
        Aborting,
        Despawning
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
        // Check for movement taking too long
        if (!_moveTakingLong && (Time.time - _moveStartTime) > (_expectedMoveTime * 1.5f))
        {
            _moveTakingLong = true;
            Debug.LogWarning($"[DroneController] Movement taking longer than expected! State={_currentState}, elapsed={Time.time - _moveStartTime:F2}s, expected={_expectedMoveTime:F2}s");
        }
        Debug.Log($"[DroneController] Update: state={_currentState}, _droneOffset.localPosition={_droneOffset.localPosition}, _targetPosition={_targetPosition}, _isMoving={_isMoving}");
        UpdateMovement();
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
    public void SpawnAt(Vector3 worldPosition, float initialOffsetY)
    {
        Debug.Log($"[DroneController] SpawnAt CALLED: worldPosition={worldPosition}, initialOffsetY={initialOffsetY}, isInitialized={_isInitialized}, isMoving={_isMoving}");
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
        // Set root position (X, 0, Z)
        transform.position = new Vector3(worldPosition.x, 0f, worldPosition.z);
        Debug.Log($"[DroneController] SpawnAt: root pos = {transform.position}");
        // Set offset Y
        if (_droneOffset != null)
        {
            var local = _droneOffset.localPosition;
            local.y = initialOffsetY;
            _droneOffset.localPosition = local;
            Debug.Log($"[DroneController] SpawnAt: offset Y = {_droneOffset.localPosition.y}");
        }
        else
        {
            Debug.LogError("[DroneController] SpawnAt: _droneOffset is null!");
        }
        // Transition to Spawning state
        Debug.Log($"[DroneController] SpawnAt: Transitioning to Spawning");
        TransitionToState(FlightState.Spawning);
    }

    /// <summary>
    /// Transition to hover state
    /// </summary>
    public void TransitionToHover()
    {
        Debug.Log($"[DroneController] TransitionToHover CALLED: currentState={_currentState}, hoverHeight={_hoverHeight}");
        if (!IsValidTransition(FlightState.Hover))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Hover");
            return;
        }
        Vector3 currentLocal = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentLocal.x, _hoverHeight, currentLocal.z);
        Debug.Log($"[DroneController] TransitionToHover: targetPosition={_targetPosition}, currentLocal={currentLocal}");
        TransitionToState(FlightState.Hover);
        StartMovement();
    }

    /// <summary>
    /// Start cruising to a target position
    /// </summary>
    public void TransitionToCruise(Vector3 targetPosition)
    {
        Debug.Log($"[DroneController] TransitionToCruise CALLED: currentState={_currentState}, targetPosition={targetPosition}");
        if (!IsValidTransition(FlightState.Cruising))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Cruising");
            return;
        }
        Vector3 localPosition = transform.InverseTransformPoint(targetPosition);
        _targetPosition = new Vector3(localPosition.x, _hoverHeight, localPosition.z);
        Debug.Log($"[DroneController] TransitionToCruise: _targetPosition={_targetPosition}, localPosition={localPosition}");
        TransitionToState(FlightState.Cruising);
        StartMovement();
    }

    /// <summary>
    /// Start descending to land at the target position
    /// </summary>
    public void TransitionToLanding(Vector3 landingPosition)
    {
        Debug.Log($"[DroneController] TransitionToLanding CALLED: currentState={_currentState}, landingPosition={landingPosition}");
        if (!IsValidTransition(FlightState.Descending))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Descending");
            return;
        }
        Vector3 localPosition = transform.InverseTransformPoint(landingPosition);
        Vector3 horizontalTarget = new Vector3(localPosition.x, _droneOffset.localPosition.y, localPosition.z);
            _targetPosition = horizontalTarget;
        Debug.Log($"[DroneController] TransitionToLanding: horizontalTarget={horizontalTarget}, localPosition={localPosition}");
        TransitionToState(FlightState.Descending);
        StartMovement();
        var horizontalCheck = new PositionValidator.AxisCheck { checkY = false, threshold = 0.05f };
        // If already at horizontal target, start descent immediately
        if (PositionValidator.IsAtPosition(_droneOffset, horizontalTarget, horizontalCheck))
        {
            _targetPosition = new Vector3(localPosition.x, 0.1f, localPosition.z);
            Debug.Log($"[DroneController] TransitionToLanding: Already aligned, begin descent to {_targetPosition}");
            StartMovement();
        }
        else
        {
            StartCoroutine(SequentialMovement(
                () => PositionValidator.IsAtPosition(_droneOffset, horizontalTarget, horizontalCheck),
                () =>
                {
                    _targetPosition = new Vector3(localPosition.x, 0.1f, localPosition.z);
                    Debug.Log($"[DroneController] TransitionToLanding: Begin descent to {_targetPosition}");
                    StartMovement();
                }
            ));
        }
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
        Debug.Log($"[DroneController] TransitionToAbort CALLED: currentState={_currentState}, abortHeight={_abortHeight}");
        if (!IsValidTransition(FlightState.Aborting))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to Aborting");
            return;
        }
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _abortHeight, currentPos.z);
        Debug.Log($"[DroneController] TransitionToAbort: _targetPosition={_targetPosition}, currentPos={currentPos}");
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
    /// Stop all movement immediately
    /// </summary>
    public void StopMovement()
    {
        _isMoving = false;
        _currentVelocity = Vector3.zero;
    }

    /// <summary>
    /// Check if movement is complete
    /// </summary>
    public bool IsMovementComplete()
    {
        return !_isMoving;
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
                       targetState == FlightState.HoveringAtPosition ||
                       targetState == FlightState.Descending || 
                       targetState == FlightState.Aborting;
            case FlightState.HoveringAtPosition:
                return targetState == FlightState.Hover ||
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
    
    private float _moveStartTime = 0f;
    private float _expectedMoveTime = 0f;
    private bool _moveTakingLong = false;

    private void StartMovement()
    {
        _isMoving = true;
        float distance = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        float speed = GetSpeedForState(_currentState);
        float smoothTime = CalculateSmoothTime(distance, speed);
        _moveStartTime = Time.time;
        _expectedMoveTime = distance / Mathf.Max(speed, 0.01f); // avoid div by zero
        _moveTakingLong = false;
        Debug.Log($"[DroneController] StartMovement: state={_currentState}, from={_droneOffset.localPosition}, to={_targetPosition}, distance={distance}, speed={speed}, smoothTime={smoothTime}, isMoving={_isMoving}");
    }
    
    private void UpdateMovement()
    {
        // Use a fixed, realistic smoothTime for vertical moves (Hover/Abort), e.g., 0.6s
        float smoothTime = 0.6f;
        if (_currentState == FlightState.Cruising)
            smoothTime = 0.4f; // horizontal moves can be a bit snappier
        else if (_currentState == FlightState.Descending)
            smoothTime = 0.8f; // landing is slower

        float snapThreshold = 0.07f; // 7cm, realistic for a drone
        float stopVelocity = 0.03f;  // 3cm/s, realistic for a drone
        float dist = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        if (dist < snapThreshold && _currentVelocity.magnitude < stopVelocity)
        {
            _droneOffset.localPosition = _targetPosition;
            _currentVelocity = Vector3.zero;
            Debug.Log("[DroneController] UpdateMovement: Snapped to target (realistic)");
        }
        else
        {
            _droneOffset.localPosition = Vector3.SmoothDamp(
                _droneOffset.localPosition,
                _targetPosition,
                ref _currentVelocity,
                smoothTime
            );
        }
        Debug.Log($"[DroneController] UpdateMovement: new _droneOffset.localPosition={_droneOffset.localPosition}");
    }
    
    private void CheckMovementCompletion()
    {
        var axisCheck = new PositionValidator.AxisCheck();
        switch (_currentState)
        {
            case FlightState.Spawning:
                axisCheck.checkX = false;
                axisCheck.checkZ = false;
                break;
            case FlightState.Hover:
            case FlightState.Aborting:
                axisCheck.checkX = false;
                axisCheck.checkZ = false;
                break;
            case FlightState.Cruising:
                axisCheck.checkY = false;
                break;
            case FlightState.Descending:
                break;
        }
        bool atTarget = PositionValidator.IsAtPosition(_droneOffset, _targetPosition, axisCheck);
        bool stopped = _currentVelocity.magnitude < 0.02f; // Allow for subtle PID jitter
        Debug.Log($"[DroneController] CheckMovementCompletion: state={_currentState}, atTarget={atTarget}, stopped={stopped}, _droneOffset.localPosition={_droneOffset.localPosition}, _targetPosition={_targetPosition}, velocity={_currentVelocity}");
        if (atTarget && stopped)
        {
            _isMoving = false;
            OnStateComplete?.Invoke(_currentState);
            switch (_currentState)
            {
                case FlightState.Spawning:
                    Debug.Log("[DroneController] CheckMovementCompletion: Transitioning to Hover");
        TransitionToState(FlightState.Hover);
                    break;
                case FlightState.Cruising:
                    Debug.Log("[DroneController] CheckMovementCompletion: Transitioning to Hover");
                    TransitionToState(FlightState.Hover);
                    break;
                case FlightState.Descending:
                    Debug.Log("[DroneController] CheckMovementCompletion: Transitioning to Idle");
                    TransitionToState(FlightState.Idle);
                    break;
                case FlightState.LandingAbort:
                    Debug.Log("[DroneController] CheckMovementCompletion: Transitioning to Hover");
                    TransitionToState(FlightState.Hover);
                    break;
                case FlightState.Aborting:
                    Debug.Log("[DroneController] CheckMovementCompletion: Transitioning to Despawning");
                    TransitionToState(FlightState.Despawning);
                    break;
            }
            Debug.Log($"[DroneController] Movement complete: {_currentState}, position: {_droneOffset.localPosition:F2}");
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

    public float AbortHeight => _abortHeight;
    public float HoverHeight => _hoverHeight;
    public float HoverSpeed => _hoverSpeed;
    public float CruiseSpeed => _cruiseSpeed;
    public float LandingSpeed => _landingSpeed;
    public float AbortSpeed => _abortSpeed;
    public float AccelerationTime => _accelerationTime;
    public float DecelerationTime => _decelerationTime;
    public bool IsInitialized => _isInitialized;

    public void MoveTo(Vector3 worldPosition, float offsetY)
    {
        Debug.Log($"[DroneController] MoveTo CALLED: worldPosition={worldPosition}, offsetY={offsetY}");
        transform.position = new Vector3(worldPosition.x, 0f, worldPosition.z);
        Debug.Log($"[DroneController] MoveTo: root pos = {transform.position}");
        if (_droneOffset != null)
        {
            var local = _droneOffset.localPosition;
            local.y = offsetY;
            _droneOffset.localPosition = local;
            Debug.Log($"[DroneController] MoveTo: offset Y = {_droneOffset.localPosition.y}");
        }
        else
        {
            Debug.LogError("[DroneController] MoveTo: _droneOffset is null!");
        }
    }

    // Add this property for clean access to the offset
    public Transform DroneOffset => _droneOffset;
    
    /// <summary>
    /// Hover at a specific position for a duration
    /// </summary>
    public IEnumerator HoverAtPosition(Vector3 position, float duration)
    {
        Debug.Log($"[DroneController] HoverAtPosition CALLED: position={position}, duration={duration}");
        if (!IsValidTransition(FlightState.HoveringAtPosition))
        {
            Debug.LogWarning($"DroneController: Invalid transition from {_currentState} to HoveringAtPosition");
            yield break;
        }

        // First cruise to the position
        Vector3 localPosition = transform.InverseTransformPoint(position);
        _targetPosition = new Vector3(localPosition.x, _hoverHeight, localPosition.z);
        TransitionToState(FlightState.Cruising);
        StartMovement();

        // Wait until we reach the position
        var horizontalCheck = new PositionValidator.AxisCheck { checkY = false };
        yield return new WaitUntil(() => PositionValidator.IsAtPosition(_droneOffset, _targetPosition, horizontalCheck));

        // Now hover at this position
        TransitionToState(FlightState.HoveringAtPosition);
        
        // Wait for the specified duration
        yield return new WaitForSeconds(duration);

        // Return to normal hover state
        TransitionToState(FlightState.Hover);
    }
}
