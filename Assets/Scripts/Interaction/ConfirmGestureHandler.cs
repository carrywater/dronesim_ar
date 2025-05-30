using UnityEngine;
using System;
using Interaction;
using Oculus.Interaction;

namespace Interaction
{
    /// <summary>
    /// Handles grab/marker placement interactions
    /// </summary>
    public class ConfirmGestureHandler : MonoBehaviour
    {
        public event Action OnThumbsUp;
        public event Action OnThumbsDown;

        // Add references to the four SelectorUnityEventWrappers
        public SelectorUnityEventWrapper thumbsUpLeft;
        public SelectorUnityEventWrapper thumbsUpRight;
        public SelectorUnityEventWrapper thumbsDownLeft;
        public SelectorUnityEventWrapper thumbsDownRight;

        private bool _isActive;

        private void Awake()
        {
            if (thumbsUpLeft != null)
                thumbsUpLeft.WhenSelected.AddListener(() => OnGestureDetected(true));
            if (thumbsUpRight != null)
                thumbsUpRight.WhenSelected.AddListener(() => OnGestureDetected(true));
            if (thumbsDownLeft != null)
                thumbsDownLeft.WhenSelected.AddListener(() => OnGestureDetected(false));
            if (thumbsDownRight != null)
                thumbsDownRight.WhenSelected.AddListener(() => OnGestureDetected(false));
        }

        public void SetActive(bool active)
        {
            _isActive = active;
        }

        // This would be called by your gesture recognition system
        public void OnGestureDetected(bool isThumbsUp)
        {
            Debug.Log($"[ConfirmGestureHandler] OnGestureDetected called with isThumbsUp={isThumbsUp}, _isActive={_isActive}");
            if (!_isActive) return;

            if (isThumbsUp)
            {
                Debug.Log("[ConfirmGestureHandler] Invoking OnThumbsUp event");
                OnThumbsUp?.Invoke();
            }
            else
            {
                Debug.Log("[ConfirmGestureHandler] Invoking OnThumbsDown event");
                OnThumbsDown?.Invoke();
            }
        }
    }
} 