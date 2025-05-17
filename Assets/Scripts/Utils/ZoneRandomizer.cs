using UnityEngine;

[RequireComponent(typeof(Transform))]
public class ZoneRandomizer : MonoBehaviour
{
    [System.Serializable]
    public class ZoneTargetPair
    {
        public string name;
        [Tooltip("The transform defining the zone's position and scale")]
        public Transform zoneTransform;
        [Tooltip("Radius of the circular zone in local units (fallback if no mesh is found)")]
        public float radius = 1f;
        [Tooltip("The target transform to reposition within this zone")]
        public Transform targetTransform;
    }

    [Header("Zone-Target Pairs")]
    [SerializeField] private ZoneTargetPair[] _zoneTargetPairs;

    /// <summary>
    /// Returns a random point within the specified zone's mesh bounds.
    /// </summary>
    /// <param name="zoneIndex">Index of the zone to use</param>
    /// <returns>Random world position within the zone</returns>
    public Vector3 GetRandomPointInZone(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= _zoneTargetPairs.Length)
        {
            Debug.LogError($"ZoneRandomizer: Invalid zone index {zoneIndex}");
            return Vector3.zero;
        }

        ZoneTargetPair pair = _zoneTargetPairs[zoneIndex];
        if (pair.zoneTransform == null)
        {
            Debug.LogError($"ZoneRandomizer: Zone transform is null for index {zoneIndex}");
            return Vector3.zero;
        }

        // Check if zone has a mesh renderer to use its bounds
        MeshRenderer meshRenderer = pair.zoneTransform.GetComponent<MeshRenderer>();
        
        if (meshRenderer != null)
        {
            // Use mesh bounds for more accurate positioning
            Bounds localBounds = meshRenderer.localBounds;
            
            // Generate random point within bounds (in local space)
            Vector3 randomLocalPoint = new Vector3(
                Random.Range(-localBounds.extents.x, localBounds.extents.x),
                0f, // Keep Y at 0 since we're working in horizontal plane
                Random.Range(-localBounds.extents.z, localBounds.extents.z)
            );
            
            // Transform to world space
            Vector3 worldPoint = pair.zoneTransform.TransformPoint(randomLocalPoint);
            return worldPoint;
        }
        else
        {
            // Fallback to radius-based approach if no mesh renderer
            Debug.LogWarning($"No MeshRenderer found on zone {pair.name}, using radius fallback");
            
            // Random point in unit circle, scaled by radius
            Vector2 sample = Random.insideUnitCircle * pair.radius;
            
            // Convert to world position relative to this zone's transform
            Vector3 worldPoint = pair.zoneTransform.TransformPoint(new Vector3(sample.x, 0f, sample.y));
            return worldPoint;
        }
    }
    
    /// <summary>
    /// Alternative method to get random point using collider bounds if available
    /// </summary>
    private Vector3 GetRandomPointUsingCollider(ZoneTargetPair pair)
    {
        // Try to get a collider (box, mesh or otherwise)
        Collider collider = pair.zoneTransform.GetComponent<Collider>();
        
        if (collider != null)
        {
            // Get the bounds in world space
            Bounds bounds = collider.bounds;
            
            // Generate random point within bounds
            Vector3 randomPoint = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                pair.targetTransform.position.y, // Keep current Y height
                Random.Range(bounds.min.z, bounds.max.z)
            );
            
            return randomPoint;
        }
        
        // Fallback to original method if no collider
        return Vector3.zero; // Signals caller to use fallback
    }

    /// <summary>
    /// Randomizes a specific target's position within its corresponding zone.
    /// </summary>
    /// <param name="zoneIndex">Index of the zone-target pair to randomize</param>
    /// <returns>The new position of the target</returns>
    public Vector3 RandomizeTargetPosition(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= _zoneTargetPairs.Length)
        {
            Debug.LogError($"ZoneRandomizer: Invalid zone index {zoneIndex}");
            return Vector3.zero;
        }

        ZoneTargetPair pair = _zoneTargetPairs[zoneIndex];
        if (pair.targetTransform == null)
        {
            Debug.LogError($"ZoneRandomizer: Target transform is null for index {zoneIndex}");
            return Vector3.zero;
        }

        // First try collider-based approach for more accurate bounds
        Vector3 newPosition = GetRandomPointUsingCollider(pair);
        
        // If no valid position from collider method (returns zero), use the mesh/radius method
        if (newPosition == Vector3.zero)
        {
            newPosition = GetRandomPointInZone(zoneIndex);
        }
        
        // Maintain the target's current Y position
        newPosition.y = pair.targetTransform.position.y;
        
        // Move target to new position
        pair.targetTransform.position = newPosition;
        
        // Log the new position for debugging
        Debug.Log($"RandomizeTargetPosition: Zone '{pair.name}' - New position: {newPosition}");
        
        return newPosition;
    }
    
    /// <summary>
    /// Randomizes all targets in their respective zones.
    /// </summary>
    public void RandomizeAllTargets()
    {
        for (int i = 0; i < _zoneTargetPairs.Length; i++)
        {
            RandomizeTargetPosition(i);
        }
    }

    /// <summary>
    /// Gets the target transform for the specified zone-target pair.
    /// </summary>
    /// <param name="zoneIndex">Index of the zone-target pair</param>
    /// <returns>The target transform or null if invalid</returns>
    public Transform GetTarget(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= _zoneTargetPairs.Length)
        {
            Debug.LogError($"ZoneRandomizer: Invalid zone index {zoneIndex}");
            return null;
        }

        return _zoneTargetPairs[zoneIndex].targetTransform;
    }
    
    /// <summary>
    /// Returns the number of zone-target pairs.
    /// </summary>
    public int ZoneCount => _zoneTargetPairs.Length;

    /// <summary>
    /// Gets the radius of the specified zone.
    /// </summary>
    /// <param name="zoneIndex">Index of the zone to get radius for</param>
    /// <returns>The zone radius or 0 if invalid</returns>
    public float GetZoneRadius(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= _zoneTargetPairs.Length)
        {
            Debug.LogError($"ZoneRandomizer: Invalid zone index {zoneIndex}");
            return 0f;
        }

        return _zoneTargetPairs[zoneIndex].radius;
    }
} 