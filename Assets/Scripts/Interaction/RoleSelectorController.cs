using UnityEngine;
using Unity.Netcode;
using Oculus.Interaction;
using UnityEngine.Events;

public class RoleSelectorController : NetworkBehaviour
{
    [Header("Interactable Event Wrappers")]
    public InteractableUnityEventWrapper assignWrapper;
    public InteractableUnityEventWrapper clearWrapper;
    public InteractableUnityEventWrapper startWrapper;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner) return;
        var local = NetworkManager.Singleton.LocalClient;
        if (local != null && local.PlayerObject != null)
        {
            var net = local.PlayerObject.GetComponent<RoleNetwork>();
            if (net != null)
            {
                net.role.OnValueChanged += OnRoleChanged;
                // Initialize Start visibility based on current role
                if (startWrapper != null)
                    startWrapper.gameObject.SetActive(net.role.Value == Role.Recipient);
            }
        }

        if (assignWrapper != null) assignWrapper.WhenSelect.AddListener(OnAssignClicked);
        if (clearWrapper  != null) clearWrapper.WhenSelect.AddListener(OnClearClicked);
        if (startWrapper  != null) startWrapper.WhenSelect.AddListener(OnStartClicked);

        // By default, Start is hidden until Recipient
        if (startWrapper != null) startWrapper.gameObject.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (!IsOwner) return;
        var local = NetworkManager.Singleton.LocalClient;
        if (local != null && local.PlayerObject != null)
        {
            var net = local.PlayerObject.GetComponent<RoleNetwork>();
            if (net != null)
            {
                net.role.OnValueChanged -= OnRoleChanged;
            }
        }
    }

    void OnAssignClicked()
    {
        AssignRecipientServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void AssignRecipientServerRpc(ulong clientId)
    {
        // Reset all to Bystander
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var rig = client.PlayerObject.GetComponent<RoleNetwork>();
            if (rig != null) rig.role.Value = Role.Bystander;
        }
        // Set this client as Recipient
        var target = NetworkManager.Singleton.ConnectedClients[clientId];
        var targetRig = target.PlayerObject.GetComponent<RoleNetwork>();
        if (targetRig != null) targetRig.role.Value = Role.Recipient;
    }

    void OnClearClicked()
    {
        ClearRecipientServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void ClearRecipientServerRpc(ulong clientId)
    {
        var rig = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<RoleNetwork>();
        if (rig != null) rig.role.Value = Role.Bystander;
    }

    void OnStartClicked()
    {
        // Trigger session start (e.g. spawn drone)
        SessionManager.Instance.StartSession();
        if (startWrapper != null) startWrapper.gameObject.SetActive(false);
    }

    private void OnRoleChanged(Role previous, Role current)
    {
        if (!IsOwner) return;
        if (startWrapper != null)
            startWrapper.gameObject.SetActive(current == Role.Recipient);
    }
} 