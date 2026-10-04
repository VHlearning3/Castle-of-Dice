using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Critical review usability (D1, D2): the pause menu's volume settings and Esc rules, the story intro
    /// and the first-fight hints.
    /// </summary>
    [TestFixture]
    public class CriticalReviewUsabilityTests
    {
        private string musicBackup;
        private string sfxBackup;

        [SetUp]
        public void SetUp()
        {
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey).ToString("R") : null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey).ToString("R") : null;
            StoryFlags.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Restore(PauseMenuUI.MusicVolumeKey, musicBackup);
            Restore(PauseMenuUI.SfxVolumeKey, sfxBackup);
            StoryPanelUI.CloseIfOpen();
            foreach (StoryPanelUI panel in Object.FindObjectsByType<StoryPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(panel.gameObject);
            }
            StoryFlags.Clear();
            GameInput.SetExplorationInputEnabled(true);
        }

        [Test]
        public void Volumes_AreRemembered()
        {
            PauseMenuUI.SetMusicVolume(0.25f);
            PauseMenuUI.SetSfxVolume(1.7f);
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey), 0.0001f);
            Assert.AreEqual(1f, PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey), 0.0001f, "Clamped to full volume.");
        }

        [Test]
        public void Esc_OpensThePauseMenuOnlyWhenNothingElseUsesIt()
        {
            Assert.IsFalse(PauseMenuUI.OtherWindowUsesEscape(), "Nothing open: Esc pauses.");
            StoryPanelUI.Show("Note", "Text");
            Assert.IsTrue(PauseMenuUI.OtherWindowUsesEscape(), "A letter on screen takes Esc first.");
        }

        [Test]
        public void Intro_PlaysOnce_AndRestoresInput()
        {
            GameInput.SetExplorationInputEnabled(true);
            TutorialHints.ShowIntro();
            Assert.IsTrue(StoryPanelUI.IsOpen);
            Assert.IsFalse(GameInput.IsExplorationInputEnabled, "The hero waits while the story is read.");
            Assert.AreEqual(4, TutorialHints.IntroPages.Length);

            StoryPanelUI.CloseIfOpen();
            Assert.IsTrue(GameInput.IsExplorationInputEnabled);

            bool closedAtOnce = false;
            TutorialHints.ShowIntro(() => closedAtOnce = true);
            Assert.IsTrue(closedAtOnce, "The second time it is skipped.");
            Assert.IsFalse(StoryPanelUI.IsOpen);
        }

        [Test]
        public void FirstCellarFight_ShowsTheFourBasics()
        {
            GameObject roomObj = new GameObject("Cellar_Room");
            try
            {
                DungeonRoomController room = roomObj.AddComponent<DungeonRoomController>();
                room.roomLocation = "Cellar";
                MethodInfo handler = typeof(TutorialHints).GetMethod("HandleCombatStarted", BindingFlags.NonPublic | BindingFlags.Static);
                handler.Invoke(null, new object[] { room });
                Assert.IsTrue(StoryPanelUI.IsOpen);
                Assert.IsTrue(StoryFlags.Has(TutorialHints.CombatHintsSeenFlag));

                StoryPanelUI.CloseIfOpen();
                handler.Invoke(null, new object[] { room });
                Assert.IsFalse(StoryPanelUI.IsOpen, "Shown only once.");
            }
            finally
            {
                Object.DestroyImmediate(roomObj);
            }
        }

        private static void Restore(string key, string value)
        {
            if (value == null) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetFloat(key, float.Parse(value));
            PlayerPrefs.Save();
        }
    }
}
