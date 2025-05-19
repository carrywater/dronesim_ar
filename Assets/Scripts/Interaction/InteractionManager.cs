using System;
using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using UnityEngine.Splines;
using Visualization;

/// <summary>
/// Combined manager that handles both AR UI elements and interaction zone logic.
/// Replaces both ARInterfaceManager and InteractionZoneController.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ZoneRandomizer _zoneRandomizer;
    [SerializeField] private DroneController _droneController;
    [SerializeField] private C1GestureHandler _c1GestureHandler;
    
    [Header("AR Cue Objects")]
    [Tooltip("Prefab for the C1 confirmation UI (thumbs up/down)")]
    [SerializeField] private GameObject _c1CuePrefab;
    [Tooltip("The C2 guidance UI")]
    [SerializeField] private GameObject _c2CueObject;
    
    [Header("Zone Indices")]
    [Tooltip("Index of the C-0 navigation zone-target pair in the ZoneRandomizer")]
    [SerializeField] private int _c0ZoneIndex = 2;
    [Tooltip("Index of the C-1 zone-target pair in the ZoneRandomizer")]
    [SerializeField] private int _c1ZoneIndex = 0;
    [Tooltip("Index of the C-2 zone-target pair in the ZoneRandomizer")]
    [SerializeField] private int _c2ZoneIndex = 1;
    
    [Header("C-1 Settings")]
    [Tooltip("How long to wait before auto-reject (seconds), 0 = infinite until user acts)")]
    [SerializeField] private float _autoRejectTime = 0f;
    
    [Header("C-2 Settings")]
    [Tooltip("How long the guidance can be active for (seconds), 0 = infinite until user stops")]
    [SerializeField] private float _guidanceMaxTime = 10f;
    
    [Header("Visualization")]
    [SerializeField] private SplineContainerVisualizer _splineVisualizer;
    
    // Runtime instance of the C1 cue
    private GameObject _c1CueInstance;
    private Vector3 _c1TargetPosition;
    
    // Events that UI elements can trigger
    public event System.Action<Vector3> OnC1Confirmation;
    public event System.Action OnC1Rejection;
    public event Action<Vector3> OnGuidance;
    
    // Completion flags used by ScenarioManager to transition between scenarios
    public bool IsC1Completed { get; private set; } = false;
    public bool IsC2Completed { get; private set; } = false;
    public bool IsC1Rejected { get; private set; } = false;
    
    // Scenario coroutines
    private Coroutine _activeCoroutine;
    private Coroutine _splineUpdateCoroutine;
    
    private void Awake()
    {
        if (_droneController == null)
        {
            _droneController = FindObjectOfType<DroneController>();
        }
        
        if (_splineVisualizer == null)
        {
            _splineVisualizer = FindObjectOfType<SplineContainerVisualizer>();
        }
        
        if (_c1GestureHandler == null)
        {
            _c1GestureHandler = GetComponentInChildren<C1GestureHandler>();
        }

        // Ensure C1 cue is deactivated at start
        if (_c1CuePrefab != null)
        {
            _c1CuePrefab.SetActive(false);
        }
    }
    
    private void OnEnable()
    {
        HideAllCues();
        
        // Subscribe to gesture events
        if (_c1GestureHandler != null)
        {
            _c1GestureHandler.OnThumbsUp += HandleC1Confirmation;
            _c1GestureHandler.OnThumbsDown += HandleC1Rejection;
        }
        
        // Subscribe to our own events to handle internal logic
        OnC1Confirmation += HandleConfirmInternal;
        OnC1Rejection += HandleRejectInternal;
        OnGuidance += HandleGuidanceInternal;
    }
    
    private void OnDisable()
    {
        // Unsubscribe from gesture events
        if (_c1GestureHandler != null)
        {
            _c1GestureHandler.OnThumbsUp -= HandleC1Confirmation;
            _c1GestureHandler.OnThumbsDown -= HandleC1Rejection;
        }
        
        // Unsubscribe to prevent memory leaks
        OnC1Confirmation -= HandleConfirmInternal;
        OnC1Rejection -= HandleRejectInternal;
        OnGuidance -= HandleGuidanceInternal;
    }
    
    #region Public Scenario Methods
    
    /// <summary>
    /// Randomizes the position for C-0 navigation zone and returns the target position.
    /// </summary>
    public Vector3 RandomizeC0Position()
    {
        // Ensure we have a valid zone randomizer
        if (_zoneRandomizer == null)
        {
            Debug.LogError("ZoneRandomizer reference is missing on InteractionManager!");
            return transform.position; // Return current position as fallback
        }

        // Get the current position before randomizing to ensure we're genuinely moving
        Vector3 currentPosition = _zoneRandomizer.GetTarget(_c0ZoneIndex)?.position ?? Vector3.zero;
        
        // Try to find a position that's sufficiently different from the current one
        Vector3 newPosition = Vector3.zero;
        bool validPositionFound = false;
        
        // Maximum retry attempts to avoid infinite loops
        const int maxAttempts = 10;
        int attempts = 0;
        
        while (!validPositionFound && attempts < maxAttempts)
        {
            // Generate a random position
            newPosition = _zoneRandomizer.RandomizeTargetPosition(_c0ZoneIndex);
            
            // Calculate horizontal distance from current position (ignore Y)
            float distance = Vector2.Distance(
                new Vector2(currentPosition.x, currentPosition.z), 
                new Vector2(newPosition.x, newPosition.z));
            
            // If we have a reasonable distance, accept it
            // Minimum distance should be at least 1/4 of the zone radius
            float minDistance = _zoneRandomizer.GetZoneRadius(_c0ZoneIndex) * 0.25f;
            
            if (distance > minDistance)
            {
                validPositionFound = true;
                Debug.Log($"RandomizeC0Position: Found new position at distance {distance:F2}m from previous position");
            }
            
            attempts++;
        }
        
        // If we failed to find a good position after max attempts, use the last generated one
        if (!validPositionFound)
        {
            Debug.LogWarning("RandomizeC0Position: Could not find position with sufficient distance after max attempts");
        }
        
        return newPosition;
    }
    
    /// <summary>
    /// Gets the current C-0 target position without randomizing it.
    /// </summary>
    public Vector3 GetC0TargetPosition()
    {
        if (_zoneRandomizer == null)
        {
            Debug.LogError("ZoneRandomizer reference is missing on InteractionManager!");
            return transform.position; // Return current position as fallback
        }
        
        Transform target = _zoneRandomizer.GetTarget(_c0ZoneIndex);
        if (target == null)
        {
            Debug.LogError($"C0 target transform not found at index {_c0ZoneIndex}!");
            return transform.position; // Return current position as fallback
        }
        
        return target.position;
    }
    
    /// <summary>
    /// Starts the C-1 Confirm scenario: randomly place cue, show UI, wait for confirm/reject.
    /// </summary>
    public void StartC1Confirm()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
        }
        _activeCoroutine = StartCoroutine(C1ConfirmRoutine());
    }
    
    /// <summary>
    /// Starts the C-2 Guidance scenario: position pad, show UI, wait for user input.
    /// </summary>
    public void StartC2Guidance()
    {
        StopActiveCoroutine();
        IsC2Completed = false;
        _activeCoroutine = StartCoroutine(RunC2Guidance());
    }
    
    /// <summary>
    /// Gets the current C-1 target position.
    /// </summary>
    public Vector3 GetC1TargetPosition()
    {
        if (_zoneRandomizer == null)
        {
            Debug.LogError("ZoneRandomizer reference is missing on InteractionManager!");
            return transform.position; // Return current position as fallback
        }
        
        Transform target = _zoneRandomizer.GetTarget(_c1ZoneIndex);
        if (target == null)
        {
            Debug.LogError($"C1 target transform not found at index {_c1ZoneIndex}!");
            return transform.position; // Return current position as fallback
        }
        
        return target.position;
    }
    
    #endregion
    
    #region Scenario Implementations
    
    private IEnumerator C1ConfirmRoutine()
    {
        // Show the C1 cue
        ShowC1Cue();
        
        // Start updating the spline
        if (_splineUpdateCoroutine != null)
        {
            StopCoroutine(_splineUpdateCoroutine);
        }
        _splineUpdateCoroutine = StartCoroutine(UpdateSplineRoutine());
        
        // Wait for user input or auto-reject
        float elapsedTime = 0f;
        bool completed = false;
        
        while (!completed)
        {
            elapsedTime += Time.deltaTime;
            
            // Check for auto-reject
            if (_autoRejectTime > 0 && elapsedTime >= _autoRejectTime)
            {
                HandleC1Rejection();
                completed = true;
            }
            
            // Check for other completion conditions
            if (IsC1Completed || IsC1Rejected)
            {
                completed = true;
            }
            
            yield return null;
        }
        
        // Stop updating the spline
        if (_splineUpdateCoroutine != null)
        {
            StopCoroutine(_splineUpdateCoroutine);
            _splineUpdateCoroutine = null;
        }
        
        // Hide all cues
        HideAllCues();
        _activeCoroutine = null;
    }
    
    private IEnumerator RunC2Guidance()
    {
        // 1) Randomize the target position
        Vector3 targetPos = _zoneRandomizer.RandomizeTargetPosition(_c2ZoneIndex);
        
        // 2) Show the C2 UI (guidance pad)
        ShowC2Cue();
        
        // Local tracking vars
        bool completed = false;
        float timer = 0f;
        
        // 3) Wait for timeout or completion
        while (!completed)
        {
            if (_guidanceMaxTime > 0f && (timer += Time.deltaTime) >= _guidanceMaxTime)
            {
                completed = true;
            }
            
            // Check for other completion conditions
            // For example, if drone is close enough to guidance position
            
            yield return null;
        }
        
        // 4) Cleanup
        HideAllCues();
        _activeCoroutine = null;
        IsC2Completed = true;
    }
    
    #endregion
    
    #region UI Methods
    
    /// <summary>Show the C-1 confirmation UI (thumbs up/down).</summary>
    public void ShowC1Cue()
    {
        if (_c1CuePrefab == null)
        {
            Debug.LogError("C1 cue prefab not assigned!");
            return;
        }

        // Randomize the target position (which will move the cue since it's a child)
        _zoneRandomizer.RandomizeTargetPosition(_c1ZoneIndex);
        
        // Simply activate the cue
        _c1CuePrefab.SetActive(true);

        // Show spline from drone to target
        if (_splineVisualizer != null)
        {
            _splineVisualizer.UpdateSpline(_droneController.transform.position, _c1CuePrefab.transform.position);
            _splineVisualizer.ShowSpline(true);
        }
        
        // Enable gesture recognition
        if (_c1GestureHandler != null)
        {
            _c1GestureHandler.SetActive(true);
        }
    }
    
    /// <summary>Show the C-2 guidance UI.</summary>
    private void ShowC2Cue()
    {
        _c1CuePrefab.SetActive(false);
        _c2CueObject.SetActive(true);
    }
    
    /// <summary>Hide all AR cues.</summary>
    public void HideAllCues()
    {
        if (_c1CuePrefab != null)
        {
            _c1CuePrefab.SetActive(false);
        }

        if (_c2CueObject != null)
        {
            _c2CueObject.SetActive(false);
        }

        if (_splineVisualizer != null)
        {
            _splineVisualizer.ShowSpline(false);
        }
        
        // Disable gesture recognition
        if (_c1GestureHandler != null)
        {
            _c1GestureHandler.SetActive(false);
        }
    }
    
    /// <summary>
    /// Resets interaction state and hides all cues.
    /// Used by ScenarioManager during scenario transitions.
    /// </summary>
    public void HideAllInteractions()
    {
        // Stop any active coroutine
        StopActiveCoroutine();
        
        // Hide all visual cues
        HideAllCues();
        
        // Reset scenario completion flags
        IsC1Completed = false;
        IsC2Completed = false;
        IsC1Rejected = false;
    }
    
    #endregion
    
    #region UI Event Handlers
    
    /// <summary>Called by UnityEvent when user confirms the landing spot.</summary>
    public void HandleC1Confirmation()
    {
        IsC1Completed = true;
        IsC1Rejected = false;
        OnC1Confirmation?.Invoke(_c1CuePrefab.transform.position);
    }
    
    /// <summary>Called by UnityEvent when user rejects the landing spot.</summary>
    public void HandleC1Rejection()
    {
        IsC1Completed = false;
        IsC1Rejected = true;
        OnC1Rejection?.Invoke();
    }
    
    /// <summary>Called by UnityEvent when user provides guidance.</summary>
    public void HandleGuidance(Vector3 worldPoint)
    {
        OnGuidance?.Invoke(worldPoint);
    }
    
    #endregion
    
    #region Internal Event Handlers
    
    private void HandleConfirmInternal(Vector3 position)
    {
        // Mark as answered to complete the scenario
        StopActiveCoroutine();
        IsC1Completed = true;
        IsC1Rejected = false;
        HideAllCues();
    }
    
    private void HandleRejectInternal()
    {
        // Just mark as rejected - the scenario manager will handle the retry logic
        IsC1Completed = false;
        IsC1Rejected = true;
        
        // Hide cues but don't randomize position - that's handled by the scenario
        HideAllCues();
    }
    
    private void HandleGuidanceInternal(Vector3 position)
    {
        // Mark as answered to complete the scenario
        StopActiveCoroutine();
        IsC2Completed = true;
    }
    
    private void StopActiveCoroutine()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }
    }
    
    #endregion
    
    private IEnumerator UpdateSplineRoutine()
    {
        while (true)
        {
            if (_splineVisualizer != null && _c1CuePrefab != null)
            {
                _splineVisualizer.UpdateSpline(_droneController.transform.position, _c1CuePrefab.transform.position);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
} 