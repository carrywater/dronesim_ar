using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Visualization;
using Interaction;

/// <summary>
/// Manages AR cues and user interaction logic in a scenario-agnostic way.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Cue References")]
    [Tooltip("Register all cues by name here (e.g., 'Thumbs', 'Ring', 'Marker', etc.)")]
    [SerializeField] private List<GameObject> _cues;

    [Header("Interaction Handlers")]
    [Tooltip("Register all interaction handlers by type (e.g., 'point', 'confirm', etc.)")]
    [SerializeField] private List<MonoBehaviour> _interactionHandlers; // e.g., PointGestureHandler, ConfirmGestureHandler

    [Header("Target Positioner Reference")]
    [Tooltip("Reference to the TargetPositioner for updating the active target position")]
    [SerializeField] private TargetPositioner _targetPositioner;

    private Dictionary<string, GameObject> _cueDict;
    private Dictionary<string, MonoBehaviour> _handlerDict;

    public bool IsInteractionComplete { get; private set; }
    public event Action OnInteractionComplete;
    public event Action<bool> OnConfirmInteractionResult; // true = confirm, false = reject
    public Vector3 LastPickedPosition { get; private set; }

    private void Awake()
    {
        _cueDict = new Dictionary<string, GameObject>();
        foreach (var cue in _cues)
        {
            if (cue != null)
                _cueDict[cue.name] = cue;
        }
        _handlerDict = new Dictionary<string, MonoBehaviour>();
        foreach (var handler in _interactionHandlers)
        {
            if (handler != null)
                _handlerDict[handler.GetType().Name.ToLower()] = handler;
        }
        HideAllCues();
    }

    /// <summary>
    /// Show a cue by name (e.g., 'Thumbs', 'Ring', 'Marker')
    /// </summary>
    public void ShowCue(string cueName)
    {
        Debug.Log($"[InteractionManager] ShowCue called for: {cueName}");
        if (_cueDict.TryGetValue(cueName, out var cue))
        {
            cue.SetActive(true);
            Debug.Log($"[InteractionManager] {cueName} set active: {cue.activeSelf}");
        }
        else
        {
            Debug.LogWarning($"[InteractionManager] Cue not found: {cueName}");
        }
    }

    /// <summary>
    /// Hide a cue by name
    /// </summary>
    public void HideCue(string cueName)
    {
        if (_cueDict.TryGetValue(cueName, out var cue))
            cue.SetActive(false);
    }

    /// <summary>
    /// Hide all cues
    /// </summary>
    public void HideAllCues()
    {
        foreach (var cue in _cueDict.Values)
            cue.SetActive(false);
    }

    /// <summary>
    /// Start an interaction by type (e.g., 'point', 'confirm')
    /// </summary>
    public void StartInteraction(string interactionType)
    {
        Debug.Log($"[InteractionManager] StartInteraction called for type: {interactionType}");
        IsInteractionComplete = false;
        interactionType = interactionType.ToLower();
        if (_handlerDict.TryGetValue(interactionType, out var handler))
        {
            if (handler is PointGestureHandler pointHandler)
            {
                Debug.Log("[InteractionManager] Enabling PointGestureHandler");
                pointHandler.EnableInteraction();
                pointHandler.OnTargetPlaced += (pos) =>
                {
                    LastPickedPosition = pos;
                    if (_targetPositioner != null)
                    {
                        _targetPositioner.SetActiveTargetPosition(pos);
                        Debug.Log($"[InteractionManager] Set active target position to {pos}");
                    }
                };
            }
            else if (handler is ConfirmGestureHandler confirmHandler)
            {
                confirmHandler.SetActive(true);
                confirmHandler.OnThumbsUp -= HandleConfirm;
                confirmHandler.OnThumbsDown -= HandleReject;
                confirmHandler.OnThumbsUp += HandleConfirm;
                confirmHandler.OnThumbsDown += HandleReject;
            }
            // Add more handler types as needed
        }
        else
        {
            Debug.LogWarning($"[InteractionManager] No handler found for type: {interactionType}");
        }
    }

    /// <summary>
    /// Stop an interaction by type (optional)
    /// </summary>
    public void StopInteraction(string interactionType)
    {
        Debug.Log($"[InteractionManager] StopInteraction called for type: {interactionType}");
        interactionType = interactionType.ToLower();
        if (_handlerDict.TryGetValue(interactionType, out var handler))
        {
            if (handler is PointGestureHandler pointHandler)
            {
                Debug.Log("[InteractionManager] Disabling PointGestureHandler");
                pointHandler.DisableInteraction();
            }
            else if (handler is ConfirmGestureHandler confirmHandler)
            {
                confirmHandler.SetActive(false);
                confirmHandler.OnThumbsUp -= HandleConfirm;
                confirmHandler.OnThumbsDown -= HandleReject;
            }
            // Add more handler types as needed
        }
        else
        {
            Debug.LogWarning($"[InteractionManager] No handler found for type: {interactionType}");
        }
    }

    private void HandleConfirm()
    {
        Debug.Log("Confirm interaction handled");
        OnConfirmInteractionResult?.Invoke(true);
        CompleteInteraction();
    }

    private void HandleReject()
    {
        Debug.Log("Reject interaction handled");
        OnConfirmInteractionResult?.Invoke(false);
        CompleteInteraction();
    }

    private void CompleteInteraction()
    {
        Debug.Log("[InteractionManager] CompleteInteraction called");
        IsInteractionComplete = true;
        OnInteractionComplete?.Invoke();
        Debug.Log("[InteractionManager] OnInteractionComplete event invoked");
    }
} 