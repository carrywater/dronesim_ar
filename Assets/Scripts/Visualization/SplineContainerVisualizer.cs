using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace Visualization
{
    [RequireComponent(typeof(SplineContainer))]
    public class SplineContainerVisualizer : MonoBehaviour
    {
        [Header("Spline Settings")]
        [SerializeField] private float _splineThickness = 0.1f;
        [SerializeField] private Material _splineMaterial;
        [SerializeField] private Color _splineColor = Color.cyan;
        
        private SplineContainer _splineContainer;
        private Spline _spline;
        private BezierKnot[] _knots;
        
        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _spline = new Spline();
            _splineContainer.Spline = _spline;
            
            // Initialize with 2 knots (start and end)
            _knots = new BezierKnot[2];
            _spline.Knots = _knots;
            
            // Set material properties
            if (_splineMaterial != null)
            {
                _splineMaterial.color = _splineColor;
            }
        }
        
        public void UpdateSpline(Vector3 startPoint, Vector3 endPoint)
        {
            // Update start knot
            _knots[0] = new BezierKnot
            {
                Position = startPoint,
                TangentIn = new float3(0, 0, 0),
                TangentOut = new float3(0, 0, 0)
            };
            
            // Update end knot
            _knots[1] = new BezierKnot
            {
                Position = endPoint,
                TangentIn = new float3(0, 0, 0),
                TangentOut = new float3(0, 0, 0)
            };
            
            // Update spline
            _spline.Knots = _knots;
        }
        
        public void ShowSpline(bool show)
        {
            gameObject.SetActive(show);
        }
    }
} 