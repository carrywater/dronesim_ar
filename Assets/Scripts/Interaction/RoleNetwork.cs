using Unity.Netcode;
using UnityEngine;

public class RoleNetwork : NetworkBehaviour
{
    // Networked variable to store this client's role (default Bystander)
    public NetworkVariable<Role> role = new NetworkVariable<Role>(Role.Bystander);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Ensure default role on spawn
            role.Value = Role.Bystander;
        }
    }
} 