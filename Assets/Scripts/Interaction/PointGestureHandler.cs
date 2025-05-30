using UnityEngine;
using System;
using Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;

namespace Interaction
{
    /// <summary>
    /// Handles point-based gestures like thumbs up/down
    /// </summary>
    public class PointGestureHandler : MonoBehaviour
    {
        public event Action<Vector3> OnTargetPlaced;

        [Tooltip("Reference to the DistanceHandGrabInteractable for this handler. If not set, will use GetComponent.")]
        [SerializeField] private DistanceHandGrabInteractable _interactable;
        private bool _isEnabled;
    
        private void Start()
        {
            if (_interactable == null)
                _interactable = GetComponent<DistanceHandGrabInteractable>();
            if (_interactable != null)
            {
                _interactable.WhenPointerEventRaised += OnPointerEvent;
            }
        }

        public void EnableInteraction()
        {
            _isEnabled = true;
            if (_interactable != null)
                _interactable.enabled = true;
            Debug.Log("[PointGestureHandler] EnableInteraction called, handler enabled");
        }

        public void DisableInteraction()
        {
            _isEnabled = false;
            if (_interactable != null)
                _interactable.enabled = false;
            Debug.Log("[PointGestureHandler] DisableInteraction called, handler disabled");
        }

        private void OnPointerEvent(PointerEvent evt)
        {
            Debug.Log($"[PointGestureHandler] OnPointerEvent called: evt.Type={evt.Type}, _isEnabled={_isEnabled}");
            if (!_isEnabled) return;

            // Check for select (grab/pinch) event
            if (evt.Type == PointerEventType.Select)
            {
                // Get the hit point from the pointer event
                Vector3 hitPoint = evt.Pose.position;
                Debug.Log($"[PointGestureHandler] PointerEventType.Select detected, invoking OnTargetPlaced with hit point {hitPoint}");
                OnTargetPlaced?.Invoke(hitPoint);
            }
        }
    
        private void OnDestroy()
        {
            if (_interactable != null)
            {
                _interactable.WhenPointerEventRaised -= OnPointerEvent;
            }
        }
    }
} 