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
        [Tooltip("Radius of the circular zone in local units")]
        public float radius = 1f;
        [Tooltip("The target transform to reposition within this zone")]
        public Transform targetTransform;
    }

    [Header("Zone-Target Pairs")]
    [SerializeField] private ZoneTargetPair[] _zoneTargetPairs;

    /// <summary>
    /// Returns a random point within the specified zone.
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

        // Random point in unit circle, scaled by radius
        Vector2 sample = Random.insideUnitCircle * pair.radius;
        
        // Convert to world position relative to this zone's transform
        Vector3 worldPoint = pair.zoneTransform.TransformPoint(new Vector3(sample.x, 0f, sample.y));
        return worldPoint;
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

        // Get random position within zone
        Vector3 newPosition = GetRandomPointInZone(zoneIndex);
        
        // Maintain the target's current Y position
        newPosition.y = pair.targetTransform.position.y;
        
        // Move target to new position
        pair.targetTransform.position = newPosition;
        
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
    /// Gets a target transform by zone index.
    /// </summary>
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
} 