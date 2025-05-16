using UnityEngine;
using System.Collections;

public enum ScenarioType
{
    C0_Abort,
    C1_Confirm,
    C2_Guidance
}

public class ScenarioManager : MonoBehaviour
{
    [Header("Scenario Selection")]
    [SerializeField] private ScenarioType _testScenario = ScenarioType.C0_Abort;
    [SerializeField] private bool _runSingleScenario = true;
    [SerializeField] private bool _useSequencer = false;
    [SerializeField] private ScenarioSequencer _sequencer;

    [Header("References")]
    [SerializeField] private DroneController _drone;
    [SerializeField] private DroneHMI _hmi;
    [SerializeField] private InteractionManager _interactionManager;

    [Header("Timings (seconds)")]
    [SerializeField] private float _initialHoverTime = 2f;
    [SerializeField] private float _cruiseTime = 2f;
    [SerializeField] private float _landingWaitTime = 1f;
    [SerializeField] private float _postAbortHoverTime = 2f;
    [SerializeField] private float _landingDepth = 1f;  // how far below the hover height to descend during abort landing
    [SerializeField] private float _scenarioDelay = 2f; // delay between scenarios

    private bool _scenarioRunning = false;

    private void Awake()
    {
        // Subscribe to drone state changes to update HMI accordingly
        if (_drone != null)
        {
            _drone.OnStateChanged += HandleDroneStateChanged;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (_drone != null)
        {
            _drone.OnStateChanged -= HandleDroneStateChanged;
        }
    }

    private void HandleDroneStateChanged(DroneController.FlightState newState)
    {
        // Update HMI based on drone flight state
        switch (newState)
        {
            case DroneController.FlightState.Idle:
                _hmi.StopHoverHum();
                
                // Hide any AR interaction elements when drone goes to idle
                if (_interactionManager != null)
                {
                    _interactionManager.HideAllInteractions();
                }
                break;
                
            case DroneController.FlightState.Hover:
                _hmi.PlayHoverHum();
                break;
                
            case DroneController.FlightState.CruiseToTarget:
                _hmi.PlayHoverHum();
                break;
                
            case DroneController.FlightState.Landing:
                // Stop hover hum during landing to let scenario control sounds
                _hmi.StopHoverHum();
                break;
                
            case DroneController.FlightState.LandAbort:
                // Let scenario control sounds during landing abort
                break;
                
            case DroneController.FlightState.Abort:
                // Let scenario control sounds during abort
                break;
        }
    }

    private void Start()
    {
        StartScenario();
    }

    public void StartScenario()
    {
        if (_scenarioRunning)
        {
            Debug.LogWarning("Cannot start a new scenario while one is running");
            return;
        }

        if (_runSingleScenario)
        {
            StartCoroutine(RunSelectedScenario());
        }
        else
        {
            StartCoroutine(RunScenarioSequence());
        }
    }

    private IEnumerator RunSelectedScenario()
    {
        _scenarioRunning = true;
        Debug.Log($"Starting single scenario: {_testScenario}");

        // Reset drone and HMI state
        ResetDroneState();

        // Run the selected scenario
        yield return StartCoroutine(GetScenarioCoroutine(_testScenario));

        _scenarioRunning = false;
        Debug.Log($"Completed scenario: {_testScenario}");
    }

    private IEnumerator RunScenarioSequence()
    {
        _scenarioRunning = true;
        
        if (_useSequencer && _sequencer != null)
        {
            // Use the sequencer to determine scenario order
            for (int i = 0; i < 3; i++) // Run all 3 scenarios
            {
                ScenarioType nextScenario = _sequencer.GetNextScenario();
                Debug.Log($"Starting sequenced scenario: {nextScenario}");
                
                // Reset drone and HMI state
                ResetDroneState();
                
                // Run the scenario
                yield return StartCoroutine(GetScenarioCoroutine(nextScenario));
                
                // Wait between scenarios
                if (i < 2) // Don't wait after the last scenario
                {
                    yield return new WaitForSeconds(_scenarioDelay);
                }
            }
        }
        else
        {
            // Run predefined sequence: C0 -> C1 -> C2
            // C-0 scenario first (high autonomy/abort)
            ResetDroneState();
            yield return StartCoroutine(RunC0Scenario());
            
            // Wait between scenarios
            yield return new WaitForSeconds(_scenarioDelay);
            
            // C-1 scenario (confirm)
            ResetDroneState();
            yield return StartCoroutine(RunC1Scenario());
            
            // Wait between scenarios
            yield return new WaitForSeconds(_scenarioDelay);
            
            // C-2 scenario (guidance)
            ResetDroneState();
            yield return StartCoroutine(RunC2Scenario());
        }
        
        _scenarioRunning = false;
        Debug.Log("Completed all scenarios");
    }

    private IEnumerator GetScenarioCoroutine(ScenarioType scenario)
    {
        switch (scenario)
        {
            case ScenarioType.C0_Abort:
                yield return StartCoroutine(RunC0Scenario());
                break;
            case ScenarioType.C1_Confirm:
                yield return StartCoroutine(RunC1Scenario());
                break;
            case ScenarioType.C2_Guidance:
                yield return StartCoroutine(RunC2Scenario());
                break;
        }
    }

    private void ResetDroneState()
    {
        // Reset HMI state
        _hmi.SetStatus(DroneHMI.HMIState.Idle);
        
        // If the drone is in an abort or landing state, return it to hover
        _drone.ReturnToHover();
        
        // Hide any interaction elements
        if (_interactionManager != null)
        {
            _interactionManager.HideAllInteractions();
        }
    }

    private IEnumerator RunC0Scenario()
    {
        Debug.Log("Starting C-0 Scenario (Autonomous/Abort)");

        // 1. Gentle descent to hover height
        // The drone already transitions to hover state in ResetDroneState()
        // Just wait for it to reach hover height
        yield return new WaitForSeconds(_initialHoverTime);
        
        // 2. Hover and play confirmation sound
        _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm);
        
        // Wait in hover while facing participant
        yield return new WaitForSeconds(_initialHoverTime);

        // 3. Perform two land-abort loops
        for (int i = 0; i < 2; i++)
        {
            // 3.1 Randomize cruise location
            Vector3 navPoint = _interactionManager.RandomizeC0Position();
            
            // 3.2 Move to the location while looking at participant
            _drone.StartCruiseTo(navPoint);
            yield return new WaitForSeconds(_cruiseTime);
            
            // 3.2.1 Wait at new location
            yield return new WaitForSeconds(_initialHoverTime);
            
            // 3.3 Attempt to land but stop at the lowest point
            Vector3 landingSpot = _interactionManager.GetC0TargetPosition();
            landingSpot.y -= _landingDepth;
            _drone.BeginLanding(landingSpot);
            
            // Play hover hum during landing attempt
            _hmi.PlayHoverHum();
            yield return new WaitForSeconds(_landingWaitTime);
            
            // 3.4 Communicate uncertainty (play sound and flash lights)
            _hmi.SetStatus(DroneHMI.HMIState.Uncertain);
            
            // Maintain hover hum during uncertainty
            _hmi.PlayHoverHum();
            yield return new WaitForSeconds(_landingWaitTime);
            
            // 3.5 Raise back up to hover height
            _drone.LandAbort();
            yield return new WaitForSeconds(_postAbortHoverTime);
            
            // Reset HMI state after uncertainty
            _hmi.SetStatus(DroneHMI.HMIState.Idle);
            
            // Ensure hover hum continues
            _hmi.PlayHoverHum();
        }

        // 4. After two attempts, perform full abort sequence
        
        // 4.1 Communicate abort mission
        _hmi.SetStatus(DroneHMI.HMIState.Abort);
        
        // 4.2 Wait to let the abort message be communicated
        yield return new WaitForSeconds(_landingWaitTime);
        
        // 4.3 Abort mission (rise up while keeping rotors on)
        _drone.Abort();
        
        // Keep hover hum during abort flight
        _hmi.PlayHoverHum();
        
        Debug.Log("Completed C-0 Scenario");
    }
    
