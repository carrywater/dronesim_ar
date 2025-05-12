using UnityEngine;
using UnityEngine.AI;
using System;

public class DroneNavigation : MonoBehaviour
{
    [Header("Agent Settings")]
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _fallbackSpeed = 3f;

    public event Action OnArrived;
    private bool _hasArrivedInvoked;

    /// <summary>
    /// Set a new destination and travel speed for the drone.
    /// </summary>
    public void SetDestination(Vector3 position, float speed)
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _hasArrivedInvoked = false;
            _agent.speed = speed;
            _agent.SetDestination(position);
        }
        else
        {
            // Fallback movement could be implemented here
        }
    }

    private void Update()
    {
        if (_agent != null && _agent.hasPath && !_agent.pathPending)
        {
            if (!_hasArrivedInvoked && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                _hasArrivedInvoked = true;
                OnArrived?.Invoke();
            }
        }
    }

    // TODO: Implement drone navigation logic (agent pathing, fallback movement)
} 