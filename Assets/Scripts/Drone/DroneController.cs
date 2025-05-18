using UnityEngine;
using System;
using System.Collections;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.XR.Oculus;
#endif
using System.Linq;

// Conditionally include Oculus namespaces if available
#if USING_XR_SDK_OCULUS || USING_OCULUS_SDK
using OculusSampleFramework;
#endif

/// <summary>
/// Core flight FSM (take-off, cruise, hover, landing, abort)
/// Handles rotor & gear animation, and PID sway
/// </summary>
public class DroneController : MonoBehaviour
{
    [Header("Essential References")]
    [SerializeField] private DroneArrivalDetector _navigation;   // For arrival callbacks
    [SerializeField] private Transform _droneOffset;             // Visual model and components
    [Tooltip("Transform to track - should be the OVR HMD (head) transform")]
    [SerializeField] private Transform _hmdTransform;
    [SerializeField] private PIDController _pidController;       // Subtle random hover sway (different from directional tilting)
    [SerializeField] private DroneHMI _hmi;                      // For controlling sound effects directly

    [Header("Drone Components")]
    [SerializeField] private Transform[] _propellers;
    [SerializeField] private LegConfig[] _legConfigs;
    
    [Header("Behavior Settings")]
    [Tooltip("Enable scanning behavior in hover state")]
    [SerializeField] private bool _enableScanning = true;
    [Tooltip("Enable subtle micro-movements during hover")]
    [SerializeField] private bool _enableMicroMovements = true;
    
    [Header("HMD Tracking")]
    [SerializeField] private float _hmdTrackingSpeed = 2f;
    [SerializeField] private float _hmdTrackingDelay = 0.5f;
    [SerializeField] private float _maxHmdRotationAngle = 45f;

    #region Advanced Settings (Hidden by Default)
    // Movement Profiles - hidden from Inspector
    private AnimationCurve _cruiseMovementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private AnimationCurve _landingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private AnimationCurve _abortCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    // Rotation Settings - hidden from Inspector
    private float _rotationSpeed = 2f;
    private float _rotationStartDelay = 0.2f;

    // Tilt and Animation Settings - hidden from Inspector
    private float _scanAngleRange = 15f;
    private float _scanCycleTime = 4f;
    private float _baseTiltAngle = 2.5f;
    private float _speedTiltAngle = 1.5f;
    private float _tiltVariability = 0.15f;
    private float _tiltRecoverySpeed = 30f;
    private float _microMovementStrength = 0.12f;
    private float _pitchOscillationStrength = 0.08f;
    private float _rollOscillationStrength = 0.06f;
    private float _oscillationSpeed = 1f;
    private float _windInfluence = 0.05f;

    // Drone Physical Settings - hidden from Inspector
    private float _rotorSpeed = 36000f;  // 6000 RPM (6000 * 360 degrees per minute = 2,160,000 degrees per minute = 36,000 degrees per second)
    private float _legRetractedAngle = 30f;
    private float _legRotateSpeed = 90f;
    
    // Leg animation types - kept for reference
    private LegRotationType[] _legRotationTypes;
    #endregion
    
    #region Flight State Management
    
    // Public flight state enum as described in README
    public enum FlightState 
    { 
        Idle,           // Motors off
        Hover,          // Hold at hover height with sway
        CruiseToTarget, // NavMesh path to target 
        Landing,        // Descent to landing spot
        LandAbort,      // Climb back to hover
        Abort           // Ascend and despawn
    }
    
    // State change event
    public delegate void StateChangedHandler(FlightState newState);
    public event StateChangedHandler OnStateChanged;
    
    // Current flight state
    private FlightState _state = FlightState.Idle;
    public FlightState CurrentState => _state;
    
    #endregion
    
    #region Leg Animation Types
    
    [System.Serializable]
    public enum LegRotationType
    {
        RotateOnX,
        RotateOnY, 
        RotateOnZ,
        RotateOnNegativeX,
        RotateOnNegativeY,
        RotateOnNegativeZ
    }
    
    #endregion
    
    #region Movement Parameters
    
    // Movement settings - these will be set by the ScenarioManager
    private float _hoverHeight = 6f;            // Default hover height (can be overridden)
    private float _hoverMovementSpeed = 2f;     // Speed for vertical adjustment
    private float _cruiseSpeed = 3f;            // Horizontal movement speed
    private float _abortClimbHeight = 8f;       // How high to climb during abort
    private float _landingDescentSpeed = 2f;    // Speed of landing descent

    // Unified position control system
    private Vector3 _targetPosition;            // Target position for all movements (single source of truth)
    private Vector3 _currentVelocity = Vector3.zero; // For SmoothDamp
    private bool _positionLocked = false;       // Locks position during critical transitions
    private float _positionSmoothTime = 1.0f;   // Increased from 0.5f for smoother movement
    
    // Target positions
    private Vector3 _cruiseTargetPosition;      // World-space target for cruise
    private Transform _cruiseTargetTransform;   // Optional transform to follow
    private Vector3 _landingSpot;               // World-space landing location
    
    #endregion
    
    #region Internal State
    
    private bool _isInitialized = false;        // Whether initialization is complete
    private bool _rotorsSpinning = false;       // Whether propellers are spinning
    private bool _gearAnimating = false;        // Whether legs are currently moving
    private float _gearTargetAngle = 0f;        // Target angle for leg animation
    private Transform _transform;               // Cached transform for optimization
    
    // Movement state tracking
    private bool _isAscendingHover = false;     // Whether currently moving to hover height
    private bool _isInTransition = false;       // Only allow one transition at a time
    private float _movementProgress = 0f;       // Progress along movement curve (legacy)
    
    // Flag to track if we're calling from ReturnToHover to avoid duplicating position preservation
    private bool _calledFromReturnToHover = false;
    
    private Vector3 _targetHmdDirection;
    private float _hmdTrackingTimer;
    
    // Add new private fields
    private float _accelerationTime = 0.5f;
    private float _decelerationTime = 0.8f;
    private float _minSmoothTime = 0.5f;
    private float _maxAbortSpeed = 4f;
    
    // Add a consistent smoothing parameter for all transitions
    [SerializeField] private float _verticalTransitionSmoothTime = 0.7f; // Exposed in Inspector for tuning
    
