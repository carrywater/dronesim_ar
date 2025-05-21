using UnityEngine;
using System.Collections;
using TMPro;
using Utils;

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
    
    [Header("Scenario Configurations")]
    [Tooltip("Config for C0 scenario")]
    [SerializeField] private ScenarioConfig _c0Config = new ScenarioConfig { interStepPause = 0.5f };
    [Tooltip("Config for C1 scenario")]
    [SerializeField] private ScenarioConfig _c1Config = new ScenarioConfig { interStepPause = 0.5f };
    [Tooltip("Config for C2 scenario")]
    [SerializeField] private ScenarioConfig _c2Config = new ScenarioConfig { interStepPause = 0.5f };

    [System.Serializable]
    public struct ScenarioConfig
    {
        [Tooltip("Pause between steps for UX (seconds)")]
        public float interStepPause;
        // Add more fields here as needed in the future
    }
    
    #endregion
    
    #region Private State Variables
    
    private bool _scenarioRunning = false;
    
    // C2 scenario state
    private Vector3 _c2GuidancePosition;
    
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
        Debug.Log("Starting C-0 Scenario (Autonomous/Abort)");
        yield return new WaitForSeconds(config.interStepPause);
        Debug.Log("Completed C-0 Scenario");
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