using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Violet lightning on the Mirror Twin's trident: a few short arcs crackle between the prongs and a light
    /// flickers at the head; <see cref="Strike"/> throws a jagged bolt from the head to a target for a moment.
    /// Line points are reused (no allocations per frame).
    /// </summary>
    public class TridentLightning : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Light glow;
        [SerializeField] private LineRenderer[] arcs = new LineRenderer[0];
        [SerializeField] private LineRenderer bolt;
        [SerializeField] private float arcRadius = 0.32f;
        [SerializeField] private float refreshSeconds = 0.07f;
        [SerializeField] private float baseLight = 1.0f;

        private const int ArcPoints = 7;
        private const int BoltPoints = 14;
        private Vector3[][] arcLocal;
        private Vector3[] arcWorld;
        private Vector3[] boltWorld;
        private float nextRefresh;
        private float boltUntil;
        private Vector3 boltTarget;

        /// <summary>Scales the light and the arcs (her second phase burns brighter).</summary>
        public float Intensity { get; set; } = 1f;

        public Transform Head
        {
            get => head;
            set => head = value;
        }

        public Light Glow
        {
            get => glow;
            set => glow = value;
        }

        public LineRenderer[] Arcs
        {
            get => arcs;
            set => arcs = value;
        }

        public LineRenderer Bolt
        {
            get => bolt;
            set => bolt = value;
        }

        /// <summary>Whether a bolt is being shown right now.</summary>
        public bool IsStriking => Time.time < boltUntil;

        private void Awake()
        {
            arcLocal = new Vector3[arcs.Length][];
            for (int i = 0; i < arcs.Length; i++)
            {
                arcLocal[i] = new Vector3[ArcPoints];
                if (arcs[i] != null) arcs[i].positionCount = ArcPoints;
            }
            arcWorld = new Vector3[ArcPoints];
            boltWorld = new Vector3[BoltPoints];
            if (bolt != null)
            {
                bolt.positionCount = BoltPoints;
                bolt.enabled = false;
            }
        }

        /// <summary>Throws a bolt from the trident's head to <paramref name="worldTarget"/>.</summary>
        public void Strike(Vector3 worldTarget, float seconds = 0.4f)
        {
            boltTarget = worldTarget;
            boltUntil = Time.time + seconds;
            nextRefresh = 0f;
        }

        private void LateUpdate()
        {
            if (head == null) return;
            bool refresh = Time.time >= nextRefresh;
            if (refresh)
            {
                nextRefresh = Time.time + refreshSeconds;
                for (int i = 0; i < arcs.Length; i++) NewArc(arcLocal[i]);
            }

            Vector3 origin = head.position;
            float scale = head.lossyScale.x;
            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcs[i] == null) continue;
                Vector3[] local = arcLocal[i];
                for (int p = 0; p < ArcPoints; p++) arcWorld[p] = origin + head.rotation * (local[p] * scale);
                arcs[i].SetPositions(arcWorld);
                arcs[i].widthMultiplier = 0.03f * scale * Mathf.Sqrt(Intensity);
            }

            if (glow != null)
            {
                float flicker = refresh ? Random.Range(0.75f, 1.25f) : 1f;
                if (refresh) glow.intensity = baseLight * Intensity * flicker * (IsStriking ? 2.4f : 1f);
            }

            if (bolt != null)
            {
                bool on = IsStriking;
                bolt.enabled = on;
                if (on && refresh)
                {
                    Vector3 from = origin;
                    Vector3 dir = boltTarget - from;
                    float len = dir.magnitude;
                    Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                    for (int p = 0; p < BoltPoints; p++)
                    {
                        float t = p / (float)(BoltPoints - 1);
                        float jitter = (p == 0 || p == BoltPoints - 1) ? 0f : len * 0.06f;
                        boltWorld[p] = from + dir * t + side * Random.Range(-jitter, jitter) + Vector3.up * Random.Range(-jitter, jitter);
                    }
                    bolt.SetPositions(boltWorld);
                }
            }
        }

        private void NewArc(Vector3[] pts)
        {
            // From one random point on the head to another, kinked in between
            Vector3 a = Random.insideUnitSphere * arcRadius;
            Vector3 b = Random.insideUnitSphere * arcRadius;
            for (int p = 0; p < ArcPoints; p++)
            {
                float t = p / (float)(ArcPoints - 1);
                Vector3 kink = (p == 0 || p == ArcPoints - 1) ? Vector3.zero : Random.insideUnitSphere * arcRadius * 0.25f;
                pts[p] = Vector3.Lerp(a, b, t) + kink;
            }
        }
    }
}
