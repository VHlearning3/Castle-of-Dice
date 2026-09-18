using System;
using UnityEngine;
using CastleOfTheD20.Combat;

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

        #region Mouse Interaction

        protected virtual void OnMouseDown()
        {
            if (!isInteractable) return;

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
                Debug.Log($"[Interactable] Too far away to interact ({distance:F1}m > {interactionRadius:F1}m). Move closer!");
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
            if (!CanInteract(player)) return;

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
