using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The small gold ring that marks where a click sent the hero (as in Baldur's Gate 3). One ring per scene,
    /// built in code and reused for every click; it shrinks in and fades out on its own.
    /// </summary>
    public class ClickMoveMarker : MonoBehaviour
    {
        private const int Segments = 32;
        private const float Radius = 0.45f;
        private const float Lifetime = 0.7f;
        private static readonly Color RingColor = new Color(1f, 0.82f, 0.35f, 0.9f);

        private static ClickMoveMarker s_instance;

        private LineRenderer ring;
        private float shownAt = -10f;

        /// <summary>Shows the ring on the ground at <paramref name="point"/>.</summary>
        public static void Show(Vector3 point)
        {
            if (s_instance == null)
            {
                GameObject go = new GameObject("ClickMoveMarker");
                s_instance = go.AddComponent<ClickMoveMarker>();
            }
            s_instance.Place(point);
        }

        /// <summary>Hides the ring (the hero arrived or was stopped).</summary>
        public static void Hide()
        {
            if (s_instance != null && s_instance.ring != null) s_instance.ring.enabled = false;
        }

        private void Awake()
        {
            ring = gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.widthMultiplier = 0.06f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) ring.sharedMaterial = new Material(shader);
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius));
            }
            ring.enabled = false;
        }

        private void Place(Vector3 point)
        {
            transform.position = point + Vector3.up * 0.06f;
            shownAt = Time.unscaledTime;
            ring.enabled = true;
            Animate(0f);
        }

        private void Update()
        {
            if (!ring.enabled) return;
            float t = (Time.unscaledTime - shownAt) / Lifetime;
            if (t >= 1f)
            {
                ring.enabled = false;
                return;
            }
            Animate(t);
        }

        private void Animate(float t)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.75f, t);
            Color c = RingColor;
            c.a *= 1f - t * t;
            ring.startColor = c;
            ring.endColor = c;
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
