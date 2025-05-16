using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simplified startup controller for role assignment and drone spawning.
/// All button events are wired in the Inspector.
/// </summary>
public class RoleSelectorController : NetworkBehaviour
{
    [Header("UI Buttons")] 
    public GameObject assignButton;
    public GameObject clearButton;
    public GameObject startButton;

    private void Awake()
    {
        // Initial UI state: only Assign & Clear visible
        if (assignButton != null) assignButton.SetActive(true);
        if (clearButton  != null) clearButton.SetActive(true);
        if (startButton  != null) startButton.SetActive(false);
    }

    /// <summary>
    /// Called by the Assign button. Assigns this client as Recipient.
    /// </summary>
    public void OnAssignClicked()
    {
        Debug.Log($"[RoleSelectorController] OnAssignClicked called on client {NetworkManager.Singleton.LocalClientId}");
        AssignRecipientServerRpc(NetworkManager.Singleton.LocalClientId);
        if (startButton != null) startButton.SetActive(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AssignRecipientServerRpc(ulong clientId)
    {
        Debug.Log($"[RoleSelectorController] AssignRecipientServerRpc invoked on server for client {clientId}");
        // Reset all clients to Bystander
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var rn = client.PlayerObject.GetComponent<RoleNetwork>();
            if (rn != null) rn.role.Value = Role.Bystander;
        }
        // Set this client to Recipient
        var target = NetworkManager.Singleton.ConnectedClients[clientId]
                             .PlayerObject.GetComponent<RoleNetwork>();
        if (target != null) target.role.Value = Role.Recipient;
    }

    /// <summary>
    /// Called by the Clear button. Clears role back to Bystander.
    /// </summary>
    public void OnClearClicked()
    {
        ClearRecipientServerRpc(NetworkManager.Singleton.LocalClientId);
        if (startButton != null) startButton.SetActive(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ClearRecipientServerRpc(ulong clientId)
    {
        var rn = NetworkManager.Singleton.ConnectedClients[clientId]
                        .PlayerObject.GetComponent<RoleNetwork>();
        if (rn != null) rn.role.Value = Role.Bystander;
    }

    /// <summary>
    /// Called by the Start button. Spawns the drone and hides all UI.
    /// </summary>
    public void OnStartClicked()
    {
        // Hide UI; spawn logic handled via UnityEvent in the Inspector.
        if (assignButton != null) assignButton.SetActive(false);
        if (clearButton  != null) clearButton.SetActive(false);
        if (startButton  != null) startButton.SetActive(false);
    }
} 