#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class SFXPoolTests
    {
        private GameObject sfxGo;
        private SFXManager sfxManager;

        [SetUp]
        public void SetUp()
        {
            sfxGo = new GameObject("Test_SFXManager");
            sfxManager = sfxGo.AddComponent<SFXManager>();
            sfxManager.InitializeAudioSources(12);
            SFXManager.SetInstanceForTesting(sfxManager);
        }

        [TearDown]
        public void TearDown()
        {
            SFXManager.SetInstanceForTesting(null);
            if (sfxGo != null) Object.DestroyImmediate(sfxGo);
        }

        [Test]
        public void SFXManager_InitializesExactly12Channels()
        {
            Assert.IsNotNull(sfxManager.AudioSources, "AudioSources array must not be null.");
            Assert.AreEqual(12, sfxManager.PoolCount, "Pool count must equal 12 channels.");

            for (int i = 0; i < 12; i++)
            {
                Assert.IsNotNull(sfxManager.AudioSources[i], $"AudioSource at channel {i} must be valid.");
                Assert.IsFalse(sfxManager.AudioSources[i].playOnAwake, "AudioSource must not play on awake.");
            }
        }

        [Test]
        public void SFXManager_RoundRobinAdvancesCorrectly()
        {
            int startIndex = sfxManager.CurrentSourceIndex;

            for (int i = 0; i < 24; i++)
            {
                sfxManager.PlaySFX(SFXClipType.ButtonClick);
                int expectedIndex = (startIndex + i + 1) % 12;
                Assert.AreEqual(expectedIndex, sfxManager.CurrentSourceIndex,
                    $"After {i + 1} plays, pool index should advance to {expectedIndex}.");
            }
        }

        [Test]
        public void SFXManager_AppliesSpatialBlendWhenPositionSupplied()
        {
            Vector3 worldPos = new Vector3(10f, 2f, 15f);
            int targetIndex = sfxManager.CurrentSourceIndex;

            sfxManager.PlaySFX(SFXClipType.SwordHit, worldPos);

            AudioSource usedSource = sfxManager.AudioSources[targetIndex];
            Assert.AreEqual(0.35f, usedSource.spatialBlend, 0.01f,
                "Spatial blend must be set to 0.35f when world position is provided.");
            Assert.AreEqual(worldPos, usedSource.transform.position,
                "Source transform position must match the provided sound origin.");
        }
    }
}
#endif
