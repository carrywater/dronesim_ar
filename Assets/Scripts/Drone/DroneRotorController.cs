using UnityEngine;
using System;

/// <summary>
/// Controls drone rotor animation and propeller sound.
/// Responsibilities:
/// - Start/stop rotor animation
/// - Play/stop propeller sound
/// Usage:
/// - Call StartRotors() to animate and play sound
/// - Call StopRotors() to stop animation and sound
/// </summary>
public class DroneRotorController : MonoBehaviour
{
    [Header("Rotor Animation")]
    [SerializeField] private Animator _rotorAnimator;

    [Header("Propeller Sound")]
    [SerializeField] private AudioSource _propellerSource;
    [SerializeField] private AudioClip _propellerHumClip;
    [SerializeField] private float _baseVolume = 0.5f;
    [SerializeField] private float _pitchMin = 0.8f;
    [SerializeField] private float _pitchMax = 1.2f;
    [SerializeField] private float _fadeSpeed = 1.5f;

    private bool _rotorsActive = false;
    private float _targetVolume = 0f;
    private float _currentVolume = 0f;
    private float _targetPitch = 1f;

    private void Awake()
    {
        if (_propellerSource != null)
            SpatialAudioHelper.Configure(_propellerSource);
    }

    public void StartRotors()
    {
        _rotorsActive = true;
        _targetVolume = _baseVolume;
        _targetPitch = 1.0f;
        if (_rotorAnimator != null) _rotorAnimator.SetBool("RotorsOn", true);
        if (_propellerSource != null && _propellerHumClip != null && !_propellerSource.isPlaying)
        {
            _propellerSource.clip = _propellerHumClip;
            _propellerSource.loop = true;
            _propellerSource.volume = 0f;
            _propellerSource.Play();
        }
    }

    public void StopRotors()
    {
        _rotorsActive = false;
        _targetVolume = 0f;
        if (_rotorAnimator != null) _rotorAnimator.SetBool("RotorsOn", false);
    }

    public void SetRotorPitch(float normalizedSpeed)
    {
        if (_rotorsActive)
        {
            _targetPitch = Mathf.Lerp(_pitchMin, _pitchMax, normalizedSpeed);
        }
    }

    private void Update()
    {
        // Smoothly adjust volume
        if (_propellerSource != null)
        {
            if (_currentVolume != _targetVolume)
            {
                _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, _fadeSpeed * Time.deltaTime);
                _propellerSource.volume = _currentVolume;
                if (_currentVolume <= 0.01f && _propellerSource.isPlaying)
                    _propellerSource.Stop();
                else if (_currentVolume > 0.01f && !_propellerSource.isPlaying && _propellerHumClip != null)
                {
                    _propellerSource.clip = _propellerHumClip;
                    _propellerSource.Play();
                }
            }
            if (_propellerSource.isPlaying && _propellerSource.pitch != _targetPitch)
            {
                _propellerSource.pitch = Mathf.Lerp(_propellerSource.pitch, _targetPitch, Time.deltaTime * 2f);
            }
        }
    }
} 