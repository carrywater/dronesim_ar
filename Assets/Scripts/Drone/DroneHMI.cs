using UnityEngine;
using System.Linq;

public class DroneHMI : MonoBehaviour
{
    [Header("LED Animator")]
    [SerializeField] private Animator _ledAnimator;  // controls emissive material animations

    [Header("Propeller Sound")]
    [SerializeField] private AudioSource _propellerSource;    // for drone humming
    [SerializeField] private AudioClip _propellerHumClip;     // continuous propeller sound
    [SerializeField] private float _propellerBaseVolume = 0.5f;
    [SerializeField] private float _propellerPitchMin = 0.8f;
    [SerializeField] private float _propellerPitchMax = 1.2f;
    [SerializeField] private float _humFadeSpeed = 1.5f;      // speed for fading volume in/out

    [Header("Signal Audio")]
    [SerializeField] private AudioSource _signalSource;       // for continuous signals
    [SerializeField] private AudioClip _landingBeepClip;      // constant landing beep

    [Header("One-Shot Audio")]
    [SerializeField] private AudioSource _oneShotSource;      // for notification sounds
    [SerializeField] private AudioClip _gestureAcceptClip;
    [SerializeField] private AudioClip _lowConfidenceClip;
    [SerializeField] private AudioClip _mediumConfidenceClip;
    [SerializeField] private AudioClip _abortClip;
    [SerializeField] private AudioClip _successClip;
    [SerializeField] private AudioClip _rejectClip;

    [Header("Spatial Audio Settings")]
    [Tooltip("Minimum distance before sound starts to attenuate")]
    [SerializeField] private float _minDistance = 1f;
    [Tooltip("Maximum distance where sound reaches minimum volume")]
    [SerializeField] private float _maxDistance = 30f;
    [Tooltip("How much the Doppler effect affects pitch")]
    [Range(0f, 5f)]
    [SerializeField] private float _dopplerLevel = 1f;
    [Tooltip("How directional the sound is (0=omnidirectional, 360=highly directional)")]
    [Range(0f, 360f)]
    [SerializeField] private float _spread = 120f;
    [Tooltip("Volume multiplier for reverb zones")]
    [Range(0f, 1.1f)]
    [SerializeField] private float _reverbZoneMix = 1.0f;

    // HMI state enumeration
    public enum HMIState { Idle, Uncertain, PromptConfirm, PromptGuide, Landing, Abort, Success, Reject }
    private HMIState _currentState;

    // Internal sound management
    private bool _propellersActive = false;
    private float _targetPropellerVolume = 0f;
    private float _currentPropellerVolume = 0f;
    private float _targetPropellerPitch = 1f;

    private void Awake()
    {
        // If a propeller source wasn't assigned, try to create one
        if (_propellerSource == null)
        {
            // Look for existing propeller audio source
            _propellerSource = GetComponents<AudioSource>()
                .FirstOrDefault(source => source.name == "PropellerSource");
            
            // If not found, create a new audio source
            if (_propellerSource == null)
            {
                _propellerSource = gameObject.AddComponent<AudioSource>();
                _propellerSource.name = "PropellerSource";
                
                // Configure for 3D spatial audio
                ConfigureSpatialAudio(_propellerSource);
                
                _propellerSource.loop = true;
                _propellerSource.playOnAwake = false;
                _propellerSource.volume = 0;
                Debug.Log("Created propeller audio source");
            }
        }
        else
        {
            // Configure existing audio source with spatial settings
            ConfigureSpatialAudio(_propellerSource);
        }
        
        // Also configure signal source if present
        if (_signalSource != null)
        {
            ConfigureSpatialAudio(_signalSource);
        }
        
        // Configure one-shot source with less strong spatial effects
        if (_oneShotSource != null)
        {
            ConfigureSpatialAudio(_oneShotSource, 0.8f);
        }
    }

    private void Update()
    {
        // Handle propeller sound volume and pitch adjustments
        if (_propellerSource != null)
        {
            // Smoothly adjust volume
            if (_currentPropellerVolume != _targetPropellerVolume)
            {
                _currentPropellerVolume = Mathf.MoveTowards(
                    _currentPropellerVolume, 
                    _targetPropellerVolume, 
                    _humFadeSpeed * Time.deltaTime);
                
                _propellerSource.volume = _currentPropellerVolume;
                
                // Start or stop the audio based on volume
                if (_currentPropellerVolume <= 0.01f && _propellerSource.isPlaying)
                {
                    _propellerSource.Stop();
                }
                else if (_currentPropellerVolume > 0.01f && !_propellerSource.isPlaying && _propellerHumClip != null)
                {
                    _propellerSource.clip = _propellerHumClip;
                    _propellerSource.Play();
                }
            }
            
            // Smoothly adjust pitch when active
            if (_propellerSource.isPlaying && _propellerSource.pitch != _targetPropellerPitch)
            {
                _propellerSource.pitch = Mathf.Lerp(
                    _propellerSource.pitch, 
                    _targetPropellerPitch, 
                    Time.deltaTime * 2f);
            }
        }
    }

