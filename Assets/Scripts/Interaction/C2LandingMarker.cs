using UnityEngine;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;

public class C2LandingMarker : MonoBehaviour
{
    [Header("References")]
    public PlaneReticleDataIcon reticleDataIcon; // Assign in Inspector
    public Transform landingMarker; // Assign in Inspector

    private DistanceHandGrabInteractable _interactable;

    private void Start()
    {
        _interactable = GetComponent<DistanceHandGrabInteractable>();
        if (_interactable != null)
        {
            _interactable.WhenPointerEventRaised += OnPointerEvent;
        }
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        // Check for select (grab/pinch) event
        if (evt.Type == PointerEventType.Select)
        {
            if (reticleDataIcon != null && landingMarker != null)
            {
                Vector3 markPosition = reticleDataIcon.transform.position;
                landingMarker.position = markPosition;
                Debug.Log($"[C2LandingMarker] Landing marker moved to: {markPosition}");
            }
        }
    }

    private void OnDestroy()
    {
        if (_interactable != null)
        {
            _interactable.WhenPointerEventRaised -= OnPointerEvent;
        }
    }
} 