using UnityEngine;
using System;

public class ARInterfaceManager : MonoBehaviour
{
    [Header("AR Cue Objects")]
    [SerializeField] private GameObject _landingProbe;
    [SerializeField] private GameObject _guidancePad;

    /// <summary>Fired when user confirms (👍) on the probe.</summary>
    public event Action<Vector3> OnConfirm;
    /// <summary>Fired when user rejects (👎) the probe.</summary>
    public event Action OnReject;
    /// <summary>Fired when user provides guidance (point-and-hold) on the pad.</summary>
    public event Action<Vector3> OnGuidance;

    private void OnEnable()
    {
        HideAll();
    }

    /// <summary>Show the landing probe UI.</summary>
    public void ShowProbe()
    {
        _guidancePad.SetActive(false);
        _landingProbe.SetActive(true);
    }

    /// <summary>Show the guidance pad UI.</summary>
    public void ShowPad()
    {
        _landingProbe.SetActive(false);
        _guidancePad.SetActive(true);
    }

    /// <summary>Hide all AR cues.</summary>
    public void HideAll()
    {
        _landingProbe.SetActive(false);
        _guidancePad.SetActive(false);
    }

    /// <summary>Called by UnityEvent when user confirms the landing spot.</summary>
    public void HandleConfirm(Vector3 worldPoint)
    {
        OnConfirm?.Invoke(worldPoint);
    }

    /// <summary>Called by UnityEvent when user rejects the landing spot.</summary>
    public void HandleReject()
    {
        OnReject?.Invoke();
    }

    /// <summary>Called by UnityEvent when user points and holds.</summary>
    public void HandleGuidance(Vector3 worldPoint)
    {
        OnGuidance?.Invoke(worldPoint);
    }
} 