    /// <summary>
    /// Transition the HMI to the given state: play animations and audio accordingly.
    /// </summary>
    public void SetStatus(HMIState newState)
    {
        if (_currentState == newState)
            return;
        _currentState = newState;

        // Stop signal audio (landing beeps, etc.)
        if (_signalSource != null)
        {
            _signalSource.Stop();
        }
        
        // Stop any one-shot (no direct stop API, but leaving pending)

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

            switch (newState)
            {
                case HMIState.Idle:
                    _ledAnimator.SetTrigger("Idle");
                    break;
                case HMIState.Uncertain:
                    _ledAnimator.SetTrigger("Uncertain");
                    PlayOneShot(_lowConfidenceClip);
                    break;
                case HMIState.PromptConfirm:
                    _ledAnimator.SetTrigger("PromptConfirm");
                    PlayOneShot(_gestureAcceptClip);
                    break;
                case HMIState.PromptGuide:
                    _ledAnimator.SetTrigger("PromptGuide");
                    PlayOneShot(_mediumConfidenceClip);
                    break;
                case HMIState.Landing:
                    _ledAnimator.SetTrigger("Landing");
                    PlayOneShot(_lowConfidenceClip);
                    break;
                case HMIState.Abort:
                    _ledAnimator.SetTrigger("Abort");
                    PlayOneShot(_abortClip);
                    break;
                case HMIState.Success:
                    _ledAnimator.SetTrigger("Success");
                    PlayOneShot(_successClip);
                    break;
                case HMIState.Reject:
                    _ledAnimator.SetTrigger("Reject");
                    PlayOneShot(_rejectClip);
                    break;
            }
        }
    }

    /// <summary>
    /// Start propeller sound at normal speed
    /// </summary>
    public void PlayHoverHum()
    {
        _propellersActive = true;
        _targetPropellerVolume = _propellerBaseVolume;
        _targetPropellerPitch = 1.0f;
        
        if (_propellerSource != null && _propellerHumClip != null && !_propellerSource.isPlaying)
        {
            _propellerSource.clip = _propellerHumClip;
            _propellerSource.loop = true;
            _propellerSource.volume = 0f; // Start at 0 and fade in
            _propellerSource.Play();
            
            Debug.Log("Started propeller sound");
        }
        else if (_propellerHumClip == null)
        {
            Debug.LogWarning("No propeller hum clip assigned!");
        }
    }

    /// <summary>
    /// Fade out and stop propeller sound
    /// </summary>
    public void StopHoverHum()
    {
        _propellersActive = false;
        _targetPropellerVolume = 0f;
        
        Debug.Log("Stopping propeller sound");
    }

    /// <summary>
    /// Set propeller sound pitch for speed variation
    /// </summary>
    public void SetPropellerPitch(float normalizedSpeed)
    {
        if (_propellersActive)
        {
            // Map 0-1 speed to pitch range
            _targetPropellerPitch = Mathf.Lerp(_propellerPitchMin, _propellerPitchMax, normalizedSpeed);
        }
    }

    /// <summary>
    /// Set propeller sound volume (relative to base volume)
    /// </summary>
    public void SetPropellerVolume(float volumeFactor)
    {
        if (_propellersActive)
        {
            // Apply volume factor to the base volume
            _targetPropellerVolume = _propellerBaseVolume * Mathf.Clamp(volumeFactor, 0f, 1.5f);
        }
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
    /// Safely play a one-shot sound
    /// </summary>
    private void PlayOneShot(AudioClip clip)
    {
        if (_oneShotSource != null && clip != null)
        {
            _oneShotSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Play gesture accept sound
    /// </summary>
    public void PlayGestureAccept()
    {
        PlayOneShot(_gestureAcceptClip);
    }

    /// <summary>
    /// Configures an AudioSource with proper spatial audio settings
    /// </summary>
    private void ConfigureSpatialAudio(AudioSource source, float spatialBlendOverride = 1.0f)
    {
        if (source == null) return;
        
        // Full 3D spatialization
        source.spatialBlend = spatialBlendOverride;
        
        // Distance attenuation
        source.minDistance = _minDistance;
        source.maxDistance = _maxDistance;
        
        // Use logarithmic rolloff for more realistic distance attenuation
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        
        // Doppler effect (pitch shift based on movement)
        source.dopplerLevel = _dopplerLevel;
        
        // Sound spread (directional vs. omnidirectional)
        source.spread = _spread;
        
        // Reverb mix
        source.reverbZoneMix = _reverbZoneMix;
        
        // For more realism, also enable Air Absorption which reduces high frequencies at distance
        source.SetCustomCurve(
            AudioSourceCurveType.ReverbZoneMix,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f)
        );
    }

    // TODO: Implement drone HMI logic
} 