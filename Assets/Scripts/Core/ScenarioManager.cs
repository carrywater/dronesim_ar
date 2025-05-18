using UnityEngine;
using System.Collections;
using TMPro;

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
    
    #region Editor Settings - Essential Only
    
    [Header("Scenario Selection")]
    [Tooltip("Which scenario type to run")]
    [SerializeField] private ScenarioType _testScenario = ScenarioType.C0_Abort;
    
    [Tooltip("Run only the selected scenario instead of a sequence")]
    [SerializeField] private bool _runSingleScenario = true;
    
    [Header("Component References")]
    [SerializeField] private DroneController _drone;
    [SerializeField] private DroneHMI _hmi;
    [SerializeField] private InteractionManager _interactionManager;
    [SerializeField] private TextMeshProUGUI _confidenceText;
    
    [Header("Flight Settings")]
    [Tooltip("Maximum horizontal cruise speed in m/s (0.1-5 m/s)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _maxCruiseSpeed = 3f;

    [Tooltip("Maximum vertical hover movement speed in m/s (0.1-5 m/s)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _maxHoverSpeed = 2f;

    [Tooltip("Maximum landing descent speed in m/s (0.1-5 m/s)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _maxLandingSpeed = 2f;

    [Tooltip("Maximum abort climb speed in m/s (0.1-5 m/s)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _maxAbortSpeed = 4f;

    [Tooltip("Abort climb height in meters (typical drone height = 1-2m)")]
    [Range(5f, 15f)]
    [SerializeField] private float _abortClimbHeight = 8f;

    [Tooltip("Hover height above ground in meters (typical drone height = 1-2m)")]
    [Range(1f, 10f)]
    [SerializeField] private float _hoverHeight = 6f;

    [Header("Safety Settings")]
    [Tooltip("Minimum height before abort (as percentage of hover height, 0.2 = 20% of hover height, 1.0 = 100% of hover height)")]
    [Range(0.2f, 1.0f)]
    [SerializeField] private float _minHeightBeforeAbort = 0.3f;

    [Header("Action Timing")]
    [Tooltip("Time to wait after reaching hover height before starting next action (seconds)")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _postHoverWaitTime = 1f;

    [Tooltip("Time to wait after landing assessment before showing uncertainty (seconds)")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _preUncertaintyWaitTime = 1f;

    [Tooltip("Time to wait after uncertainty before starting abort (seconds)")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _postUncertaintyWaitTime = 1f;

    [Tooltip("Time to wait after abort before next action (seconds)")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _postAbortWaitTime = 1f;

    [Header("Movement Smoothing")]
    [Tooltip("How quickly the drone reaches its maximum speed (0.5s = moderate, 1s = gentle, 2s = very smooth)")]
    [Range(0.1f, 2f)]
    [SerializeField] private float _accelerationTime = 0.5f;

    [Tooltip("How quickly the drone slows down when approaching target (0.5s = moderate, 1s = gentle, 2s = very smooth)")]
    [Range(0.1f, 2f)]
    [SerializeField] private float _decelerationTime = 0.8f;

    [Tooltip("Minimum smoothing time regardless of distance (prevents abrupt movements)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _minSmoothTime = 0.5f;
    
    [Header("Safety Timeouts")]
    [Tooltip("Maximum time (seconds) before automatic mission abort if no landing spot found")]
    [SerializeField] private float _c0AbortTimeout = 15f;
    
    [Tooltip("Maximum wait time (seconds) for user confirmation")]
    [SerializeField] private float _c1MaxWaitTime = 30f;
    
    [Tooltip("Maximum wait time (seconds) for user guidance")]
    [SerializeField] private float _c2MaxWaitTime = 45f;
    
    #endregion
    
    #region Hidden Settings - Not Visible in Editor
    
    // C0 timing settings - derived from speed
    private float _c0InitialHoverTime = 2f;
    private float _c0CruiseTime = 2f;
    private float _c0LandingWaitTime = 1f;
    private float _c0PostAbortHoverTime = 2f;
    private float _c0AbortHeight = 1f;
    
    // C1 timing settings - derived from speed
    private float _c1CruiseTime = 2f;
    private bool _c1DestroyDroneAfterCompletion = false;
    
    // C2 timing settings - derived from speed
    private float _c2CruiseTime = 2f;
    private bool _c2DestroyDroneAfterCompletion = false;
    
    // Confidence settings - simplified, only needed for C1/C2
    private float _baseConfidence = 0.65f;
    private float _landingConfidenceThreshold = 0.9f;
    private bool _showConfidenceDebug = true;
    
    // Scenario sequence settings
    private float _scenarioDelay = 2f; // delay between scenarios
    private bool _useSequencer = false;
    private ScenarioSequencer _sequencer;
    
    #endregion
    
    #region Private State Variables
    
    private bool _scenarioRunning = false;
    private bool _droneInitialized = false;
    
    // C1 scenario state
    private bool _c1ConfirmationReceived = false;
    
    // C2 scenario state
    private bool _c2GuidanceReceived = false;
    private Vector3 _c2GuidancePosition;
    
    #endregion
    
    #region Unity Lifecycle Methods
    
    private void Awake()
    {
        // Make sure components exist
        ValidateComponents();
        
        // Calculate timing values based on speeds
        CalculateTimingValues();
    }
    
    private void OnEnable()
    {
        // Subscribe to drone state changes
        if (_drone != null)
        {
            _drone.OnStateChanged += HandleDroneStateChanged;
        }
        
        // Note: In a full implementation, we would subscribe to InteractionManager events here
        // For now, we'll use polling in the coroutines instead of event-based communication
    }
    
    private void OnDisable()
    {
        // Unsubscribe from drone state changes
        if (_drone != null)
        {
            _drone.OnStateChanged -= HandleDroneStateChanged;
        }
        
        // Note: Unsubscribe from InteractionManager events would go here in full implementation
    }
    
    private void Start()
    {
        // Initialize the drone with flight settings
        InitializeDrone();
        // Instantly set drone to abort height before scenario starts
        if (_drone != null)
        {
            float abortHeight = _hoverHeight * _minHeightBeforeAbort;
            Vector3 pos = _drone.transform.position;
            pos.y = abortHeight;
            _drone.transform.position = pos;
            if (_drone.transform.childCount > 0)
            {
                // Also set the offset if needed
                Transform offset = _drone.transform.GetChild(0);
                Vector3 offsetPos = offset.localPosition;
                offsetPos.y = abortHeight;
                offset.localPosition = offsetPos;
            }
        }
        // Start the selected scenario after a short delay
        StartCoroutine(DelayedScenarioStart(0.5f));
    }
    
    #endregion
    
    #region Initialization and Setup
    
    /// <summary>
    /// Calculate timing values based on speeds
    /// </summary>
    private void CalculateTimingValues()
    {
        // Calculate times based on speeds
        _c0CruiseTime = 5f / _maxCruiseSpeed;  // Approximately 5 meters divided by speed
        _c1CruiseTime = 5f / _maxCruiseSpeed;
        _c2CruiseTime = 5f / _maxCruiseSpeed;
    }
    
    /// <summary>
    /// Validate that all required components exist
    /// </summary>
    private void ValidateComponents()
    {
        if (_drone == null)
        {
            Debug.LogError("DroneController reference missing on ScenarioManager!");
        }
        
        if (_hmi == null)
        {
            Debug.LogError("DroneHMI reference missing on ScenarioManager!");
        }
        
        if (_interactionManager == null)
        {
            Debug.LogError("InteractionManager reference missing on ScenarioManager!");
        }
    }
    
    /// <summary>
    /// Initialize the drone with flight settings
    /// </summary>
    private void InitializeDrone()
    {
        if (_drone != null)
        {
            _drone.Initialize(
                _hoverHeight,
                _maxHoverSpeed,
                _maxCruiseSpeed,
                _abortClimbHeight,
                _maxLandingSpeed,
                _accelerationTime,
                _decelerationTime,
                _minSmoothTime,
                _maxAbortSpeed
            );
            _droneInitialized = true;
            Debug.Log("Drone initialized with flight settings");
        }
        else
        {
            Debug.LogError("Cannot initialize drone - reference missing!");
        }
    }
    
    /// <summary>
    /// Start the scenario after a delay to ensure initialization
    /// </summary>
    private IEnumerator DelayedScenarioStart(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (_droneInitialized)
        {
            // Start the drone in hover state
            _drone.StartInHoverState();
            
            // Then start the scenario
            StartScenario();
        }
        else
        {
            Debug.LogError("Cannot start scenario - drone not initialized!");
        }
    }
    
    /// <summary>
    /// Reset the drone state between scenarios
    /// </summary>
    private void ResetDroneState()
    {
        // Reset HMI state
        _hmi.SetStatus(DroneHMI.HMIState.Idle);
        
        // Enable position debugging to help diagnose jump issues
        _drone.EnablePositionDebugging(true);
        
        // Reset hover height to ensure consistency with initialization value
        _drone.Initialize(
            _hoverHeight,
            _maxHoverSpeed,
            _maxCruiseSpeed,
            _abortClimbHeight,
            _maxLandingSpeed,
            _accelerationTime,
            _decelerationTime,
            _minSmoothTime,
            _maxAbortSpeed
        );
        
        // Wait a short delay before changing states to ensure everything is settled
        StartCoroutine(DelayedHoverReturn(0.3f));
        
        // Hide any interaction elements
        _interactionManager.HideAllInteractions();
        
        // Reset confidence display
        if (_confidenceText != null)
        {
            _confidenceText.text = "";
        }
        
        _drone.EnableScanning(false); // Disable scanning

    }
    
    /// <summary>
    /// Delayed return to hover to prevent state conflicts
    /// </summary>
    private IEnumerator DelayedHoverReturn(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // If the drone is in an abort or landing state, return it to hover
        _drone.ReturnToHover();
        
        // Add a longer delay before extending legs to allow hover transition to fully stabilize
        yield return new WaitForSeconds(0.8f); // Increased from 0.3f
        _drone.ExtendLegs();
    }
    
    /// <summary>
    /// Enable tracking and recognition after a short delay
    /// </summary>
    
    #endregion
    
    #region Drone Event Handling
    
    /// <summary>
    /// Handle state changes from the drone controller
    /// </summary>
    private void HandleDroneStateChanged(DroneController.FlightState newState)
    {
        // Update HMI based on drone flight state
        switch (newState)
        {
            case DroneController.FlightState.Idle:
                // Hide any AR interaction elements when drone goes to idle
                _interactionManager.HideAllInteractions();
                break;
                
            case DroneController.FlightState.Landing:
                // The drone controller now handles sound effects directly
                break;
                
            case DroneController.FlightState.LandAbort:
                // The drone controller now handles sound effects directly  
                break;
                
            case DroneController.FlightState.Abort:
                // The drone controller now handles sound effects directly
                break;
        }
    }
    
    #endregion
    
    #region Interaction Event Handling
    
    /// <summary>
    /// In a full implementation, this would handle confirmation events from InteractionManager
    /// Currently simulated in coroutines
    /// </summary>
    private void HandleC1Confirmation(Vector3 targetPosition)
    {
        if (_scenarioRunning && _testScenario == ScenarioType.C1_Confirm)
        {
            // Begin landing at the confirmed spot
            _drone.BeginLanding(targetPosition);
            
            // Update HMI for landing
            _hmi.SetStatus(DroneHMI.HMIState.Landing);
            
            // Update confidence display
            if (_confidenceText != null)
            {
                _confidenceText.text = "Landing at Confirmed Location";
                _confidenceText.color = Color.green;
            }
        }
    }
    
    /// <summary>
    /// In a full implementation, this would handle rejection events from InteractionManager
    /// Currently simulated in coroutines
    /// </summary>
    private void HandleC1Rejection()
    {
        if (_scenarioRunning && _testScenario == ScenarioType.C1_Confirm)
        {
            // In the full implementation, we would call InteractionManager.RandomizeC1Position()
            
            // Update confidence display
            if (_confidenceText != null)
            {
                _confidenceText.text = "Awaiting New Confirmation";
                _confidenceText.color = Color.yellow;
            }
            
            // Update HMI state to show waiting for confirmation
            _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm);
        }
    }
    
    /// <summary>
    /// In a full implementation, this would handle guidance events from InteractionManager
    /// Currently simulated in coroutines
    /// </summary>
    private void HandleC2Guidance(Vector3 targetPosition)
    {
        if (_scenarioRunning && _testScenario == ScenarioType.C2_Guidance)
        {
            _c2GuidancePosition = targetPosition;
            
            // Begin landing at the guided spot
            _drone.BeginLanding(targetPosition);
            
            // Update HMI for landing
            _hmi.SetStatus(DroneHMI.HMIState.Landing);
            
            // Update confidence display
            if (_confidenceText != null)
            {
                _confidenceText.text = "Landing at Guided Location";
                _confidenceText.color = Color.green;
            }
        }
    }
    
    #endregion
    
    #region Confidence Evaluation
    
    /// <summary>
    /// Update confidence display with current value
    /// </summary>
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
    
    /// <summary>
    /// Evaluate if landing should proceed based on confidence
    /// </summary>
    private bool EvaluateLandingConfidence(float confidence)
    {
        UpdateConfidenceDisplay(confidence);
        return confidence >= _landingConfidenceThreshold;
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
            StartCoroutine(RunSelectedScenario());
        }
        else
        {
            StartCoroutine(RunScenarioSequence());
        }
    }
    
    /// <summary>
    /// Run the currently selected scenario
    /// </summary>
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
    
    /// <summary>
    /// Run a sequence of scenarios (using sequencer if available)
    /// </summary>
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
            // *** IMPORTANT: Only call the scenario once ***
            Debug.Log("Running C0 in sequence");
            yield return StartCoroutine(GetScenarioCoroutine(ScenarioType.C0_Abort));
            
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
    
    /// <summary>
    /// Get the appropriate coroutine for a scenario type
    /// </summary>
    private IEnumerator GetScenarioCoroutine(ScenarioType scenario)
    {
        Debug.Log($"GetScenarioCoroutine called for {scenario}");
        
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
    
    #endregion
    
    #region C0 Scenario Implementation (High Autonomy - Abort)
    
    /// <summary>
    /// Run the C0 scenario (high autonomy with abort)
    /// </summary>
    private IEnumerator RunC0Scenario()
    {
        Debug.Log("Starting C-0 Scenario (Autonomous/Abort) - Demonstrating abort sequence");

        // Wait a moment for initial stabilization
        yield return new WaitForSeconds(0.3f);
        
        // Enable drone scanning behavior for initial hover
        _drone.EnableScanning(true);
        
        // Make sure tracking is enabled after a delay to ensure drone is stabilized
        yield return new WaitForSeconds(0.5f);

        // Wait for hover height to be reached with proper acceleration
        Debug.Log($"C0: Waiting {_c0InitialHoverTime}s in initial hover before starting assessment");
        yield return new WaitForSeconds(_c0InitialHoverTime - 0.5f);
        
        // Hover and play confirmation sound - indicates drone is beginning autonomous operation
        _drone.EnableScanning(false);
        
        // Add a small delay to ensure scanning is fully disabled before rotation
        yield return new WaitForSeconds(0.1f);
        _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm);
        
        // Brief pause with no scanning during confirmation sound
        Debug.Log("C0: Playing confirmation sound to signal autonomous operation start");
        yield return new WaitForSeconds(0.5f);
        
        // Resume scanning behavior
        _drone.EnableScanning(true);
        
        // Ensure tracking is still active after confirmation sound
        yield return new WaitForSeconds(0.2f);
    
        // Wait a moment to show the drone is analyzing the environment
        Debug.Log("C0: Analyzing environment before landing assessment");
        yield return new WaitForSeconds(_c0InitialHoverTime - 0.5f);
        
        Debug.Log("C0: Preparing for landing attempts");
        
        // Retract legs before starting flight loop
        _drone.RetractLegs();
        
        // Wait for legs to fully retract with timeout protection
        float legTimeout = 0f;
        float maxLegTime = 3.0f;
        while (_drone.AreLegsAnimating() && legTimeout < maxLegTime)
        {
            legTimeout += Time.deltaTime;
            yield return null;
        }
        
        if (legTimeout >= maxLegTime)
        {
            Debug.LogWarning("C0: Leg retraction timed out, continuing anyway");
        }

        Debug.Log("C0: Legs retracted, starting landing attempts");

        // Set number of landing attempts to demonstrate
        int attemptCount = 2;
        
        // Make 'attemptCount' landing attempts
        for (int i = 0; i < attemptCount; i++)
        {
            Debug.Log($"C0: Starting landing attempt {i + 1}/{attemptCount}");
            
            // Force the drone back to hover state before starting cruise
            // This will use proper acceleration/deceleration based on distance
            _drone.ReturnToHover();
            yield return new WaitForSeconds(_decelerationTime + 0.2f); // Wait for deceleration to complete
            
            // Randomize cruise location for the demonstration
            Vector3 navPoint = _interactionManager.RandomizeC0Position();
            Debug.Log($"C0: Moving to new assessment position {navPoint}");
            
            // Disable scanning during cruise for more stable movement
            _drone.EnableScanning(false);
            
            // Move to the randomized location with proper acceleration
            _drone.StartCruiseTo(navPoint);
            
            // Wait for cruise to complete with proper acceleration/deceleration
            float cruiseTimer = 0f;
            float maxCruiseTime = _c0CruiseTime * 3f; // Triple the expected time as safety margin
            bool reachedDestination = false;
            
            Debug.Log($"C0: Waiting for cruise to complete (estimated {_c0CruiseTime}s)");
            while (cruiseTimer < maxCruiseTime && !reachedDestination)
            {
                if (_drone.CurrentState != DroneController.FlightState.CruiseToTarget)
                {
                    // The drone has finished cruising and is in hover state
                    reachedDestination = true;
                    Debug.Log("C0: Cruise completed normally");
                }
                else
                {
                    cruiseTimer += Time.deltaTime;
                    yield return null;
                }
            }
            
            // Handle cruise timeout
            if (!reachedDestination)
            {
                Debug.LogWarning("C0: Cruise to position took too long, forcing hover state");
                _drone.ReturnToHover();
                yield return new WaitForSeconds(_decelerationTime + 0.2f); // Wait for deceleration
            }
            
            Debug.Log("C0: Arrived at new position");
            
            // Wait for the drone to stabilize
            yield return new WaitForSeconds(_decelerationTime + 0.1f);
            
            // Re-enable scanning for hovering behavior
            _drone.EnableScanning(true);
            
            // Hovering at target location to analyze landing spot
            Debug.Log($"C0: Hovering at position to analyze landing spot");
            yield return new WaitForSeconds(_c0InitialHoverTime * 0.5f);
            
            // Extend legs for landing
            Debug.Log("C0: Extending legs for landing");
            _drone.ExtendLegs();
            
            // Wait for legs to fully extend with timeout protection
            legTimeout = 0f;
            while (_drone.AreLegsAnimating() && legTimeout < maxLegTime)
            {
                legTimeout += Time.deltaTime;
                yield return null;
            }
            
            // Disable scanning during landing for more stable behavior
            _drone.EnableScanning(false);
            yield return new WaitForSeconds(0.2f);
            
            // Attempt to land but stop at the abort height
            Vector3 landingSpot = _interactionManager.GetC0TargetPosition();
            // Calculate abort height as a percentage of hover height
            float abortHeight = _hoverHeight * _minHeightBeforeAbort;
            landingSpot.y = abortHeight;
            Debug.Log($"C0: Beginning landing assessment, will stop at {abortHeight:F2}m above ground (minHeightBeforeAbort={_minHeightBeforeAbort:P0} of hover height {_hoverHeight:F2}m)");
            _drone.BeginLanding(landingSpot);
            
            // Wait for landing descent to complete and stabilize
            Debug.Log($"C0: Waiting for landing descent to complete and stabilize");
            yield return new WaitForSeconds(_decelerationTime + 0.2f);
            
            // Wait the configured time before showing uncertainty
            Debug.Log($"C0: Waiting {_preUncertaintyWaitTime}s before showing uncertainty");
            yield return new WaitForSeconds(_preUncertaintyWaitTime);
            
            // C0 always shows uncertainty and aborts - this is simplified compared to original
            // No confidence calculation needed - just display a simulated low confidence value
            float simulatedConfidence = 0.6f; // Always below threshold
            
            // Just for display purposes
            if (_showConfidenceDebug)
            {
                Debug.Log($"C0: Simulated landing confidence: {simulatedConfidence:F2} (Always insufficient in C0)");
                
                if (_confidenceText != null)
                {
                    _confidenceText.text = $"Confidence: {simulatedConfidence:P0}";
                    _confidenceText.color = Color.red;
                }
            }
            
            // Ensure drone is stable before showing uncertainty
            _drone.EnableScanning(false);
            yield return new WaitForSeconds(0.2f);
            
            // Signal uncertainty with the uncertainty animation using low confidence sound
            Debug.Log("C0: Signaling uncertainty about landing spot");
            _hmi.SetStatus(DroneHMI.HMIState.Uncertain);
            
            // Wait during uncertainty signaling
            Debug.Log($"C0: Waiting {_postUncertaintyWaitTime}s during uncertainty signaling");
            yield return new WaitForSeconds(_postUncertaintyWaitTime);
            
            // Raise back up to hover height (abort this landing attempt)
            Debug.Log("C0: Aborting landing attempt");
            _drone.LandAbort();
            
            // Wait for abort to complete with proper acceleration
            float abortTimer = 0f;
            float maxAbortTime = 5f;
            bool abortComplete = false;
            
            while (abortTimer < maxAbortTime && !abortComplete)
            {
                if (_drone.CurrentState != DroneController.FlightState.LandAbort)
                {
                    // Abort has completed when state changes from LandAbort
                    abortComplete = true;
                    Debug.Log("C0: Landing abort completed normally");
                }
                else
                {
                    abortTimer += Time.deltaTime;
                    yield return null;
                }
            }
            
            if (!abortComplete)
            {
                Debug.LogWarning("C0: Landing abort took too long, forcing hover state");
                _drone.ReturnToHover();
                yield return new WaitForSeconds(_decelerationTime + 0.2f); // Wait for deceleration
            }
            
            // Wait after abort before next action
            Debug.Log($"C0: Waiting {_postAbortWaitTime}s after abort before next action");
            yield return new WaitForSeconds(_postAbortWaitTime);
            
            // Reset HMI state after uncertainty
            _hmi.SetStatus(DroneHMI.HMIState.Idle);
        }

        Debug.Log("C0: All landing attempts completed, preparing for final abort");
        
        // Make sure legs are retracted before final abort
        if (!_drone.AreLegsRetracted())
        {
            _drone.RetractLegs();
            
            // Wait for legs to fully retract with timeout
            legTimeout = 0f;
            while (_drone.AreLegsAnimating() && legTimeout < maxLegTime)
            {
                legTimeout += Time.deltaTime;
                yield return null;
            }
        }

        // Communicate abort mission
        Debug.Log("C0: Signaling mission abort");
        _hmi.SetStatus(DroneHMI.HMIState.Abort);
        
        // Wait to let the abort message be communicated
        yield return new WaitForSeconds(_c0LandingWaitTime);
        
        // Abort mission (rise up while keeping rotors on)
        // This will use proper acceleration based on distance
        Debug.Log("C0: Performing final abort");
        _drone.Abort();
        
        // Update confidence display to show final decision
        if (_confidenceText != null)
        {
            _confidenceText.text = "Mission Aborted - Demonstration Complete";
            _confidenceText.color = Color.red;
        }
        
        Debug.Log("Completed C-0 Scenario");
    }
    
    #endregion
    
    #region C1 Scenario Implementation (Medium - Confirm)
    
    /// <summary>
    /// Run the C1 scenario (medium autonomy with confirmation)
    /// </summary>
    private IEnumerator RunC1Scenario()
    {
        Debug.Log("Starting C-1 Scenario (Confirm)");
        
        // Enable scanning behavior for more lifelike movement
        _drone.EnableScanning(true);

 
        // Update confidence display
        if (_confidenceText != null)
        {
            _confidenceText.text = "Awaiting User Confirmation";
            _confidenceText.color = Color.yellow;
        }
        
        // Wait a moment while tracking participant before starting cruise
        yield return new WaitForSeconds(1.0f);
        
        // 1. Move drone to interaction zone
        // Disable scanning during cruise
        _drone.EnableScanning(false);
        
        Debug.Log("C1: Starting cruise to interaction zone (tracking will be temporarily disabled)");
        Vector3 interactionPos = _interactionManager.transform.position;
        interactionPos.y = _drone.transform.position.y; // Keep same height
        _drone.StartCruiseTo(interactionPos);
        
        // Wait for cruise time with a short additional buffer
        yield return new WaitForSeconds(_c1CruiseTime + 0.5f);
        
        // 2. Re-enable scanning when hovering
        _drone.EnableScanning(true);
        

        // Show landing probe and prompt for confirmation
        // In full implementation, we would call _interactionManager.ShowLandingProbe()
        _hmi.SetStatus(DroneHMI.HMIState.PromptConfirm);
        
        // 3. Wait for confirmation or rejection
        float elapsedTime = 0f;
        // Simulate user confirmation after a delay
        bool userConfirmed = false;
        
        // Periodically check tracking status during waiting period
        while (!userConfirmed && elapsedTime < _c1MaxWaitTime)
        {
            elapsedTime += Time.deltaTime;
            
           
            
            // After 3 seconds, simulate user confirmation (in real implementation this would come from InteractionManager)
            if (elapsedTime > 3f && !userConfirmed)
            {
                userConfirmed = true;
                
                Vector3 landingPosition = _drone.transform.position;
                landingPosition.y = 0f; // Land on ground
                
                Debug.Log("C1: User confirmed, beginning landing");
                
                // Begin landing at the confirmed spot
                _drone.BeginLanding(landingPosition);
                
                // Update HMI for landing
                _hmi.SetStatus(DroneHMI.HMIState.Landing);
     
            }
            
            yield return null;
        }
        
        // Check if we timed out
        if (elapsedTime >= _c1MaxWaitTime && !userConfirmed)
        {
            // Handle timeout - abort the mission
            if (_confidenceText != null)
            {
                _confidenceText.text = "Confirmation Timeout - Aborting";
                _confidenceText.color = Color.red;
            }
            
            // Abort
            _hmi.SetStatus(DroneHMI.HMIState.Abort);
            _drone.Abort();
        }
        else if (userConfirmed)
        {
            // Wait for landing to complete (drone will transition to Idle automatically)
            while (_drone.CurrentState != DroneController.FlightState.Idle)
            {
                yield return null;
            }
            
            // Show success HMI state
            _hmi.SetStatus(DroneHMI.HMIState.Success);
            
            // Update confidence display to show success
            if (_confidenceText != null)
            {
                _confidenceText.text = "Landing Successful (User Confirmed)";
                _confidenceText.color = Color.green;
            }
            
            // Wait a moment to show success state
            yield return new WaitForSeconds(2f);
            
            // Handle completion (optionally destroy drone)
            if (_c1DestroyDroneAfterCompletion)
            {
                Destroy(_drone.gameObject);
            }
        }
        
        Debug.Log("Completed C-1 Scenario");
    }
    
    #endregion
    
    #region C2 Scenario Implementation (High - Guidance)
    
    /// <summary>
    /// Run the C2 scenario (high involvement with guidance)
    /// </summary>
    private IEnumerator RunC2Scenario()
    {
        Debug.Log("Starting C-2 Scenario (Guidance)");
        
        // Enable scanning behavior for more lifelike movement
        _drone.EnableScanning(true);
        
        Debug.Log("C2: Initial tracking enabled, participant recognized and faced");
        
        // Update confidence display
        if (_confidenceText != null)
        {
            _confidenceText.text = "Awaiting User Guidance";
            _confidenceText.color = Color.yellow;
        }
        
        // Wait a moment while tracking participant before starting cruise
        yield return new WaitForSeconds(1.0f);
        
        // 1. Move drone to interaction zone
        // Disable scanning during cruise
        _drone.EnableScanning(false);
        
        Debug.Log("C2: Starting cruise to interaction zone (tracking will be temporarily disabled)");
        Vector3 interactionPos = _interactionManager.transform.position;
        interactionPos.y = _drone.transform.position.y; // Keep same height
        _drone.StartCruiseTo(interactionPos);
        
        // Wait for cruise time with a short additional buffer
        yield return new WaitForSeconds(_c2CruiseTime + 0.5f);
        
        // 2. Re-enable scanning when hovering
        _drone.EnableScanning(true);
    
    
        
        // Show guidance pad and prompt for guidance
        // In full implementation, we would call _interactionManager.ShowGuidancePad()
        _hmi.SetStatus(DroneHMI.HMIState.PromptGuide);
        
        // 3. Wait for guidance from user
        float elapsedTime = 0f;
        // Simulate user guidance after a delay
        bool userGuided = false;
        
        // Periodically check tracking status during waiting period
        while (!userGuided && elapsedTime < _c2MaxWaitTime)
        {
            elapsedTime += Time.deltaTime;
            
            
            // After 4 seconds, simulate user guidance (in real implementation this would come from InteractionManager)
            if (elapsedTime > 4f && !userGuided)
            {
                userGuided = true;
                
                // Simulate user pointing at a landing spot
                Vector3 guidedPosition = _drone.transform.position + new Vector3(1f, 0f, 1f);
                guidedPosition.y = 0f; // Land on ground
                _c2GuidancePosition = guidedPosition;
                
                Debug.Log("C2: User provided guidance, beginning landing");
                
                // Begin landing at the guided spot
                _drone.BeginLanding(guidedPosition);
                
                // Update HMI for landing
                _hmi.SetStatus(DroneHMI.HMIState.Landing);
         
            }
            
            yield return null;
        }
        
        // Check if we timed out
        if (elapsedTime >= _c2MaxWaitTime && !userGuided)
        {
            // Handle timeout - abort the mission
            if (_confidenceText != null)
            {
                _confidenceText.text = "Guidance Timeout - Aborting";
                _confidenceText.color = Color.red;
            }
            
            // Abort
            _hmi.SetStatus(DroneHMI.HMIState.Abort);
            _drone.Abort();
        }
        else if (userGuided)
        {
            // Wait for landing to complete (drone will transition to Idle automatically)
            while (_drone.CurrentState != DroneController.FlightState.Idle)
            {
                yield return null;
            }
            
            // Show success HMI state
            _hmi.SetStatus(DroneHMI.HMIState.Success);
            
            // Update confidence display to show success
            if (_confidenceText != null)
            {
                _confidenceText.text = "Landing Successful (User Guided)";
                _confidenceText.color = Color.green;
            }
            
            // Wait a moment to show success state
            yield return new WaitForSeconds(2f);
            
            // Handle completion (optionally destroy drone)
            if (_c2DestroyDroneAfterCompletion)
            {
                Destroy(_drone.gameObject);
            }
        }
        
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