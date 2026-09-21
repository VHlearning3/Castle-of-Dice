using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Real-time 3D exploration character controller for Castle of the D20.
    /// Handles WASD / arrow key locomotion oriented relative to the main camera,
    /// smooth rotational steering, gravity and slope grounding via CharacterController.
    /// Yields control and automatically snaps to the nearest grid tile when transitioning into turn-based combat.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerExplorationMovement : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Movement Settings")]
        [Tooltip("Forward and lateral movement speed during free exploration in units per second.")]
        [SerializeField] private float moveSpeed = 5.5f;

        [Tooltip("Turning and orientation smoothing speed in degrees/radians per second.")]
        [SerializeField] private float rotationSpeed = 14.0f;

        [Header("Gravity & Physics")]
        [Tooltip("Downward gravitational acceleration applied when not grounded.")]
        [SerializeField] private float gravity = -20.0f;

        [Tooltip("Slight downward velocity applied while grounded to stick firmly to terrain slopes.")]
        [SerializeField] private float groundedStickForce = -2.0f;

        [Header("Camera Alignment")]
        [Tooltip("Camera used to calculate horizontal movement vectors. Defaults to Camera.main.")]
        [SerializeField] private Camera explorationCamera;

        [Header("Combat Transition")]
        [Tooltip("Automatically snap to the nearest tactical GridTile when entering Combat mode.")]
        [SerializeField] private bool autoSnapOnCombat = true;

        [Header("Diagnostics")]
        [Tooltip("Enables verbose runtime diagnostic logging for input and component states.")]
        [SerializeField] private bool debugLogging = true;

        #endregion

        #region Private State

        private CharacterController characterController;
        private PlayerUnit playerUnit;
        private float verticalVelocity = 0f;
        private bool hasLoggedMissingCamera = false;
        private float lastBlockedInputLogTime = -10f;

        #endregion

        #region Public Properties

        /// <summary>Whether the player is currently actively moving via input.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Current normalized world-space horizontal movement direction.</summary>
        public Vector3 CurrentMoveDirection { get; private set; } = Vector3.zero;

        /// <summary>Current movement speed setting.</summary>
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                Debug.LogWarning($"[PlayerExplorationMovement] CharacterController missing on {name}; automatically attached.");
            }

            // Ensure CharacterController is enabled and configured with valid dimensions
            if (!characterController.enabled)
            {
                characterController.enabled = true;
                Debug.Log($"[PlayerExplorationMovement] Explicitly enabled CharacterController on {name}.");
            }

            if (characterController.height < 0.2f)
            {
                characterController.height = 2.0f;
                characterController.center = new Vector3(0, 1.0f, 0);
            }

            playerUnit = GetComponent<PlayerUnit>();
            EnsureCameraReference();

            if (debugLogging)
            {
                string camName = explorationCamera != null ? explorationCamera.name : "None";
                Debug.Log($"[PlayerExplorationMovement] Awake on '{name}'. CC Enabled: {characterController.enabled}, Camera: '{camName}', MoveSpeed: {moveSpeed}.");
            }
        }

        private void Start()
        {
            EnsureCameraReference();

            if (!characterController.enabled)
            {
                characterController.enabled = true;
                Debug.LogWarning($"[PlayerExplorationMovement] CharacterController was disabled at Start on {name}; re-enabled.");
            }

            if (debugLogging)
            {
                GamePlayMode currentMode = GameManager.Instance != null ? GameManager.Instance.CurrentMode : GamePlayMode.Exploration;
                Debug.Log($"[PlayerExplorationMovement] Start: Active GamePlayMode is '{currentMode}'. Ready for WASD movement.");
            }
        }

        private void OnEnable()
        {
            GameManager.OnPlayModeChanged += HandlePlayModeChanged;
            TurnManager.OnTurnStateChanged += HandleTurnStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnPlayModeChanged -= HandlePlayModeChanged;
            TurnManager.OnTurnStateChanged -= HandleTurnStateChanged;
        }

        private void Update()
        {
            // Verify CharacterController remains enabled
            if (characterController != null && !characterController.enabled)
            {
                // Only re-enable if we are not actively in turn-based combat
                if (GameManager.Instance == null || GameManager.Instance.CurrentMode == GamePlayMode.Exploration)
                {
                    characterController.enabled = true;
                }
            }

            // Mode Guard: Movement is only active in Exploration mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration)
            {
                // If the player attempts to move during Combat or Dialogue, log diagnostic warning
                Vector3 attemptedInput = PollRawInput();
                if (attemptedInput.sqrMagnitude > 0.001f && (Time.time - lastBlockedInputLogTime > 2.0f))
                {
                    lastBlockedInputLogTime = Time.time;
                    Debug.LogWarning($"[PlayerExplorationMovement] WASD input ignored: CurrentMode is '{GameManager.Instance.CurrentMode}' (Exploration required).");
                }

                if (IsMoving)
                {
                    IsMoving = false;
                    CurrentMoveDirection = Vector3.zero;
                    if (debugLogging) Debug.Log("[PlayerExplorationMovement] Locomotion halted due to mode transition.");
                }

                ApplyGravityOnly();
                return;
            }

            HandleLocomotion();
        }

        #endregion

        #region Camera Management

        private Camera EnsureCameraReference()
        {
            if (explorationCamera == null)
            {
                explorationCamera = Camera.main;

                if (explorationCamera == null)
                {
                    explorationCamera = FindAnyObjectByType<Camera>();
                }

                if (explorationCamera == null && !hasLoggedMissingCamera)
                {
                    hasLoggedMissingCamera = true;
                    Debug.LogError("[PlayerExplorationMovement] No Camera found in scene! Movement direction will default to world coordinates.");
                }
            }

            return explorationCamera;
        }

        #endregion

        #region Locomotion & Steering

        /// <summary>
        /// Reads player input through cross-compatible GameInput bridge
        /// (supporting both New Input System and Legacy Input Manager).
        /// </summary>
        private Vector3 PollRawInput()
        {
            Vector2 move2D = GameInput.GetMovementVector();
            return new Vector3(move2D.x, 0f, move2D.y);
        }

        private void HandleLocomotion()
        {
            Vector3 inputDirection = PollRawInput();
            Camera cam = EnsureCameraReference();
            Vector3 moveDirection = Vector3.zero;

            bool wasMoving = IsMoving;

            if (inputDirection.sqrMagnitude > 0.001f)
            {
                // Project camera orientation onto horizontal XZ plane
                Vector3 camForward = cam != null ? cam.transform.forward : Vector3.forward;
                Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;

                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                moveDirection = (camForward * inputDirection.z + camRight * inputDirection.x).normalized;
                CurrentMoveDirection = moveDirection;

                // Smooth rotational steering towards movement vector
                if (moveDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                IsMoving = true;

                if (!wasMoving && debugLogging)
                {
                    Debug.Log($"[PlayerExplorationMovement] Locomotion started (Input: {inputDirection}).");
                }
            }
            else
            {
                IsMoving = false;
                CurrentMoveDirection = Vector3.zero;

                if (wasMoving && debugLogging)
                {
                    Debug.Log("[PlayerExplorationMovement] Locomotion stopped.");
                }
            }

            // Gravity & Vertical Ground Sticking
            if (characterController != null && characterController.enabled)
            {
                if (characterController.isGrounded)
                {
                    if (verticalVelocity < 0f)
                    {
                        verticalVelocity = groundedStickForce;
                    }
                }
                else
                {
                    verticalVelocity += gravity * Time.deltaTime;
                }

                Vector3 motion = moveDirection * moveSpeed + Vector3.up * verticalVelocity;
                characterController.Move(motion * Time.deltaTime);
            }
        }

        private void ApplyGravityOnly()
        {
            if (characterController == null || !characterController.enabled) return;

            if (characterController.isGrounded)
            {
                verticalVelocity = groundedStickForce;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            characterController.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        }

        #endregion

        #region Grid Snapping on Combat Transition

        /// <summary>
        /// Snaps the player smoothly to the nearest GridTile coordinate and updates PlayerUnit spatial registration.
        /// </summary>
        public void SnapToNearestGridTile()
        {
            if (GridManager.Instance == null) return;

            Vector2Int gridCoord = GridManager.Instance.GetGridPosition(transform.position);
            GridTile targetTile = GridManager.Instance.GetTileAt(gridCoord);

            if (targetTile == null)
            {
                // Search nearby valid tiles if outside exact boundary
                float closestDist = float.MaxValue;
                foreach (var kvp in GridManager.Instance.Tiles)
                {
                    if (kvp.Value != null && kvp.Value.IsWalkable && !kvp.Value.IsOccupied)
                    {
                        float d = Vector3.Distance(transform.position, kvp.Value.transform.position);
                        if (d < closestDist)
                        {
                            closestDist = d;
                            targetTile = kvp.Value;
                            gridCoord = kvp.Key;
                        }
                    }
                }
            }

            if (targetTile != null)
            {
                // Safely disable CharacterController during coordinate repositioning
                bool wasEnabled = characterController != null && characterController.enabled;
                if (characterController != null) characterController.enabled = false;

                try
                {
                    if (playerUnit != null)
                    {
                        playerUnit.MoveToTile(targetTile);
                    }
                    else
                    {
                        transform.position = GridManager.Instance.GetWorldPosition(gridCoord);
                    }
                }
                finally
                {
                    if (characterController != null) characterController.enabled = wasEnabled;
                }

                verticalVelocity = 0f;
                Debug.Log($"[PlayerExplorationMovement] Combat transition: Snapped {name} to GridTile ({gridCoord.x}, {gridCoord.y}).");
            }
        }

        private void HandlePlayModeChanged(GamePlayMode mode)
        {
            if (debugLogging)
            {
                Debug.Log($"[PlayerExplorationMovement] GamePlayMode changed to: {mode}.");
            }

            if (mode == GamePlayMode.Combat && autoSnapOnCombat)
            {
                SnapToNearestGridTile();
            }
        }

        private void HandleTurnStateChanged(TurnState state)
        {
            if ((state == TurnState.PlayerTurn || state == TurnState.EnemyTurn) && autoSnapOnCombat)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Combat)
                {
                    GameManager.Instance.SetMode(GamePlayMode.Combat);
                }
                SnapToNearestGridTile();
            }
        }

        #endregion
    }
}
