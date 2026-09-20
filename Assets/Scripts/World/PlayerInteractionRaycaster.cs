using UnityEngine;
using UnityEngine.EventSystems;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Player mouse interaction controller for 3D isometric exploration.
    /// Raycasts from the main camera on mouse click to detect Interactable objects (NPCs, chests, doors)
    /// and invokes TriggerInteraction when within valid interaction radius.
    /// </summary>
    public class PlayerInteractionRaycaster : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Targeting & Camera")]
        [Tooltip("Camera used for mouse screen-to-world raycasting (defaults to Camera.main).")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("The player character instance performing interactions.")]
        [SerializeField] private PlayerUnit player;

        [Header("Raycast Settings")]
        [Tooltip("Maximum raycast distance from the camera into the world.")]
        [SerializeField] private float maxRaycastDistance = 100f;

        [Tooltip("Layers checked for interactable colliders.")]
        [SerializeField] private LayerMask interactableLayers = ~0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerUnit>();
            }
        }

        private void Update()
        {
            // Only perform exploration raycasting while in free exploration mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration)
            {
                return;
            }

            HandleMouseInteraction();
        }

        #endregion

        #region Interaction Handling

        private void HandleMouseInteraction()
        {
            // Primary mouse button (Left click)
            if (!Input.GetMouseButtonDown(0)) return;

            // Prevent interaction through open UI panels or canvas elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerUnit>();
                if (player == null) return;
            }

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, interactableLayers))
            {
                Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null && interactable.IsInteractable)
                {
                    if (interactable.CanInteract(player))
                    {
                        interactable.TriggerInteraction(player);
                    }
                    else
                    {
                        float dist = Vector3.Distance(player.transform.position, interactable.transform.position);
                        Debug.Log($"[PlayerInteractionRaycaster] Cannot interact with '{interactable.PromptMessage}': Too far away ({dist:F1}m > {interactable.InteractionRadius:F1}m).");
                    }
                }
            }
        }

        #endregion
    }
}