    #if UNITY_EDITOR
    private Vector3 _lastSetPosition;
    #endif
    
    #endregion
    
    #region Initialization & Setup

    private void Awake()
    {
        // Cache transform for performance
        _transform = transform;
        
        // Initialize leg configurations
        InitializeLegConfigs();
        
        // Auto-find drone offset if not set
        if (_droneOffset == null && transform.childCount > 0)
        {
            _droneOffset = transform.GetChild(0);
            Debug.Log($"Auto-assigned drone offset to first child: {_droneOffset.name}");
        }
        
        if (_droneOffset == null)
        {
            Debug.LogError("No drone offset assigned or found! Drone visuals won't move correctly.");
        }
        
        // Reset offset position to avoid initial jump
        if (_droneOffset != null)
        {
            _droneOffset.localPosition = Vector3.zero;
#if UNITY_EDITOR
            _lastSetPosition = _droneOffset.localPosition;
#endif
        }
        
        // Log if multiple drones detected (debugging)
        if (FindObjectsByType<DroneController>(FindObjectsSortMode.None).Length > 1)
        {
            Debug.LogWarning($"Multiple drone controllers detected: {FindObjectsByType<DroneController>(FindObjectsSortMode.None).Length}");
        }
    }
    
    /// <summary>
    /// Initialize the drone with flight settings
    /// This must be called before using the drone
    /// </summary>
    public void Initialize(float hoverHeight, float hoverMovementSpeed, float cruiseSpeed, 
                          float abortClimbHeight, float landingDescentSpeed, float accelerationTime, float decelerationTime, float minSmoothTime, float maxAbortSpeed)
    {
        _hoverHeight = hoverHeight;
        _hoverMovementSpeed = hoverMovementSpeed;
        _cruiseSpeed = cruiseSpeed;
        _abortClimbHeight = abortClimbHeight;
        _landingDescentSpeed = landingDescentSpeed;
        _accelerationTime = accelerationTime;
        _decelerationTime = decelerationTime;
        _minSmoothTime = minSmoothTime;
        _maxAbortSpeed = maxAbortSpeed;
        
        // Auto-find HMI component if not assigned
        if (_hmi == null)
        {
            _hmi = GetComponent<DroneHMI>();
            if (_hmi == null)
            {
                _hmi = GetComponentInChildren<DroneHMI>();
            }
            
            if (_hmi != null)
            {
                Debug.Log("Auto-assigned DroneHMI: " + _hmi.name);
            }
        }
        
        _isInitialized = true;
        Debug.Log("DroneController initialized with flight settings");
    }
    
    /// <summary>
    /// Start the drone in hover state after initialization
    /// </summary>
    public void StartInHoverState()
    {
        if (!_isInitialized)
        {
            Debug.LogError("Cannot start drone hover - drone not initialized! Call Initialize first.");
            return;
        }
        
        // Initialize target position at hover height
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        
        // Use a smoother transition for initial hover
        _positionSmoothTime = _minSmoothTime;
        
        // Transition to hover state
        TransitionToState(FlightState.Hover);
    }
    
    #endregion
    
    #region Update Loop & State Management

    private void Update()
    {
        // Skip update if not initialized
        if (!_isInitialized) return;

        // Animate propellers if spinning
        if (_rotorsSpinning) SpinRotors();
        
        // Animate legs if in transition
        if (_gearAnimating) AnimateLegs();

        // Update position using unified control system
        UpdateUnifiedPosition();
        
        // Handle rotation to face direction or participant
        UpdateRotation();
        
        UpdateHMDTracking();
        
        // State-specific updates
        switch (_state)
        {
            case FlightState.Hover:
                UpdateHoverState();
                break;
                
            case FlightState.CruiseToTarget:
                UpdateCruiseState();
                break;
                
            case FlightState.Landing:
                UpdateLandingState();
                break;
                
            case FlightState.LandAbort:
                UpdateLandAbortState();
                break;
                
            case FlightState.Abort:
                UpdateAbortState();
                break;
        }
    }

    /// <summary>
    /// Unified position control system that handles all drone movement smoothly
    /// </summary>
    private void UpdateUnifiedPosition()
    {
        if (_positionLocked) return;
        float dist = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        if (dist > 2.0f)
        {
            Debug.LogWarning($"DRONE_SANITY: Drone position far from target! localY={_droneOffset.localPosition.y:F2}, targetY={_targetPosition.y:F2}, dist={dist:F2}");
        }
        if (_currentVelocity.magnitude > 20f)
        {
            Debug.LogWarning($"Extreme velocity detected: {_currentVelocity.magnitude}! Clamping velocity.");
            _currentVelocity = Vector3.ClampMagnitude(_currentVelocity, 20f);
        }
#if UNITY_EDITOR
        if (_droneOffset.localPosition != _lastSetPosition)
        {
            var stack = new System.Diagnostics.StackTrace();
            string caller = stack.GetFrame(1).GetMethod().Name;
            if (caller != nameof(UpdateUnifiedPosition) && caller != nameof(Awake))
            {
                Debug.LogWarning($"DRONE_SANITY: _droneOffset.localPosition was set outside of UpdateUnifiedPosition or Awake! Called from: {caller}");
            }
            _lastSetPosition = _droneOffset.localPosition;
        }
#endif
        // SmoothDamp for all position changes
        Vector3 newPos = Vector3.SmoothDamp(
            _droneOffset.localPosition, 
            _targetPosition, 
            ref _currentVelocity, 
            _positionSmoothTime
        );
        // Clamp Y during landing and abort
        if (_state == FlightState.Landing)
        {
            if (newPos.y < _targetPosition.y)
            {
                Debug.LogWarning($"DRONE_CLAMP: Prevented drone from falling below landing/abort height. AttemptedY={newPos.y:F2}, ClampY={_targetPosition.y:F2}");
                newPos.y = _targetPosition.y;
            }
        }
        if (_state == FlightState.LandAbort)
        {
            if (newPos.y > _hoverHeight)
            {
                Debug.LogWarning($"DRONE_CLAMP: Prevented drone from rising above hover height during abort. AttemptedY={newPos.y:F2}, ClampY={_hoverHeight:F2}");
                newPos.y = _hoverHeight;
            }
        }
        _droneOffset.localPosition = newPos;
    }

