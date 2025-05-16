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
    
    [Header("Confidence Settings")]
    [Tooltip("Threshold required for successful landing (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _landingConfidenceThreshold = 0.9f;
    
    [Tooltip("Base confidence for C0 scenario (always below threshold)")]
    [Range(0f, 1f)]
    [SerializeField] private float _c0BaseConfidence = 0.65f;
    
    [Tooltip("Additional confidence gained on second landing attempt")]
    [Range(0f, 0.3f)]
    [SerializeField] private float _c0SecondAttemptBonus = 0.1f;
    
    [Tooltip("Maximum number of landing attempts before final abort")]
    [Range(1, 3)]
    [SerializeField] private int _maxLandingAttempts = 2;
    
    [Tooltip("Whether to display confidence values for debugging")]
    [SerializeField] private bool _showConfidenceDebug = true;

    [Header("Timings (seconds)")]
    [SerializeField] private float _initialHoverTime = 2f;
    [SerializeField] private float _cruiseTime = 2f;
    [SerializeField] private float _landingWaitTime = 1f;
    [SerializeField] private float _postAbortHoverTime = 2f;
    [SerializeField] private float _landingDepth = 1f;  // how far below the hover height to descend during abort landing
    [SerializeField] private float _scenarioDelay = 2f; // delay between scenarios
    
    [Header("UI Feedback (Optional)")]
    [SerializeField] private TMPro.TextMeshProUGUI _confidenceText;

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

    // Helper method to display confidence
    private void UpdateConfidenceDisplay(float confidence)
    {
        if (_showConfidenceDebug)
        {
            Debug.Log($"Landing confidence: {confidence:F2}, Threshold: {_landingConfidenceThreshold:F2}");
            
            if (_confidenceText != null)
            {
                _confidenceText.text = $"Confidence: {confidence:P0}";
                _confidenceText.color = confidence >= _landingConfidenceThreshold ? 
                    Color.green : (confidence > _landingConfidenceThreshold * 0.8f ? Color.yellow : Color.red);
            }
        }
    }
    
    // Helper method to evaluate if landing should proceed
    private bool EvaluateLandingConfidence(float confidence)
    {
        UpdateConfidenceDisplay(confidence);
        return confidence >= _landingConfidenceThreshold;
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
        
        // Stop any hover hum for a clean start
        _hmi.StopHoverHum();
        
        // If the drone is in an abort or landing state, return it to hover
        _drone.ReturnToHover();
        
        // Ensure legs are extended at the start of any scenario
        _drone.ExtendLegs();
        
        // Hide any interaction elements
        if (_interactionManager != null)
        {
            _interactionManager.HideAllInteractions();
        }
        
        // Reset confidence display
        if (_confidenceText != null)
        {
            _confidenceText.text = "";
        }
    }

    private IEnumerator RunC0Scenario()
    {
        Debug.Log("Starting C-0 Scenario (Autonomous/Abort)");

        // 1. Gentle descent to hover height
        // The drone already transitions to hover state in ResetDroneState()
        
        // Start hover hum immediately and keep it running throughout the scenario
        _hmi.PlayHoverHum();
        
        // Just wait for it to reach hover height
        yield return new WaitForSeconds(_initialHoverTime);
        
        // 2. Hover and play confirmation sound
        _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm);
        
        // Wait in hover while facing participant
        yield return new WaitForSeconds(_initialHoverTime);
        
        // Retract legs before starting flight loop
        _drone.RetractLegs();
        
        // Wait for legs to fully retract
        while (_drone.AreLegsAnimating())
        {
            yield return null;
        }

        // 3. Perform landing attempts (maximum of 2)
        for (int attemptCount = 0; attemptCount < _maxLandingAttempts; attemptCount++)
        {
            // Calculate this attempt's confidence (slightly higher on 2nd attempt)
            float currentConfidence = _c0BaseConfidence;
            if (attemptCount > 0)
            {
                currentConfidence += _c0SecondAttemptBonus;
            }
            
            // Show the confidence early so user can see it
            UpdateConfidenceDisplay(currentConfidence);
            
            // 3.1 Randomize cruise location
            Vector3 navPoint = _interactionManager.RandomizeC0Position();
            
            // 3.2 Move to the location while looking at participant
            _drone.StartCruiseTo(navPoint);
            yield return new WaitForSeconds(_cruiseTime);
            
            // 3.2.1 Wait at new location
            yield return new WaitForSeconds(_initialHoverTime);
            
            // 3.3 Extend legs for landing
            _drone.ExtendLegs();
            
            // Wait a moment for legs to start extending before beginning descent
            yield return new WaitForSeconds(0.5f);
            
            // 3.4 Attempt to land but stop at the lowest point
            Vector3 landingSpot = _interactionManager.GetC0TargetPosition();
            landingSpot.y -= _landingDepth;
            _drone.BeginLanding(landingSpot);
            
            // No need to play hover hum again as it's already playing
            yield return new WaitForSeconds(_landingWaitTime);
            
            // 3.5 Check confidence again - evaluate and communicate uncertainty
            bool confidenceOK = EvaluateLandingConfidence(currentConfidence);
            
            // In C0, we ensure the confidence is always below threshold
            Debug.Assert(!confidenceOK, "C0 scenario should always have confidence below threshold!");
            
            // Signal uncertainty with the uncertainty animation but using low confidence sound
            // (we've updated DroneHMI to use low confidence sound for Uncertain state)
            _hmi.SetStatus(DroneHMI.HMIState.Uncertain);
            
            // Maintain hover hum during uncertainty (no need to call again, it's already running)
            yield return new WaitForSeconds(_landingWaitTime);
            
            // 3.6 Raise back up to hover height (abort this landing attempt)
            _drone.LandAbort();
            
            // Retract legs during abort
            _drone.RetractLegs();
            
            yield return new WaitForSeconds(_postAbortHoverTime);
            
            // Reset HMI state after uncertainty
            _hmi.SetStatus(DroneHMI.HMIState.Idle);
            
            // No need to call PlayHoverHum again, it's already playing
        }
        
        // Make sure legs are retracted before final abort
        if (!_drone.AreLegsRetracted())
        {
            _drone.RetractLegs();
            
            // Wait for legs to fully retract
            while (_drone.AreLegsAnimating())
            {
                yield return null;
            }
        }

        // 4. After configured attempts, perform full abort sequence
        
        // 4.1 Communicate abort mission
        _hmi.SetStatus(DroneHMI.HMIState.Abort);
        
        // 4.2 Wait to let the abort message be communicated
        yield return new WaitForSeconds(_landingWaitTime);
        
        // 4.3 Abort mission (rise up while keeping rotors on)
        _drone.Abort();
        
        // Hover hum is already playing - no need to start it again
        
        // Update confidence display to show final decision
        if (_confidenceText != null)
        {
            _confidenceText.text = "Mission Aborted - Insufficient Confidence";
            _confidenceText.color = Color.red;
        }
        
        Debug.Log("Completed C-0 Scenario");
    }
    
    private IEnumerator RunC1Scenario()
    {
        Debug.Log("Starting C-1 Scenario (Confirm)");
        
        // Clear any previous confidence display
        if (_confidenceText != null)
        {
            _confidenceText.text = "Awaiting User Confirmation";
            _confidenceText.color = Color.yellow;
        }
        
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
        
        // Update confidence to show success after user confirmation
        if (_confidenceText != null)
        {
            _confidenceText.text = "Confidence: 100% (User Confirmed)";
            _confidenceText.color = Color.green;
        }
        
        Debug.Log("Completed C-1 Scenario");
    }
    
    private IEnumerator RunC2Scenario()
    {
        Debug.Log("Starting C-2 Scenario (Guidance)");
        
        // Clear any previous confidence display
        if (_confidenceText != null)
        {
            _confidenceText.text = "Awaiting User Guidance";
            _confidenceText.color = Color.yellow;
        }
        
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
        
        // Update confidence to show success after user guidance
        if (_confidenceText != null)
        {
            _confidenceText.text = "Confidence: 95% (User Guided)";
            _confidenceText.color = Color.green;
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