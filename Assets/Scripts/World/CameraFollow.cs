using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Smooth isometric camera follow controller for Castle of the D20.
    /// Tracks a target Transform (the player hero) using smooth damping in LateUpdate,
    /// maintaining an isometric perspective offset and optional pitch angle.
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
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);

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

        [Tooltip("Fixed isometric rotation applied if Look At Target is disabled.")]
        [SerializeField] private Vector3 fixedRotationEuler = new Vector3(50f, 0f, 0f);

        #endregion

        #region Private State

        private Vector3 currentVelocity = Vector3.zero;

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

        /// <summary>Smooth damping time in seconds.</summary>
        public float SmoothTime
        {
            get => smoothTime;
            set => smoothTime = Mathf.Max(0.001f, value);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayerTarget();
            }
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

            Vector3 targetPosition = target.position + offset;

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
                transform.rotation = Quaternion.Euler(fixedRotationEuler);
            }
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
            currentVelocity = Vector3.zero;

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
            transform.position = target.position + offset;

            if (lookAtTarget)
            {
                Vector3 lookTarget = target.position + Vector3.up * lookAtHeightOffset;
                transform.LookAt(lookTarget);
            }
            else
            {
                transform.rotation = Quaternion.Euler(fixedRotationEuler);
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
                return;
            }

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        #endregion
    }
}
