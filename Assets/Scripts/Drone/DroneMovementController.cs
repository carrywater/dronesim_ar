using UnityEngine;
using System;

/// <summary>
/// Handles all drone movement execution and position tracking.
/// 
/// Responsibilities:
/// - Execute movement commands (MoveTo, Hover, Land, Abort)
/// - Manage movement smoothing and acceleration
/// - Detect when movements are complete
/// - Provide events for movement completion
/// - Track current position and velocity
/// 
/// Rules of Use:
/// - Do NOT change transform position directly; use movement methods
/// - Always check IsMovementComplete before starting a new movement
/// - Subscribe to events for reliable completion notification
/// - Configure speed parameters before movement starts
/// </summary>
public class DroneMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _droneOffset;

    [Header("Movement Settings")]
    [Range(0.1f, 5f)]
    [SerializeField] private float _hoverSpeed = 2f;
    [Range(0.1f, 5f)]
    [SerializeField] private float _cruiseSpeed = 3f;
    [Range(0.1f, 5f)]
    [SerializeField] private float _landingSpeed = 2f;
    [Range(0.1f, 5f)]
    [SerializeField] private float _abortSpeed = 4f;
    
    [Header("Height Settings")]
    [Range(1f, 10f)]
    [SerializeField] private float _hoverHeight = 6f;
    [Range(5f, 15f)]
    [SerializeField] private float _abortHeight = 8f;
    
    [Header("Movement Smoothing")]
    [Range(0.1f, 2f)]
    [SerializeField] private float _accelerationTime = 0.5f;
    [Range(0.1f, 2f)]
    [SerializeField] private float _decelerationTime = 0.8f;
    [Range(0.05f, 0.5f)]
    [SerializeField] private float _arrivalThreshold = 0.1f;
    [Range(0.05f, 0.5f)]
    [SerializeField] private float _speedThreshold = 0.1f;
    
    // Events
    public event Action OnMovementComplete;
    public event Action OnHoverReached;
    public event Action OnLandingComplete;
    public event Action OnAbortComplete;
    
    // Movement state
    private Vector3 _targetPosition;
    private Vector3 _currentVelocity = Vector3.zero;
    private bool _isMoving = false;
    private MovementType _currentMovementType = MovementType.None;
    
    public enum MovementType
    {
        None,
        Hover,
        Cruise,
        Land,
        Abort
    }
    
    private void Awake()
    {
        if (_droneOffset == null && transform.childCount > 0)
        {
            _droneOffset = transform.GetChild(0);
            Debug.Log("Auto-assigned drone offset to first child");
        }
        
        if (_droneOffset == null)
        {
            Debug.LogError("DroneMovementController requires a drone offset transform!");
        }
    }
    
    private void Update()
    {
        if (!_isMoving || _droneOffset == null) return;
        
        // Execute movement based on target position
        UpdateMovement();
        
        // Check if we've reached the target
        CheckMovementCompletion();
    }
    
    /// <summary>
    /// Begin moving to hover at default hover height
    /// </summary>
    public void StartHover()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        StartMovement(MovementType.Hover);
    }
    
    /// <summary>
    /// Begin moving to a specific position (XZ only, maintain hover height)
    /// </summary>
    public void CruiseTo(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        _targetPosition = new Vector3(localPosition.x, _hoverHeight, localPosition.z);
        StartMovement(MovementType.Cruise);
    }
    
    /// <summary>
    /// Begin landing at the specified position
    /// </summary>
    public void LandAt(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        // First align horizontally with landing spot
        Vector3 horizontalTarget = new Vector3(localPosition.x, _droneOffset.localPosition.y, localPosition.z);
        _targetPosition = horizontalTarget;
        
        // When horizontal alignment is complete, begin descent
        StartCoroutine(SequentialMovement(
            () => IsAtTargetPosition(horizontalTarget, 0.1f), 
            () => 
            {
                _targetPosition = new Vector3(localPosition.x, 0.1f, localPosition.z);
                _currentMovementType = MovementType.Land;
            }
        ));
        
        StartMovement(MovementType.Cruise);
    }
    
    /// <summary>
    /// Abort landing and return to hover height
    /// </summary>
    public void AbortLanding()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _hoverHeight, currentPos.z);
        StartMovement(MovementType.Hover);
    }
    
    /// <summary>
    /// Begin abort sequence (rise to abort height)
    /// </summary>
    public void Abort()
    {
        Vector3 currentPos = _droneOffset.localPosition;
        _targetPosition = new Vector3(currentPos.x, _abortHeight, currentPos.z);
        StartMovement(MovementType.Abort);
    }
    
    /// <summary>
    /// Set the drone's position immediately (for initialization)
    /// </summary>
    public void SetPosition(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        _droneOffset.localPosition = localPosition;
        _targetPosition = localPosition;
        _currentVelocity = Vector3.zero;
        _isMoving = false;
    }
    
    /// <summary>
    /// Stop all movement immediately
    /// </summary>
    public void StopMovement()
    {
        _isMoving = false;
        _currentVelocity = Vector3.zero;
        _currentMovementType = MovementType.None;
    }
    
    /// <summary>
    /// Configure movement parameters
    /// </summary>
    public void Configure(float hoverHeight, float hoverSpeed, float cruiseSpeed, 
                           float abortHeight, float landingSpeed, float abortSpeed,
                           float accelerationTime, float decelerationTime)
    {
        _hoverHeight = hoverHeight;
        _hoverSpeed = hoverSpeed;
        _cruiseSpeed = cruiseSpeed;
        _abortHeight = abortHeight;
        _landingSpeed = landingSpeed;
        _abortSpeed = abortSpeed;
        _accelerationTime = accelerationTime;
        _decelerationTime = decelerationTime;
    }
    
    /// <summary>
    /// Check if we have arrived at the target position and are no longer moving
    /// </summary>
    public bool IsMovementComplete()
    {
        return !_isMoving;
    }
    
    /// <summary>
    /// Check if we're at the target position
    /// </summary>
    public bool IsAtTargetPosition(float threshold = 0.1f)
    {
        if (_droneOffset == null) return false;
        return Vector3.Distance(_droneOffset.localPosition, _targetPosition) < threshold;
    }
    
    /// <summary>
    /// Check if we're at a specific position
    /// </summary>
    private bool IsAtTargetPosition(Vector3 target, float threshold = 0.1f)
    {
        if (_droneOffset == null) return false;
        return Vector3.Distance(_droneOffset.localPosition, target) < threshold;
    }
    
    /// <summary>
    /// Get the current local position
    /// </summary>
    public Vector3 GetCurrentPosition()
    {
        return _droneOffset != null ? _droneOffset.localPosition : Vector3.zero;
    }
    
    /// <summary>
    /// Get the current movement velocity
    /// </summary>
    public Vector3 GetCurrentVelocity()
    {
        return _currentVelocity;
    }
    
    /// <summary>
    /// Start a movement of the specified type
    /// </summary>
    private void StartMovement(MovementType movementType)
    {
        _currentMovementType = movementType;
        _isMoving = true;
        
        // Calculate smooth time based on movement type and distance
        float distance = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        float speed = GetSpeedForMovementType(movementType);
        float smoothTime = CalculateSmoothTime(distance, speed, movementType);
        
        Debug.Log($"Starting {movementType} movement to {_targetPosition}, distance: {distance:F2}m, speed: {speed:F2}m/s, smooth time: {smoothTime:F2}s");
    }
    
    /// <summary>
    /// Update movement based on target position and movement type
    /// </summary>
    private void UpdateMovement()
    {
        // Calculate smooth time based on current parameters
        float smoothTime = CalculateSmoothTime(
            Vector3.Distance(_droneOffset.localPosition, _targetPosition),
            GetSpeedForMovementType(_currentMovementType),
            _currentMovementType
        );
        
        // Update position with smoothing
        _droneOffset.localPosition = Vector3.SmoothDamp(
            _droneOffset.localPosition,
            _targetPosition,
            ref _currentVelocity,
            smoothTime
        );
    }
    
    /// <summary>
    /// Check if the movement is complete
    /// </summary>
    private void CheckMovementCompletion()
    {
        // Calculate distance to target and current speed
        float distanceToTarget = Vector3.Distance(_droneOffset.localPosition, _targetPosition);
        float currentSpeed = _currentVelocity.magnitude;
        
        // Consider movement complete if we're close enough AND moving slowly enough
        if (distanceToTarget <= _arrivalThreshold && currentSpeed <= _speedThreshold)
        {
            _isMoving = false;
            
            // Trigger appropriate event based on movement type
            switch (_currentMovementType)
            {
                case MovementType.Hover:
                    OnHoverReached?.Invoke();
                    break;
                case MovementType.Land:
                    OnLandingComplete?.Invoke();
                    break;
                case MovementType.Abort:
                    OnAbortComplete?.Invoke();
                    break;
            }
            
            // Always trigger the generic completion event
            OnMovementComplete?.Invoke();
            
            Debug.Log($"Movement complete: {_currentMovementType}, position: {_droneOffset.localPosition:F2}");
        }
    }
    
    /// <summary>
    /// Calculate appropriate smooth time based on distance, speed and movement type
    /// </summary>
    private float CalculateSmoothTime(float distance, float speed, MovementType movementType)
    {
        // Base smooth time calculation
        float timeToTarget = distance / speed;
        
        // Different movement types need different smoothing characteristics
        switch (movementType)
        {
            case MovementType.Hover:
            case MovementType.Abort:
                // Vertical movements use acceleration time
                return Mathf.Max(0.1f, timeToTarget * _accelerationTime);
            
            case MovementType.Cruise:
                // Horizontal movements need more precision near the target
                return Mathf.Max(0.1f, timeToTarget * _decelerationTime);
            
            case MovementType.Land:
                // Landing needs to be extra smooth and careful
                return Mathf.Max(0.2f, timeToTarget * (_decelerationTime * 1.5f));
            
            default:
                return 0.5f;
        }
    }
    
    /// <summary>
    /// Get the appropriate speed for the current movement type
    /// </summary>
    private float GetSpeedForMovementType(MovementType movementType)
    {
        switch (movementType)
        {
            case MovementType.Hover: return _hoverSpeed;
            case MovementType.Cruise: return _cruiseSpeed;
            case MovementType.Land: return _landingSpeed;
            case MovementType.Abort: return _abortSpeed;
            default: return 1.0f;
        }
    }
    
    /// <summary>
    /// Execute a second movement after the first one completes
    /// </summary>
    private System.Collections.IEnumerator SequentialMovement(Func<bool> completionCheck, Action nextAction)
    {
        // Wait for the first movement to complete
        yield return new WaitUntil(completionCheck);
        
        // Execute the next action
        nextAction?.Invoke();
    }
} 