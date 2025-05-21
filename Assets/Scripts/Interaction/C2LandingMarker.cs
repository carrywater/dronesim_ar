using UnityEngine;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;
using Utils;

public class C2LandingMarker : MonoBehaviour
{
    [Header("References")]
    public PlaneReticleDataIcon reticleDataIcon; // Assign in Inspector
    public TargetPositioner targetPositioner; // Assign in Inspector

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
            if (reticleDataIcon != null && targetPositioner != null)
            {
                Vector3 markPosition = reticleDataIcon.transform.position;
                targetPositioner.SetTargetPosition("c2_target", markPosition);
                Debug.Log($"[C2LandingMarker] Landing marker set via TargetPositioner: {markPosition}");
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