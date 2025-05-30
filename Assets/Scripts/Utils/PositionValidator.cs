using UnityEngine;
using System.Collections;

/// <summary>
/// Validates position relationships with configurable axis checks.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PositionValidator : MonoBehaviour
{
    [Header("Global Position Validation Settings")]
    [Tooltip("Default threshold for position validation (meters)")]
    [Range(0.01f, 1f)]
    [SerializeField] private float _globalThreshold = 0.1f;
    public static float GlobalThreshold { get; private set; } = 0.1f;

    private static PositionValidator _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        GlobalThreshold = _globalThreshold;
    }

    private void OnValidate()
    {
        if (_instance == this)
        {
            GlobalThreshold = _globalThreshold;
        }
    }

    [System.Serializable]
    public class AxisCheck
    {
        public bool checkX = true;
        public bool checkY = true;
        public bool checkZ = true;
        public float threshold = 0f; // Use 0 to indicate 'use global'

        public static AxisCheck Horizontal => new AxisCheck { checkY = false };
        public static AxisCheck Vertical => new AxisCheck { checkX = false, checkZ = false };
        public static AxisCheck XZ => new AxisCheck { checkY = false };
        public static AxisCheck XY => new AxisCheck { checkZ = false };
        public static AxisCheck YZ => new AxisCheck { checkX = false };
        public static AxisCheck All => new AxisCheck();
    }

    /// <summary>
    /// Check if current position is at target position, considering only specified axes
    /// </summary>
    public static bool IsAtPosition(Vector3 current, Vector3 target, AxisCheck check)
    {
        float totalDistance = 0f;
        int checkedAxes = 0;

        if (check.checkX)
        {
            totalDistance += Mathf.Abs(current.x - target.x);
            checkedAxes++;
        }
        if (check.checkY)
        {
            totalDistance += Mathf.Abs(current.y - target.y);
            checkedAxes++;
        }
        if (check.checkZ)
        {
            totalDistance += Mathf.Abs(current.z - target.z);
            checkedAxes++;
        }

        float threshold = check.threshold > 0f ? check.threshold : GlobalThreshold;
        return checkedAxes > 0 && (totalDistance / checkedAxes) <= threshold;
    }

    /// <summary>
    /// Check if current position is at target position, considering only specified axes
    /// </summary>
    public static bool IsAtPosition(Transform current, Vector3 target, AxisCheck check)
    {
        return IsAtPosition(current.position, target, check);
    }

    /// <summary>
    /// Check if current position is at target position, considering only specified axes
    /// </summary>
    public static bool IsAtPosition(Transform current, Transform target, AxisCheck check)
    {
        return IsAtPosition(current.position, target.position, check);
    }
} 