    /// <summary>
    /// Updates drone rotation to face movement direction or participant
    /// </summary>
    private void UpdateRotation()
    {
        // Skip rotation if we don't have necessary references
        if (_droneOffset == null) return;
        
        // Handle rotation based on conditions
        if (_state == FlightState.CruiseToTarget)
        {
            // Face movement direction during cruise
            FaceMovementDirection();
        }
        else
        {
            // Face participant
            Quaternion targetRotation = Quaternion.LookRotation(_targetHmdDirection);
            
            // Smooth rotation
            _droneOffset.rotation = Quaternion.Slerp(
                _droneOffset.rotation, 
                targetRotation, 
                _rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Transition to a new flight state
    /// </summary>
    public void ForceTransitionToState(FlightState newState)
    {
        Debug.Log($"DRONE_STATE_DEBUG: ForceTransitionToState called: {_state} -> {newState}");
        _isInTransition = false; // Allow forced transition
        TransitionToState(newState, true);
    }

    private void TransitionToState(FlightState newState, bool force = false)
    {
        Debug.Log($"DRONE_STATE_DEBUG: TransitionToState called: {_state} -> {newState} | _isInTransition={_isInTransition} | force={force}");
        if (_isInTransition && !force) return;
        _isInTransition = true;
        _positionLocked = true;
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = currentPos;
        switch (_state)
        {
            case FlightState.CruiseToTarget:
                _navigation.OnArrived -= OnCruiseArrived;
                break;
        }
        _state = newState;
        switch (newState)
        {
            case FlightState.Landing:
            case FlightState.LandAbort:
            case FlightState.Abort:
            case FlightState.Idle:
                _enableScanning = false;
                break;
        }
        OnStateChanged?.Invoke(newState);
        switch (_state)
        {
            case FlightState.Idle:
                EnterIdle();
                break;
            case FlightState.Hover:
                EnterHover();
                break;
            case FlightState.CruiseToTarget:
                EnterCruise();
                break;
            case FlightState.Landing:
                EnterLanding();
                break;
            case FlightState.LandAbort:
                EnterLandAbort();
                break;
            case FlightState.Abort:
                EnterAbort();
                break;
        }
        _calledFromReturnToHover = false;
        _positionLocked = false;
        StartCoroutine(MonitorTransitionCompletion(newState));
    }
    
    private IEnumerator MonitorTransitionCompletion(FlightState state)
    {
        float targetY = _targetPosition.y;
        float timeout = 5f; // 5 seconds max wait
        float elapsed = 0f;
        while (Mathf.Abs(_droneOffset.localPosition.y - targetY) > 0.05f && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        if (elapsed >= timeout)
        {
            Debug.LogWarning($"DRONE_STATE_DEBUG: MonitorTransitionCompletion timed out for state {state}. Forcing _isInTransition=false");
        }
        _isInTransition = false;
        Debug.Log($"DRONE_STATE_DEBUG: Transition to {state} complete. localY={_droneOffset.localPosition.y:F2}, targetY={targetY:F2}");
    }
    
    #endregion
    
    #region State Entry Methods

    private void EnterIdle()
    {
        // Motors off
        _rotorsSpinning = false;
        _pidController.enabled = false;
        
        // Stop propeller sounds if HMI is available
        if (_hmi != null)
        {
        _hmi.StopHoverHum();
        }
    }

    private void EnterHover()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        _positionSmoothTime = _verticalTransitionSmoothTime;
        Debug.Log($"DRONE_Y_DEBUG: EnterHover | localY={currentPos.y:F2} -> targetY={_targetPosition.y:F2} (hoverHeight={_hoverHeight:F2})");
        _rotorsSpinning = true;
        StartCoroutine(DelayedPIDEnable());
        if (_hmi != null)
        {
            _hmi.PlayHoverHum();
        }
        _isAscendingHover = true;
    }

    /// <summary>
    /// Delayed PID controller activation to prevent conflicts during hover transitions
    /// </summary>
    private IEnumerator DelayedPIDEnable()
    {
        // Disable PID initially to prevent interference
        if (_pidController != null)
        {
            _pidController.enabled = false;
        }
        
        // Wait until we've reached the hover height (with small tolerance)
        float startTime = Time.time;
        while (_isAscendingHover)
        {
            // Safety timeout (5 seconds max wait)
            if (Time.time - startTime > 5f)
            {
                Debug.LogWarning("DelayedPIDEnable: Timeout waiting for hover height, enabling PID anyway");
                break;
            }
            yield return null;
        }
        
        // Add a small buffer delay for stability
        yield return new WaitForSeconds(0.2f);
        
        // Now enable PID for subtle hover movements
        if (_pidController != null)
        {
            _pidController.enabled = true;
        }
    }

    private void EnterCruise()
    {
        _rotorsSpinning = true;
        if (_hmi != null)
        {
            _hmi.PlayHoverHum();
            _hmi.SetPropellerPitch(1.1f);
        }
        _navigation.SetDestination(_cruiseTargetPosition, _cruiseSpeed);
        _navigation.OnArrived += OnCruiseArrived;
        Vector3 targetLocalPos = WorldToLocalPoint(_cruiseTargetPosition);
        _targetPosition = new Vector3(targetLocalPos.x, _hoverHeight, targetLocalPos.z);
        Debug.Log($"DRONE_Y_DEBUG: EnterCruise | localY={_droneOffset.localPosition.y:F2} -> targetY={_targetPosition.y:F2} (hoverHeight={_hoverHeight:F2})");
        float distance = Vector3.Distance(new Vector3(_droneOffset.localPosition.x, _hoverHeight, _droneOffset.localPosition.z), new Vector3(_targetPosition.x, _hoverHeight, _targetPosition.z));
        _positionSmoothTime = Mathf.Max(_minSmoothTime, distance / _cruiseSpeed);
    }

    private void EnterLanding()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        try
        {
            Vector3 targetLocalPos = WorldToLocalPoint(_landingSpot);
            if (float.IsNaN(targetLocalPos.x) || float.IsNaN(targetLocalPos.y) || float.IsNaN(targetLocalPos.z) || float.IsInfinity(targetLocalPos.x) || float.IsInfinity(targetLocalPos.y) || float.IsInfinity(targetLocalPos.z))
            {
                Debug.LogError($"Invalid target position after world-to-local conversion: {targetLocalPos}. Using safe position.");
                targetLocalPos = new Vector3(currentPos.x, 0.1f, currentPos.z);
            }
            _targetPosition = targetLocalPos;
            Vector2 currentHorizontal = new Vector2(currentPos.x, currentPos.z);
            Vector2 targetHorizontal = new Vector2(_targetPosition.x, _targetPosition.z);
            float horizontalDistance = Vector2.Distance(currentHorizontal, targetHorizontal);
            if (horizontalDistance > 50f)
            {
                Debug.LogWarning($"Landing spot too far horizontally: {horizontalDistance}m. Limiting distance.");
                Vector2 direction = (targetHorizontal - currentHorizontal).normalized;
                targetHorizontal = currentHorizontal + direction * 50f;
                _targetPosition = new Vector3(targetHorizontal.x, _landingSpot.y, targetHorizontal.y);
            }
            if (_targetPosition.y < 0.1f)
            {
                _targetPosition.y = 0.1f;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error setting landing target: {ex.Message}. Using safe position.");
            _targetPosition = new Vector3(currentPos.x, 0.1f, currentPos.z);
        }
        _positionSmoothTime = Mathf.Max(_minSmoothTime, Mathf.Abs(currentPos.y - _targetPosition.y) / _landingDescentSpeed);
        _rotorsSpinning = true;
        if (_pidController != null)
        {
            _pidController.enabled = false;
        }
        if (_hmi != null)
        {
            _hmi.SetPropellerPitch(0.9f);
            _hmi.PlayLandingSignal();
        }
        Debug.Log($"DRONE_Y_DEBUG: EnterLanding | localY={currentPos.y:F2} -> targetY={_targetPosition.y:F2}");
    }

    private void EnterLandAbort()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        _positionSmoothTime = _verticalTransitionSmoothTime;
        Debug.Log($"DRONE_Y_DEBUG: EnterLandAbort | localY={currentPos.y:F2} -> targetY={_targetPosition.y:F2} (hoverHeight={_hoverHeight:F2})");
        _rotorsSpinning = true;
        if (_pidController != null)
        {
            _pidController.enabled = false;
        }
        if (_hmi != null)
        {
            _hmi.SetPropellerPitch(1.2f);
            _hmi.StopLandingSignal();
        }
        _isInTransition = false;
    }

    private void EnterAbort()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _abortClimbHeight, currentPos.z);
        _positionSmoothTime = _verticalTransitionSmoothTime;
        Debug.Log($"DRONE_Y_DEBUG: EnterAbort | localY={currentPos.y:F2} -> targetY={_targetPosition.y:F2} (abortClimbHeight={_abortClimbHeight:F2})");
        _rotorsSpinning = true;
        if (_pidController != null)
        {
            _pidController.enabled = false;
        }
        if (_hmi != null)
        {
            _hmi.SetPropellerPitch(1.5f);
            _hmi.StopLandingSignal();
        }
    }
    
