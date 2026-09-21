using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Player mouse interaction controller for 3D isometric exploration.
    /// Raycasts from the active camera on mouse click to detect Interactable objects (NPCs, chests, doors)
    /// and invokes TriggerInteraction when within valid interaction radius.
    /// Includes intelligent UI pass-through filtering to prevent full-screen HUD panels from swallowing clicks.
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
        [SerializeField] private float maxRaycastDistance = 150f;

        [Tooltip("Layers checked for interactable colliders (defaults to Everything if unassigned).")]
        [SerializeField] private LayerMask interactableLayers = ~0;

        [Header("UI Filtering")]
        [Tooltip("When enabled, clicks over non-interactive UI backgrounds (like HUD canvas panels) pass through to 3D interactables.")]
        [SerializeField] private bool passThroughNonInteractiveUI = true;

        [Header("Diagnostics")]
        [Tooltip("Enables verbose runtime logging for mouse raycasts, hits, and UI intercepts.")]
        [SerializeField] private bool debugLogging = true;

        #endregion

        #region Private State

        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureCamera();
            EnsurePlayer();
            ValidateLayerMask();

            if (debugLogging)
            {
                string camName = targetCamera != null ? targetCamera.name : "None";
                string pName = player != null ? player.name : "None";
                Debug.Log($"[PlayerInteractionRaycaster] Awake on '{name}'. Camera: '{camName}', Player: '{pName}', LayerMask: {interactableLayers.value}.");
            }
        }

        private void Start()
        {
            EnsureCamera();
            EnsurePlayer();
            ValidateLayerMask();
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

        #region Setup & Validation

        private Camera EnsureCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindAnyObjectByType<Camera>();
                }

                if (targetCamera == null && debugLogging)
                {
                    Debug.LogWarning("[PlayerInteractionRaycaster] No Camera found in scene! Mouse raycasting inactive until a Camera exists.");
                }
            }

            return targetCamera;
        }

        private PlayerUnit EnsurePlayer()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerUnit>();
                if (player == null && debugLogging)
                {
                    Debug.LogWarning("[PlayerInteractionRaycaster] No PlayerUnit found in scene to associate with interactions.");
                }
            }

            return player;
        }

        private void ValidateLayerMask()
        {
            if (interactableLayers.value == 0)
            {
                interactableLayers = ~0;
                Debug.LogWarning("[PlayerInteractionRaycaster] interactableLayers was set to Nothing (0); defaulting to Everything (~0).");
            }
        }

        #endregion

        #region Interaction Handling

        private void HandleMouseInteraction()
        {
            // Primary mouse button (Left click) via cross-compatible GameInput
            if (!GameInput.GetLeftMouseButtonDown()) return;

            // Check if mouse is over interactive UI elements
            if (IsPointerOverInteractiveUI())
            {
                return;
            }

            Camera cam = EnsureCamera();
            if (cam == null) return;

            PlayerUnit activePlayer = EnsurePlayer();
            if (activePlayer == null) return;

            Vector2 mousePos = GameInput.GetMousePosition();
            Ray ray = cam.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                Interactable interactable = hit.collider.GetComponentInParent<Interactable>();

                if (interactable != null)
                {
                    if (!interactable.IsInteractable)
                    {
                        if (debugLogging)
                        {
                            Debug.Log($"[PlayerInteractionRaycaster] '{interactable.name}' is currently not interactable.");
                        }
                        return;
                    }

                    if (interactable.CanInteract(activePlayer))
                    {
                        if (debugLogging)
                        {
                            Debug.Log($"[PlayerInteractionRaycaster] Interaction triggered on '{interactable.name}' ({interactable.PromptMessage}).");
                        }
                        interactable.TriggerInteraction(activePlayer);
                    }
                    else
                    {
                        float dist = Vector3.Distance(activePlayer.transform.position, interactable.transform.position);
                        Debug.LogWarning($"[PlayerInteractionRaycaster] Out of range! Cannot interact with '{interactable.name}' ({interactable.PromptMessage}). Distance: {dist:F1}m > Radius: {interactable.InteractionRadius:F1}m. Walk closer!");
                    }
                }
                else
                {
                    if (debugLogging)
                    {
                        Debug.Log($"[PlayerInteractionRaycaster] Clicked '{hit.collider.name}' (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}), but no Interactable component was found.");
                    }
                }
            }
            else
            {
                if (debugLogging)
                {
                    Debug.Log($"[PlayerInteractionRaycaster] Raycast from {mousePos} hit no 3D colliders within {maxRaycastDistance:F0}m.");
                }
            }
        }

        /// <summary>
        /// Determines whether the mouse cursor is hovering over an interactive UI element
        /// (Buttons, Sliders, InputFields, Scrollbars, or active Dialogue/Shop modal windows).
        /// Non-interactive elements (such as full-screen transparent Canvas roots) are ignored if pass-through is enabled.
        /// </summary>
        private bool IsPointerOverInteractiveUI()
        {
            if (EventSystem.current == null) return false;

            if (!EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            if (!passThroughNonInteractiveUI)
            {
                if (debugLogging)
                {
                    Debug.Log("[PlayerInteractionRaycaster] Click blocked: Pointer is over UI GameObject (pass-through disabled).");
                }
                return true;
            }

            // Inspect UI elements under pointer via EventSystem raycast
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = GameInput.GetMousePosition()
            };

            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                GameObject uiObj = uiRaycastResults[i].gameObject;
                if (uiObj == null) continue;

                // Interactive UI Controls
                if (uiObj.GetComponentInParent<Selectable>() != null ||
                    uiObj.GetComponentInParent<Button>() != null ||
                    uiObj.GetComponentInParent<Slider>() != null ||
                    uiObj.GetComponentInParent<TMP_InputField>() != null ||
                    uiObj.GetComponentInParent<ScrollRect>() != null)
                {
                    if (debugLogging)
                    {
                        Debug.Log($"[PlayerInteractionRaycaster] Click intercepted by interactive UI: '{uiObj.name}'.");
                    }
                    return true;
                }

                // Modal dialog or shop panels
                string lowerName = uiObj.name.ToLowerInvariant();
                if (lowerName.Contains("dialogue") || lowerName.Contains("shop") || lowerName.Contains("popup") || lowerName.Contains("modal"))
                {
                    if (debugLogging)
                    {
                        Debug.Log($"[PlayerInteractionRaycaster] Click intercepted by modal UI window: '{uiObj.name}'.");
                    }
                    return true;
                }
            }

            // Click hit only passive background images or transparent Canvas panels; allow 3D raycast
            return false;
        }

        #endregion
    }
}
