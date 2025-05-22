using UnityEngine;
using System;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace Interaction
{
    /// <summary>
    /// Handles point-based gestures and provides the pointed position
    /// </summary>
    public class PointGestureHandler : MonoBehaviour
    {
        public event Action OnPointGestureDetected;
        public Vector3 LastPointedPosition { get; private set; }

        private DistanceHandGrabInteractable _interactable;
        private bool _isEnabled;

        private void Start()
        {
            _interactable = GetComponent<DistanceHandGrabInteractable>();
            if (_interactable != null)
            {
                _interactable.WhenPointerEventRaised += OnPointerEvent;
            }
        }

        public void SetActive(bool active)
        {
            _isEnabled = active;
            if (_interactable != null)
                _interactable.enabled = active;
        }

        private void OnPointerEvent(PointerEvent evt)
        {
            if (!_isEnabled) return;

            // Check for hover event to get pointed position
            if (evt.Type == PointerEventType.Hover)
            {
                LastPointedPosition = evt.Pose.position;
                OnPointGestureDetected?.Invoke();
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