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

        #endregion

        #region Private State

        private CharacterController characterController;
        private PlayerUnit playerUnit;
        private float verticalVelocity = 0f;

        #endregion

        #region Public Properties

        /// <summary>Whether the player is currently actively moving via input.</summary>
        public bool IsMoving { get; private set; }

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
            playerUnit = GetComponent<PlayerUnit>();

            if (explorationCamera == null)
            {
                explorationCamera = Camera.main;
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
            // Guard: Only allow exploration movement when in Exploration mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration)
            {
                IsMoving = false;
                ApplyGravityOnly();
                return;
            }

            HandleLocomotion();
        }

        #endregion

        #region Locomotion & Steering

        private void HandleLocomotion()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 inputDirection = new Vector3(horizontal, 0f, vertical);

            if (explorationCamera == null)
            {
                explorationCamera = Camera.main;
            }

            Vector3 moveDirection = Vector3.zero;

            if (inputDirection.sqrMagnitude > 0.001f)
            {
                // Project camera forward and right onto horizontal XZ plane
                Vector3 camForward = explorationCamera != null ? explorationCamera.transform.forward : Vector3.forward;
                Vector3 camRight = explorationCamera != null ? explorationCamera.transform.right : Vector3.right;

                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                moveDirection = (camForward * inputDirection.z + camRight * inputDirection.x).normalized;

                // Smooth steering towards movement direction
                if (moveDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                IsMoving = true;
            }
            else
            {
                IsMoving = false;
            }

            // Gravity & Vertical Velocity Handling
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
                // Temporarily disable CharacterController so Unity allows manual transform repositioning
                bool wasEnabled = characterController.enabled;
                characterController.enabled = false;

                if (playerUnit != null)
                {
                    playerUnit.MoveToTile(targetTile);
                }
                else
                {
                    transform.position = GridManager.Instance.GetWorldPosition(gridCoord);
                }

                characterController.enabled = wasEnabled;
                verticalVelocity = 0f;

                Debug.Log($"[PlayerExplorationMovement] Combat transition: Snapped {name} to GridTile ({gridCoord.x}, {gridCoord.y}).");
            }
        }

        private void HandlePlayModeChanged(GamePlayMode mode)
        {
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
