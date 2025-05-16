using UnityEngine;
using System.Collections;

public class DroneController : MonoBehaviour
{
    // Serialized references to other layers
    [Header("References")]
    [SerializeField] private DroneArrivalDetector _navigation;       // for arrival callbacks

    [Header("Propellers")]
    [SerializeField] private Transform[] _propellers;           // assign 8 propeller transforms

    [Header("Legs")]
    [SerializeField] private Transform[] _legs;                 // landing gear transforms
    
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
    
    [Tooltip("Set the rotation type for each leg in the editor")]
    [SerializeField] private LegRotationType[] _legRotationTypes;

    [Header("Sway Controller")]
    [SerializeField] private PIDController _pidController;      // subtle hover sway

    [Header("Movement Profiles")]
    [Tooltip("How the drone accelerates/decelerates during cruise")]
    [SerializeField] private AnimationCurve _cruiseMovementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("How the drone moves during landing")]
    [SerializeField] private AnimationCurve _landingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("How the drone moves during abort")]
    [SerializeField] private AnimationCurve _abortCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Rotation Settings")]
    [Tooltip("Whether the drone should rotate to face movement direction")]
    [SerializeField] private bool _faceMovementDirection = true;
    [Tooltip("Whether the drone should face the participant when hovering")]
    [SerializeField] private bool _faceParticipant = true;
    [Tooltip("Speed at which the drone rotates to face a new direction")]
    [SerializeField] private float _rotationSpeed = 2f;
    [Tooltip("Transform representing the participant's position (typically camera/head)")]
    [SerializeField] private Transform _participantTransform;

    [Header("Scenario Targets")]
    [SerializeField] private Transform _c1Target;  // C-1 landing probe target (zone child)
    [SerializeField] private Transform _c2Target;  // C-2 guidance pad target (zone child)
    [SerializeField] private Transform _c3Target;  // C-3 zone random target

    [Header("Flight Settings")]
    [SerializeField] private float _hoverHeight = 6f;
    [SerializeField] private float _hoverMovementSpeed = 2f;  // speed factor for smooth ascend/descend to hover
    [SerializeField] private float _cruiseSpeed = 3f;
    [SerializeField] private float _abortClimbHeight = 8f;
    [SerializeField] private float _c0Timeout = 5f;
    [SerializeField] private float _rotorSpeed = 360f;            // rotor spin speed (deg/sec)
    [SerializeField] private float _legRetractedAngle = 30f;      // gear fold angle (deg)
    [SerializeField] private float _legRotateSpeed = 90f;         // gear rotation speed (deg/sec)
    [SerializeField] private float _landingDescentSpeed = 2f;     // landing / climb speed

    // Make FlightState public so it can be used in events and properties
    public enum FlightState { Idle, Hover, CruiseToTarget, Landing, LandAbort, Abort }
    private FlightState _state;

    // State changed event that ScenarioManager can listen to
    public delegate void StateChangedHandler(FlightState newState);
    public event StateChangedHandler OnStateChanged;

    // target for Cruise & landing
    private Vector3 _cruiseTargetPosition;
    private Transform _cruiseTargetTransform;
    private Vector3 _landingSpot;
    private bool _isAscendingHover;  // tracks smooth ascend/descend to hover height

    // Movement transition variables
    private Vector3 _moveStartPosition;
    private float _movementProgress = 0f;
    private float _movementDuration = 0f;
    private bool _isInTransition = false;

    // Internal state
    private Coroutine _c0Coroutine;
    private bool _rotorsSpinning;
    private bool _gearAnimating;
    private float _gearTargetAngle;
    
    // Cached transform for optimization
    private Transform _transform;

    public FlightState CurrentState => _state;

    private void Awake()
    {
        _state = FlightState.Idle;
        _transform = transform;
        
        // Auto-find participant transform if not set (usually the main camera)
        if (_participantTransform == null)
        {
            _participantTransform = Camera.main?.transform;
        }
    }

    private void Start()
    {
        // Initialize leg rotation data if not set
        InitializeLegRotationTypes();
        
        // begin in hover on game start
        TransitionToState(FlightState.Hover);
    }

    private void Update()
    {
        // smooth ascend/descend into hover state
        if (_state == FlightState.Hover && _isAscendingHover)
        {
            Vector3 pos = _transform.position;
            Vector3 target = new Vector3(pos.x, _hoverHeight, pos.z);
            _transform.position = Vector3.Lerp(pos, target, Time.deltaTime * _hoverMovementSpeed);
            if (Mathf.Abs(_transform.position.y - _hoverHeight) < 0.01f)
            {
                _isAscendingHover = false;
                _c0Coroutine = StartCoroutine(C0AbortTimer());
            }
        }

        // sync a moving target
        if (_cruiseTargetTransform != null)
            _cruiseTargetPosition = _cruiseTargetTransform.position;

        // animate rotors
        if (_rotorsSpinning) SpinRotors();
        // animate gear
        if (_gearAnimating) AnimateLegs();
        
        // Handle rotation to face direction or participant
        UpdateRotation();

        // state-specific updates
        switch (_state)
        {
            case FlightState.CruiseToTarget:
                // Handle direct cruise movement now that we're not using NavMesh
                PerformCruise();
                break;
            case FlightState.Landing:
                PerformLanding();
                break;
            case FlightState.LandAbort:
                PerformLandAbort();
                break;
            case FlightState.Abort:
                PerformAbort();
                break;
        }
    }

    /// <summary>
    /// Set the cruise target position at runtime (e.g., pass spawn location).
    /// </summary>
    public void SetCruiseTarget(Vector3 position)
    {
        _cruiseTargetTransform = null;
        _cruiseTargetPosition = position;
    }

    /// <summary>
    /// Set the cruise target by Transform, syncing its position each frame.
    /// </summary>
    public void SetCruiseTarget(Transform targetTransform)
    {
        _cruiseTargetTransform = targetTransform;
        _cruiseTargetPosition = targetTransform.position;
    }

    /// <summary>
    /// Begin cruising to any arbitrary world position.
    /// </summary>
    public void StartCruiseTo(Vector3 position)
    {
        SetCruiseTarget(position);
        TransitionToState(FlightState.CruiseToTarget);
    }

    /// <summary>
    /// Begin cruising to the C-1 scenario target.
    /// </summary>
    public void StartCruiseToC1()
    {
        SetCruiseTarget(_c1Target);
        TransitionToState(FlightState.CruiseToTarget);
    }

    /// <summary>
    /// Begin cruising to the C-2 scenario target.
    /// </summary>
    public void StartCruiseToC2()
    {
        SetCruiseTarget(_c2Target);
        TransitionToState(FlightState.CruiseToTarget);
    }

    /// <summary>
    /// Begin cruising to the C-3 scenario target.
    /// </summary>
    public void StartCruiseToC3()
    {
        SetCruiseTarget(_c3Target);
        TransitionToState(FlightState.CruiseToTarget);
    }

    /// <summary>Invoked by scenario logic to begin a landing at the given spot.</summary>
    public void BeginLanding(Vector3 spot)
    {
        _landingSpot = spot;
        TransitionToState(FlightState.Landing);
    }

    /// <summary>Invoked on palm-up gesture to abort landing.</summary>
    public void LandAbort()
    {
        TransitionToState(FlightState.LandAbort);
    }

    /// <summary>Invoked by C-0 timer or low-confidence rule to abort mission.</summary>
    public void Abort()
    {
        TransitionToState(FlightState.Abort);
    }

    /// <summary>
    /// Forces the drone to return to hover state regardless of its current state.
    /// Used for scenario transitions and reset.
    /// </summary>
    public void ReturnToHover()
    {
        // Cancel any active scenario timers
        if (_c0Coroutine != null)
        {
            StopCoroutine(_c0Coroutine);
            _c0Coroutine = null;
        }
        
        // Force transition to hover regardless of current state
        TransitionToState(FlightState.Hover);
    }

    private void TransitionToState(FlightState newState)
    {
        // exit logic
        switch (_state)
        {
            case FlightState.CruiseToTarget:
                _navigation.OnArrived -= OnCruiseArrived;
                break;
            case FlightState.Hover:
                if (_c0Coroutine != null)
                    StopCoroutine(_c0Coroutine);
                break;
        }

        _state = newState;

        // Notify listeners of the state change
        OnStateChanged?.Invoke(newState);

        // enter logic
        switch (_state)
        {
            case FlightState.Idle:          EnterIdle(); break;
            case FlightState.Hover:         EnterHover(); break;
            case FlightState.CruiseToTarget:EnterCruise(); break;
            case FlightState.Landing:       EnterLanding(); break;
            case FlightState.LandAbort:     EnterLandAbort(); break;
            case FlightState.Abort:         EnterAbort(); break;
        }
    }

    private void EnterIdle()
    {
        // motors off, but don't automatically change gear position
        _rotorsSpinning = false;
        _pidController.enabled = false;
    }

    private void EnterHover()
    {
        // begin smooth ascend/descend to hover height
        _isAscendingHover = true;

        // start motors and sway, but don't automatically retract gear
        _rotorsSpinning = true;
        _pidController.enabled = true;
    }

    private IEnumerator C0AbortTimer()
    {
        yield return new WaitForSeconds(_c0Timeout);
        Abort();
    }

    private void EnterCruise()
    {
        // Start rotors and enable sway, but don't automatically retract gear
        _rotorsSpinning = true;
        _pidController.enabled = true;
        
        // Set destination for arrival detection only (movement handled by PerformCruise)
        _navigation.SetDestination(_cruiseTargetPosition, _cruiseSpeed);
        _navigation.OnArrived += OnCruiseArrived;
        
        // Setup movement transition
        _moveStartPosition = _transform.position;
        _movementProgress = 0f;
        
        // Calculate movement duration based on distance and cruise speed
        float distance = Vector3.Distance(
            new Vector3(_moveStartPosition.x, _hoverHeight, _moveStartPosition.z),
            new Vector3(_cruiseTargetPosition.x, _hoverHeight, _cruiseTargetPosition.z));
        _movementDuration = distance / _cruiseSpeed;
        _isInTransition = true;
    }

    private void OnCruiseArrived()
    {
        TransitionToState(FlightState.Hover);
    }

    private void EnterLanding()
    {
        // Keep rotors spinning during landing, but don't automatically extend gear
        _rotorsSpinning = true;
        _pidController.enabled = true;
        
        // Setup movement transition
        _moveStartPosition = _transform.position;
        _movementProgress = 0f;
        _movementDuration = Vector3.Distance(_moveStartPosition, _landingSpot) / _landingDescentSpeed;
        _isInTransition = true;
    }

    private void PerformLanding()
    {
        // Increment progress along landing curve
        if (_isInTransition)
        {
            _movementProgress += Time.deltaTime / _movementDuration;
            if (_movementProgress >= 1f)
            {
                _movementProgress = 1f;
                _isInTransition = false;
            }
            
            // Apply landing curve for smooth motion
            float curveValue = _landingCurve.Evaluate(_movementProgress);
            _transform.position = Vector3.Lerp(_moveStartPosition, _landingSpot, curveValue);
        }
        else if (Vector3.Distance(_transform.position, _landingSpot) < 0.1f)
        {
            TransitionToState(FlightState.Idle);
        }
    }

    private void EnterLandAbort()
    {
        // Keep rotors spinning during abort, but don't automatically retract gear
        _rotorsSpinning = true;
        _pidController.enabled = true;
        
        // Setup movement transition
        _moveStartPosition = _transform.position;
        Vector3 target = new Vector3(_transform.position.x, _hoverHeight, _transform.position.z);
        _movementProgress = 0f;
        _movementDuration = Vector3.Distance(_moveStartPosition, target) / _landingDescentSpeed;
        _isInTransition = true;
    }

    private void PerformLandAbort()
    {
        // Calculate target hover position
        Vector3 target = new Vector3(_transform.position.x, _hoverHeight, _transform.position.z);
        
        // Increment progress along abort curve
        if (_isInTransition)
        {
            _movementProgress += Time.deltaTime / _movementDuration;
            if (_movementProgress >= 1f)
            {
                _movementProgress = 1f;
                _isInTransition = false;
            }
            
            // Apply abort curve for smooth motion
            float curveValue = _abortCurve.Evaluate(_movementProgress);
            _transform.position = Vector3.Lerp(_moveStartPosition, target, curveValue);
        }
        else if (Mathf.Abs(_transform.position.y - _hoverHeight) < 0.1f)
        {
            TransitionToState(FlightState.Hover);
        }
    }

    private void EnterAbort()
    {
        // Keep rotors spinning during abort, but don't automatically retract gear
        _rotorsSpinning = true;
        _pidController.enabled = true;
        
        // Setup movement transition
        _moveStartPosition = _transform.position;
        Vector3 target = new Vector3(_transform.position.x, _abortClimbHeight, _transform.position.z);
        _movementProgress = 0f;
        _movementDuration = Vector3.Distance(_moveStartPosition, target) / _cruiseSpeed;
        _isInTransition = true;
    }

    private void PerformAbort()
    {
        // Calculate target abort position
        Vector3 target = new Vector3(_transform.position.x, _abortClimbHeight, _transform.position.z);
        
        // Increment progress along abort curve
        if (_isInTransition)
        {
            _movementProgress += Time.deltaTime / _movementDuration;
            if (_movementProgress >= 1f)
            {
                _movementProgress = 1f;
                _isInTransition = false;
            }
            
            // Apply abort curve for smooth motion
            float curveValue = _abortCurve.Evaluate(_movementProgress);
            _transform.position = Vector3.Lerp(_moveStartPosition, target, curveValue);
        }
        else if (_transform.position.y >= _abortClimbHeight - 0.1f)
        {
            Destroy(gameObject);
        }
    }

    private void SpinRotors()
    {
        float deg = _rotorSpeed * Time.deltaTime;
        foreach (var prop in _propellers)
            prop.Rotate(0f, 0f, deg, Space.Self);
    }

    private void StartGearAnimation(float targetAngle)
    {
        _gearTargetAngle = targetAngle;
        _gearAnimating = true;
    }

    private void AnimateLegs()
    {
        bool done = true;
        
        for (int i = 0; i < _legs.Length; i++)
        {
            Transform leg = _legs[i];
            Vector3 euler = leg.localEulerAngles;
            
            // Extract current angles, handling wraparound
            float angleX = euler.x > 180f ? euler.x - 360f : euler.x;
            float angleY = euler.y > 180f ? euler.y - 360f : euler.y;
            float angleZ = euler.z > 180f ? euler.z - 360f : euler.z;
            
            // Apply rotation based on leg type
            switch (_legRotationTypes[i])
            {
                case LegRotationType.RotateOnX:
                    angleX = Mathf.MoveTowards(angleX, _gearTargetAngle, _legRotateSpeed * Time.deltaTime);
                    break;
                case LegRotationType.RotateOnY:
                    angleY = Mathf.MoveTowards(angleY, _gearTargetAngle, _legRotateSpeed * Time.deltaTime);
                    break;
                case LegRotationType.RotateOnZ:
                    angleZ = Mathf.MoveTowards(angleZ, _gearTargetAngle, _legRotateSpeed * Time.deltaTime);
                    break;
                case LegRotationType.RotateOnNegativeX:
                    angleX = Mathf.MoveTowards(angleX, -_gearTargetAngle, _legRotateSpeed * Time.deltaTime);
                    break;
                case LegRotationType.RotateOnNegativeY:
                    angleY = Mathf.MoveTowards(angleY, -_gearTargetAngle, _legRotateSpeed * Time.deltaTime);
                    break;
                case LegRotationType.RotateOnNegativeZ:
                    angleZ = Mathf.MoveTowards(angleZ, -_gearTargetAngle, _legRotateSpeed * Time.deltaTime);
                    break;
            }
            
            // Apply the new rotation
            leg.localEulerAngles = new Vector3(angleX, angleY, angleZ);
            
            // Check if this leg is at target position
            bool legAtTarget = false;
            switch (_legRotationTypes[i])
            {
                case LegRotationType.RotateOnX:
                    legAtTarget = Mathf.Abs(angleX - _gearTargetAngle) < 0.1f;
                    break;
                case LegRotationType.RotateOnY:
                    legAtTarget = Mathf.Abs(angleY - _gearTargetAngle) < 0.1f;
                    break;
                case LegRotationType.RotateOnZ:
                    legAtTarget = Mathf.Abs(angleZ - _gearTargetAngle) < 0.1f;
                    break;
                case LegRotationType.RotateOnNegativeX:
                    legAtTarget = Mathf.Abs(angleX - (-_gearTargetAngle)) < 0.1f;
                    break;
                case LegRotationType.RotateOnNegativeY:
                    legAtTarget = Mathf.Abs(angleY - (-_gearTargetAngle)) < 0.1f;
                    break;
                case LegRotationType.RotateOnNegativeZ:
                    legAtTarget = Mathf.Abs(angleZ - (-_gearTargetAngle)) < 0.1f;
                    break;
            }
            
            if (!legAtTarget)
            {
                done = false;
            }
        }
        
        if (done) _gearAnimating = false;
    }

    /// <summary>
    /// Perform direct cruise movement toward target position
    /// </summary>
    private void PerformCruise()
    {
        // Use cruise movement curve for smooth acceleration/deceleration
        if (_isInTransition)
        {
            _movementProgress += Time.deltaTime / _movementDuration;
            if (_movementProgress >= 1f)
            {
                _movementProgress = 1f;
                _isInTransition = false;
            }
            
            // Apply cruise curve for smooth motion
            float curveValue = _cruiseMovementCurve.Evaluate(_movementProgress);
            Vector3 targetPos = new Vector3(_cruiseTargetPosition.x, _hoverHeight, _cruiseTargetPosition.z);
            Vector3 startPos = new Vector3(_moveStartPosition.x, _hoverHeight, _moveStartPosition.z);
            _transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
        }
        else
        {
            // If we've completed the curve transition but haven't yet triggered arrival
            // (this handles small discrepancies or moving targets)
            _transform.position = Vector3.MoveTowards(
                _transform.position, 
                new Vector3(_cruiseTargetPosition.x, _hoverHeight, _cruiseTargetPosition.z), 
                _cruiseSpeed * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Update drone rotation to face movement direction or participant
    /// </summary>
    private void UpdateRotation()
    {
        if (_state == FlightState.CruiseToTarget && _faceMovementDirection)
        {
            // Get movement direction in XZ plane
            Vector3 movement = new Vector3(_cruiseTargetPosition.x, 0, _cruiseTargetPosition.z) - 
                              new Vector3(_transform.position.x, 0, _transform.position.z);
            
            if (movement.magnitude > 0.1f)
            {
                // Calculate target rotation to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                
                // Smooth rotation
                _transform.rotation = Quaternion.Slerp(
                    _transform.rotation, 
                    targetRotation, 
                    _rotationSpeed * Time.deltaTime);
            }
        }
        else if (_state == FlightState.Hover && _faceParticipant && _participantTransform != null)
        {
            // Face the participant when hovering
            Vector3 direction = new Vector3(_participantTransform.position.x, 0, _participantTransform.position.z) - 
                               new Vector3(_transform.position.x, 0, _transform.position.z);
            
            if (direction.magnitude > 0.1f)
            {
                // Calculate target rotation to face participant
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                
                // Smooth rotation
                _transform.rotation = Quaternion.Slerp(
                    _transform.rotation, 
                    targetRotation, 
                    _rotationSpeed * Time.deltaTime);
            }
        }
    }

    // Initialize leg rotation types if not already set
    private void InitializeLegRotationTypes()
    {
        // Make sure we have the right number of rotation types
        if (_legRotationTypes == null || _legRotationTypes.Length != _legs.Length)
        {
            _legRotationTypes = new LegRotationType[_legs.Length];
            // Default to X rotation
            for (int i = 0; i < _legs.Length; i++)
            {
                _legRotationTypes[i] = LegRotationType.RotateOnX;
            }
        }
    }

    // Reset all legs to their default position (for testing/debugging)
    public void ResetLegs()
    {
        foreach (var leg in _legs)
        {
            leg.localEulerAngles = Vector3.zero;
        }
    }

    // Add public methods to control leg retraction/extension independently
    
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
}