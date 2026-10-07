using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>Gentle candle or hearth flicker on a light's intensity (allocation-free, per-light phase).</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Tooltip("Share of the base intensity the flicker swings by (0.08 candle, 0.2 hearth).")]
        [SerializeField] private float amount = 0.1f;

        [SerializeField] private float speed = 7f;

        private Light target;
        private float baseIntensity;
        private float seed;

        public float Amount { get => amount; set => amount = value; }

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * speed + seed;
            float n = Mathf.PerlinNoise(t, seed) * 2f - 1f;
            target.intensity = baseIntensity * (1f + n * amount);
        }
    }
}
