using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Aligns the drone's root transform so its Y=0 matches the real-world floor anchor detected by MRUK.
/// Attach this to your drone or a manager object and assign the drone root if needed.
/// </summary>
public class AlignDroneToFloor : MonoBehaviour
{
    [Tooltip("The drone root to offset (if not this object)")]
    public Transform droneRoot;

    private void Start()
    {
        // Wait for MRUK room to load
        if (MRUK.Instance != null)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(OnRoomLoaded);
        }
        else
        {
            Debug.LogError("MRUK.Instance is null! Make sure MRUK is in your scene.");
        }
    }

    private void OnRoomLoaded()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[AlignDroneToFloor] No current room found!");
            return;
        }
        var floorAnchor = room.FloorAnchor;
        if (floorAnchor != null)
        {
            Vector3 floorWorldPos = floorAnchor.transform.position;
            Debug.Log($"[AlignDroneToFloor] Floor anchor found at {floorWorldPos}");

            // Offset the drone so its Y=0 matches the floor anchor's Y
            var root = droneRoot != null ? droneRoot : transform;
            Vector3 dronePos = root.position;
            float yOffset = floorWorldPos.y - dronePos.y;
            root.position += new Vector3(0, yOffset, 0);

            Debug.Log($"[AlignDroneToFloor] Drone root offset by {yOffset} to align with real floor.");
            return;
        }

        Debug.LogWarning("[AlignDroneToFloor] No floor anchor found in current room!");
    }
} 