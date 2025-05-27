using UnityEngine;
using System.Collections;
using TMPro;
using Utils;
using Visualization;

/// <summary>
/// Orchestrates C-0/C-1/C-2 scenario flows; drives drone, HMI, and AR interfaces
/// </summary>
public class ScenarioManager : MonoBehaviour
{
    #region Scenario Types
    
    public enum ScenarioType
    {
        C0_Abort,    // High autonomy with timeout abort
        C1_Confirm,  // Medium autonomy with confirmation request
        C2_Guidance  // High involvement with user guidance
    }
    
    #endregion
    
    #region Editor Settings
    
    [Header("Scenario Selection")]
    [Tooltip("If checked, run only a single scenario (for testing). Otherwise, scenarios are randomized/counterbalanced.")]
    [SerializeField] private bool _runSingleScenario = false;
    
    [Tooltip("Which scenario type to run (only visible if 'Run Single Scenario' is checked)")]
    [SerializeField] private ScenarioType _testScenario = ScenarioType.C0_Abort;
    
    [Header("Scenario Sequencing")]
    [Tooltip("Sequencer for randomized/counterbalanced scenario order")]
    [SerializeField] private ScenarioSequencer _sequencer;
    
    [Header("Component References")]
    [SerializeField] private DroneController _drone;
    [SerializeField] private DroneHMI _hmi;
    [SerializeField] private InteractionManager _interactionManager;
    [SerializeField] private TextMeshProUGUI _confidenceText;
    [SerializeField] private TargetPositioner _targetPositioner;
    [SerializeField] private DroneManager _droneManager;
    [SerializeField] private DroneRotorController _rotors;
    [SerializeField] private DroneLandingGear _landingGear;
    [SerializeField] private SplineManager _splineManager;
    
    [Header("Scenario Configurations")]
    [Tooltip("Config for C0 scenario")]
    [SerializeField] private ScenarioConfig _c0Config = new ScenarioConfig { interStepPause = 0.5f };
    [Tooltip("Config for C1 scenario")]
    [SerializeField] private ScenarioConfig _c1Config = new ScenarioConfig { interStepPause = 0.5f };
    [Tooltip("Config for C2 scenario")]
    [SerializeField] private ScenarioConfig _c2Config = new ScenarioConfig { interStepPause = 0.5f };

    [Header("Drone Initial Placement")]
    [Tooltip("Initial X position for drone root")] [SerializeField] private float _droneStartX = 0f;
    [Tooltip("Initial Z position for drone root")] [SerializeField] private float _droneStartZ = 0f;
    public enum DroneOffsetYMode { AbortHeight, HoverHeight, Ground }
    [Tooltip("How to set the child Y offset for Drone Runtime Offset")] [SerializeField] private DroneOffsetYMode _droneOffsetYMode = DroneOffsetYMode.AbortHeight;

    [System.Serializable]
    private class ScenarioConfig
    {
        public float interStepPause;
        public int landingAttempts;

        public ScenarioConfig()
        {
            interStepPause = 2f;
            landingAttempts = 2;
        }
    }
    
    #endregion
    
    #region Private State Variables
    
    private bool _scenarioRunning = false;
    private bool _hmiComplete = false;  // Track HMI completion state
    
    // C2 scenario state
    private Vector3 _c2GuidancePosition;
    
    #endregion
    
    #region Helper Methods

    /// <summary>
    /// Helper coroutine to wait for HMI sound/animation completion
    /// </summary>
    private IEnumerator WaitForHMIComplete()
    {
        _hmiComplete = false;
        _hmi.OnSoundComplete += () => _hmiComplete = true;
        yield return new WaitUntil(() => _hmiComplete);
        _hmi.OnSoundComplete -= () => _hmiComplete = true;
    }

    private float GetDroneOffsetY()
    {
        switch (_droneOffsetYMode)
        {
            case DroneOffsetYMode.AbortHeight:
                return _drone != null ? _drone.AbortHeight : 0f;
            case DroneOffsetYMode.HoverHeight:
                return _drone != null ? _drone.HoverHeight : 0f;
            case DroneOffsetYMode.Ground:
            default:
                return 0f;
        }
    }

    // Helper to configure the drone using its inspector values
    private void ConfigureDroneFromInspector()
    {
        _drone.Configure(
            _drone.HoverHeight,
            _drone.HoverSpeed,
            _drone.CruiseSpeed,
            _drone.AbortHeight,
            _drone.LandingSpeed,
            _drone.AbortSpeed,
            _drone.AccelerationTime,
            _drone.DecelerationTime
        );
    }

    // Helper to ensure the drone is at the correct position and offset, handling both initialization and repositioning
    private void EnsureDroneAt(Vector3 worldPosition, float offsetY)
    {
        if (!_drone.IsInitialized)
        {
            ConfigureDroneFromInspector();
            _drone.SpawnAt(worldPosition, offsetY);
            Debug.Log($"[C0] After SpawnAt: Drone root pos = {_drone.transform.position}, Offset Y = {_drone.transform.Find("Drone Runtime Offset").localPosition.y}");
        }
        else
        {
            _drone.MoveTo(worldPosition, offsetY);
            Debug.Log($"[C0] After MoveTo: Drone root pos = {_drone.transform.position}, Offset Y = {_drone.transform.Find("Drone Runtime Offset").localPosition.y}");
        }
    }

