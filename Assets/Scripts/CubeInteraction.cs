using UnityEngine;
using Unity.Netcode;
using System.Linq;
using DroneSim;

namespace DroneSim
{
    /// <summary>
    /// Handles the interaction with the Recipient and Bystander cubes.
    /// When both cubes are picked up, it triggers the scenario selection.
    /// </summary>
    public class CubeInteraction : NetworkBehaviour
    {
        [Header("References")]
        public Renderer cubeRenderer; // To change the color of the cube
        
        [Header("Settings")]
        public string objectType; // "Recipient" or "Bystander"
        public Color pickedUpColor = Color.green;
        public Color defaultColor = Color.white;
        
        // Networked pickup state
        private NetworkVariable<bool> isPickedUp = new NetworkVariable<bool>(false, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Owner);

        // References to other components
        private ScenarioManager scenarioManager;
        private DroneSpawner droneSpawner;
        
        // Static references to track both cubes
        private static CubeInteraction recipientCube;
        private static CubeInteraction bystanderCube;

        private void Start()
        {
            // Ensure the cube has a renderer to change its color
            if (cubeRenderer == null) cubeRenderer = GetComponent<Renderer>();

            // Find the ScenarioManager in the scene
            scenarioManager = FindObjectOfType<ScenarioManager>();
            if (scenarioManager == null)
            {
                Debug.LogError("[CubeInteraction] ScenarioManager not found in scene!");
                return;
            }
            
            // Find the DroneSpawner in the scene
            droneSpawner = FindObjectOfType<DroneSpawner>();
            if (droneSpawner == null)
            {
                Debug.LogError("[CubeInteraction] DroneSpawner not found in scene!");
                return;
            }

            // Set up static references based on object type
            if (string.IsNullOrEmpty(objectType))
            {
                Debug.LogError($"[CubeInteraction] Object type not set for {gameObject.name}!");
                return;
            }

            if (objectType == "Recipient")
            {
                recipientCube = this;
            }
            else if (objectType == "Bystander")
            {
                bystanderCube = this;
            }
            else
            {
                Debug.LogError($"[CubeInteraction] Invalid object type '{objectType}' for {gameObject.name}!");
                return;
            }

            // Subscribe to network variable changes
            isPickedUp.OnValueChanged += OnPickupStateChanged;
            
            // Set initial color
            ChangeColor(defaultColor);
        }

        private void OnDestroy()
        {
            // Clean up static references
            if (recipientCube == this) recipientCube = null;
            if (bystanderCube == this) bystanderCube = null;

            // Unsubscribe from network variable changes
            isPickedUp.OnValueChanged -= OnPickupStateChanged;
        }

        // This function is called when the player interacts with the cube
        public void OnPickup()
        {
            if (!IsOwner) return; // Make sure only the object owner can pick it up

            // Update the networked picked-up status
            isPickedUp.Value = true;
        }

        // Server RPC to reset the pickup state
        [ServerRpc(RequireOwnership = false)]
        public void ResetPickupStateServerRpc()
        {
            isPickedUp.Value = false;
        }

        private void OnPickupStateChanged(bool previousValue, bool newValue)
        {
            // Change the color to indicate the object was picked up
            if (newValue)
            {
                ChangeColor(pickedUpColor);
                CheckIfBothPickedUp();
            }
            else
            {
                ChangeColor(defaultColor);
            }
        }

        private void ChangeColor(Color color)
        {
            if (cubeRenderer != null && cubeRenderer.material != null)
            {
                cubeRenderer.material.color = color;
            }
        }

        // Check if both the recipient and bystander have been picked up
        private void CheckIfBothPickedUp()
        {
            if (!IsServer || scenarioManager == null || droneSpawner == null) return;

            // Check if both static references are valid and picked up
            if (recipientCube != null && bystanderCube != null &&
                recipientCube.isPickedUp.Value && bystanderCube.isPickedUp.Value)
            {
                // Select a random scenario
                scenarioManager.SelectRandomScenario();
                
                // Spawn the drone
                droneSpawner.SpawnDrone();
            }
        }

        // Public method to check if this cube is currently picked up
        public bool IsCurrentlyPickedUp()
        {
            return isPickedUp.Value;
        }
    }
}
