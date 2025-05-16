using UnityEngine;

public class DroneHMI : MonoBehaviour
{
    [Header("LED Animator")]
    [SerializeField] private Animator _ledAnimator;  // controls emissive material animations

    [Header("Looping Audio")]
    [SerializeField] private AudioSource _loopSource;         // set Loop = true in Inspector
    [SerializeField] private AudioClip   _landingBeepClip;    // constant landing beep

    [Header("One-Shot Audio")]
    [SerializeField] private AudioSource _oneShotSource;      // Loop = false
    [SerializeField] private AudioClip   _uncertaintyClip;
    [SerializeField] private AudioClip   _gestureAcceptClip;
    [SerializeField] private AudioClip   _lowConfidenceClip;
    [SerializeField] private AudioClip   _mediumConfidenceClip;
    [SerializeField] private AudioClip   _abortClip;
    [SerializeField] private AudioClip   _successClip;
    [SerializeField] private AudioClip   _falseGestureClip;

    // HMI state enumeration
    public enum HMIState { Idle, Uncertain, PromptConfirm, PromptGuide, Landing, Abort, Success }
    private HMIState _currentState;

    /// <summary>
    /// Transition the HMI to the given state: play animations and audio accordingly.
    /// </summary>
    public void SetStatus(HMIState newState)
    {
        if (_currentState == newState)
            return;
        _currentState = newState;

        // Stop all looped audio by default
        _loopSource.Stop();
        // Stop any one-shot (no direct stop API, but leaving pending)

        // Trigger LED animation state
        _ledAnimator.ResetTrigger("Idle");
  
        _ledAnimator.ResetTrigger("Uncertain");
        _ledAnimator.ResetTrigger("PromptConfirm");
        _ledAnimator.ResetTrigger("PromptGuide");
        _ledAnimator.ResetTrigger("Landing");
        _ledAnimator.ResetTrigger("Abort");
        _ledAnimator.ResetTrigger("Success");

        switch (newState)
        {
            case HMIState.Idle:
                _ledAnimator.SetTrigger("Idle");
                break;
            case HMIState.Uncertain:
                _ledAnimator.SetTrigger("Uncertain");
                // looped hover hum
                _loopSource.clip = _landingBeepClip;
                _loopSource.loop = true;
                _loopSource.Play();
                // one-shot uncertainty tone
                _oneShotSource.PlayOneShot(_uncertaintyClip);
                break;
            case HMIState.PromptConfirm:
                _ledAnimator.SetTrigger("PromptConfirm");
                _oneShotSource.PlayOneShot(_gestureAcceptClip);
                break;
            case HMIState.PromptGuide:
                _ledAnimator.SetTrigger("PromptGuide");
                _oneShotSource.PlayOneShot(_mediumConfidenceClip);
                break;
            case HMIState.Landing:
                _ledAnimator.SetTrigger("Landing");
                _oneShotSource.PlayOneShot(_lowConfidenceClip);
                break;
            case HMIState.Abort:
                _ledAnimator.SetTrigger("Abort");
                _oneShotSource.PlayOneShot(_abortClip);
                break;
            case HMIState.Success:
                _ledAnimator.SetTrigger("Success");
                _oneShotSource.PlayOneShot(_successClip);
                break;
        }
    }

    public void PlayHoverHum()
    {
        _loopSource.clip = _landingBeepClip;
        _loopSource.loop = true;
        _loopSource.Play();
    }

    public void StopHoverHum()
    {
        _loopSource.Stop();
    }

    public void PlayUncertainty()
        => _oneShotSource.PlayOneShot(_uncertaintyClip);

    public void PlayGestureAccept()
        => _oneShotSource.PlayOneShot(_gestureAcceptClip);

    // TODO: Implement drone HMI logic
} 