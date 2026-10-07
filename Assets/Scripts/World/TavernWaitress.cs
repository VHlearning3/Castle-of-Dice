using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The tavern's elf waitress makes her rounds: she walks from table to table along the runtime NavMesh,
    /// turns to the table and serves (Serve trigger), waits a moment and moves on. Walking sets IsMoving.
    /// She never fights here; the Animator's Attack and Die states are for other uses.
    /// </summary>
    public class TavernWaitress : MonoBehaviour
    {
        [Tooltip("Stops on her round, in order. Each faces the table she serves.")]
        [SerializeField] private Transform[] stops = new Transform[0];

        [Tooltip("Tables she serves at each stop (same order as the stops; may be shorter).")]
        [SerializeField] private Transform[] tables = new Transform[0];

        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float turnSpeed = 360f;
        [SerializeField] private Vector2 waitRange = new Vector2(2.5f, 5f);
        [SerializeField] private float serveDuration = 2.2f;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int ServeHash = Animator.StringToHash("Serve");

        private Animator animator;
        private readonly Vector3[] corners = new Vector3[32];
        private int cornerCount;
        private int cornerIndex;
        private int stopIndex = -1;
        private float waitUntil;
        private bool walking;
        private bool served;

        public Transform[] Stops { get => stops; set => stops = value; }
        public Transform[] Tables { get => tables; set => tables = value; }
        public bool IsWalking => walking;
        public int StopIndex => stopIndex;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            waitUntil = Time.time + Random.Range(0.5f, 2f);
        }

        private void Update()
        {
            if (stops == null || stops.Length == 0) return;

            if (walking)
            {
                StepAlongPath();
                return;
            }

            if (!served && stopIndex >= 0)
            {
                FaceTable(Time.deltaTime);
            }

            if (Time.time < waitUntil) return;

            if (!served && stopIndex >= 0)
            {
                if (animator != null) animator.SetTrigger(ServeHash);
                served = true;
                waitUntil = Time.time + serveDuration + Random.Range(waitRange.x, waitRange.y);
                return;
            }

            BeginWalk((stopIndex + 1) % stops.Length);
        }

        private void BeginWalk(int next)
        {
            stopIndex = next;
            served = false;
            Vector3 goal = stops[stopIndex].position;

            cornerCount = 0;
            if (RuntimeNavMesh.EnsureCovers(transform.position, goal, null))
            {
                cornerCount = RuntimeNavMesh.FindPath(transform.position, goal, corners);
            }
            if (cornerCount < 2)
            {
                corners[0] = transform.position;
                corners[1] = goal;
                cornerCount = 2;
            }
            cornerIndex = 1;
            walking = true;
            if (animator != null) animator.SetBool(IsMovingHash, true);
        }

        private void StepAlongPath()
        {
            Vector3 target = corners[cornerIndex];
            Vector3 pos = transform.position;
            Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            float step = walkSpeed * Time.deltaTime;

            if (flat.sqrMagnitude > 0.0004f)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            if (flat.magnitude <= step)
            {
                transform.position = new Vector3(target.x, target.y, target.z);
                cornerIndex++;
                if (cornerIndex >= cornerCount)
                {
                    walking = false;
                    if (animator != null) animator.SetBool(IsMovingHash, false);
                    waitUntil = Time.time + 0.6f;
                }
                return;
            }

            Vector3 next = pos + flat.normalized * step;
            // follow the path's height (stairs) in step with the horizontal progress
            float total = flat.magnitude;
            next.y = Mathf.Lerp(pos.y, target.y, step / Mathf.Max(step, total));
            transform.position = next;
        }

        private void FaceTable(float dt)
        {
            Transform table = stopIndex < tables.Length ? tables[stopIndex] : null;
            if (table == null) return;
            Vector3 to = table.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), turnSpeed * dt);
        }
    }
}
