using UnityEngine;
using System;

/// <summary>
/// Manages physical drone components like rotors and legs.
/// 
/// Responsibilities:
/// - Control rotor animations and states
/// - Manage leg extensions/retractions
/// - Handle physical component visibility
/// - Provide events for component state changes
/// 
/// Rules of Use:
/// - Control through public methods (StartRotors, StopRotors, ExtendLegs, RetractLegs)
/// - Subscribe to events for state change notifications
/// - Ensure components are properly referenced in inspector
/// - Handle component animations through Unity's animation system
/// </summary>
public class DroneComponents : MonoBehaviour
{
    [Header("Rotor References")]
    [SerializeField] private Transform[] _rotors;
    [SerializeField] private float _rotorSpinSpeed = 720f; // degrees per second
    
    [Header("Leg Configuration")]
    [SerializeField] private LegConfig[] _legs;
    [SerializeField] private float _legExtensionSpeed = 90f; // degrees per second
    
    private bool _rotorsSpinning = false;
    private bool _legsExtended = false;
    
    // Events for component state changes
    public event Action OnRotorsStarted;
    public event Action OnRotorsStopped;
    public event Action OnLegsExtended;
    public event Action OnLegsRetracted;
    public event Action<bool> OnRotorsStateChanged;
    public event Action<bool> OnLegsStateChanged;
    
    private void Update()
    {
        // Handle rotor spinning
        if (_rotorsSpinning && _rotors != null)
        {
            foreach (Transform rotor in _rotors)
            {
                if (rotor != null)
                {
                    rotor.Rotate(Vector3.up, _rotorSpinSpeed * Time.deltaTime);
                }
            }
        }
    }
    
    /// <summary>
    /// Start rotor spinning
    /// </summary>
    public void StartRotors()
    {
        if (!_rotorsSpinning)
        {
            _rotorsSpinning = true;
            OnRotorsStarted?.Invoke();
            OnRotorsStateChanged?.Invoke(true);
        }
    }
    
    /// <summary>
    /// Stop rotor spinning
    /// </summary>
    public void StopRotors()
    {
        if (_rotorsSpinning)
        {
            _rotorsSpinning = false;
            OnRotorsStopped?.Invoke();
            OnRotorsStateChanged?.Invoke(false);
        }
    }
    
    /// <summary>
    /// Extend landing legs
    /// </summary>
    public void ExtendLegs()
    {
        if (_legs == null) return;
        
        foreach (var leg in _legs)
        {
            if (leg.enabled && leg.legTransform != null)
            {
                StartCoroutine(AnimateLeg(leg, leg.extendedAngle));
            }
        }
        
        _legsExtended = true;
        OnLegsExtended?.Invoke();
        OnLegsStateChanged?.Invoke(true);
    }
    
    /// <summary>
    /// Retract landing legs
    /// </summary>
    public void RetractLegs()
    {
        if (_legs == null) return;
        
        foreach (var leg in _legs)
        {
            if (leg.enabled && leg.legTransform != null)
            {
                StartCoroutine(AnimateLeg(leg, 0f));
            }
        }
        
        _legsExtended = false;
        OnLegsRetracted?.Invoke();
        OnLegsStateChanged?.Invoke(false);
    }
    
    /// <summary>
    /// Get current state of rotors
    /// </summary>
    public bool AreRotorsSpinning => _rotorsSpinning;
    
    /// <summary>
    /// Get current state of legs
    /// </summary>
    public bool AreLegsExtended => _legsExtended;
    
    /// <summary>
    /// Animate a single leg to target angle
    /// </summary>
    private System.Collections.IEnumerator AnimateLeg(LegConfig leg, float targetAngle)
    {
        float currentAngle = leg.legTransform.localEulerAngles[(int)leg.rotationAxis];
        float direction = leg.invertDirection ? -1f : 1f;
        
        while (Mathf.Abs(currentAngle - targetAngle) > 0.1f)
        {
            currentAngle = Mathf.MoveTowards(
                currentAngle,
                targetAngle,
                _legExtensionSpeed * Time.deltaTime * direction
            );
            
            Vector3 newRotation = leg.legTransform.localEulerAngles;
            newRotation[(int)leg.rotationAxis] = currentAngle;
            leg.legTransform.localEulerAngles = newRotation;
            
            yield return null;
        }
    }
} 