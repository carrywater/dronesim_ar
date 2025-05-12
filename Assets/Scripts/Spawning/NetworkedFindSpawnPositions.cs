using UnityEngine;
using Unity.Netcode;
using Meta.XR.MRUtilityKit;
using Meta.XR.Util;

/// <summary>
/// Spatially places and network-spawns a prefab into MR Utility Kit rooms when the scene loads.
/// Only the server performs the spatial instantiation and then registers the objects with Netcode.
/// </summary>
public class NetworkedFindSpawnPositions : MonoBehaviour
{
    [Tooltip("Which rooms to spawn in when scene data is loaded.")]
    public MRUK.RoomFilter SpawnOnStart = MRUK.RoomFilter.CurrentRoomOnly;

    [SerializeField, Tooltip("Network prefab to spawn. Must have a NetworkObject component.")]
    public GameObject networkPrefab;

    [SerializeField, Tooltip("Number of instances to place per room.")]
    public int SpawnAmount = 1;

    [SerializeField, Tooltip("Maximum attempts to find a valid position per instance.")]
    public int MaxIterations = 1000;

    [SerializeField, Tooltip("Spawn type: floating or surface-based.")]
    public FindSpawnPositions.SpawnLocation SpawnLocation = FindSpawnPositions.SpawnLocation.Floating;

    [SerializeField, Tooltip("Which anchor labels to use when spawning on surfaces.")]
    public MRUKAnchor.SceneLabels Labels = ~(MRUKAnchor.SceneLabels)0;

    [SerializeField, Tooltip("Enable overlap checking before placement.")]
    public bool CheckOverlaps = true;

    [SerializeField, Tooltip("Override required free space; negative auto-detects.")]
    public float OverrideBounds = -1f;

    [SerializeField, Tooltip("Physics layers to avoid when checking overlaps.")]
    public LayerMask LayerMask = -1;

    [SerializeField, Tooltip("Clearance distance from surfaces when using surface spawn.")]
    public float SurfaceClearanceDistance = 0.1f;

    private void Start()
    {
        if (MRUK.Instance && SpawnOnStart != MRUK.RoomFilter.None)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(() =>
            {
                if (!IsServer)
                    return;
                switch (SpawnOnStart)
                {
                    case MRUK.RoomFilter.AllRooms:
                        SpawnAllRooms();
                        break;
                    case MRUK.RoomFilter.CurrentRoomOnly:
                        SpawnRoom(MRUK.Instance.GetCurrentRoom());
                        break;
                }
            });
        }
    }

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    /// <summary>Spawn instances in all available rooms.</summary>
    public void SpawnAllRooms()
    {
        foreach (var room in MRUK.Instance.Rooms)
        {
            SpawnRoom(room);
        }
    }

    /// <summary>Spawn instances in a specific room.</summary>
    public void SpawnRoom(MRUKRoom room)
    {
        if (!IsServer || networkPrefab == null)
            return;

        // Determine bounds for overlap checks
        var prefabBounds = Utilities.GetPrefabBounds(networkPrefab);
        float minRadius = 0f;
        Vector3 centerOffset = Vector3.zero;
        Bounds adjustedBounds = new Bounds();

        if (prefabBounds.HasValue)
        {
            var b = prefabBounds.Value;
            minRadius = Mathf.Min(-b.min.x, -b.min.z, b.max.x, b.max.z);
            if (minRadius < 0f) minRadius = 0f;

            var min = b.min;
            var max = b.max;
            min.y += SurfaceClearanceDistance;
            if (max.y < min.y) max.y = min.y;
            adjustedBounds.SetMinMax(min, max);

            centerOffset = new Vector3(0f, b.center.y, 0f);
            if (OverrideBounds > 0f)
            {
                float ext = OverrideBounds;
                adjustedBounds = new Bounds(Vector3.up * SurfaceClearanceDistance, Vector3.one * ext * 2f);
            }
        }

        // Spawn each instance
        for (int i = 0; i < SpawnAmount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < MaxIterations; attempt++)
            {
                Vector3 spawnPosition = Vector3.zero;
                Vector3 spawnNormal = Vector3.up;

                // Floating vs surface logic
                if (SpawnLocation == FindSpawnPositions.SpawnLocation.Floating)
                {
                    var posOpt = room.GenerateRandomPositionInRoom(minRadius, true);
                    if (!posOpt.HasValue) break;
                    spawnPosition = posOpt.Value;
                }
                else
                {
                    // Configure surface types
                    MRUK.SurfaceType surfaceMask = (MRUK.SurfaceType)0;
                    switch (SpawnLocation)
                    {
                        case FindSpawnPositions.SpawnLocation.AnySurface:
                            surfaceMask = MRUK.SurfaceType.FACING_UP | MRUK.SurfaceType.VERTICAL | MRUK.SurfaceType.FACING_DOWN;
                            break;
                        case FindSpawnPositions.SpawnLocation.VerticalSurfaces:
                            surfaceMask = MRUK.SurfaceType.VERTICAL;
                            break;
                        case FindSpawnPositions.SpawnLocation.OnTopOfSurfaces:
                            surfaceMask = MRUK.SurfaceType.FACING_UP;
                            break;
                        case FindSpawnPositions.SpawnLocation.HangingDown:
                            surfaceMask = MRUK.SurfaceType.FACING_DOWN;
                            break;
                    }
                    if (room.GenerateRandomPositionOnSurface(surfaceMask, minRadius, new LabelFilter(Labels), out var pos, out var normal))
                    {
                        spawnPosition = pos + Vector3.up * centerOffset.y;
                        spawnNormal = normal;
                    }
                    else
                    {
                        continue;
                    }
                }

                var spawnRotation = Quaternion.FromToRotation(Vector3.up, spawnNormal);

                // Overlap check
                if (CheckOverlaps && prefabBounds.HasValue)
                {
                    if (Physics.CheckBox(spawnPosition + spawnRotation * adjustedBounds.center, adjustedBounds.extents, spawnRotation, LayerMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                }

                // Instantiate and network-spawn
                var instance = Instantiate(networkPrefab, spawnPosition, spawnRotation);
                var netObj = instance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                    Debug.Log($"[NetworkedFindSpawnPositions] Spawned '{networkPrefab.name}' at {spawnPosition}");
                }
                else
                {
                    Debug.LogError("NetworkedFindSpawnPositions: networkPrefab lacks NetworkObject component.");
                }

                placed = true;
                break;
            }
            if (!placed)
            {
                Debug.LogWarning($"[NetworkedFindSpawnPositions] Failed to place '{networkPrefab.name}' after {MaxIterations} attempts in room.");
            }
        }
    }
} 