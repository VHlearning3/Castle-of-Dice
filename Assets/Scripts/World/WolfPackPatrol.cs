using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The Forest Path wolves (Vili, 2026-10-08): instead of waiting hidden behind a trigger, the pack walks a
    /// wide circle in the woods. When the hero comes within <see cref="noticeRadius"/> of a wolf, the pack stops,
    /// turns to the hero and the room's fight starts there (the grid is laid between the hero and the pack).
    /// The room's own walk-in trigger stays off. After a lost fight the room resets and the wolves walk again;
    /// once the room is cleared they stay gone.
    /// </summary>
    public class WolfPackPatrol : MonoBehaviour
    {
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        [SerializeField] private DungeonRoomController room;
        [Tooltip("Centre of the patrol circle (world X/Z; Y is ignored).")]
        [SerializeField] private Vector3 center;
        [SerializeField] private float radius = 14f;
        [Tooltip("Walking speed along the circle, metres per second.")]
        [SerializeField] private float speed = 2.2f;
        [Tooltip("Degrees between the wolves on the circle.")]
        [SerializeField] private float spacing = 14f;
        [Tooltip("The fight starts when the hero comes this close to a wolf.")]
        [SerializeField] private float noticeRadius = 9f;

        private float angle;
        private PlayerUnit hero;
        private bool walking;

        public DungeonRoomController Room => room;
        public Vector3 Center => center;
        public float Radius => radius;
        public float NoticeRadius => noticeRadius;

        /// <summary>Configures the patrol (used by the scene setup and tests).</summary>
        public void Configure(DungeonRoomController patrolRoom, Vector3 circleCenter, float circleRadius)
        {
            room = patrolRoom;
            center = circleCenter;
            radius = circleRadius;
        }

        private void Update()
        {
            if (room == null || room.roomEnemies == null || !room.isActiveAndEnabled) return;
            if (room.CurrentState != RoomState.Unexplored || room.IsAwaitingBossDialogue)
            {
                SetWalking(false);
                return;
            }

            // The room's walk-in trigger never starts this fight (a reset after a defeat turns it back on)
            Collider trigger = room.GetComponent<Collider>();
            if (trigger != null && trigger.enabled) trigger.enabled = false;

            bool exploring = GameManager.Instance == null || GameManager.Instance.CurrentMode == GamePlayMode.Exploration;
            if (!exploring)
            {
                SetWalking(false);
                return;
            }

            angle += speed / Mathf.Max(1f, radius) * Mathf.Rad2Deg * Time.deltaTime;
            if (angle > 360f) angle -= 360f;
            for (int i = 0; i < room.roomEnemies.Count; i++)
            {
                GameObject wolf = room.roomEnemies[i];
                if (wolf == null) continue;
                if (!wolf.activeSelf) wolf.SetActive(true);
                Place(wolf.transform, angle - i * spacing);
            }
            SetWalking(true);

            if (hero == null) hero = FindAnyObjectByType<PlayerUnit>();
            if (hero == null || !hero.IsAlive || room.HeroGoesUnnoticed) return;
            if (!HeroIsNear(out Vector3 pack)) return;
            StartFight(pack);
        }

        private void Place(Transform wolf, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            var pos = new Vector3(center.x + Mathf.Cos(a) * radius, wolf.position.y, center.z + Mathf.Sin(a) * radius);
            // walking counter-clockwise: the tangent is (-sin, cos)
            var forward = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            wolf.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
        }

        private bool HeroIsNear(out Vector3 pack)
        {
            pack = Vector3.zero;
            int count = 0;
            bool near = false;
            Vector3 h = hero.transform.position;
            foreach (GameObject wolf in room.roomEnemies)
            {
                if (wolf == null || !wolf.activeInHierarchy) continue;
                Vector3 w = wolf.transform.position;
                pack += w;
                count++;
                float dx = w.x - h.x, dz = w.z - h.z;
                if (dx * dx + dz * dz <= noticeRadius * noticeRadius) near = true;
            }
            if (count > 0) pack /= count;
            return near;
        }

        private void StartFight(Vector3 pack)
        {
            SetWalking(false);
            Vector3 h = hero.transform.position;
            foreach (GameObject wolf in room.roomEnemies)
            {
                if (wolf == null) continue;
                Vector3 to = h - wolf.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) wolf.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
            // The grid is laid where the meeting happens: between the hero and the pack
            Vector3 meet = (pack + h) * 0.5f;
            room.transform.position = new Vector3(meet.x, room.transform.position.y, meet.z);
            room.BeginEncounter(hero);
        }

        private void SetWalking(bool on)
        {
            if (walking == on || room == null || room.roomEnemies == null) return;
            walking = on;
            foreach (GameObject wolf in room.roomEnemies)
            {
                if (wolf == null || !wolf.activeInHierarchy) continue;
                Animator anim = wolf.GetComponentInChildren<Animator>();
                if (anim == null || anim.runtimeAnimatorController == null) continue;
                foreach (AnimatorControllerParameter p in anim.parameters)
                {
                    if (p.nameHash == IsMovingHash && p.type == AnimatorControllerParameterType.Bool) anim.SetBool(IsMovingHash, on);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.5f, 0.2f);
            const int steps = 48;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i * Mathf.PI * 2f / steps, a1 = (i + 1) * Mathf.PI * 2f / steps;
                Gizmos.DrawLine(new Vector3(center.x + Mathf.Cos(a0) * radius, center.y + 0.2f, center.z + Mathf.Sin(a0) * radius),
                                new Vector3(center.x + Mathf.Cos(a1) * radius, center.y + 0.2f, center.z + Mathf.Sin(a1) * radius));
            }
        }
    }
}
