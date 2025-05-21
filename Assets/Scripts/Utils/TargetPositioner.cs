using UnityEngine;
using System.Collections.Generic;

namespace Utils
{
    public class TargetPositioner : MonoBehaviour
    {
        [System.Serializable]
        public class Zone
        {
            public string name;  // e.g., "InteractionZone", "NavigationZone"
            [Tooltip("The collider defining the zone's position and bounds")]
            public Collider zoneCollider; // Assign in Inspector
        }

        [Header("Zones")]
        [SerializeField] private Zone[] _zones;

        [Header("Active Target")]
        [Tooltip("The single active target transform for all scenarios")]
        [SerializeField] private Transform _activeTarget;

        // Dictionary for quick lookup by zone name
        private Dictionary<string, Zone> _zoneLookup;

        private void Awake()
        {
            InitializeZoneLookup();
        }

        private void InitializeZoneLookup()
        {
            _zoneLookup = new Dictionary<string, Zone>();
            foreach (var zone in _zones)
            {
                if (string.IsNullOrEmpty(zone.name))
                {
                    Debug.LogError($"TargetPositioner: Zone has no name!");
                    continue;
                }
                if (_zoneLookup.ContainsKey(zone.name))
                {
                    Debug.LogError($"TargetPositioner: Duplicate zone name: {zone.name}");
                    continue;
                }
                _zoneLookup[zone.name] = zone;
            }
        }

        /// <summary>
        /// Sets the single active target for all scenarios
        /// </summary>
        public void SetActiveTarget(Transform target)
        {
            _activeTarget = target;
        }

        /// <summary>
        /// Gets the current active target
        /// </summary>
        public Transform GetActiveTarget()
        {
            return _activeTarget;
        }

        /// <summary>
        /// Sets the position of the active target
        /// </summary>
        public void SetActiveTargetPosition(Vector3 position)
        {
            if (_activeTarget != null)
                _activeTarget.position = position;
        }

        /// <summary>
        /// Gets a random position within the specified zone
        /// </summary>
        public Vector3 GetRandomPositionInZone(string zoneName)
        {
            if (!_zoneLookup.TryGetValue(zoneName, out var zone))
            {
                Debug.LogError($"TargetPositioner: No zone found with name {zoneName}");
                return Vector3.zero;
            }
            return GetRandomPositionInZone(zone);
        }

        private Vector3 GetRandomPositionInZone(Zone zone)
        {
            if (zone.zoneCollider == null)
            {
                Debug.LogError($"TargetPositioner: Zone collider is null for {zone.name}");
                return Vector3.zero;
            }
            Bounds bounds = zone.zoneCollider.bounds;
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y,
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }
    }
} 