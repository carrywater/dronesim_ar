using UnityEngine;
using Unity.Netcode;
using Meta.XR.MRUtilityKit;

[RequireComponent(typeof(FindSpawnPositions))]
public class RoleSelectionSpawner : NetworkBehaviour
{
    [Header("Role Selection UI")]
    public GameObject roleSelectionPanelPrefab;

    private FindSpawnPositions finder;
    private GameObject spawnedPanel;

    void Awake()
    {
        finder = GetComponent<FindSpawnPositions>();
        if (finder != null)
        {
            // Spawn exactly one panel at the table anchor
            finder.SpawnAmount = 1;
            finder.SpawnObject = roleSelectionPanelPrefab;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        // Panel instantiation is handled by FindSpawnPositions on MRUK room ready
    }

    void Update()
    {
        if (!IsServer || spawnedPanel != null) return;

        // Look for the spawned panel as a child
        foreach (Transform child in transform)
        {
            if (child.gameObject.CompareTag("RoleSelectionPanel"))
            {
                spawnedPanel = child.gameObject;
                break;
            }
        }
    }
} 