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