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
        public string PromptMessage
        {
            get => promptMessage;
            set => promptMessage = value;
        }

        /// <summary>Effective interaction distance threshold.</summary>
        public float InteractionRadius
        {
            get => interactionRadius;
            set => interactionRadius = value;
        }

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

        private static PlayerUnit s_cachedPlayer;
        private static int s_clickClaimedFrame = -1;

        /// <summary>
        /// One click reaches both <see cref="OnMouseDown"/> and PlayerInteractionRaycaster; whichever comes first
        /// claims it, so the object is used (or walked to) once. Returns false when the click was already handled.
        /// </summary>
        public static bool ClaimClick()
        {
            if (s_clickClaimedFrame == Time.frameCount) return false;
            s_clickClaimedFrame = Time.frameCount;
            return true;
        }

        protected virtual void OnMouseDown()
        {
            if (!isInteractable) return;
            if (!GameInput.GetLeftMouseButtonDown()) return;

            // Shared cache: one scene search per hero instance instead of one per click on every interactable
            if (s_cachedPlayer == null)
            {
                s_cachedPlayer = FindAnyObjectByType<PlayerUnit>();
            }
            PlayerUnit player = s_cachedPlayer;
            if (player == null)
            {
                Debug.LogWarning("[Interactable] No PlayerUnit found in scene to perform interaction.");
                return;
            }

            if (!ClaimClick()) return;

            if (CanInteract(player))
            {
                TriggerInteraction(player);
            }
            else
            {
                // Click-to-move: walk up to it and use it on arrival
                PlayerExplorationMovement movement = player.GetComponent<PlayerExplorationMovement>();
                if (movement != null && movement.WalkToInteract(this)) return;

                float distance = DistanceTo(player.transform.position);
                Debug.Log($"[Interactable] Too far away to interact with '{promptMessage}' ({distance:F1}m > {interactionRadius:F1}m). Move closer!");
                ShowTooFarHint();
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
            return DistanceTo(player.transform.position) <= interactionRadius;
        }

        /// <summary>
        /// Ground distance from <paramref name="point"/> to this object: to the nearest side of its own collider
        /// when it has one, else to its pivot. Height is ignored, so a tall gate whose pivot sits halfway up
        /// (or a wide one the hero stands beside) is in reach from where the hero can actually stand.
        /// </summary>
        public float DistanceTo(Vector3 point)
        {
            Vector3 target = transform.position;
            Collider col = GetComponent<Collider>();
            if (col != null && col.enabled && col.gameObject.activeInHierarchy && !(col is MeshCollider mesh && !mesh.convex))
            {
                Vector3 probe = point;
                probe.y = col.bounds.center.y;
                target = col.ClosestPoint(probe);
            }
            float dx = target.x - point.x, dz = target.z - point.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Popup shown when the hero clicks something out of reach.</summary>
        public const string TooFarMessage = "Too far away. Walk closer.";

        private static float s_lastTooFarHint = -10f;

        /// <summary>Tells the player to walk closer (once per click, even when two click handlers see it).</summary>
        public static void ShowTooFarHint()
        {
            if (Time.unscaledTime - s_lastTooFarHint < 0.3f) return;
            s_lastTooFarHint = Time.unscaledTime;
            CastleOfTheD20.UI.LockpickMinigameUI.ShowToast(TooFarMessage);
        }

        /// <summary>
        /// Initiates the interaction flow for the specified player.
        /// </summary>
        public void TriggerInteraction(PlayerUnit player)
        {
            if (!CanInteract(player))
            {
                float dist = player != null ? DistanceTo(player.transform.position) : -1f;
                Debug.Log($"[Interactable] Cannot interact with '{promptMessage}': Out of range ({dist:F1}m > {interactionRadius:F1}m) or inactive.");
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