    private IEnumerator RunC1Scenario()
    {
        Debug.Log("Starting C-1 Scenario (Confirm)");
        
        // 1. Move drone to interaction zone
        Vector3 interactionPos = _interactionManager.transform.position;
        interactionPos.y = _drone.transform.position.y; // Keep the same height
        _drone.StartCruiseTo(interactionPos);
        yield return new WaitForSeconds(_cruiseTime);
        
        // 2. Run the C-1 confirmation scenario
        _interactionManager.StartC1Confirm();
        
        // 3. Wait until C-1 scenario is completed
        while (!_interactionManager.IsC1Completed)
        {
            yield return null;
        }
        
        Debug.Log("Completed C-1 Scenario");
    }
    
    private IEnumerator RunC2Scenario()
    {
        Debug.Log("Starting C-2 Scenario (Guidance)");
        
        // 1. Move drone to interaction zone
        Vector3 interactionPos = _interactionManager.transform.position;
        interactionPos.y = _drone.transform.position.y; // Keep the same height
        _drone.StartCruiseTo(interactionPos);
        yield return new WaitForSeconds(_cruiseTime);
        
        // 2. Run the C-2 guidance scenario
        _interactionManager.StartC2Guidance();
        
        // 3. Wait until C-2 scenario is completed
        while (!_interactionManager.IsC2Completed)
        {
            yield return null;
        }
        
        Debug.Log("Completed C-2 Scenario");
    }

    // Method to manually trigger a scenario from UI or other components
    public void TriggerScenario(ScenarioType scenario)
    {
        if (!_scenarioRunning)
        {
            _testScenario = scenario;
            _runSingleScenario = true;
            StartScenario();
        }
        else
        {
            Debug.LogWarning("Cannot trigger scenario while another is running");
        }
    }
} 