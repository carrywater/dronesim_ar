using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Synchronizes each player's role across the network.
/// </summary>
public class RoleNetwork : NetworkBehaviour
{
    /// <summary>
    /// The client's current role. Defaults to Bystander.
    /// </summary>
    public NetworkVariable<Role> role = new NetworkVariable<Role>(Role.Bystander);
} 