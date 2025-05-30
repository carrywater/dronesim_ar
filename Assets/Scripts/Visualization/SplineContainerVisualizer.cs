using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Visualization
{
    [RequireComponent(typeof(SplineContainer))]
    public class SplineContainerVisualizer : MonoBehaviour
    {
        private SplineContainer _splineContainer;

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
            if (_splineContainer == null)
                Debug.LogError("SplineContainer not found!");
        }

        /// <summary>
        /// Update the spline to connect two points in world space.
        /// </summary>
        public void UpdateSpline(Vector3 worldStart, Vector3 worldEnd)
        {
            if (_splineContainer == null) return;
            var spline = _splineContainer.Spline;
            // Convert world positions to local positions relative to the SplineContainer's transform
            Vector3 localStart = transform.InverseTransformPoint(worldStart);
            Vector3 localEnd = transform.InverseTransformPoint(worldEnd);

            if (spline.Count != 2)
            {
                spline.Clear();
                spline.Add(new BezierKnot(localStart));
                spline.Add(new BezierKnot(localEnd));
            }
            else
            {
                spline[0] = new BezierKnot(localStart);
                spline[1] = new BezierKnot(localEnd);
            }
            _splineContainer.Spline = spline;
        }

        /// <summary>
        /// Show or hide the spline.
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
} 