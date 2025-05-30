using UnityEngine;
using System;

/// <summary>
/// Controls drone landing gear (legs) animation and sound.
/// Responsibilities:
/// - Extend/retract landing legs using Animator
/// - Play sound when legs move
/// Usage:
/// - Call RetractLegs() to retract legs
/// - Call HideLegs() to hide legs
/// </summary>
public class DroneLandingGear : MonoBehaviour
{
    [Header("Legs Animation")]
    [SerializeField] private Animator _legsAnimator;

    [Header("Gear Movement Sound")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _gearMoveClip;

    private void Awake()
    {
        if (_audioSource != null)
            SpatialAudioHelper.Configure(_audioSource);
    }

    /// <summary>
    /// Retract the landing gear (transition to Retracted state)
    /// </summary>
    public void RetractLegs()
    {
        if (_legsAnimator != null) _legsAnimator.SetTrigger("retract");
        PlayGearSound();
    }

    /// <summary>
    /// Hide the landing gear (transition to Hidden state)
    /// </summary>
    public void HideLegs()
    {
        if (_legsAnimator != null) _legsAnimator.SetTrigger("hide");
        PlayGearSound();
    }

    /// <summary>
    /// Play the gear movement sound effect
    /// </summary>
    private void PlayGearSound()
    {
        if (_audioSource != null && _gearMoveClip != null)
        {
            _audioSource.PlayOneShot(_gearMoveClip);
        }
    }
} 