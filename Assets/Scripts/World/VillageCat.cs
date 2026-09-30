using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The village cat: sits and breathes, and meows when the hero interacts with it.
    /// The meow is a synthesized placeholder; assign <see cref="meowClip"/> to use a recorded sound instead.
    /// </summary>
    [SelectionBase]
    public class VillageCat : Interactable
    {
        [Tooltip("Model child that breathes.")]
        [SerializeField] private Transform visual;
        [Tooltip("Optional recorded meow. When empty a synthesized placeholder meow plays.")]
        [SerializeField] private AudioClip meowClip;

        private static AudioClip s_placeholderMeow;
        private Vector3 visualBaseScale;

        protected override void Awake()
        {
            base.Awake();
            if (visual != null) visualBaseScale = visual.localScale;
        }

        private void Update()
        {
            if (visual == null) return;
            float breath = 1f + Mathf.Sin(Time.time * 2.4f) * 0.025f;
            visual.localScale = new Vector3(visualBaseScale.x, visualBaseScale.y * breath, visualBaseScale.z);
        }

        public override void Interact(PlayerUnit player)
        {
            if (SFXManager.Instance == null) return;
            AudioClip clip = meowClip;
            if (clip == null)
            {
                if (s_placeholderMeow == null) s_placeholderMeow = SynthesizeMeow();
                clip = s_placeholderMeow;
            }
            SFXManager.Instance.PlaySFX(clip, transform.position, 0.8f, randomizePitch: true);
        }

        /// <summary>
        /// "Mi-aa-uu": a voiced pitch glide (rise then fall) whose brightness opens and closes like a mouth.
        /// </summary>
        private static AudioClip SynthesizeMeow()
        {
            const int sampleRate = 22050;
            const float duration = 0.62f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float u = t / duration;

                // Pitch: 560 Hz up to ~880 Hz at a third, then down to ~470 Hz, with a little vibrato
                float pitch = u < 0.33f ? Mathf.Lerp(560f, 880f, u / 0.33f) : Mathf.Lerp(880f, 470f, (u - 0.33f) / 0.67f);
                pitch *= 1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                phase += 2f * Mathf.PI * pitch / sampleRate;

                // Mouth opening: bright in the middle ("aa"), dull at the ends ("m", "uu")
                float open = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.1f));
                float s = Mathf.Sin(phase)
                        + 0.55f * open * Mathf.Sin(2f * phase)
                        + 0.35f * open * open * Mathf.Sin(3f * phase)
                        + 0.18f * open * open * Mathf.Sin(4f * phase);

                float env = Mathf.Min(1f, t * 25f) * Mathf.Min(1f, (duration - t) * 8f);
                data[i] = s * env * 0.28f;
            }
            AudioClip clip = AudioClip.Create("SFX_CatMeow_Synth", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