    #endregion
    
    #region Unity Lifecycle Methods
    
    private void Awake()
    {
        ValidateComponents();
    }
    
    private void Start()
    {
        StartScenario();
    }
    
    #endregion
    
    #region Initialization and Setup
    
    /// <summary>
    /// Validate that all required components exist
    /// </summary>
    private void ValidateComponents()
    {
        if (_sequencer == null)
            Debug.LogWarning("ScenarioSequencer reference missing on ScenarioManager! Randomized scenario order will not work.");
        if (_drone == null)
            Debug.LogError("DroneController reference missing on ScenarioManager!");
        if (_hmi == null)
            Debug.LogError("DroneHMI reference missing on ScenarioManager!");
        if (_interactionManager == null)
            Debug.LogError("InteractionManager reference missing on ScenarioManager!");
        if (_targetPositioner == null)
            Debug.LogError("TargetPositioner reference missing on ScenarioManager!");
    }
    
    #endregion
    
    #region Scenario Flow Control
    
    /// <summary>
    /// Begin the scenario sequence
    /// </summary>
    public void StartScenario()
    {
        if (_scenarioRunning)
        {
            Debug.LogWarning("Cannot start a new scenario while one is running");
            return;
        }

        if (_runSingleScenario)
        {
            StartCoroutine(RunSingleScenario(_testScenario));
        }
        else
        {
            StartCoroutine(RunRandomizedSequence());
        }
    }
    
    /// <summary>
    /// Run the currently selected scenario
    /// </summary>
    private IEnumerator RunSingleScenario(ScenarioType scenario)
    {
        _scenarioRunning = true;
        Debug.Log($"Starting single scenario: {scenario}");
        yield return StartCoroutine(RunScenarioWithConfig(scenario));
        _scenarioRunning = false;
        Debug.Log($"Completed scenario: {scenario}");
    }
    
    /// <summary>
    /// Run a randomized sequence of scenarios
    /// </summary>
    private IEnumerator RunRandomizedSequence()
    {
        _scenarioRunning = true;
        if (_sequencer == null)
        {
            Debug.LogError("No ScenarioSequencer assigned! Cannot randomize scenario order.");
            yield break;
        }

        // Example: run all three scenarios in randomized order
        for (int i = 0; i < 3; i++)
        {
            var scenario = _sequencer.GetNextScenario();
            Debug.Log($"Running randomized scenario: {scenario}");
            yield return StartCoroutine(RunScenarioWithConfig(scenario));
        }

        _scenarioRunning = false;
        Debug.Log("Completed all randomized scenarios");
    }
    
    /// <summary>
    /// Get the appropriate coroutine for a scenario type
    /// </summary>
    private IEnumerator RunScenarioWithConfig(ScenarioType scenario)
    {
        ScenarioConfig config = GetConfigForScenario(scenario);

        // Example: pass config.interStepPause to scenario logic
        switch (scenario)
        {
            case ScenarioType.C0_Abort:
                yield return StartCoroutine(RunC0Scenario(config));
                break;
            case ScenarioType.C1_Confirm:
                yield return StartCoroutine(RunC1Scenario(config));
                break;
            case ScenarioType.C2_Guidance:
                yield return StartCoroutine(RunC2Scenario(config));
                break;
        }
    }
    
    private ScenarioConfig GetConfigForScenario(ScenarioType scenario)
    {
        switch (scenario)
        {
            case ScenarioType.C0_Abort: return _c0Config;
            case ScenarioType.C1_Confirm: return _c1Config;
            case ScenarioType.C2_Guidance: return _c2Config;
            default: return _c0Config;
        }
    }
    
    #endregion
    
    #region C0 Scenario Implementation (High Autonomy - Abort)
    
    /// <summary>
    /// Run the C0 scenario (high autonomy with abort)
    /// </summary>
    private IEnumerator RunC0Scenario(ScenarioConfig config)
    {
        Debug.Log("[C0] Starting C-0 Scenario (Abort)");

        // 1. Start at abort height
        Vector3 startPos = new Vector3(-3f, 0f, 2f);
        EnsureDroneAt(startPos, _drone.AbortHeight);
        Debug.Log($"[C0] Initial position set: root={startPos}, offset Y={_drone.AbortHeight}");

        yield return new WaitForSeconds(config.interStepPause);

        // 2. Transition to hover height
        _drone.TransitionToHover();
        Debug.Log($"[C0] Transitioning to hover at height={_drone.HoverHeight}");
        
        // Wait for hover with vertical check only
        yield return new WaitUntil(() => !_drone.IsMovementComplete());
        Debug.Log($"[C0] Reached hover position: {_drone.DroneOffset.position}");

        // 3. Start rotors and wait for HMI
        _rotors.StartRotors();
        _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C0] HMI sequence complete");

