using UnityEngine;
using System.Collections;

public class DroneController : MonoBehaviour
{
    // Serialized references to other layers
    [Header("References")]
    [SerializeField] private DroneNavigation _navigation;       // for nav & arrival callbacks
    [SerializeField] private DroneHMI _hmi;                     // for LED & audio feedback
    [SerializeField] private ARInterfaceManager _arInterface;   // for AR probe & pad UI

    [Header("Propellers")]
    [SerializeField] private Transform[] _propellers;           // assign 8 propeller transforms

    [Header("Legs")]
    [SerializeField] private Transform[] _legs;                 // landing gear transforms

    [Header("Sway Controller")]
    [SerializeField] private PIDController _pidController;      // subtle hover sway

    [Header("Scenario Targets")]
    [SerializeField] private Transform _c1Target;  // C-1 landing probe target (zone child)
    [SerializeField] private Transform _c2Target;  // C-2 guidance pad target (zone child)
    [SerializeField] private Transform _c3Target;  // C-3 zone random target

    [Header("Flight Settings")]
    [SerializeField] private float _hoverHeight = 6f;
    [SerializeField] private float _hoverAscendSpeed = 2f;    // speed to ascend to hover height
    [SerializeField] private float _cruiseSpeed = 3f;
    [SerializeField] private float _abortClimbHeight = 8f;
    [SerializeField] private float _c0Timeout = 5f;
    [SerializeField] private float _rotorSpeed = 360f;            // rotor spin speed (deg/sec)
    [SerializeField] private float _legRetractedAngle = 30f;      // gear fold angle (deg)
    [SerializeField] private float _legRotateSpeed = 90f;         // gear rotation speed (deg/sec)
    [SerializeField] private float _landingDescentSpeed = 2f;     // landing / climb speed

    private enum FlightState { Idle, Hover, CruiseToTarget, Landing, LandAbort, Abort }
    private FlightState _state;

    // target for Cruise & landing
    private Vector3 _cruiseTargetPosition;
    private Transform _cruiseTargetTransform;
    private Vector3 _landingSpot;
    private bool _isAscendingHover;  // tracks smooth ascend to hover height

    // Internal state
    private Coroutine _c0Coroutine;
    private bool _rotorsSpinning;
    private bool _gearAnimating;
    private float _gearTargetAngle;

    private void Awake()
    {
        _state = FlightState.Idle;
    }

    private void Start()
    {
        // begin in hover on game start
        TransitionToState(FlightState.Hover);
    }

    private void Update()
    {
        // handle smooth ascend into hover state
        if (_state == FlightState.Hover && _isAscendingHover)
        {
            Vector3 pos = transform.position;
            Vector3 target = new Vector3(pos.x, _hoverHeight, pos.z);
            transform.position = Vector3.MoveTowards(pos, target, _hoverAscendSpeed * Time.deltaTime);
            if (Mathf.Abs(transform.position.y - _hoverHeight) < 0.01f)
            {
                _isAscendingHover = false;
                // now start the C-0 abort timer
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

        // state-specific updates
        switch (_state)
        {
            case FlightState.CruiseToTarget:
                // arrival handled via callback
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
        // motors off, gear deployed
        _rotorsSpinning = false;
        StartGearAnimation(0f);
        _pidController.enabled = false;
        _hmi.StopHoverHum();
        _arInterface.HideAll();
    }

    private void EnterHover()
    {
        // begin smooth ascend to hover height
        _isAscendingHover = true;

        // start motors, retract gear, start sway, uncertainty tone
        _rotorsSpinning = true;
        StartGearAnimation(_legRetractedAngle);
        _pidController.enabled = true;
        _hmi.PlayHoverHum();
        _hmi.PlayUncertainty();

        // C-0 abort timer will start after ascend completes
    }

    private IEnumerator C0AbortTimer()
    {
        yield return new WaitForSeconds(_c0Timeout);
        Abort();
    }

    private void EnterCruise()
    {
        // navigate to target
        _pidController.enabled = false;
        _hmi.PlayHoverHum();
        _navigation.SetDestination(_cruiseTargetPosition, _cruiseSpeed);
        _navigation.OnArrived += OnCruiseArrived;
    }

    private void OnCruiseArrived()
    {
        TransitionToState(FlightState.Hover);
    }

    private void EnterLanding()
    {
        // stop motors, play landing cue
        _rotorsSpinning = false;
        _hmi.StopHoverHum();
    }

    private void PerformLanding()
    {
        transform.position = Vector3.MoveTowards(transform.position, _landingSpot, _landingDescentSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, _landingSpot) < 0.1f)
            TransitionToState(FlightState.Idle);
    }

    private void EnterLandAbort()
    {
        // resume climb to hover
        _rotorsSpinning = false;
        _hmi.PlayHoverHum();
    }

    private void PerformLandAbort()
    {
        var target = new Vector3(transform.position.x, _hoverHeight, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, target, _landingDescentSpeed * Time.deltaTime);
        if (Mathf.Abs(transform.position.y - _hoverHeight) < 0.1f)
            TransitionToState(FlightState.Hover);
    }

    private void EnterAbort()
    {
        // abort climb and despawn
        _rotorsSpinning = false;
        _hmi.StopHoverHum();
    }

    private void PerformAbort()
    {
        var target = new Vector3(transform.position.x, _abortClimbHeight, transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, target, _cruiseSpeed * Time.deltaTime);
        if (transform.position.y >= _abortClimbHeight - 0.1f)
            Destroy(gameObject);
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
        foreach (var leg in _legs)
        {
            var e = leg.localEulerAngles;
            float curr = e.x > 180f ? e.x - 360f : e.x;
            float nxt = Mathf.MoveTowards(curr, _gearTargetAngle, _legRotateSpeed * Time.deltaTime);
            leg.localEulerAngles = new Vector3(nxt, e.y, e.z);
            if (Mathf.Abs(nxt - _gearTargetAngle) > 0.01f)
                done = false;
        }
        if (done) _gearAnimating = false;
    }
}