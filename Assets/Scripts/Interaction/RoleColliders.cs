using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

public class RoleColliders : MonoBehaviour
{
    [Tooltip("Collider representing the recipient target")]
    public Collider recipientCollider;
    [Tooltip("NavMeshObstacle representing the bystander obstacle")]
    public NavMeshObstacle bystanderObstacle;

    private RoleNetwork roleNetwork;

    private void Start()
    {
        // Try to find RoleNetwork on this GameObject or parent
        roleNetwork = GetComponent<RoleNetwork>();
        if (roleNetwork == null)
        {
            roleNetwork = GetComponentInParent<RoleNetwork>();
            if (roleNetwork != null)
                Debug.LogWarning($"[RoleColliders] RoleNetwork found on parent: {roleNetwork.gameObject.name}");
        }
        if (roleNetwork == null)
        {
            Debug.LogError($"[RoleColliders] RoleNetwork component not found on {gameObject.name} or its parents.");
            return;
        }
        // Subscribe to role changes
        roleNetwork.role.OnValueChanged += HandleRoleChanged;
        // Initialize colliders based on current role
        HandleRoleChanged(roleNetwork.role.Value, roleNetwork.role.Value);
    }

    private void OnDestroy()
    {
        if (roleNetwork != null)
            roleNetwork.role.OnValueChanged -= HandleRoleChanged;
    }

    private void HandleRoleChanged(Role previous, Role current)
    {
        bool isRecipient = current == Role.Recipient;
        bool isBystander = current == Role.Bystander;

        if (recipientCollider != null)
            recipientCollider.gameObject.SetActive(isRecipient);

        if (bystanderObstacle != null)
        {
            bystanderObstacle.carving = isBystander;
            bystanderObstacle.gameObject.SetActive(isBystander);
        }

        Debug.Log($"[RoleColliders] {gameObject.name} role changed to {current}. Recipient active: {isRecipient}, Bystander active: {isBystander}");
    }
} 