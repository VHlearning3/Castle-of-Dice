using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Small allocation-free ambient motion for zone dressing: flickering fire lights, pulsing glows, floating books
    /// and crystals, and storm lightning flashes. Added by the zone dressing tool; no gameplay effect.
    /// </summary>
    public class ZoneAmbientMotion : MonoBehaviour
    {
        public enum MotionKind { FlickerLight, Float, Lightning, Pulse }

        public MotionKind kind = MotionKind.FlickerLight;

        [Tooltip("Flicker: fraction of the base intensity that varies. Float: bob height in metres. Lightning: flash peak intensity.")]
        public float amount = 0.25f;

        [Tooltip("Flicker and float speed.")]
        public float speed = 6f;

        [Tooltip("Float: spin in degrees per second around the vertical axis.")]
        public float spin = 0f;

        [Tooltip("Lightning: average seconds between flashes.")]
        public float flashInterval = 9f;

        private Light cachedLight;
        private float baseIntensity;
        private Vector3 basePosition;
        private float phase;
        private float nextFlash;
        private float flashTime = -1f;

        private void Awake()
        {
            cachedLight = GetComponent<Light>();
            if (cachedLight != null) baseIntensity = cachedLight.intensity;
            basePosition = transform.localPosition;
            phase = (transform.position.x * 0.37f + transform.position.z * 0.71f) % 10f;
            nextFlash = Time.time + flashInterval * Random.Range(0.4f, 1.2f);
        }

        private void Update()
        {
            float t = Time.time;
            switch (kind)
            {
                case MotionKind.FlickerLight:
                    if (cachedLight == null) return;
                    float n = Mathf.PerlinNoise(t * speed, phase) - 0.5f;
                    cachedLight.intensity = baseIntensity * (1f + n * 2f * amount);
                    break;

                case MotionKind.Pulse:
                    // amount = fraction of base intensity, speed = pulses per second
                    if (cachedLight == null) return;
                    cachedLight.intensity = baseIntensity * (1f + Mathf.Sin(t * speed * Mathf.PI * 2f) * amount);
                    break;

                case MotionKind.Float:
                    transform.localPosition = basePosition + new Vector3(0f, Mathf.Sin((t + phase) * speed) * amount, 0f);
                    if (spin != 0f) transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
                    break;

                case MotionKind.Lightning:
                    if (cachedLight == null) return;
                    if (flashTime < 0f && t >= nextFlash)
                    {
                        flashTime = t;
                        nextFlash = t + flashInterval * Random.Range(0.5f, 1.5f);
                    }
                    if (flashTime >= 0f)
                    {
                        float k = t - flashTime;
                        // Two quick strobes, then fade out
                        float strobe = k < 0.08f ? 1f : (k < 0.16f ? 0.2f : (k < 0.24f ? 0.9f : Mathf.Max(0f, 1f - (k - 0.24f) * 3f)));
                        cachedLight.intensity = amount * strobe;
                        if (k > 0.6f) { flashTime = -1f; cachedLight.intensity = 0f; }
                    }
                    else
                    {
                        cachedLight.intensity = 0f;
                    }
                    break;
            }
        }
    }
}
