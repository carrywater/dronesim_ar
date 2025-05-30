using UnityEngine;
using Interaction;
public class ThumbCueLookAtHMD : MonoBehaviour
{
    [Header("Reference to the HMD (center eye)")]
    [SerializeField] private Transform _centerEye;

    private void Update()
    {
        if (_centerEye == null) return;

        // Direction from this object to the HMD
        Vector3 toViewer = _centerEye.position - transform.position;
        // Project onto XZ plane for yaw-only rotation
        toViewer.y = 0;
        if (toViewer.sqrMagnitude < 0.0001f) return;

        // Make +X point at the viewer (default LookRotation makes +Z point at target)
        Quaternion lookRot = Quaternion.LookRotation(toViewer, Vector3.up);
        // Rotate 90 degrees around Y to make +X point at the viewer
        transform.rotation = lookRot * Quaternion.Euler(0, -90, 0);
    }
} 