    #endregion
    
    #region State Update Methods
    
    private void UpdateHoverState()
    {
        // Only check for hover height completion if we're still ascending
        if (_isAscendingHover)
        {
            // Check if we've reached target height (with a small tolerance)
            float heightDifference = Mathf.Abs(_droneOffset.localPosition.y - _hoverHeight);
            
            if (heightDifference < 0.025f) // Very small tolerance
            {
                _isAscendingHover = false;
            }
        }
    }
    
    private void UpdateCruiseState()
    {
        // Get current position and calculate direction to target
        Vector3 currentPos = _droneOffset.localPosition;
        Vector3 directionToTarget = _targetPosition - currentPos;
        directionToTarget.y = 0; // Ignore vertical difference
        
        // Calculate horizontal distance to target
        float distanceToTarget = directionToTarget.magnitude;
        
        // Apply tilt based on movement
        if (distanceToTarget > 0.05f && _currentVelocity.magnitude > 0.01f)
        {
            // Apply tilt based on movement direction and speed
            Vector3 moveDirection = directionToTarget.normalized;
            float speedFactor = _currentVelocity.magnitude / _cruiseSpeed;
            ApplyMovementTilt(moveDirection, Mathf.Clamp01(speedFactor));
            
            // Update propeller sound based on speed
            if (_hmi != null)
            {
                // Map speed factor to propeller pitch: faster = higher pitch (1.0-1.3)
                float pitchValue = Mathf.Lerp(1.0f, 1.3f, Mathf.Clamp01(speedFactor));
                _hmi.SetPropellerPitch(pitchValue);
            }
        }
        else
        {
            // When nearly stationary, recover from tilt
            RecoverFromTilt();
            
            // Return to normal hover pitch
            if (_hmi != null)
            {
                _hmi.SetPropellerPitch(1.0f);
            }
        }
        
        // Update cruise target if following a transform
        if (_cruiseTargetTransform != null)
        {
            _cruiseTargetPosition = _cruiseTargetTransform.position;
            
            // Update target position based on transform
            Vector3 targetLocalPos = WorldToLocalPoint(_cruiseTargetPosition);
            _targetPosition = new Vector3(targetLocalPos.x, _hoverHeight, targetLocalPos.z);
        }
    }

    private void UpdateLandingState()
    {
        float distanceToLanding = Mathf.Abs(_droneOffset.localPosition.y - _targetPosition.y);
        if (_hmi != null)
        {
            float landingProgress = 1f - Mathf.Clamp01(distanceToLanding / 2f);
            float pitchValue = Mathf.Lerp(0.9f, 0.7f, landingProgress);
            _hmi.SetPropellerPitch(pitchValue);
            if (landingProgress > 0.8f)
            {
                float volumeFactor = Mathf.Clamp01((1f - landingProgress) * 5f);
                _hmi.SetPropellerVolume(volumeFactor);
            }
        }
        // Only allow transition to LandAbort when within 0.1m of target
        if (_state == FlightState.Landing && distanceToLanding < 0.1f)
        {
            // Ready for abort or next state
        }
    }

