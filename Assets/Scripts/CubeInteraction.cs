using UnityEngine;
using Unity.Netcode;
using DroneSim;
using Oculus.Interaction; // Meta Interaction SDK

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
        private NetworkVariable<bool> isPickedUp = new NetworkVariable<bool>(false);

        // Meta SDK Grabbable reference
        private Grabbable grabbable;

        private void Awake()
        {
            grabbable = GetComponent<Grabbable>();
        }

        private void Start()
        {
            if (cubeRenderer == null) cubeRenderer = GetComponent<Renderer>();
            if (cubeRenderer == null)
            {
                Debug.LogError($"[CubeInteraction] No renderer found on {gameObject.name}!");
                return;
            }
            ChangeColor(defaultColor);
            Debug.Log($"[CubeInteraction] {objectType} cube initialized with default color");
        }

        // This will be hooked up in the Inspector via UnityEvent
        public void OnGrabbed()
        {
            Debug.Log($"[CubeInteraction] {objectType} grabbed via Meta event!");

            if (!IsOwner)
            {
                Debug.Log($"[CubeInteraction] {objectType} grab ignored - not owner");
                return;
            }

            isPickedUp.Value = true;
            ChangeColor(pickedUpColor);
            Debug.Log($"[CubeInteraction] {objectType} color changed to {pickedUpColor}");
        }

        private void ChangeColor(Color color)
        {
            if (cubeRenderer != null && cubeRenderer.material != null)
            {
                cubeRenderer.material.color = color;
                Debug.Log($"[CubeInteraction] {objectType} cube color changed to {color}");
            }
            else
            {
                Debug.LogError($"[CubeInteraction] {objectType} cube renderer not found!");
            }
        }

        // Public method to check if this cube is currently picked up
        public bool IsCurrentlyPickedUp()
        {
            return isPickedUp.Value;
        }
    }
}
