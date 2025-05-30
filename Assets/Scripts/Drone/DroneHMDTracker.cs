using UnityEngine;

/// <summary>
/// Handles all HMD (head-mounted display) tracking functionality for the drone.
/// 
/// Responsibilities:
/// - Track HMD position and orientation
/// - Rotate drone to face the user
/// - Smooth and limit rotation to prevent jerky movements
/// - Provide options for tracking delay and speed
/// 
/// Rules of Use:
/// - Assign an HMD transform to track (usually the OVR camera or similar)
/// - Do not directly modify the drone's rotation in other scripts
/// - Configure tracking parameters before starting tracking
/// </summary>
public class DroneHMDTracker : MonoBehaviour
{
    [Header("HMD Tracking")]
    [SerializeField] private Transform _hmdTransform;
    [SerializeField] private Transform _droneOffset;

    [Header("Tracking Settings")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _trackingSpeed = 2f;
    [Range(0f, 2f)]
    [SerializeField] private float _trackingDelay = 0.5f;
    [Range(10f, 180f)]
    [SerializeField] private float _maxRotationAngle = 45f;
    [SerializeField] private bool _trackingEnabled = true;

    private Vector3 _targetHmdDirection;
    private float _hmdTrackingTimer;
    
    private void Awake()
    {
        if (_droneOffset == null && transform.childCount > 0)
        {
            _droneOffset = transform.GetChild(0);
            Debug.Log("Auto-assigned drone offset to first child");
        }
        
        if (_droneOffset == null)
        {
            Debug.LogError("DroneHMDTracker: No drone offset found!");
        }
        
        if (_hmdTransform == null)
        {
            Debug.LogWarning("DroneHMDTracker: No HMD transform assigned! Tracking will be disabled until set.");
        }
    }

    /// <summary>
    /// Set the HMD transform to track
    /// </summary>
    public void SetHMDTransform(Transform hmdTransform)
    {
        _hmdTransform = hmdTransform;
        Debug.Log($"DroneHMDTracker: Set tracking target to {hmdTransform.name}");
    }
    
    /// <summary>
    /// Enable or disable HMD tracking
    /// </summary>
    public void EnableTracking(bool enable)
    {
        _trackingEnabled = enable;
    }
    
    /// <summary>
    /// Configure tracking parameters
    /// </summary>
    public void Configure(float trackingSpeed, float trackingDelay, float maxRotationAngle)
    {
        _trackingSpeed = trackingSpeed;
        _trackingDelay = trackingDelay;
        _maxRotationAngle = maxRotationAngle;
    }
    
    /// <summary>
    /// Temporarily boost tracking speed for faster response
    /// </summary>
    public void BoostTrackingSpeed(float boostFactor, float duration)
    {
        StartCoroutine(TemporarilyBoostSpeed(boostFactor, duration));
    }
    
    private System.Collections.IEnumerator TemporarilyBoostSpeed(float boostFactor, float duration)
    {
        float originalSpeed = _trackingSpeed;
        _trackingSpeed *= boostFactor;
        
        yield return new WaitForSeconds(duration);
        
        _trackingSpeed = originalSpeed;
    }

    private void Update()
    {
        if (!_trackingEnabled || _hmdTransform == null || _droneOffset == null) return;

        // Calculate direction to HMD
        Vector3 hmdPosition = _hmdTransform.position;
        Vector3 directionToHMD = hmdPosition - transform.position;
        directionToHMD.y = 0; // Keep rotation only in horizontal plane

        // Only update target direction after delay (reduces jitter)
        _hmdTrackingTimer += Time.deltaTime;
        if (_hmdTrackingTimer >= _trackingDelay)
        {
            _targetHmdDirection = directionToHMD.normalized;
            _hmdTrackingTimer = 0f;
        }

        // Calculate target rotation
        Quaternion targetRotation = Quaternion.LookRotation(_targetHmdDirection);
        
        // Limit rotation angle
        float currentAngle = Quaternion.Angle(_droneOffset.rotation, targetRotation);
        if (currentAngle > _maxRotationAngle)
        {
            targetRotation = Quaternion.RotateTowards(
                _droneOffset.rotation,
                targetRotation,
                _maxRotationAngle
            );
        }

        // Smoothly rotate only the drone offset towards target
        _droneOffset.rotation = Quaternion.Slerp(
            _droneOffset.rotation,
            targetRotation,
            _trackingSpeed * Time.deltaTime
        );
    }
    
    /// <summary>
    /// Face a specific world position immediately
    /// </summary>
    public void FacePosition(Vector3 worldPosition)
    {
        if (_droneOffset == null) return;
        
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0; // Keep rotation in horizontal plane
        
        if (direction.magnitude > 0.01f)
        {
            _targetHmdDirection = direction.normalized;
            _droneOffset.rotation = Quaternion.LookRotation(_targetHmdDirection);
        }
    }
} 