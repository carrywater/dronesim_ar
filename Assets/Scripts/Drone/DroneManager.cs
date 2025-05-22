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
    /// Spawns or moves the drone to the specified (x, z) world position and sets the runtime offset Y to the desired height.
    /// </summary>
    public DroneController SpawnOrMoveDroneWithOffset(Vector2 xzPosition, float offsetY)
    {
        if (!HasActiveDrone())
        {
            Vector3 position = new Vector3(xzPosition.x, 0f, xzPosition.y);
            var droneObj = Instantiate(_dronePrefab, position, Quaternion.identity);
            _activeDrone = droneObj.GetComponent<DroneController>();
            if (_activeDrone == null)
            {
                Debug.LogError("DroneManager: Prefab does not have a DroneController component!");
                return null;
            }
            SetRuntimeOffsetY(offsetY); // Only set offset Y at spawn
        }
        else
        {
            // Only update X and Z, preserve current Y
            Vector3 position = _activeDrone.transform.position;
            position.x = xzPosition.x;
            position.z = xzPosition.y;
            _activeDrone.transform.position = position;
            // Do not set offset Y again here
        }
        return _activeDrone;
    }

    // Deprecated: Use SpawnOrMoveDroneWithOffset instead for robust initialization
    public DroneController SpawnOrMoveDrone(Vector2 xzPosition)
    {
        Debug.LogWarning("SpawnOrMoveDrone(Vector2) is deprecated. Use SpawnOrMoveDroneWithOffset(Vector2, float) instead.");
        return SpawnOrMoveDroneWithOffset(xzPosition, 0f);
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

    /// <summary>
    /// Sets the local Y offset of the 'Drone Runtime Offset' child.
    /// </summary>
    public void SetRuntimeOffsetY(float y)
    {
        if (!HasActiveDrone()) return;
        var offset = _activeDrone.transform.Find("Drone Runtime Offset");
        if (offset != null)
        {
            var local = offset.localPosition;
            local.y = y;
            offset.localPosition = local;
            Debug.Log($"[DroneManager] SetRuntimeOffsetY called with y={y} (frame {Time.frameCount})");
        }
        else
        {
            Debug.LogWarning("DroneManager: Could not find 'Drone Runtime Offset' child.");
        }
    }
} 