using UnityEngine;
using System;

public class DroneArrivalDetector : MonoBehaviour
{
    [Header("Arrival Settings")]
    [SerializeField] private float _arrivalSpeedThreshold = 0.15f;
    [SerializeField] private float _closeDistanceThreshold = 0.05f;
    [SerializeField] private float _closeSpeedThreshold = 0.3f;
    [SerializeField] private bool _showArrivalGizmo = false;
    
    private Vector3 _currentDestination;
    private bool _isTracking;
    private bool _hasArrivedInvoked;
    private Vector3 _lastPosition;
    private float _lastSpeed;
    
    public event Action OnArrived;
    
    /// <summary>
    /// Set a new destination for the drone to track arrival.
    /// Speed parameter kept for compatibility with existing interface.
    /// </summary>
    public void SetDestination(Vector3 position, float speed)
    {
        _currentDestination = position;
        _isTracking = true;
        _hasArrivedInvoked = false;
        _lastPosition = transform.position;
        _lastSpeed = 0f;
    }
    
    private void Update()
    {
        if (_isTracking && !_hasArrivedInvoked)
        {
            // Calculate speed (magnitude of velocity)
            float speed = (transform.position - _lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastPosition = transform.position;
            _lastSpeed = speed;

            // Calculate distance to target
            float distance = Vector3.Distance(transform.position, _currentDestination);

            // Arrive if either:
            // 1. Speed is below threshold (normal arrival)
            // 2. We're very close AND moving slowly (prevent false triggers)
            if (speed <= _arrivalSpeedThreshold || (distance <= _closeDistanceThreshold && speed <= _closeSpeedThreshold))
            {
                _hasArrivedInvoked = true;
                _isTracking = false;
                OnArrived?.Invoke();
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_showArrivalGizmo)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_currentDestination, _closeDistanceThreshold);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(_currentDestination + Vector3.up * 0.2f, 
                $"Speed Threshold: {_arrivalSpeedThreshold:F2}m/s\n" +
                $"Close Distance: {_closeDistanceThreshold:F2}m\n" +
                $"Close Speed: {_closeSpeedThreshold:F2}m/s");
#endif
        }
    }
#endif
} 