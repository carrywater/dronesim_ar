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
        var verticalCheck = new PositionValidator.AxisCheck { checkX = false, checkZ = false };
        Vector3 hoverTarget = new Vector3(startPos.x, _drone.HoverHeight, startPos.z);
        yield return PositionValidator.WaitForStateAndPosition(
            _drone,
            DroneController.FlightState.Hover,
            hoverTarget,
            verticalCheck
        );
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

            // 0. Set new random target position
            Vector3 targetPos = _targetPositioner.GetRandomPositionInZone("NavigationZone");
            targetPos.y = _drone.transform.position.y; // keep at hover height
            _targetPositioner.SetActiveTargetPosition(targetPos);
            Debug.Log($"[C0] Selected target position: {targetPos}");

            // 1. Show ring cue and spline
            Debug.Log("[C0] Showing ring cue and spline");
            _interactionManager.ShowCue("ring");
            _splineManager.ShowSpline();

            // 2. Cruise to target and hover there for 3 seconds
            yield return StartCoroutine(_drone.HoverAtPosition(targetPos, 3f));
            Debug.Log($"[C0] Completed hover at target position: {_drone.DroneOffset.position}");

            // 3. Hide visual cues
            Debug.Log("[C0] Hiding ring cue and spline");
            _interactionManager.HideCue("ring");
            _splineManager.HideSpline();

            // 4. Signal uncertainty
            Debug.Log("[C0] Setting HMI to Uncertain");
            _hmi.SetStatus(DroneHMI.HMIState.Uncertain, true);
            yield return StartCoroutine(WaitForHMIComplete());
            Debug.Log("[C0] Uncertainty HMI complete");

            // 5. Return to abort height
            Vector3 abortPos = new Vector3(targetPos.x, _drone.AbortHeight, targetPos.z);
            Debug.Log($"[C0] Calling TransitionToAbort with abortPos={abortPos}");
            _drone.TransitionToAbort();
            yield return PositionValidator.WaitForStateAndPosition(
                _drone,
                DroneController.FlightState.Aborting,
                abortPos,
                verticalCheck
            );
            Debug.Log($"[C0] Reached abort position: {_drone.DroneOffset.position}");
        }

        // 9. Signal mission abort
        _hmi.SetStatus(DroneHMI.HMIState.Abort, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C0] Abort HMI complete");

        // 10. Final abort movement
        Vector3 finalAbortPos = new Vector3(_drone.transform.position.x, _drone.AbortHeight, _drone.transform.position.z);
        _drone.TransitionToAbort();
        yield return PositionValidator.WaitForStateAndPosition(
            _drone,
            DroneController.FlightState.Aborting,
            finalAbortPos,
            verticalCheck
        );
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
        Debug.Log("[C1] Starting C-1 Scenario (Confirm)");

        // 1. Start at abort height
        Vector3 startPos = new Vector3(-3f, 0f, 2f);
        EnsureDroneAt(startPos, _drone.AbortHeight);
        Debug.Log($"[C1] Initial position set: root={startPos}, offset Y={_drone.AbortHeight}");

        yield return new WaitForSeconds(config.interStepPause);

        // 2. Start rotors and wait for HMI
        _rotors.StartRotors();
        _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C1] HMI sequence complete");

        // 3. Transition to hover height
        _drone.TransitionToHover();
        Debug.Log($"[C1] Transitioning to hover at height={_drone.HoverHeight}");
        
        // Wait for hover with vertical check only
        var verticalCheck = new PositionValidator.AxisCheck { checkX = false, checkZ = false };
        Vector3 hoverTarget = new Vector3(startPos.x, _drone.HoverHeight, startPos.z);
        yield return PositionValidator.WaitForStateAndPosition(
            _drone,
            DroneController.FlightState.Hover,
            hoverTarget,
            verticalCheck
        );
        Debug.Log($"[C1] Reached hover position: {_drone.DroneOffset.position}");

        // 4. Wait in hover
        yield return new WaitForSeconds(config.interStepPause);

        // 5. Get initial random position from NavigationZone
        Vector3 initialTargetPos = _targetPositioner.GetRandomPositionInZone("NavigationZone");
        initialTargetPos.y = _drone.transform.position.y; // keep at hover height
        _targetPositioner.SetActiveTargetPosition(initialTargetPos);
        Debug.Log($"[C1] Selected initial target position: {initialTargetPos}");

        // 6. Cruise to target and hover
        yield return StartCoroutine(_drone.HoverAtPosition(initialTargetPos, 3f));
        Debug.Log($"[C1] Completed hover at initial target position: {_drone.DroneOffset.position}");

        // 7. Signal uncertainty
        _hmi.SetStatus(DroneHMI.HMIState.Uncertain, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C1] Uncertainty HMI complete");

        // 8. Wait
        yield return new WaitForSeconds(config.interStepPause);

        // 9. Show interaction zone and enable confirmation
        _interactionManager.ShowCue("interaction_zone");
        _interactionManager.ShowCue("thumbs_up");
        _interactionManager.ShowCue("thumbs_down");
        _interactionManager.StartInteraction("confirm");
        Debug.Log("[C1] Enabled confirmation interaction and showing thumbs cues");

        // 10. Landing attempt loop
        for (int attempt = 0; attempt < config.landingAttempts; attempt++)
        {
            Debug.Log($"[C1] Starting landing attempt {attempt + 1}/{config.landingAttempts}");
            yield return new WaitForSeconds(config.interStepPause);

            // Get random position from InteractionZone
            Vector3 targetPos = _targetPositioner.GetRandomPositionInZone("InteractionZone");
            targetPos.y = _drone.transform.position.y; // keep at hover height
            _targetPositioner.SetActiveTargetPosition(targetPos);
            Debug.Log($"[C1] Selected target position: {targetPos}");

            // Show ring cue and spline
            _interactionManager.ShowCue("ring");
            _splineManager.ShowSpline();
            Debug.Log("[C1] Showing ring cue and spline");

            // Wait for user confirmation
            bool isConfirmed = false;
            _interactionManager.OnInteractionComplete += () => isConfirmed = true;
            yield return new WaitUntil(() => isConfirmed);
            _interactionManager.OnInteractionComplete -= () => isConfirmed = true;

            // Hide thumbs cues after interaction
            _interactionManager.HideCue("thumbs_up");
            _interactionManager.HideCue("thumbs_down");

            // Check if user confirmed or declined
            if (isConfirmed)
            {
                // Play confirmation HMI
                _hmi.SetStatus(DroneHMI.HMIState.Success, true);
                yield return StartCoroutine(WaitForHMIComplete());
                Debug.Log("[C1] Confirmation HMI complete");

                // Extend legs
                _landingGear.RetractLegs();
                yield return new WaitForSeconds(config.interStepPause);

                // Cruise to target and hover briefly
                yield return StartCoroutine(_drone.HoverAtPosition(targetPos, 1f));
                Debug.Log($"[C1] Completed hover at target position: {_drone.DroneOffset.position}");

                // Begin landing
                float descentHeight = _drone.AbortHeight * 0.8f;
                Vector3 descentPos = targetPos;
                descentPos.y = descentHeight;
                _drone.TransitionToLanding(descentPos);
                yield return PositionValidator.WaitForStateAndPosition(
                    _drone,
                    DroneController.FlightState.Descending,
                    descentPos,
                    verticalCheck
                );
                Debug.Log($"[C1] Completed landing at: {_drone.DroneOffset.position}");

                // Landing successful, break the loop
                break;
            }
            else
            {
                // Play decline HMI
                _hmi.SetStatus(DroneHMI.HMIState.Reject, true);
                yield return StartCoroutine(WaitForHMIComplete());
                Debug.Log("[C1] Decline HMI complete");

                // Hide visual cues
                _interactionManager.HideCue("ring");
                _splineManager.HideSpline();
            }
        }

        // Hide interaction zone at the end
        _interactionManager.HideCue("interaction_zone");

        // 11. Show completion HMI
        _hmi.SetStatus(DroneHMI.HMIState.Success, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C1] Completion HMI complete");

        // 12. Stop rotors
        _rotors.StopRotors();
        
        Debug.Log("[C1] Completed C-1 Scenario");
    }
    
    #endregion
    
    #region C2 Scenario Implementation (High - Guidance)
    
    /// <summary>
    /// Run the C2 scenario (high involvement with guidance)
    /// </summary>
    private IEnumerator RunC2Scenario(ScenarioConfig config)
    {
        Debug.Log("[C2] Starting C-2 Scenario (Guidance)");

        // 1. Start at abort height
        Vector3 startPos = new Vector3(-3f, 0f, 2f);
        EnsureDroneAt(startPos, _drone.AbortHeight);
        Debug.Log($"[C2] Initial position set: root={startPos}, offset Y={_drone.AbortHeight}");

        yield return new WaitForSeconds(config.interStepPause);

        // 2. Start rotors and wait for HMI
        _rotors.StartRotors();
        _hmi.SetStatus(DroneHMI.HMIState.PromptGuide, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C2] HMI sequence complete");

        // 3. Transition to hover height
        _drone.TransitionToHover();
        Debug.Log($"[C2] Transitioning to hover at height={_drone.HoverHeight}");
        
        // Wait for hover with vertical check only
        var verticalCheck = new PositionValidator.AxisCheck { checkX = false, checkZ = false };
        Vector3 hoverTarget = new Vector3(startPos.x, _drone.HoverHeight, startPos.z);
        yield return PositionValidator.WaitForStateAndPosition(
            _drone,
            DroneController.FlightState.Hover,
            hoverTarget,
            verticalCheck
        );
        Debug.Log($"[C2] Reached hover position: {_drone.DroneOffset.position}");

        // 4. Wait in hover
        yield return new WaitForSeconds(config.interStepPause);

        // 5. Get initial random position from NavigationZone
        Vector3 initialTargetPos = _targetPositioner.GetRandomPositionInZone("NavigationZone");
        initialTargetPos.y = _drone.transform.position.y; // keep at hover height
        _targetPositioner.SetActiveTargetPosition(initialTargetPos);
        Debug.Log($"[C2] Selected initial target position: {initialTargetPos}");

        // 6. Cruise to target and hover
        yield return StartCoroutine(_drone.HoverAtPosition(initialTargetPos, 3f));
        Debug.Log($"[C2] Completed hover at initial target position: {_drone.DroneOffset.position}");

        // 7. Signal uncertainty
        _hmi.SetStatus(DroneHMI.HMIState.Uncertain, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C2] Uncertainty HMI complete");

        // 8. Wait
        yield return new WaitForSeconds(config.interStepPause);

        // 9. Show interaction zone and enable point gesture
        _interactionManager.ShowCue("interaction_zone");
        _interactionManager.StartInteraction("point");
        Debug.Log("[C2] Enabled point gesture interaction");

        // 10. Landing attempt loop
        for (int attempt = 0; attempt < config.landingAttempts; attempt++)
        {
            Debug.Log($"[C2] Starting landing attempt {attempt + 1}/{config.landingAttempts}");
            yield return new WaitForSeconds(config.interStepPause);

            // Wait for user to point to a location
            bool hasPointed = false;
            Vector3 pointedPosition = Vector3.zero;
            _interactionManager.OnInteractionComplete += () => 
            {
                hasPointed = true;
                // Get the pointed position from the interaction handler
                if (_interactionManager.TryGetPointedPosition(out Vector3 pos))
                {
                    pointedPosition = pos;
                }
            };
            yield return new WaitUntil(() => hasPointed);
            _interactionManager.OnInteractionComplete -= () => hasPointed = true;

            // Set the active target to the pointed position
            pointedPosition.y = _drone.transform.position.y; // keep at hover height
            _targetPositioner.SetActiveTargetPosition(pointedPosition);
            Debug.Log($"[C2] User pointed to position: {pointedPosition}");

            // Show ring cue and spline at pointed position
            _interactionManager.ShowCue("ring");
            _splineManager.ShowSpline();
            Debug.Log("[C2] Showing ring cue and spline at pointed position");

            // Wait for user confirmation of the pointed position
            bool isConfirmed = false;
            _interactionManager.OnInteractionComplete += () => isConfirmed = true;
            yield return new WaitUntil(() => isConfirmed);
            _interactionManager.OnInteractionComplete -= () => isConfirmed = true;

            if (isConfirmed)
            {
                // Play confirmation HMI
                _hmi.SetStatus(DroneHMI.HMIState.Success, true);
                yield return StartCoroutine(WaitForHMIComplete());
                Debug.Log("[C2] Confirmation HMI complete");

                // Extend legs
                _landingGear.RetractLegs();
                yield return new WaitForSeconds(config.interStepPause);

                // Cruise to target and hover briefly
                yield return StartCoroutine(_drone.HoverAtPosition(pointedPosition, 1f));
                Debug.Log($"[C2] Completed hover at target position: {_drone.DroneOffset.position}");

                // Begin landing
                float descentHeight = _drone.AbortHeight * 0.8f;
                Vector3 descentPos = pointedPosition;
                descentPos.y = descentHeight;
                _drone.TransitionToLanding(descentPos);
                yield return PositionValidator.WaitForStateAndPosition(
                    _drone,
                    DroneController.FlightState.Descending,
                    descentPos,
                    verticalCheck
                );
                Debug.Log($"[C2] Completed landing at: {_drone.DroneOffset.position}");

                // Landing successful, break the loop
                break;
            }
            else
            {
                // Play decline HMI
                _hmi.SetStatus(DroneHMI.HMIState.Reject, true);
                yield return StartCoroutine(WaitForHMIComplete());
                Debug.Log("[C2] Decline HMI complete");

                // Hide visual cues
                _interactionManager.HideCue("ring");
                _splineManager.HideSpline();
            }
        }

        // Hide interaction zone at the end
        _interactionManager.HideCue("interaction_zone");

        // 11. Show completion HMI
        _hmi.SetStatus(DroneHMI.HMIState.Success, true);
        yield return StartCoroutine(WaitForHMIComplete());
        Debug.Log("[C2] Completion HMI complete");

        // 12. Stop rotors
        _rotors.StopRotors();
        
        Debug.Log("[C2] Completed C-2 Scenario");
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