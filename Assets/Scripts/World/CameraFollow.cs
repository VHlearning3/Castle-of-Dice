using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Smooth isometric and dynamic follow camera for Castle of the D20.
    /// Tracks the player hero in LateUpdate with smooth damping, and can slowly
    /// orbit/turn towards the direction the player is walking for an intuitive,
    /// modern exploration camera experience.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Target Tracking")]
        [Tooltip("The target Transform to follow (typically the player hero).")]
        [SerializeField] private Transform target;

        [Tooltip("If true, automatically searches for a PlayerUnit or GameObject tagged 'Player' if target is unassigned.")]
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Offset & Positioning")]
        [Tooltip("Isometric world-space offset from the target (e.g. (0, 10, -8)).")]
        [SerializeField] private Vector3 offset = new Vector3(-6f, 8f, -8f);

        [Header("Smoothing & Damping")]
        [Tooltip("Approximate time in seconds to reach the target position (lower is snappier, higher is smoother).")]
        [Range(0.01f, 1.0f)]
        [SerializeField] private float smoothTime = 0.2f;

        [Tooltip("Maximum movement speed for the camera in units per second.")]
        [SerializeField] private float maxSpeed = 50f;

        [Header("Rotation & Orientation")]
        [Tooltip("If true, automatically aligns camera rotation to look at the target position + lookOffset.")]
        [SerializeField] private bool lookAtTarget = true;

        [Tooltip("Height offset added to the target position when looking at the target.")]
        [SerializeField] private float lookAtHeightOffset = 1.0f;

        [Tooltip("Fixed isometric pitch/roll applied if Look At Target is disabled.")]
        [SerializeField] private Vector3 fixedRotationEuler = new Vector3(50f, 0f, 0f);

        [Header("Dynamic Direction Follow")]
        [Tooltip("If true, the camera smoothly turns towards the direction the player is walking.")]
        [SerializeField] private bool turnTowardsMovementDirection = true;

        [Tooltip("Smooth time in seconds for camera rotation towards the player's walking direction (higher = slower, smoother turn).")]
        [Range(0.2f, 4.0f)]
        [SerializeField] private float rotationSmoothTime = 1.5f;

        [Tooltip("Maximum camera turning speed in degrees per second.")]
        [SerializeField] private float maxRotationSpeed = 60.0f;

        [Tooltip("Minimum movement speed in units/sec to consider the player actively walking.")]
        [SerializeField] private float minWalkSpeed = 0.15f;

        [Tooltip("Only dynamically rotate camera during Exploration mode. Freezes rotation in Combat mode.")]
        [SerializeField] private bool onlyDuringExploration = true;

        [Tooltip("Optional lateral (left/right) camera offset relative to the heading angle.")]
        [SerializeField] private float lateralOffset = 0f;

        #endregion

        #region Private State

        private Vector3 currentVelocity = Vector3.zero;
        private float currentYaw = 0f;
        private float targetYaw = 0f;
        private float currentYawVelocity = 0f;
        private Vector3 lastTargetPosition = Vector3.zero;
        private bool isInitialized = false;
        private PlayerExplorationMovement cachedPlayerMovement;

        #endregion

        #region Public Properties

        /// <summary>Current target Transform being followed.</summary>
        public Transform Target => target;

        /// <summary>Isometric offset vector relative to the target.</summary>
        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        /// <summary>Smooth damping time in seconds for position tracking.</summary>
        public float SmoothTime
        {
            get => smoothTime;
            set => smoothTime = Mathf.Max(0.001f, value);
        }

        /// <summary>Whether the camera dynamically rotates behind the player's walking direction.</summary>
        public bool TurnTowardsMovementDirection
        {
            get => turnTowardsMovementDirection;
            set => turnTowardsMovementDirection = value;
        }

        /// <summary>Smooth time in seconds for camera turning.</summary>
        public float RotationSmoothTime
        {
            get => rotationSmoothTime;
            set => rotationSmoothTime = Mathf.Max(0.05f, value);
        }

        /// <summary>Maximum camera turning speed in degrees per second.</summary>
        public float MaxRotationSpeed
        {
            get => maxRotationSpeed;
            set => maxRotationSpeed = Mathf.Max(1f, value);
        }

        /// <summary>Current horizontal heading angle (yaw) of the camera in degrees.</summary>
        public float CurrentYaw => currentYaw;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayerTarget();
            }

            InitializeYaw();
        }

        private void Start()
        {
            if (target != null)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (autoFindPlayer)
                {
                    FindPlayerTarget();
                }

                if (target == null) return;
            }

            if (!isInitialized)
            {
                InitializeYaw();
            }

            Vector3 targetPosition;

            if (turnTowardsMovementDirection)
            {
                // Verify whether dynamic rotation is allowed in the current game mode
                bool allowRotation = true;
                if (onlyDuringExploration && GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration)
                {
                    allowRotation = false;
                }

                if (allowRotation)
                {
                    UpdateWalkDirection();
                }

                // Compute orbit position from horizontal distance, height, and current yaw
                float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
                if (horizontalDistance < 0.1f) horizontalDistance = 8f;
                float height = offset.y > 0.1f ? offset.y : 8f;

                Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
                Vector3 localOffset = new Vector3(lateralOffset, height, -horizontalDistance);
                Vector3 rotatedOffset = yawRotation * localOffset;
                targetPosition = target.position + rotatedOffset;
            }
            else
            {
                targetPosition = target.position + offset;
            }

            // Smoothly interpolate position using SmoothDamp to avoid frame-rate jitter
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref currentVelocity,
                smoothTime,
                maxSpeed,
                Time.deltaTime
            );

            // Handle camera orientation
            if (lookAtTarget)
            {
                Vector3 lookTarget = target.position + Vector3.up * lookAtHeightOffset;
                transform.LookAt(lookTarget);
            }
            else
            {
                transform.rotation = Quaternion.Euler(fixedRotationEuler.x, currentYaw, fixedRotationEuler.z);
            }

            lastTargetPosition = target.position;
        }

        #endregion

        #region Direction Tracking

        private void InitializeYaw()
        {
            if (target == null) return;

            cachedPlayerMovement = target.GetComponent<PlayerExplorationMovement>();
            lastTargetPosition = target.position;

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                currentYaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            }
            else
            {
                currentYaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
            }

            targetYaw = currentYaw;
            isInitialized = true;
        }

        /// <summary>
        /// Detects the target's current horizontal walking direction and smoothly updates the camera yaw angle.
        /// </summary>
        private void UpdateWalkDirection()
        {
            Vector3 walkDir = Vector3.zero;

            // 1. Direct query from PlayerExplorationMovement if attached
            if (cachedPlayerMovement == null && target != null)
            {
                cachedPlayerMovement = target.GetComponent<PlayerExplorationMovement>();
            }

            if (cachedPlayerMovement != null && cachedPlayerMovement.IsMoving)
            {
                walkDir = cachedPlayerMovement.CurrentMoveDirection;
                if (walkDir.sqrMagnitude < 0.001f)
                {
                    walkDir = target.forward;
                }
            }

            // 2. Fallback to physical displacement delta between frames
            if (walkDir.sqrMagnitude < 0.001f)
            {
                Vector3 displacement = target.position - lastTargetPosition;
                displacement.y = 0f;
                float currentSpeed = displacement.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

                if (currentSpeed >= minWalkSpeed && displacement.sqrMagnitude > 0.0001f)
                {
                    walkDir = displacement.normalized;
                }
            }

            // If a valid walking direction is detected, update the target camera yaw
            if (walkDir.sqrMagnitude > 0.001f)
            {
                targetYaw = Mathf.Atan2(walkDir.x, walkDir.z) * Mathf.Rad2Deg;
            }

            // Smoothly interpolate currentYaw towards targetYaw (handles 360-degree angle wrapping)
            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref currentYawVelocity,
                rotationSmoothTime,
                maxRotationSpeed,
                Time.deltaTime
            );
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets a new target to follow, with an option to immediately snap to it.
        /// </summary>
        /// <param name="newTarget">Target Transform to track.</param>
        /// <param name="snapImmediately">If true, skips interpolation and places the camera at target + offset immediately.</param>
        public void SetTarget(Transform newTarget, bool snapImmediately = false)
        {
            target = newTarget;
            cachedPlayerMovement = newTarget != null ? newTarget.GetComponent<PlayerExplorationMovement>() : null;
            currentVelocity = Vector3.zero;
            currentYawVelocity = 0f;

            if (newTarget != null)
            {
                lastTargetPosition = newTarget.position;
                InitializeYaw();
            }

            if (snapImmediately && target != null)
            {
                SnapToTarget();
            }
        }

        /// <summary>
        /// Instantly teleports the camera to the target position plus offset without smoothing.
        /// Useful during level transitions, respawns, or teleport abilities (e.g. Blink).
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            currentVelocity = Vector3.zero;
            currentYawVelocity = 0f;

            Vector3 targetPosition;
            if (turnTowardsMovementDirection)
            {
                float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
                if (horizontalDistance < 0.1f) horizontalDistance = 8f;
                float height = offset.y > 0.1f ? offset.y : 8f;

                Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
                Vector3 localOffset = new Vector3(lateralOffset, height, -horizontalDistance);
                Vector3 rotatedOffset = yawRotation * localOffset;
                targetPosition = target.position + rotatedOffset;
            }
            else
            {
                targetPosition = target.position + offset;
            }

            transform.position = targetPosition;
            lastTargetPosition = target.position;

            if (lookAtTarget)
            {
                Vector3 lookTarget = target.position + Vector3.up * lookAtHeightOffset;
                transform.LookAt(lookTarget);
            }
            else
            {
                transform.rotation = Quaternion.Euler(fixedRotationEuler.x, currentYaw, fixedRotationEuler.z);
            }
        }

        #endregion

        #region Target Discovery

        /// <summary>
        /// Searches the scene for a PlayerUnit or GameObject tagged 'Player'.
        /// </summary>
        private void FindPlayerTarget()
        {
            PlayerUnit playerUnit = FindAnyObjectByType<PlayerUnit>();
            if (playerUnit != null)
            {
                target = playerUnit.transform;
                cachedPlayerMovement = target.GetComponent<PlayerExplorationMovement>();
                InitializeYaw();
                return;
            }

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
                cachedPlayerMovement = target.GetComponent<PlayerExplorationMovement>();
                InitializeYaw();
            }
        }

        #endregion
    }
}
