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
        public event Action OnTargetPlaced;

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

        public void EnableInteraction()
        {
            _isEnabled = true;
            if (_interactable != null)
                _interactable.enabled = true;
        }

        public void DisableInteraction()
        {
            _isEnabled = false;
            if (_interactable != null)
                _interactable.enabled = false;
        }

        private void OnPointerEvent(PointerEvent evt)
        {
            if (!_isEnabled) return;

            // Check for select (grab/pinch) event
            if (evt.Type == PointerEventType.Select)
        {
                OnTargetPlaced?.Invoke();
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