using UnityEngine;
using System;

public class DroneArrivalDetector : MonoBehaviour
{
    [Header("Arrival Settings")]
    [SerializeField] private float _arrivalThreshold = 0.1f;
    
    private Vector3 _currentDestination;
    private bool _isTracking;
    private bool _hasArrivedInvoked;
    
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
    }
    
    private void Update()
    {
        if (_isTracking && !_hasArrivedInvoked)
        {
            // Check if drone has reached destination
            if (Vector3.Distance(transform.position, _currentDestination) <= _arrivalThreshold)
            {
                _hasArrivedInvoked = true;
                _isTracking = false;
                OnArrived?.Invoke();
            }
        }
    }
} 