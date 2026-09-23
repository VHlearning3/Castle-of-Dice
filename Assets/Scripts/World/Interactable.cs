using System;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Abstract base class for all interactive world elements (NPCs, loot chests, levers, doors, herbs).
    /// Handles mouse interaction, player proximity validation, and prompt messages.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Interaction Settings")]
        [Tooltip("Text displayed in the interaction prompt HUD (e.g., 'Talk to Baldur', 'Pick Lock', 'Pick Flower').")]
        [SerializeField] protected string promptMessage = "Interact";

        [Tooltip("Maximum distance in Unity world units the player must be within to trigger interaction.")]
        [SerializeField] protected float interactionRadius = 2.5f;

        [Tooltip("Whether this object is currently receptive to player interaction.")]
        [SerializeField] protected bool isInteractable = true;

        #endregion

        #region Public Properties

        /// <summary>Prompt text shown to the player.</summary>
        public string PromptMessage => promptMessage;

        /// <summary>Effective interaction distance threshold.</summary>
        public float InteractionRadius => interactionRadius;

        /// <summary>Active interaction availability.</summary>
        public bool IsInteractable
        {
            get => isInteractable;
            set => isInteractable = value;
        }

        #endregion

        #region Events

        /// <summary>Fired when interaction is successfully initiated.</summary>
        public event Action<PlayerUnit> OnInteracted;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col == null && GetComponentInChildren<Collider>() == null)
            {
                // Ensure there is at least a trigger/solid collider for raycast detection
                CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 1.0f, 0);
                capsule.radius = 0.6f;
                capsule.height = 2.0f;
                Debug.Log($"[Interactable] Auto-created CapsuleCollider on '{name}' to ensure raycast detectability.");
            }
        }

        #endregion

        #region Mouse Interaction

        protected virtual void OnMouseDown()
        {
            if (!isInteractable) return;
            if (!GameInput.GetLeftMouseButtonDown()) return;

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player == null)
            {
                Debug.LogWarning("[Interactable] No PlayerUnit found in scene to perform interaction.");
                return;
            }

            if (CanInteract(player))
            {
                TriggerInteraction(player);
            }
            else
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                Debug.LogWarning($"[Interactable] Too far away to interact with '{promptMessage}' ({distance:F1}m > {interactionRadius:F1}m). Move closer!");
            }
        }

        #endregion

        #region Interaction Methods

        /// <summary>
        /// Validates whether the player is close enough to interact with this object.
        /// </summary>
        public virtual bool CanInteract(PlayerUnit player)
        {
            if (player == null || !isInteractable) return false;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= interactionRadius;
        }

        /// <summary>
        /// Initiates the interaction flow for the specified player.
        /// </summary>
        public void TriggerInteraction(PlayerUnit player)
        {
            if (!CanInteract(player))
            {
                float dist = player != null ? Vector3.Distance(transform.position, player.transform.position) : -1f;
                Debug.LogWarning($"[Interactable] Cannot interact with '{promptMessage}': Out of range ({dist:F1}m > {interactionRadius:F1}m) or inactive.");
                return;
            }

            Debug.Log($"[Interactable] Triggered: '{promptMessage}' with {player.UnitName}");
            Interact(player);
            OnInteracted?.Invoke(player);
        }

        /// <summary>
        /// Core custom interaction logic implemented by derived interactables.
        /// </summary>
        public abstract void Interact(PlayerUnit player);

        #endregion

        #region Gizmos

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }

        #endregion
    }
}
