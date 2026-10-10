using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-10: the save shrines get a floating name and a "Game Saved" text, and the expedition map
    /// opened in the tavern names the right zone with its "YOU ARE HERE: TAVERN" tag on one line. Starts a New
    /// Adventure (the editor save slot is backed up and restored). With MAPSAVE_SHOTS_DIR set, saves screenshots.
    /// </summary>
    public class MapSavePointPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private float? musicBackup;
        private float? sfxBackup;
        private string shots;

        [SetUp]
        public void BackupPrefs()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey) : (float?)null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey) : (float?)null;
            shots = Environment.GetEnvironmentVariable("MAPSAVE_SHOTS_DIR");
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (DungeonMapUIController.Instance != null && DungeonMapUIController.Instance.IsMapOpen) DungeonMapUIController.Instance.CloseMap();
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (musicBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.MusicVolumeKey, musicBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.MusicVolumeKey);
            if (sfxBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.SfxVolumeKey, sfxBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.SfxVolumeKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator SaveShrines_HaveALabel_AndSayGameSaved()
        {
            yield return StartAdventure("Character_Warrior_SirRoland");
            yield return CheckShrine("Village");

            SceneLoader.Instance.LoadScene("Zone_5_CastleHall");
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            yield return CheckShrine("GreatHall");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Map_InTheTavern_NamesTheTavern()
        {
            yield return StartAdventure("Character_Warrior_SirRoland");
            SceneLoader.Instance.LoadScene(TavernEntrance.SceneName);
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            yield return WaitSeconds(1f);

            DungeonMapUIController map = DungeonMapUIController.Instance;
            Assert.IsNotNull(map, "The map controller is alive in the tavern.");
            map.ShowMap();
            yield return Frames(5);
            StringAssert.Contains(TavernEntrance.DisplayName, map.CurrentLocationHeader);
            Assert.AreEqual(DungeonMapUIController.KeyVillage, map.CurrentNodeKey);
            yield return Shot("Map_Tavern.png");
            map.CloseMap();

            SceneLoader.Instance.LoadScene("Zone_4_Library");
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            map = DungeonMapUIController.Instance;
            map.ShowMap();
            yield return Frames(5);
            yield return Shot("Map_Library.png");
        }

        private IEnumerator CheckShrine(string tag)
        {
            yield return WaitSeconds(1f);
            SavePoint shrine = UnityEngine.Object.FindAnyObjectByType<SavePoint>();
            Assert.IsNotNull(shrine, $"{tag} has a save shrine.");
            Assert.IsNotNull(shrine.Label, $"{tag}: the shrine got its floating label.");
            Assert.AreEqual(shrine.ShrineName, shrine.Label.Title.text);

            PlayerUnit hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();
            Vector3 toShrine = shrine.transform.position - hero.transform.position;
            toShrine.y = 0f;
            Vector3 spot = shrine.transform.position - toShrine.normalized * 2.2f;
            PlayerExplorationMovement mover = hero.GetComponent<PlayerExplorationMovement>();
            if (mover != null) mover.TeleportTo(spot, Quaternion.LookRotation(toShrine.normalized));
            else hero.transform.position = spot;
            CameraFollow cam = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
            if (cam != null) cam.SnapToTarget();
            yield return Frames(20);
            yield return Shot($"SavePoint_{tag}_Label.png");

            shrine.Interact(hero);
            Assert.IsTrue(shrine.Label.IsShowingSaved, $"{tag}: \"Game Saved\" shows after the save.");
            yield return WaitSeconds(0.6f);
            yield return Shot($"SavePoint_{tag}_GameSaved.png");
            yield return WaitSeconds(SavePointLabel.SavedSeconds);
            Assert.IsFalse(shrine.Label.IsShowingSaved, $"{tag}: the label goes back to its name.");
        }

        #region Helpers

        private IEnumerator StartAdventure(string classAsset)
        {
            LogAssert.ignoreFailingMessages = true;
            DevTools.EncounterDebugOverlay.Visible = false;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            yield return Frames(10);
#if UNITY_EDITOR
            CharacterClassSO cls = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{classAsset}.asset");
#else
            CharacterClassSO cls = null;
#endif
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsTrue(menu != null && cls != null, "Main menu and the class load.");
            menu.SelectCharacterClass(cls);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            yield return Frames(10);
        }

        // Renders the main camera with the overlay canvases moved onto it for one frame, so the UI shows
        private IEnumerator Shot(string file)
        {
            if (string.IsNullOrEmpty(shots)) yield break;
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) yield break;

            List<Canvas> moved = new List<Canvas>();
            foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.1f;
                moved.Add(c);
            }
            Canvas.ForceUpdateCanvases();

            const int w = 1600, h = 900;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(shots, file), tex.EncodeToPNG());
            cam.targetTexture = previous;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);

            foreach (Canvas c in moved)
            {
                if (c != null) c.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        private static IEnumerator WaitForLoad()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            yield return Frames(10);
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        #endregion
    }
}
