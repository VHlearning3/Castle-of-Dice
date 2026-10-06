using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Wooden castle door (Door_Double / Door_Single models): each leaf pivots on its hinge at the outer
    /// edge and swings into the dark passage behind the arch. By default the door opens on its own when
    /// the hero comes near and closes again when they leave; code can also call <see cref="Open"/>,
    /// <see cref="Close"/> or <see cref="SetOpenImmediate"/> (set <see cref="AutoOpen"/> false for that).
    /// </summary>
    public class SwingDoor : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Leaves")]
        [Tooltip("Door leaves. Each one's pivot must sit on its hinge (the models are exported that way).")]
        [SerializeField] private Transform[] leaves = new Transform[0];

        [Tooltip("Dark passage behind the opening; the leaves swing toward it. If empty, they swing toward local -Z.")]
        [SerializeField] private Transform passage;

        [Tooltip("How far each leaf swings open, in degrees.")]
        [Range(10f, 120f)]
        [SerializeField] private float openAngle = 95f;

        [Tooltip("Swing speed in degrees per second.")]
        [Min(1f)]
        [SerializeField] private float swingSpeed = 190f;

        [Header("Auto Open")]
        [Tooltip("Open when the hero is within the radius and close when they walk away.")]
        [SerializeField] private bool autoOpen = true;

        [Tooltip("Ground distance from the door at which it opens for the hero.")]
        [Min(0.5f)]
        [SerializeField] private float autoOpenRadius = 7f;

        [Tooltip("Play the door creak when it starts to open.")]
        [SerializeField] private bool playSound = true;

        #endregion

        #region Private State

        private const float PlayerSearchInterval = 1f;

        private Quaternion[] closedRotations;
        private float[] swingSigns;
        private float openAmount;
        private bool wantOpen;
        private Transform player;
        private float nextPlayerSearch;

        #endregion

        #region Public Properties

        /// <summary>Leaves this door swings.</summary>
        public Transform[] Leaves => leaves;

        /// <summary>0 = shut, 1 = fully open.</summary>
        public float OpenAmount => openAmount;

        /// <summary>Whether the door is opening or open.</summary>
        public bool IsOpen => wantOpen;

        /// <summary>Opening angle in degrees.</summary>
        public float OpenAngle => openAngle;

        /// <summary>Whether the hero's distance drives the door.</summary>
        public bool AutoOpen
        {
            get => autoOpen;
            set => autoOpen = value;
        }

        /// <summary>Distance at which the door opens for the hero.</summary>
        public float AutoOpenRadius
        {
            get => autoOpenRadius;
            set => autoOpenRadius = value;
        }

        /// <summary>Sets up the door from code (editor setup and tests).</summary>
        public void Configure(Transform[] doorLeaves, Transform passageTransform)
        {
            leaves = doorLeaves ?? new Transform[0];
            passage = passageTransform;
            closedRotations = null;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CacheLeaves();
        }

        private void Update()
        {
            if (autoOpen)
            {
                UpdateAutoOpen();
            }

            float target = wantOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target)) return;
            openAmount = Mathf.MoveTowards(openAmount, target, swingSpeed / openAngle * Time.deltaTime);
            ApplyPose();
        }

        #endregion

        #region Open / Close

        /// <summary>Starts swinging the door open.</summary>
        public void Open()
        {
            if (wantOpen) return;
            wantOpen = true;
            if (playSound && SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(SFXClipType.DoorOpen, transform.position, 0.7f);
            }
        }

        /// <summary>Starts swinging the door shut.</summary>
        public void Close()
        {
            wantOpen = false;
        }

        /// <summary>Snaps the door fully open or shut without animating.</summary>
        public void SetOpenImmediate(bool open)
        {
            wantOpen = open;
            openAmount = open ? 1f : 0f;
            ApplyPose();
        }

        /// <summary>Signed swing of a leaf in degrees at the current opening (for tests).</summary>
        public float LeafAngle(int index)
        {
            CacheLeaves();
            if (index < 0 || index >= leaves.Length) return 0f;
            return swingSigns[index] * openAngle * Ease(openAmount);
        }

        #endregion

        #region Internals

        private void CacheLeaves()
        {
            if (closedRotations != null && closedRotations.Length == leaves.Length) return;

            closedRotations = new Quaternion[leaves.Length];
            swingSigns = new float[leaves.Length];

            // The leaves swing toward the passage behind the arch
            Vector3 back = passage != null ? passage.position - transform.position : -transform.forward;
            if (passage != null && passage.TryGetComponent(out Renderer passageRenderer))
            {
                back = passageRenderer.bounds.center - transform.position;
            }
            back.y = 0f;

            for (int i = 0; i < leaves.Length; i++)
            {
                Transform leaf = leaves[i];
                if (leaf == null) continue;
                closedRotations[i] = leaf.localRotation;

                // A positive turn about the hinge moves the leaf's centre along up x (centre - hinge)
                Vector3 centre = leaf.TryGetComponent(out Renderer r) ? r.bounds.center : leaf.position;
                Vector3 arm = centre - leaf.position;
                float toward = Vector3.Dot(Vector3.Cross(leaf.up, arm), back);
                swingSigns[i] = toward >= 0f ? 1f : -1f;
            }
        }

        private void ApplyPose()
        {
            CacheLeaves();
            float angle = openAngle * Ease(openAmount);
            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] == null) continue;
                leaves[i].localRotation = closedRotations[i] * Quaternion.AngleAxis(swingSigns[i] * angle, Vector3.up);
            }
        }

        private void UpdateAutoOpen()
        {
            if (player == null)
            {
                if (Time.unscaledTime < nextPlayerSearch) return;
                nextPlayerSearch = Time.unscaledTime + PlayerSearchInterval;
                PlayerUnit unit = FindAnyObjectByType<PlayerUnit>();
                if (unit == null) return;
                player = unit.transform;
            }

            Vector3 d = player.position - transform.position;
            d.y = 0f;
            bool near = d.sqrMagnitude <= autoOpenRadius * autoOpenRadius;
            if (near && !wantOpen) Open();
            else if (!near && wantOpen) Close();
        }

        private static float Ease(float t)
        {
            return t * t * (3f - 2f * t);
        }

        #endregion
    }
}
