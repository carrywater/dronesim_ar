using UnityEngine;
using System;

/// <summary>
/// Controls drone rotor animation and propeller sound.
/// Responsibilities:
/// - Start/stop rotor rotation via code (local Z axis)
/// - Play/stop propeller sound
/// Usage:
/// - Call StartRotors() to animate and play sound
/// - Call StopRotors() to stop animation and sound
/// </summary>
public class DroneRotorController : MonoBehaviour
{
    [Header("Rotor Transforms")]
    [SerializeField] private Transform[] _rotors;
    [SerializeField] private float _rotorSpinSpeed = 720f; // degrees per second

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

    private void Update()
    {
        // Rotate all rotors if active
        if (_rotorsActive && _rotors != null)
        {
            foreach (var rotor in _rotors)
            {
                if (rotor != null)
                {
                    rotor.Rotate(Vector3.forward, _rotorSpinSpeed * Time.deltaTime, Space.Self);
                }
            }
        }
        // Handle propeller sound fade and pitch
        if (_propellerSource != null)
        {
            _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, _fadeSpeed * Time.deltaTime);
            _propellerSource.volume = _currentVolume;
            _propellerSource.pitch = Mathf.Lerp(_propellerSource.pitch, _targetPitch, _fadeSpeed * Time.deltaTime);
            if (_currentVolume <= 0f && !_rotorsActive && _propellerSource.isPlaying)
                _propellerSource.Stop();
        }
    }

    public void StartRotors()
    {
        _rotorsActive = true;
        _targetVolume = _baseVolume;
        _targetPitch = 1.0f;
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
    }
} 