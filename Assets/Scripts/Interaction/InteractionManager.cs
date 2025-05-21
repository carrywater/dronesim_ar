using System;
using System.Collections;
using UnityEngine;
using Utils;

/// <summary>
/// Manages AR cues and user interaction logic (gestures, confirmations).
/// Shows/hides cues as needed for the current scenario.
/// Never manages targets directly—just shows/hides cues relative to the active target.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Cue References (under Active Target)")]
    [SerializeField] private GameObject ringCue;
    [SerializeField] private GameObject thumbsCue;
    [SerializeField] private GameObject thumbUpCue;
    [SerializeField] private GameObject thumbDownCue;
    [SerializeField] private GenericGestureHandler gestureHandler;

    [Header("Debug/Startup")]
    [Tooltip("Show cues at startup for debugging/demo")]
    [SerializeField] private bool _showCuesAtStartup = false;

    // Completion flags for scenario manager
    public bool IsGestureConfirmed { get; private set; } = false;
    public bool IsGestureRejected { get; private set; } = false;

    private Coroutine _activeCoroutine;

    private void Awake()
    {
        if (_showCuesAtStartup)
        {
            ShowCue("thumbs");
        }
        else
        {
            HideAllCues();
        }
    }

    private void OnEnable()
    {
        if (!_showCuesAtStartup)
            HideAllCues();
        if (gestureHandler != null)
        {
            gestureHandler.OnConfirm += HandleGestureConfirm;
            gestureHandler.OnReject += HandleGestureReject;
        }
    }

    private void OnDisable()
    {
        if (gestureHandler != null)
        {
            gestureHandler.OnConfirm -= HandleGestureConfirm;
            gestureHandler.OnReject -= HandleGestureReject;
        }
    }

    /// <summary>
    /// Show a specific cue by name (e.g., "ring", "thumbs", "thumbUp", "thumbDown")
    /// </summary>
    public void ShowCue(string cueName)
    {
        HideAllCues();
        switch (cueName.ToLower())
        {
            case "ring":
                if (ringCue != null) ringCue.SetActive(true);
                break;
            case "thumbs":
                if (thumbsCue != null) thumbsCue.SetActive(true);
                break;
            case "thumbup":
                if (thumbUpCue != null) thumbUpCue.SetActive(true);
                break;
            case "thumbdown":
                if (thumbDownCue != null) thumbDownCue.SetActive(true);
                break;
        }
    }

    /// <summary>
    /// Hide all cues
    /// </summary>
    public void HideAllCues()
    {
        if (ringCue != null) ringCue.SetActive(false);
        if (thumbsCue != null) thumbsCue.SetActive(false);
        if (thumbUpCue != null) thumbUpCue.SetActive(false);
        if (thumbDownCue != null) thumbDownCue.SetActive(false);
    }

    /// <summary>
    /// Handle user input (gesture confirmation/rejection)
    /// </summary>
    public void HandleUserInput()
    {
        // This can be expanded for more complex input handling
        // For now, just a placeholder
    }

    private void HandleGestureConfirm()
    {
        IsGestureConfirmed = true;
        IsGestureRejected = false;
        HideAllCues();
    }

    private void HandleGestureReject()
    {
        IsGestureConfirmed = false;
        IsGestureRejected = true;
        HideAllCues();
    }
} 