    private void UpdateLandAbortState()
    {
        float distanceToHover = Mathf.Abs(_droneOffset.localPosition.y - _hoverHeight);
        Debug.Log($"DRONE_Y_DEBUG: UpdateLandAbortState | localY={_droneOffset.localPosition.y:F2} targetY={_hoverHeight:F2} dist={distanceToHover:F2}");
        if (_hmi != null)
        {
            float abortProgress = 1f - Mathf.Clamp01(distanceToHover / 2f);
            float pitchValue = Mathf.Lerp(1.4f, 1.1f, abortProgress);
            _hmi.SetPropellerPitch(pitchValue);
            _hmi.SetPropellerVolume(1.2f);
        }
        if (_currentVelocity.y < 0.01f && distanceToHover > 0.1f)
        {
            Vector3 currentPos = _droneOffset.localPosition;
            _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
            _positionSmoothTime = _verticalTransitionSmoothTime;
            Debug.LogWarning($"DRONE_Y_DEBUG: Forcing upward movement in LandAbort. localY={currentPos.y:F2} -> targetY={_hoverHeight:F2}");
        }
        if (distanceToHover < 0.1f)
        {
            Debug.Log($"DRONE_Y_DEBUG: LandAbort complete. Transitioning to Hover.");
            ForceTransitionToState(FlightState.Hover);
            if (_hmi != null)
            {
                _hmi.SetPropellerVolume(1.0f);
            }
        }
    }

    private void UpdateAbortState()
    {
        // Check if we've reached abort height
        if (_droneOffset.localPosition.y >= _abortClimbHeight - 0.1f)
        {
            // Destroy the GameObject when we reach abort height
            Destroy(gameObject);
        }
    }
    
    #endregion
    
    #region Public API Methods
    
    /// <summary>
    /// Set the cruise target position at runtime
    /// </summary>
    public void SetCruiseTarget(Vector3 position)
    {
        _cruiseTargetTransform = null;
        _cruiseTargetPosition = position;
    }

    /// <summary>
    /// Set the cruise target by Transform, syncing its position each frame
    /// </summary>
    public void SetCruiseTarget(Transform targetTransform)
    {
        _cruiseTargetTransform = targetTransform;
        if (targetTransform != null)
        {
        _cruiseTargetPosition = targetTransform.position;
        }
    }

    /// <summary>
    /// Start cruising to any arbitrary world position
    /// </summary>
    public void StartCruiseTo(Vector3 position)
    {
        if (!_isInitialized)
        {
            Debug.LogError("Cannot start cruise - drone not initialized!");
            return;
        }
        
        SetCruiseTarget(position);
        TransitionToState(FlightState.CruiseToTarget);
    }

    /// <summary>
    /// Begin landing at the given spot
    /// </summary>
    public void BeginLanding(Vector3 spot)
    {
        if (!_isInitialized)
        {
            Debug.LogError("Cannot begin landing - drone not initialized!");
            return;
        }
        
        // CRITICAL: Validate landing spot for safety
        Vector3 originalSpot = spot;
        Vector3 currentWorldPos = transform.position;
        
        // Check for NaN or Infinity values that would cause erratic movement
        if (float.IsNaN(spot.x) || float.IsNaN(spot.y) || float.IsNaN(spot.z) || 
            float.IsInfinity(spot.x) || float.IsInfinity(spot.y) || float.IsInfinity(spot.z))
        {
            Debug.LogError($"Invalid landing spot detected: {spot}. Using current position instead.");
            spot = currentWorldPos;
            spot.y = 0.1f; // Just above ground level
        }
        
        // Check for extreme values that would cause the drone to fly off
        float maxAllowedDistance = 50f; // Maximum allowed horizontal distance
        Vector2 currentHorizontal = new Vector2(currentWorldPos.x, currentWorldPos.z);
        Vector2 targetHorizontal = new Vector2(spot.x, spot.z);
        
        if (Vector2.Distance(targetHorizontal, currentHorizontal) > maxAllowedDistance)
        {
            Debug.LogWarning($"Landing spot too far: {spot}, distance: {Vector2.Distance(targetHorizontal, currentHorizontal)}. Limiting distance.");
            Vector2 direction = (targetHorizontal - currentHorizontal).normalized;
            targetHorizontal = currentHorizontal + direction * maxAllowedDistance;
            _targetPosition = new Vector3(targetHorizontal.x, _landingSpot.y, targetHorizontal.y);
        }
        
        // Ensure landing spot has reasonable Y value
        if (spot.y < 0.1f)
        {
            Debug.LogWarning($"Landing spot Y value is too low ({spot.y}), adjusting to prevent floor penetration");
            spot.y = 0.1f;
        }
        
        // Store the landing spot for use in the landing state
        _landingSpot = spot;
        
        // If the landing spot was modified, log it
        if (spot != originalSpot)
        {
            Debug.LogWarning($"Landing spot adjusted from {originalSpot} to {_landingSpot}");
        }
        
        // Use a slightly longer smooth time for landing to prevent sudden movements
        _positionSmoothTime = Mathf.Max(_minSmoothTime, Mathf.Max(_minSmoothTime, 1.2f / _landingDescentSpeed));
        
        // Transition to landing state
        TransitionToState(FlightState.Landing);
    }

    /// <summary>
    /// Abort landing and return to hover (palm-up gesture)
    /// </summary>
    public void LandAbort()
    {
        if (!_isInitialized)
        {
            Debug.LogError("Cannot abort landing - drone not initialized!");
            return;
        }
        
        // Store original rotation before state change
        Quaternion originalRotation = _droneOffset.rotation;
        
        TransitionToState(FlightState.LandAbort);
        
        // Restore original rotation to maintain facing direction
        _droneOffset.rotation = originalRotation;
    }

    /// <summary>
    /// Abort mission and depart (C0 timeout or low confidence)
    /// </summary>
    public void Abort()
    {
        if (!_isInitialized)
        {
            Debug.LogError("Cannot abort mission - drone not initialized!");
            return;
        }
        
        TransitionToState(FlightState.Abort);
    }

    /// <summary>
    /// Forces the drone to return to hover state regardless of its current state
    /// </summary>
    public void ReturnToHover()
    {
        if (!_isInitialized)
        {
            Debug.LogError("Cannot return to hover - drone not initialized!");
            return;
        }
        if (_isInTransition) return;
        _positionLocked = true;
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        _positionSmoothTime = _verticalTransitionSmoothTime;
        _calledFromReturnToHover = true;
        TransitionToState(FlightState.Hover);
        _isAscendingHover = true;
        _positionLocked = false;
    }

