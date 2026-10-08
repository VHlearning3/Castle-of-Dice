using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Makes the Mirror Twin's long skirt move (Vili, 2026-10-08): five chains of two bones (Skirt_N_Up/Low, under
    /// the hips) swing on springs after the Animator has posed her. The hem trails behind when she moves or turns,
    /// and the panels in front of a thigh are pushed forward as the leg swings, so a stride or a lunge never cuts
    /// through the cloth. Humanoid clips leave these bones alone; all state is preallocated.
    /// </summary>
    public class SkirtSway : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string bonePrefix = "Skirt_";
        [SerializeField] private int chainCount = 5;
        [Tooltip("Degrees of trailing per metre a second of movement.")]
        [SerializeField] private float dragPerSpeed = 7f;
        [Tooltip("Degrees of trailing per degree a second of turning.")]
        [SerializeField] private float dragPerTurn = 0.05f;
        [Tooltip("Degrees the cloth in front of a thigh moves per unit of forward leg swing (sine of the angle).")]
        [SerializeField] private float legPush = 55f;
        [SerializeField] private float maxAngle = 38f;
        [SerializeField] private float stiffness = 55f;
        [SerializeField] private float damping = 9f;
        [Tooltip("How much more the lower half bends than the upper.")]
        [SerializeField] private float lowerFollow = 0.6f;

        private Transform hips, leftThigh, rightThigh, leftKnee, rightKnee;
        private Transform[] upper, lower;
        private Quaternion[] upperRest, lowerRest;
        private Vector3[] outwardLocal;     // each chain's outward direction in the hips' space
        private float[] side;               // +1 left, -1 right, 0 back
        private Vector3[] offset, velocity; // spring state per chain (world, degrees)
        private Vector3 lastHipsPos;
        private float lastYaw;
        private bool ready;

        public Animator Animator
        {
            get => animator;
            set => animator = value;
        }

        /// <summary>Bones found and swinging.</summary>
        public bool IsReady => ready;

        /// <summary>The current swing of chain <paramref name="i"/> in degrees (tests).</summary>
        public float ChainAngle(int i) => ready && i >= 0 && i < chainCount ? offset[i].magnitude : 0f;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            Setup();
        }

        private void Setup()
        {
            ready = false;
            if (animator == null || !animator.isHuman) return;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (hips == null) return;

            upper = new Transform[chainCount];
            lower = new Transform[chainCount];
            upperRest = new Quaternion[chainCount];
            lowerRest = new Quaternion[chainCount];
            outwardLocal = new Vector3[chainCount];
            side = new float[chainCount];
            offset = new Vector3[chainCount];
            velocity = new Vector3[chainCount];
            Transform[] all = animator.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < chainCount; i++)
            {
                upper[i] = FindIn(all, bonePrefix + i + "_Up");
                lower[i] = FindIn(all, bonePrefix + i + "_Low");
                if (upper[i] == null || lower[i] == null) return;
                upperRest[i] = upper[i].localRotation;
                lowerRest[i] = lower[i].localRotation;
                Vector3 local = hips.InverseTransformPoint(lower[i].position);
                local.y = 0f;
                outwardLocal[i] = local.normalized;
                side[i] = Mathf.Abs(local.x) < 0.02f ? 0f : Mathf.Sign(local.x);
            }
            lastHipsPos = hips.position;
            lastYaw = hips.eulerAngles.y;
            ready = true;
        }

        private static Transform FindIn(Transform[] all, string name)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }
            return null;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            // How the hips moved and turned since last frame
            Vector3 hipsPos = hips.position;
            Vector3 vel = (hipsPos - lastHipsPos) / dt;
            vel.y = 0f;
            float scale = Mathf.Max(0.01f, transform.lossyScale.y);
            vel /= scale; // her own metres, so a bigger model sways the same
            lastHipsPos = hipsPos;
            float yaw = hips.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;

            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float leftSwing = ThighSwing(leftThigh, leftKnee, fwd);
            float rightSwing = ThighSwing(rightThigh, rightKnee, fwd);

            for (int i = 0; i < chainCount; i++)
            {
                Vector3 outward = hips.TransformDirection(outwardLocal[i]);
                outward.y = 0f;
                outward.Normalize();
                float front = Mathf.Clamp(Vector3.Dot(outward, fwd), -0.3f, 1f);

                // trailing behind movement and turning, pushed by the thigh on that side
                Vector3 target = -vel * dragPerSpeed;
                target += Vector3.Cross(Vector3.up, outward) * (-yawRate * dragPerTurn);
                float swing = side[i] > 0f ? leftSwing : side[i] < 0f ? rightSwing : Mathf.Min(leftSwing, rightSwing);
                if (side[i] == 0f) front = -0.5f; // the back panel lifts when a leg swings back
                target += fwd * (swing * legPush * (side[i] == 0f ? 0.5f : front));
                target = Vector3.ClampMagnitude(target, maxAngle);

                velocity[i] += ((target - offset[i]) * stiffness - velocity[i] * damping) * dt;
                offset[i] += velocity[i] * dt;
                offset[i] = Vector3.ClampMagnitude(offset[i], maxAngle);

                upper[i].localRotation = upperRest[i];
                lower[i].localRotation = lowerRest[i];
                float angle = offset[i].magnitude;
                if (angle < 0.01f) continue;
                Vector3 axis = Vector3.Cross(Vector3.down, offset[i] / angle);
                if (axis.sqrMagnitude < 1e-6f) continue;
                axis.Normalize();
                upper[i].rotation = Quaternion.AngleAxis(angle, axis) * upper[i].rotation;
                lower[i].rotation = Quaternion.AngleAxis(angle * lowerFollow, axis) * lower[i].rotation;
            }
        }

        /// <summary>Forward swing of a thigh: the sine of its angle in front of straight down (negative behind).</summary>
        private static float ThighSwing(Transform thigh, Transform knee, Vector3 fwd)
        {
            if (thigh == null || knee == null) return 0f;
            Vector3 dir = (knee.position - thigh.position).normalized;
            return Vector3.Dot(dir, fwd);
        }
    }
}
