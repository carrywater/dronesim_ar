using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

public class C1GestureHandler : MonoBehaviour
{
    [Header("Gesture Selectors")]
    [SerializeField] private SelectorUnityEventWrapper _leftThumbsUpSelector;
    [SerializeField] private SelectorUnityEventWrapper _leftThumbsDownSelector;
    [SerializeField] private SelectorUnityEventWrapper _rightThumbsUpSelector;
    [SerializeField] private SelectorUnityEventWrapper _rightThumbsDownSelector;
    
    // Events for gesture recognition
    public event System.Action OnThumbsUp;
    public event System.Action OnThumbsDown;
    
    private bool _isActive = false;
    
    private void OnEnable()
    {
        // Subscribe to gesture events
        if (_leftThumbsUpSelector != null)
        {
            _leftThumbsUpSelector.WhenSelected.AddListener(HandleThumbsUp);
            Debug.Log("[C1GestureHandler] Subscribed to left thumbs up selector.");
        }
        if (_leftThumbsDownSelector != null)
        {
            _leftThumbsDownSelector.WhenSelected.AddListener(HandleThumbsDown);
            Debug.Log("[C1GestureHandler] Subscribed to left thumbs down selector.");
        }
        if (_rightThumbsUpSelector != null)
        {
            _rightThumbsUpSelector.WhenSelected.AddListener(HandleThumbsUp);
            Debug.Log("[C1GestureHandler] Subscribed to right thumbs up selector.");
        }
        if (_rightThumbsDownSelector != null)
        {
            _rightThumbsDownSelector.WhenSelected.AddListener(HandleThumbsDown);
            Debug.Log("[C1GestureHandler] Subscribed to right thumbs down selector.");
        }
    }
    
    private void OnDisable()
    {
        // Unsubscribe from gesture events
        if (_leftThumbsUpSelector != null)
        {
            _leftThumbsUpSelector.WhenSelected.RemoveListener(HandleThumbsUp);
        }
        if (_leftThumbsDownSelector != null)
        {
            _leftThumbsDownSelector.WhenSelected.RemoveListener(HandleThumbsDown);
        }
        if (_rightThumbsUpSelector != null)
        {
            _rightThumbsUpSelector.WhenSelected.RemoveListener(HandleThumbsUp);
        }
        if (_rightThumbsDownSelector != null)
        {
            _rightThumbsDownSelector.WhenSelected.RemoveListener(HandleThumbsDown);
        }
    }
    
    public void SetActive(bool active)
    {
        _isActive = active;
    }
    
    private void HandleThumbsUp()
    {
        Debug.Log("[C1GestureHandler] HandleThumbsUp called. isActive=" + _isActive);
        if (_isActive)
        {
            Debug.Log("[C1GestureHandler] OnThumbsUp event invoked.");
            OnThumbsUp?.Invoke();
        }
        else
        {
            Debug.LogWarning("[C1GestureHandler] Thumbs up gesture ignored because handler is not active.");
        }
    }
    
    private void HandleThumbsDown()
    {
        Debug.Log("[C1GestureHandler] HandleThumbsDown called. isActive=" + _isActive);
        if (_isActive)
        {
            Debug.Log("[C1GestureHandler] OnThumbsDown event invoked.");
            OnThumbsDown?.Invoke();
        }
        else
        {
            Debug.LogWarning("[C1GestureHandler] Thumbs down gesture ignored because handler is not active.");
        }
    }
} 