    /// <summary>
    /// Retracts the drone legs (for flight mode)
    /// </summary>
    public void RetractLegs()
    {
        StartGearAnimation(_legRetractedAngle);
    }
    
    /// <summary>
    /// Extends the drone legs (for landing mode)
    /// </summary>
    public void ExtendLegs()
    {
        StartGearAnimation(0f);
    }
    
    /// <summary>
    /// Returns whether the legs are currently moving (animating)
    /// </summary>
    public bool AreLegsAnimating()
    {
        return _gearAnimating;
    }
    
    /// <summary>
    /// Returns whether the legs are currently retracted
    /// </summary>
    public bool AreLegsRetracted()
    {
        return !_gearAnimating && Mathf.Approximately(_gearTargetAngle, _legRetractedAngle);
    }
    
    /// <summary>
    /// Returns whether the legs are currently extended
    /// </summary>
    public bool AreLegsExtended()
    {
        return !_gearAnimating && Mathf.Approximately(_gearTargetAngle, 0f);
    }
    
    /// <summary>
    /// Enable or disable the scanning behavior of the drone
    /// </summary>
    public void EnableScanning(bool enable)
    {
        _enableScanning = enable;
    }
    
    /// <summary>
    /// Face the current movement direction
    /// </summary>
    private void FaceMovementDirection()
    {
        // Store original position
        Vector3 originalPosition = _droneOffset.localPosition;
        
        // Get movement direction in XZ plane (world space)
        Vector3 movement = new Vector3(_cruiseTargetPosition.x, 0, _cruiseTargetPosition.z) - 
                          new Vector3(_transform.position.x + _droneOffset.localPosition.x, 0, 
                                     _transform.position.z + _droneOffset.localPosition.z);
        
        if (movement.magnitude > 0.1f)
        {
            // Calculate target rotation to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            
            // Smooth rotation - apply to the offset transform
            _droneOffset.rotation = Quaternion.Slerp(
                _droneOffset.rotation, 
                targetRotation, 
                _rotationSpeed * Time.deltaTime);
        }
        
        // Preserve original position to prevent jumping
        _droneOffset.localPosition = originalPosition;
    }

    /// <summary>
    /// Temporarily increase rotation speed for faster response
    /// </summary>
    private IEnumerator TemporarilyBoostTrackingSpeed()
    {
        float originalSpeed = _rotationSpeed;
        _rotationSpeed *= 2.0f;
        
        yield return new WaitForSeconds(1.0f);
        
        _rotationSpeed = originalSpeed;
    }
    
    #endregion
    
    #region Utility Methods

    /// <summary>
    /// Callback when cruise arrives at destination
    /// </summary>
    private void OnCruiseArrived()
    {
        // Mark transition as complete
        _isInTransition = false;
        
        // Clean up navigation event listener
        if (_navigation != null)
        {
            _navigation.OnArrived -= OnCruiseArrived;
        }
        
        // Switch back to hover state
        TransitionToState(FlightState.Hover);
    }

    /// <summary>
    /// Converts a world space position to local space relative to the drone root
    /// </summary>
    private Vector3 WorldToLocalPoint(Vector3 worldPoint)
    {
        return _transform.InverseTransformPoint(worldPoint);
    }
    
    /// <summary>
    /// Spins all propellers based on rotor speed
    /// </summary>
    private void SpinRotors()
    {
        float deg = _rotorSpeed * Time.deltaTime;
        foreach (var prop in _propellers)
        {
            if (prop != null)
            {
            prop.Rotate(0f, 0f, deg, Space.Self);
            }
        }
    }

    /// <summary>
    /// Start gear animation with target angle
    /// </summary>
    private void StartGearAnimation(float targetAngle)
    {
        _gearTargetAngle = targetAngle;
        _gearAnimating = true;
    }

