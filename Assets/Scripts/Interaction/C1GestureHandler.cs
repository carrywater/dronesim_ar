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
        }
        if (_leftThumbsDownSelector != null)
        {
            _leftThumbsDownSelector.WhenSelected.AddListener(HandleThumbsDown);
        }
        if (_rightThumbsUpSelector != null)
        {
            _rightThumbsUpSelector.WhenSelected.AddListener(HandleThumbsUp);
        }
        if (_rightThumbsDownSelector != null)
        {
            _rightThumbsDownSelector.WhenSelected.AddListener(HandleThumbsDown);
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
        if (_isActive)
        {
            OnThumbsUp?.Invoke();
        }
    }
    
    private void HandleThumbsDown()
    {
        if (_isActive)
        {
            OnThumbsDown?.Invoke();
        }
    }
} 