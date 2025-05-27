using UnityEngine;
using System;
using Interaction;

namespace Interaction
{
    /// <summary>
    /// Handles point-based gestures like thumbs up/down
    /// </summary>
    public class PointGestureHandler : MonoBehaviour
    {
        public event Action OnThumbsUp;
        public event Action OnThumbsDown;

        private bool _isActive;

        public void SetActive(bool active)
        {
            _isActive = active;
        }

        // This would be called by your gesture recognition system
        public void OnGestureDetected(bool isThumbsUp)
        {
            if (!_isActive) return;

            if (isThumbsUp)
                OnThumbsUp?.Invoke();
            else
                OnThumbsDown?.Invoke();
        }
    }
} 