    /// <summary>
    /// Animate legs toward target angle
    /// </summary>
    private void AnimateLegs()
    {
        if (_legConfigs == null || _legConfigs.Length == 0) return;
        bool done = true;
        for (int i = 0; i < _legConfigs.Length; i++)
        {
            var config = _legConfigs[i];
            if (config == null || !config.enabled || config.legTransform == null) continue;
            Transform leg = config.legTransform;
            Quaternion currentRotation = leg.localRotation;
            float targetAngle = _gearTargetAngle == 0f ? config.extendedAngle : 0f;
            if (config.invertDirection) targetAngle = -targetAngle;
            // Use the axis in the leg's local space
            Vector3 localAxis = config.GetRotationAxisVector();
            Vector3 axis = leg.TransformDirection(localAxis); // axis in world space
            Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, axis);
            leg.localRotation = Quaternion.RotateTowards(currentRotation, targetRotation, _legRotateSpeed * Time.deltaTime);
            if (Quaternion.Angle(leg.localRotation, targetRotation) > 0.1f) done = false;
        }
        if (done) _gearAnimating = false;
    }

    /// <summary>
    /// Initialize leg configurations
    /// </summary>
    private void InitializeLegConfigs()
    {
        if (_legConfigs == null || _legConfigs.Length == 0) return; // Only initialize if configs are set
        foreach (var legConfig in _legConfigs)
        {
            if (legConfig != null && legConfig.legTransform != null)
            {
                Debug.Log($"Initialized leg configuration: {legConfig.legTransform.name}, Axis: {legConfig.rotationAxis}, Extended Angle: {legConfig.extendedAngle}, Invert: {legConfig.invertDirection}");
            }
        }
        Debug.Log($"Initialized {_legConfigs.Length} leg configurations for drone");
    }
    
    /// <summary>
    /// Apply a realistic tilt based on movement direction and speed
    /// </summary>
    private void ApplyMovementTilt(Vector3 moveDirection, float speedFactor)
    {
        if (moveDirection.magnitude < 0.01f) return;
        
        // Get elapsed time for variations
        float time = Time.time;
        
        // Perlin noise for natural variation based on time
        // Use different sampling points for pitch and roll variations
        float pitchNoise = Mathf.PerlinNoise(time * 0.6f, 23.4f);
        float rollNoise = Mathf.PerlinNoise(41.2f, time * 0.5f);
        
        // Map from 0-1 to -1 to 1 range, but reduce range by 25% for less erratic movement
        pitchNoise = ((pitchNoise * 2f) - 1f) * 0.75f;
        rollNoise = ((rollNoise * 2f) - 1f) * 0.75f;
        
        // Create variability that changes over time, influenced by speed but not proportional
        // Reduce variability by 25% for more stable movement
        float dynamicVariability = _tiltVariability * 0.75f * (0.8f + 0.4f * Mathf.PerlinNoise(time * 0.3f, time * 0.7f));
        
        // Apply dynamic variability based on perlin noise
        float randomFactor = 1f + (pitchNoise * rollNoise) * dynamicVariability;
        
        // Calculate base tilt proportional to speed, with dynamic range
        float tiltAmount = _baseTiltAngle + (_speedTiltAngle * speedFactor * speedFactor) * randomFactor;
        
        // Calculate tilt based on movement direction
        // Forward movement = pitch down, backward = pitch up
        float pitchAngle = -moveDirection.z * tiltAmount;
        
        // Right movement = roll right, left = roll left
        float rollAngle = moveDirection.x * tiltAmount;
        
        // Add wind influence that changes over time - reduced by 25% for stability
        float windStrength = _windInfluence * 0.75f * Mathf.PerlinNoise(time * 0.2f, 0);
        float windDirection = Mathf.PerlinNoise(0, time * 0.17f) * 360f;
        
        // Wind affects roll and pitch differently
        float windPitch = Mathf.Sin(windDirection * Mathf.Deg2Rad) * windStrength;
        float windRoll = Mathf.Cos(windDirection * Mathf.Deg2Rad) * windStrength;
        
        // Subtle micro-movements using Perlin noise with different frequencies - reduced by 25%
        float microPitch = (Mathf.PerlinNoise(time * 1.2f, time * 0.7f) * 2f - 1f) * 0.75f;
        float microRoll = (Mathf.PerlinNoise(time * 0.9f, time * 1.4f) * 2f - 1f) * 0.75f;
        float microYaw = (Mathf.PerlinNoise(time * 0.7f, time * 1.1f) * 2f - 1f) * 0.75f;
        
        // Scale micro-movements
        microPitch *= _microMovementStrength;
        microRoll *= _microMovementStrength;
        microYaw *= _microMovementStrength * 0.5f; // Less yaw movement
        
        // Apply micro-movements to pitch and roll
        pitchAngle += microPitch + windPitch;
        rollAngle += microRoll + windRoll;
        
        // Get current rotation excluding tilt (just the yaw)
        Vector3 currentRotation = _droneOffset.rotation.eulerAngles;
        float yaw = currentRotation.y;
        
        // Add micro-yaw to current yaw
        yaw += microYaw;
        
        // Adjust pitch and roll to align with the drone's current yaw
        float finalPitch = pitchAngle * Mathf.Cos(yaw * Mathf.Deg2Rad) - rollAngle * Mathf.Sin(yaw * Mathf.Deg2Rad);
        float finalRoll = rollAngle * Mathf.Cos(yaw * Mathf.Deg2Rad) + pitchAngle * Mathf.Sin(yaw * Mathf.Deg2Rad);
        
        // Create target rotation with tilt applied
        Quaternion targetTilt = Quaternion.Euler(finalPitch, yaw, finalRoll);
        
        // Apply tilt with varied speed based on movement
        float tiltSpeed = _rotationSpeed * (0.7f + speedFactor * 0.6f + Mathf.PerlinNoise(time * 0.4f, 0) * 0.3f);
        _droneOffset.rotation = Quaternion.Slerp(_droneOffset.rotation, targetTilt, tiltSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// Gradually recover from tilt when not moving with subtle movements even at rest
    /// </summary>
    private void RecoverFromTilt()
    {
        // Get current rotation
        Vector3 currentRotation = _droneOffset.rotation.eulerAngles;
        
        // Normalize angles to -180 to 180
        float pitch = currentRotation.x > 180 ? currentRotation.x - 360 : currentRotation.x;
        float roll = currentRotation.z > 180 ? currentRotation.z - 360 : currentRotation.z;
        float yaw = currentRotation.y;
        
        // Get time values for oscillations
        float time = Time.time;
        
        // Apply subtle oscillations in hover
        if (_enableMicroMovements)
        {
            // Multi-frequency oscillations for more organic movement
            float pitchOsc1 = Mathf.Sin(time * 0.77f * _oscillationSpeed) * _pitchOscillationStrength * 0.7f;
            float pitchOsc2 = Mathf.Sin(time * 1.31f * _oscillationSpeed) * _pitchOscillationStrength * 0.3f;
            float rollOsc1 = Mathf.Cos(time * 0.85f * _oscillationSpeed) * _rollOscillationStrength * 0.7f;
            float rollOsc2 = Mathf.Cos(time * 1.43f * _oscillationSpeed) * _rollOscillationStrength * 0.3f;
            
            // Add subtle wind influence
            float windPitch = Mathf.PerlinNoise(time * 0.13f, 0) * 2f - 1f;
            float windRoll = Mathf.PerlinNoise(0, time * 0.11f) * 2f - 1f;
            
            // Calculate target oscillation values
            float targetPitch = pitchOsc1 + pitchOsc2 + (windPitch * _windInfluence);
            float targetRoll = rollOsc1 + rollOsc2 + (windRoll * _windInfluence);
            
            // Vary recovery speed slightly with Perlin noise
            float recoveryVariation = 0.8f + (Mathf.PerlinNoise(time * 0.27f, time * 0.31f) * 0.4f);
            float finalRecoverySpeed = _tiltRecoverySpeed * recoveryVariation * Time.deltaTime;
            
            // Apply recovery toward oscillation values instead of zero
            pitch = Mathf.Lerp(pitch, targetPitch, finalRecoverySpeed);
            roll = Mathf.Lerp(roll, targetRoll, finalRecoverySpeed);
            
            // Add subtle yaw drift
            float yawDrift = Mathf.PerlinNoise(time * 0.07f, time * 0.19f) * 2f - 1f;
            yaw += yawDrift * _microMovementStrength * 0.3f * Time.deltaTime;
        }
        else
        {
            // Standard recovery toward level when micro-movements disabled
            float pitchStep = Mathf.Sign(pitch) * Mathf.Min(Mathf.Abs(pitch), _tiltRecoverySpeed * Time.deltaTime);
            float rollStep = Mathf.Sign(roll) * Mathf.Min(Mathf.Abs(roll), _tiltRecoverySpeed * Time.deltaTime);
            
            // Apply recovery
            pitch -= pitchStep;
            roll -= rollStep;
        }
        
        // Apply the new rotation
        _droneOffset.rotation = Quaternion.Euler(pitch, yaw, roll);
    }
    
    /// <summary>
    /// Gets an approximation of the speed factor from an animation curve at a specific time
    /// Higher values indicate faster change rates at that point in the curve
    /// </summary>
    private float GetCurveSpeedFactor(AnimationCurve curve, float time)
    {
        // Simple approximation of curve derivative (speed) at the given time
        // We sample the curve slightly before and after the current time
        const float delta = 0.05f;
        
        float timeBefore = Mathf.Max(0, time - delta);
        float timeAfter = Mathf.Min(1, time + delta);
        
        float valueBefore = curve.Evaluate(timeBefore);
        float valueAfter = curve.Evaluate(timeAfter);
        
        // Approximate the first derivative (rate of change)
        float derivative = (valueAfter - valueBefore) / (timeAfter - timeBefore);
        
        // Normalize to a 0-1 range (assuming typical curve derivatives)
        return Mathf.Clamp01(Mathf.Abs(derivative) / 2.0f);
    }
    
    /// <summary>
    /// Logs detailed position information for debugging transitional jumps
    /// </summary>
    private void LogPositionDebug(string context, Vector3 position)
    {
        // No debug logs or conditional blocks should reference _nonTrackingStates, _faceMovementDirection, _scanCycleProgress, _showParticipantDebug, _participantTrackingActive, or related debug or participant tracking logic.
    }

    /// <summary>
    /// Enables detailed position logging during state changes and transitions
    /// Can be called from ScenarioManager for diagnostics
    /// </summary>
    public void EnablePositionDebugging(bool enable)
    {
        // No debug logs or conditional blocks should reference _nonTrackingStates, _faceMovementDirection, _scanCycleProgress, _showParticipantDebug, _participantTrackingActive, or related debug or participant tracking logic.
    }
    
    /// <summary>
    /// Last chance verification of drone position to prevent flying away or disappearing
    /// </summary>
    private void FixedUpdate()
    {
        // Skip if not initialized
        if (!_isInitialized) return;
        
        // SAFETY CHECK: Prevent the drone from moving to extreme positions
        if (_droneOffset != null)
        {
            Vector3 currentPos = _droneOffset.localPosition;
            bool positionFixed = false;
            
            // Check for NaN or infinity values (rare but catastrophic)
            if (float.IsNaN(currentPos.x) || float.IsNaN(currentPos.y) || float.IsNaN(currentPos.z) ||
                float.IsInfinity(currentPos.x) || float.IsInfinity(currentPos.y) || float.IsInfinity(currentPos.z))
            {
                Debug.LogError("CRITICAL ERROR: Drone position contains NaN or infinity! Resetting to origin.");
                _droneOffset.localPosition = Vector3.zero;
                positionFixed = true;
            }
            
            // Check for extreme local positions indicating something went wrong
            float maxAllowedLocalPosition = 200f;
            if (Mathf.Abs(currentPos.x) > maxAllowedLocalPosition || 
                Mathf.Abs(currentPos.y) > maxAllowedLocalPosition ||
                Mathf.Abs(currentPos.z) > maxAllowedLocalPosition)
            {
                Debug.LogError($"CRITICAL ERROR: Drone position is extreme! {currentPos} - Clamping to safe values.");
                _droneOffset.localPosition = new Vector3(
                    Mathf.Clamp(currentPos.x, -maxAllowedLocalPosition, maxAllowedLocalPosition),
                    Mathf.Clamp(currentPos.y, 0f, maxAllowedLocalPosition),
                    Mathf.Clamp(currentPos.z, -maxAllowedLocalPosition, maxAllowedLocalPosition)
                );
                positionFixed = true;
            }
            
            // Reset target position and velocity if we had to fix the position
            if (positionFixed)
            {
                // Reset velocity and target to current position
                _currentVelocity = Vector3.zero;
                _targetPosition = _droneOffset.localPosition;
                
                // Force drone to return to hover
                StartCoroutine(EmergencyReturnToHover());
            }
        }
    }
    
    /// <summary>
    /// Emergency procedure to return drone to safe hover state
    /// </summary>
    private IEnumerator EmergencyReturnToHover()
    {
        // Wait a frame to allow other systems to stabilize
        yield return null;
        
        Debug.LogWarning("Emergency procedure: Returning drone to hover state");
        
        // Disable all complex behaviors first
        _positionLocked = true;
        if (_pidController != null) _pidController.enabled = false;
        _enableScanning = false;
        
        // Reset target to safe hover position
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        
        // Set a slower smooth time for safety
        _positionSmoothTime = 1.5f;
        
        // Wait a bit then unlock
        yield return new WaitForSeconds(0.5f);
        _positionLocked = false;
        
        // Transition to hover after position stabilizes
        yield return new WaitForSeconds(1.0f);
        TransitionToState(FlightState.Hover);
    }
    
    private void UpdateHMDTracking()
    {
        if (_hmdTransform == null) return;

        // Calculate direction to HMD
        Vector3 hmdPosition = _hmdTransform.position;
        Vector3 directionToHMD = hmdPosition - transform.position;
        directionToHMD.y = 0; // Keep rotation only in horizontal plane

        // Smoothly update target direction with delay
        _hmdTrackingTimer += Time.deltaTime;
        if (_hmdTrackingTimer >= _hmdTrackingDelay)
        {
            _targetHmdDirection = directionToHMD.normalized;
            _hmdTrackingTimer = 0f;
        }

        // Calculate target rotation
        Quaternion targetRotation = Quaternion.LookRotation(_targetHmdDirection);
        
        // Limit rotation angle
        float currentAngle = Quaternion.Angle(transform.rotation, targetRotation);
        if (currentAngle > _maxHmdRotationAngle)
        {
            targetRotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _maxHmdRotationAngle
            );
        }

        // Smoothly rotate towards target
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            _hmdTrackingSpeed * Time.deltaTime
        );
    }
    
    // Returns true if the drone is at its target Y within a threshold
    public bool IsAtTargetY(float threshold = 0.1f)
    {
        return Mathf.Abs(_droneOffset.localPosition.y - _targetPosition.y) < threshold;
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