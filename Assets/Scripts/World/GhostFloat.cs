using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Makes a ghost model drift: the whole figure bobs and turns a little, its wispy tail pieces and cloak
    /// sway one after another, and its glow breathes. Bones are not needed; it moves the model's parts
    /// (Ghost_Tail_1..3, Ghost_Cloak) about their own origins. Zero allocations per frame.
    /// </summary>
    public class GhostFloat : MonoBehaviour
    {
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 1.1f;
        [SerializeField] private float turnDegrees = 4f;
        [SerializeField] private float tailSway = 9f;
        [SerializeField] private float cloakSway = 5f;
        [SerializeField] private Light glow;

        private Transform[] tails;
        private Quaternion[] tailRest;
        private Transform cloak;
        private Quaternion cloakRest;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float glowBase;
        private float seed;

        public Light Glow
        {
            get => glow;
            set => glow = value;
        }

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            seed = (transform.position.x * 0.37f + transform.position.z * 0.61f) % 6.28f;

            tails = new Transform[3];
            tailRest = new Quaternion[3];
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < tails.Length; i++)
                {
                    if (t.name == "Ghost_Tail_" + (i + 1)) { tails[i] = t; tailRest[i] = t.localRotation; }
                }
                if (t.name == "Ghost_Cloak") { cloak = t; cloakRest = t.localRotation; }
            }
            if (glow == null && transform.parent != null) glow = transform.parent.GetComponentInChildren<Light>();
            if (glow != null) glowBase = glow.intensity;
        }

        private void Update()
        {
            // Unscaled: he keeps drifting while a conversation or menu holds the game still
            float t = Time.unscaledTime * bobSpeed + seed;
            transform.localPosition = basePosition + new Vector3(0f, Mathf.Sin(t) * bobHeight, 0f);
            transform.localRotation = baseRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.43f) * turnDegrees, 0f);

            for (int i = 0; i < tails.Length; i++)
            {
                if (tails[i] == null) continue;
                float k = i + 1;
                float phase = t * 1.6f - i * 0.8f;
                tails[i].localRotation = tailRest[i] * Quaternion.Euler(Mathf.Sin(phase) * tailSway * k * 0.6f, 0f, Mathf.Cos(phase * 0.8f) * tailSway * 0.5f * k);
            }
            if (cloak != null)
            {
                cloak.localRotation = cloakRest * Quaternion.Euler(Mathf.Sin(t * 1.3f + 0.5f) * cloakSway, 0f, Mathf.Sin(t * 0.9f) * cloakSway * 0.4f);
            }
            if (glow != null)
            {
                glow.intensity = glowBase * (0.8f + 0.25f * Mathf.Sin(t * 2.1f) + 0.1f * Mathf.Sin(t * 5.3f));
            }
        }
    }
}
