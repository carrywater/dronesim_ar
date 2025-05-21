using UnityEngine;

public class DroneManager : MonoBehaviour
{
    [Header("Drone Prefab & Settings")]
    [SerializeField] private GameObject _dronePrefab; // Prefab reference
    private DroneController _activeDrone;

    /// <summary>
    /// Checks if a DroneController instance exists in the scene.
    /// </summary>
    public bool HasActiveDrone()
    {
        if (_activeDrone != null) return true;
        _activeDrone = FindAnyObjectByType<DroneController>();
        return _activeDrone != null;
    }

    /// <summary>
    /// Spawns or moves the drone to the specified (x, y, z) world position.
    /// Sets the child 'Drone Runtime Offset' localPosition.z to abortHeight.
    /// </summary>
    public DroneController SpawnOrMoveDrone(Vector3 position, float abortHeight)
    {
        if (!HasActiveDrone())
        {
            var droneObj = Instantiate(_dronePrefab, position, Quaternion.identity);
            _activeDrone = droneObj.GetComponent<DroneController>();
            if (_activeDrone == null)
            {
                Debug.LogError("DroneManager: Prefab does not have a DroneController component!");
                return null;
            }
        }
        else
        {
            _activeDrone.transform.position = position;
        }
        // Set the child 'Drone Runtime Offset' z to abortHeight
        var offset = _activeDrone.transform.Find("Drone Runtime Offset");
        if (offset != null)
        {
            var local = offset.localPosition;
            local.z = abortHeight;
            offset.localPosition = local;
        }
        else
        {
            Debug.LogWarning("DroneManager: Could not find 'Drone Runtime Offset' child.");
        }
        return _activeDrone;
    }

    /// <summary>
    /// Destroys the active drone instance.
    /// </summary>
    public void DespawnDrone()
    {
        if (_activeDrone != null)
        {
            Destroy(_activeDrone.gameObject);
            _activeDrone = null;
        }
    }

    /// <summary>
    /// Returns the currently active drone.
    /// </summary>
    public DroneController GetActiveDrone() => _activeDrone;
} 