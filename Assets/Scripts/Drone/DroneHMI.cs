using UnityEngine;
using System;
using System.Linq;

public class DroneHMI : MonoBehaviour
{
    [Header("LED Animator")]
    [SerializeField] private Animator _ledAnimator;  // controls emissive material animations

    [Header("Signal Audio")]
    [SerializeField] private AudioSource _signalSource;       // for continuous signals
    [SerializeField] private AudioClip _landingBeepClip;      // constant landing beep

    [Header("One-Shot Audio")]
    [SerializeField] private AudioSource _oneShotSource;      // for notification sounds
    [SerializeField] private AudioClip ConfirmClip;
    [SerializeField] private AudioClip UncertaintyClip;
    [SerializeField] private AudioClip PromptGuideClip;
    [SerializeField] private AudioClip AbortClip;
    [SerializeField] private AudioClip SuccessClip;
    [SerializeField] private AudioClip FalseClip;

    // HMI state enumeration
    public enum HMIState { Idle, Uncertain, PromptConfirm, PromptGuide, Landing, Abort, Success, Reject }
    private HMIState _currentState;

    // Internal sound management
    // (Propeller sound logic removed; now handled by DroneRotorController)

    public event Action OnSoundComplete;
    public event Action OnAnimationComplete;

    private void Awake()
    {
        // Configure spatial audio for signal and one-shot sources
        if (_signalSource != null)
            SpatialAudioHelper.Configure(_signalSource);
        if (_oneShotSource != null)
            SpatialAudioHelper.Configure(_oneShotSource, 0.8f); // slightly less spatial
    }

    private void Update()
    {
        // Handle propeller sound volume and pitch adjustments
        // (Propeller sound logic removed; now handled by DroneRotorController)
    }

    /// <summary>
    /// Transition the HMI to the given state: play animations and audio accordingly.
    /// Optionally wait for completion and fire events.
    /// </summary>
    public void SetStatus(HMIState newState, bool waitForCompletion = false)
    {
        if (_currentState == newState)
            return;
        _currentState = newState;

        // Stop signal audio (landing beeps, etc.)
        if (_signalSource != null)
        {
            _signalSource.Stop();
        }

        // Trigger LED animation state
        if (_ledAnimator != null)
        {
            _ledAnimator.ResetTrigger("Idle");
            _ledAnimator.ResetTrigger("Uncertain");
            _ledAnimator.ResetTrigger("PromptConfirm");
            _ledAnimator.ResetTrigger("PromptGuide");
            _ledAnimator.ResetTrigger("Landing");
            _ledAnimator.ResetTrigger("Abort");
            _ledAnimator.ResetTrigger("Success");
            _ledAnimator.ResetTrigger("Reject");

            string trigger = null;
            AudioClip clip = null;
            switch (newState)
            {
                case HMIState.Idle:
                    trigger = "Idle";
                    break;
                case HMIState.Uncertain:
                    trigger = "Uncertain";
                    clip = UncertaintyClip;
                    break;
                case HMIState.PromptConfirm:
                    trigger = "PromptConfirm";
                    clip = ConfirmClip;
                    break;
                case HMIState.PromptGuide:
                    trigger = "PromptGuide";
                    clip = PromptGuideClip;
                    break;
                case HMIState.Landing:
                    trigger = "Landing";
                    clip = UncertaintyClip;
                    break;
                case HMIState.Abort:
                    trigger = "Abort";
                    clip = AbortClip;
                    break;
                case HMIState.Success:
                    trigger = "Success";
                    clip = SuccessClip;
                    break;
                case HMIState.Reject:
                    trigger = "Reject";
                    clip = FalseClip;
                    break;
            }
            if (trigger != null)
                _ledAnimator.SetTrigger(trigger);
            if (clip != null)
                PlayOneShot(clip, waitForCompletion);
            else if (waitForCompletion)
                StartCoroutine(FakeAnimationWait(trigger));
        }
    }

    /// <summary>
    /// Safely play a one-shot sound, optionally wait for completion and fire event.
    /// </summary>
    private void PlayOneShot(AudioClip clip, bool waitForCompletion = false)
    {
        if (_oneShotSource != null && clip != null)
        {
            _oneShotSource.PlayOneShot(clip);
            if (waitForCompletion)
                StartCoroutine(WaitForSoundToEnd(clip.length));
        }
        else if (waitForCompletion)
        {
            // If no sound, still fire event after a short delay
            StartCoroutine(FakeSoundWait());
        }
    }

    private System.Collections.IEnumerator WaitForSoundToEnd(float duration)
    {
        yield return new WaitForSeconds(duration);
        OnSoundComplete?.Invoke();
    }
    private System.Collections.IEnumerator FakeSoundWait()
    {
        yield return new WaitForSeconds(0.1f);
        OnSoundComplete?.Invoke();
    }
    private System.Collections.IEnumerator FakeAnimationWait(string trigger)
    {
        // If you want to wait for animation, you can use AnimatorStateInfo.length if needed
        yield return new WaitForSeconds(0.5f);
        OnAnimationComplete?.Invoke();
    }

    /// <summary>
    /// Play a landing signal sound
    /// </summary>
    public void PlayLandingSignal()
    {
        if (_signalSource != null && _landingBeepClip != null)
        {
            _signalSource.clip = _landingBeepClip;
            _signalSource.loop = true;
            _signalSource.volume = 1.0f; // Ensure volume is set
            _signalSource.Play();
            Debug.Log("Playing landing signal sound");
        }
        else
        {
            Debug.LogWarning("Cannot play landing signal - missing audio source or clip");
        }
    }

    /// <summary>
    /// Stop the landing signal sound
    /// </summary>
    public void StopLandingSignal()
    {
        if (_signalSource != null)
        {
            _signalSource.Stop();
            Debug.Log("Stopped landing signal sound");
        }
    }

    /// <summary>
    /// Play gesture accept sound
    /// </summary>
    public void PlayGestureAccept()
    {
        PlayOneShot(ConfirmClip);
    }

    // TODO: Implement drone HMI logic
} 