        // Retract landing gear before starting landing attempts
         yield return new WaitForSeconds(config.interStepPause);
        _landingGear.RetractLegs();
         yield return new WaitForSeconds(config.interStepPause);

        // 4-8. Landing attempt loop
        for (int attempt = 0; attempt < config.landingAttempts; attempt++)
        {
            Debug.Log($"[C0] Starting landing attempt {attempt + 1}/{config.landingAttempts}");
            yield return new WaitForSeconds(config.interStepPause);

            // Get random position from NavigationZone
            Vector3 targetPos = _targetPositioner.GetRandomPositionInZone("NavigationZone");
            targetPos.y = _drone.transform.position.y; // keep at hover height
            // Move the active target to the new random position
            _targetPositioner.SetActiveTargetPosition(targetPos);
            Debug.Log($"[C0] Selected target position: {targetPos}");

            // Show ring cue and spline before descent
            Debug.Log("[C0] Showing ring cue and spline");
            _interactionManager.ShowCue("ring");
            _splineManager.ShowSpline();
             yield return new WaitForSeconds(config.interStepPause);

            // Move horizontally to target (XZ only)
            Debug.Log("[C0] Calling TransitionToCruise");
            _drone.TransitionToCruise(targetPos);
            yield return new WaitUntil(() => !_drone.IsMovementComplete());
            Debug.Log($"[C0] Reached cruise position: {_drone.DroneOffset.position}");

            // Wait for cruise to finish and state to be Cruising
            yield return new WaitUntil(() => !_drone.IsMovementComplete());
            yield return new WaitUntil(() => _drone.CurrentState == DroneController.FlightState.Cruising);

            // Begin descent to 80% of hover height, directly above the active target
            float descentHeight = _drone.HoverHeight * 0.8f;
            Vector3 descentPos = targetPos;
            descentPos.y = descentHeight;
            Debug.Log($"[C0] Calling TransitionToLanding with descentPos={descentPos}");
            _drone.TransitionToLanding(descentPos);
            yield return new WaitUntil(() => !_drone.IsMovementComplete());
            Debug.Log($"[C0] Reached descent position: {_drone.DroneOffset.position}");

            // Hide ring cue and spline after descent
            Debug.Log("[C0] Hiding ring cue and spline");
            _interactionManager.HideCue("ring");
            _splineManager.HideSpline();

            // Signal uncertainty
            Debug.Log("[C0] Setting HMI to Uncertain");
            _hmi.SetStatus(DroneHMI.HMIState.Uncertain, true);
            yield return StartCoroutine(WaitForHMIComplete());
            Debug.Log("[C0] Uncertainty HMI complete");

            // Wait for current movement to complete
            yield return new WaitUntil(() => !_drone.IsMovementComplete());
            
            // Return to hover height
            Vector3 hoverPos = new Vector3(targetPos.x, _drone.HoverHeight, targetPos.z);
            Debug.Log($"[C0] Returning to hover height at {hoverPos}");
            _drone.TransitionToHover();
            yield return new WaitUntil(() => !_drone.IsMovementComplete());
            Debug.Log($"[C0] Reached hover position: {_drone.DroneOffset.position}");
        }

        // 9. Signal mission abort
        _hmi.SetStatus(DroneHMI.HMIState.Abort, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C0] Abort HMI complete");

        // 10. Final abort movement
        Vector3 finalAbortPos = new Vector3(_drone.transform.position.x, _drone.AbortHeight, _drone.transform.position.z);
        _drone.TransitionToAbort();
        yield return new WaitUntil(() => !_drone.IsMovementComplete());
        Debug.Log($"[C0] Reached final abort position: {_drone.DroneOffset.position}");

        // 11. Stay at abort height
        yield return new WaitForSeconds(2f);

        // Stop rotors
        _rotors.StopRotors();
        
        Debug.Log("[C0] Completed C-0 Scenario");
    }
    
    #endregion
    
    #region C1 Scenario Implementation (Medium - Confirm)
    
    /// <summary>
    /// Run the C1 scenario (medium autonomy with confirmation)
    /// </summary>
    private IEnumerator RunC1Scenario(ScenarioConfig config)
    {
        Debug.Log("Starting C-1 Scenario (Confirm)");
        yield return new WaitForSeconds(config.interStepPause);
        Debug.Log("Completed C-1 Scenario");
    }
    
    #endregion
    
    #region C2 Scenario Implementation (High - Guidance)
    
    /// <summary>
    /// Run the C2 scenario (high involvement with guidance)
    /// </summary>
    private IEnumerator RunC2Scenario(ScenarioConfig config)
    {
        Debug.Log("Starting C-2 Scenario (Guidance)");
        yield return new WaitForSeconds(config.interStepPause);
        Debug.Log("Completed C-2 Scenario");
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Manually trigger a specific scenario (for UI or external calls)
    /// </summary>
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
    
    #endregion
} 