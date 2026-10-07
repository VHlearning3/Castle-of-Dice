using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Birdsong for the Whispering Woods: every few seconds a soft synthesized bird call (a chirp, a two-note
    /// whistle or a short trill) sounds somewhere around the camera, through the shared SFX pool so the SFX
    /// volume slider applies. Quiet during combat. The calls are synthesized once; assign recorded
    /// <see cref="calls"/> to use real birds instead.
    /// </summary>
    public class ForestAmbience : MonoBehaviour
    {
        [Tooltip("Optional recorded bird calls. When empty, synthesized placeholder calls play.")]
        [SerializeField] private AudioClip[] calls;
        [SerializeField] private Vector2 interval = new Vector2(2.5f, 7f);
        [SerializeField] private float volume = 0.22f;
        [SerializeField] private float radius = 14f;

        private static AudioClip[] s_synthCalls;
        private float nextCall;

        private void Start()
        {
            nextCall = Time.time + Random.Range(0.8f, 2.5f);
        }

        private void Update()
        {
            if (Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(interval.x, interval.y);
            if (SFXManager.Instance == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            AudioClip[] set = calls != null && calls.Length > 0 ? calls : SynthCalls();
            AudioClip clip = set[Random.Range(0, set.Length)];
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(radius * 0.4f, radius);
            Vector3 at = cam.transform.position + new Vector3(offset.x, 4f, offset.y);
            SFXManager.Instance.PlaySFX(clip, at, volume, randomizePitch: true);
        }

        private static AudioClip[] SynthCalls()
        {
            if (s_synthCalls != null) return s_synthCalls;
            s_synthCalls = new[]
            {
                Synth("SFX_Bird_Chirp", new[] { (3600f, 4600f, 0.07f), (0f, 0f, 0.05f), (3800f, 5000f, 0.06f) }),
                Synth("SFX_Bird_Whistle", new[] { (2600f, 2700f, 0.16f), (0f, 0f, 0.04f), (2100f, 2050f, 0.2f) }),
                Synth("SFX_Bird_Trill", new[] { (4200f, 3900f, 0.035f), (0f, 0f, 0.02f), (4200f, 3900f, 0.035f), (0f, 0f, 0.02f),
                                                 (4300f, 3900f, 0.035f), (0f, 0f, 0.02f), (4300f, 3800f, 0.035f), (0f, 0f, 0.02f),
                                                 (4400f, 3600f, 0.05f) }),
                Synth("SFX_Bird_Coo", new[] { (520f, 600f, 0.18f), (0f, 0f, 0.08f), (560f, 480f, 0.3f) }),
            };
            return s_synthCalls;
        }

        /// <summary>Notes of (start Hz, end Hz, seconds); a zero pitch is a rest. Soft attack and release per note.</summary>
        private static AudioClip Synth(string clipName, (float from, float to, float seconds)[] notes)
        {
            const int sampleRate = 22050;
            float total = 0f;
            foreach (var n in notes) total += n.seconds;
            int samples = Mathf.CeilToInt(total * sampleRate) + 1;
            float[] data = new float[samples];
            int i = 0;
            float phase = 0f;
            foreach (var n in notes)
            {
                int count = Mathf.CeilToInt(n.seconds * sampleRate);
                for (int k = 0; k < count && i < samples; k++, i++)
                {
                    if (n.from <= 0f) { data[i] = 0f; continue; }
                    float u = k / (float)count;
                    float pitch = Mathf.Lerp(n.from, n.to, u);
                    phase += 2f * Mathf.PI * pitch / sampleRate;
                    float env = Mathf.Sin(Mathf.PI * u);
                    data[i] = (Mathf.Sin(phase) + 0.18f * Mathf.Sin(2f * phase)) * env * env * 0.3f;
                }
            }
            AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
