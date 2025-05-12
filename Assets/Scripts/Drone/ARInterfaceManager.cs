using UnityEngine;
using System;

public class ARInterfaceManager : MonoBehaviour
{
    [Header("AR Cue Objects")]
    [SerializeField] private GameObject _landingProbe;
    [SerializeField] private GameObject _guidancePad;

    [Header("Gesture Selectors")]
    [SerializeField] private ThumbsUpSelector _thumbsUp;
    [SerializeField] private ThumbsDownSelector _thumbsDown;
    [SerializeField] private PointSelector _pointAndHold;

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

    /// <summary>Activate probe UI and subscribe to thumbs-up/down.</summary>
    public void ShowProbe()
    {
        _guidancePad.SetActive(false);
        _landingProbe.SetActive(true);
        _thumbsUp.OnSelected += HandleConfirm;
        _thumbsDown.OnSelected += HandleReject;
    }

    /// <summary>Activate guidance pad and subscribe to point selector.</summary>
    public void ShowPad()
    {
        _landingProbe.SetActive(false);
        _guidancePad.SetActive(true);
        _pointAndHold.OnSelected += HandleGuidance;
    }

    /// <summary>Deactivate all AR cues and unsubscribe from selectors.</summary>
    public void HideAll()
    {
        _landingProbe.SetActive(false);
        _guidancePad.SetActive(false);
        _thumbsUp.OnSelected -= HandleConfirm;
        _thumbsDown.OnSelected -= HandleReject;
        _pointAndHold.OnSelected -= HandleGuidance;
    }

    private void HandleConfirm(Vector3 spot)
    {
        OnConfirm?.Invoke(spot);
    }

    private void HandleReject(Vector3 spot)
    {
        OnReject?.Invoke();
    }

    private void HandleGuidance(Vector3 spot)
    {
        OnGuidance?.Invoke(spot);
    }
} 