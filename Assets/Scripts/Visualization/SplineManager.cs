using UnityEngine;
using Utils;

namespace Visualization
{
    public class SplineManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private Transform _dronePivot;
        [SerializeField] private SplineContainerVisualizer _splineVisualizer;
        [SerializeField] private TargetPositioner _targetPositioner;

        [Header("Spline Prefab/Cue References")]
        [Tooltip("Optional: assign different spline visualizer prefabs/cues by name if needed")]
        [SerializeField] private string _defaultSplineCue = "DefaultSpline";
        // Add more cues as needed for future extensibility

        private Coroutine _updateCoroutine;
        private const float UPDATE_INTERVAL = 0.05f; // 20 updates per second

        private void Awake()
        {
            ValidateComponents();
        }

        private void ValidateComponents()
        {
            if (_dronePivot == null)
                Debug.LogError("SplineManager: Drone pivot reference is missing!");
            if (_splineVisualizer == null)
                Debug.LogError("SplineManager: Spline visualizer reference is missing!");
            if (_targetPositioner == null)
                Debug.LogError("SplineManager: TargetPositioner reference is missing!");
        }

        /// <summary>
        /// Shows the spline from the drone to the current active target
        /// </summary>
        public void ShowSpline()
        {
            Debug.Log("[SplineManager] ShowSpline called");
            var target = _targetPositioner != null ? _targetPositioner.GetActiveTarget() : null;
            if (target == null)
            {
                Debug.LogError("SplineManager: No active target set in TargetPositioner!");
                return;
            }

            if (_splineVisualizer != null)
            {
                _splineVisualizer.gameObject.SetActive(true); // Force active
                Debug.Log("[SplineManager] SplineVisualizer.SetVisible(true)");
                _splineVisualizer.SetVisible(true);
                _splineVisualizer.UpdateSpline(_dronePivot.position, target.position); // Immediate update
                StartSplineUpdate(target);
            }
            else
            {
                Debug.LogError("SplineManager: Spline visualizer is null!");
            }
        }

        /// <summary>
        /// Hides the current spline
        /// </summary>
        public void HideSpline()
        {
            if (_splineVisualizer != null)
                _splineVisualizer.SetVisible(false);
            StopSplineUpdate();
        }

        private void StartSplineUpdate(Transform target)
        {
            Debug.Log($"[SplineManager] StartSplineUpdate called for target {target.name}");
            StopSplineUpdate();
            _updateCoroutine = StartCoroutine(UpdateSplineRoutine(target));
        }

        private void StopSplineUpdate()
        {
            if (_updateCoroutine != null)
            {
                StopCoroutine(_updateCoroutine);
                _updateCoroutine = null;
            }
        }

        private System.Collections.IEnumerator UpdateSplineRoutine(Transform target)
        {
            Vector3 lastDronePos = Vector3.zero;
            Vector3 lastTargetPos = Vector3.zero;

            while (true)
            {
                if (_splineVisualizer != null && _dronePivot != null && target != null)
                {
                    Vector3 dronePos = _dronePivot.position;
                    Vector3 targetPos = target.position;
                    Debug.Log($"[SplineManager] UpdateSplineRoutine: dronePos={dronePos}, targetPos={targetPos}");
                    // Only update if positions changed significantly
                    if ((dronePos - lastDronePos).sqrMagnitude > 0.0001f ||
                        (targetPos - lastTargetPos).sqrMagnitude > 0.0001f)
                    {
                        Debug.Log("[SplineManager] Updating spline");
                        _splineVisualizer.UpdateSpline(dronePos, targetPos);
                        lastDronePos = dronePos;
                        lastTargetPos = targetPos;
                    }
                }
                else
                {
                    Debug.LogWarning("[SplineManager] Missing dependency in UpdateSplineRoutine");
                }
                yield return new WaitForSeconds(UPDATE_INTERVAL);
            }
        }
    }
} 