using System;
using UnityEngine;

namespace CastleOfTheD20.Tests.E2E.Common
{
    /// <summary>
    /// Base class for all E2E test fixtures providing standard setup, teardown,
    /// and audio clip creation utilities.
    /// </summary>
    public abstract class E2ETestSuite
    {
        protected MusicManagerTestDriver Driver { get; private set; }

        public virtual void SetUp()
        {
            Driver = new MusicManagerTestDriver();
        }

        public virtual void TearDown()
        {
            if (Driver != null)
            {
                Driver.Teardown();
                Driver = null;
            }
        }

        /// <summary>
        /// Creates an in-memory procedural test AudioClip for isolated testing without file IO.
        /// </summary>
        protected AudioClip CreateMockAudioClip(string name, float durationSeconds = 1.0f)
        {
            int sampleRate = 44100;
            int length = Mathf.Max(1, (int)(sampleRate * durationSeconds));
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * ((float)i / sampleRate)) * 0.2f;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
