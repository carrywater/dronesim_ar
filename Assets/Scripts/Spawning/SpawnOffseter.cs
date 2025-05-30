using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Offsets the drone vertically as soon as it is network-spawned.
/// Attach this to your Drone prefab alongside NetworkObject.
/// </summary>
public class DroneSpawnOffsetter : NetworkBehaviour
{
    [Tooltip("Local offset (X,Y,Z) applied on network spawn.")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 5f, 0f);

    public override void OnNetworkSpawn()
    {
        // Move the drone by the configured offset
        transform.position += _spawnOffset;
    }
} 