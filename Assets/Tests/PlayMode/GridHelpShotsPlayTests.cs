using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
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
    /// Vili, 2026-10-07: the How to Play page ran under its Close button, and the combat grid covered the floor
    /// with opaque tiles. With GRID_SHOTS_DIR set, saves review screenshots of both (and GRID_SHOTS_PREFIX names them).
    /// </summary>
    public class GridHelpShotsPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private float? musicBackup;
        private float? sfxBackup;
        private string shots;
        private string prefix;

        [SetUp]
        public void BackupPrefs()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey) : (float?)null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey) : (float?)null;
            shots = Environment.GetEnvironmentVariable("GRID_SHOTS_DIR");
            prefix = Environment.GetEnvironmentVariable("GRID_SHOTS_PREFIX") ?? string.Empty;
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        }

        [TearDown]
        public void RestorePrefs()
        {
            Time.timeScale = 1f;
            StoryPanelUI.CloseIfOpen();
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (musicBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.MusicVolumeKey, musicBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.MusicVolumeKey);
            if (sfxBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.SfxVolumeKey, sfxBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.SfxVolumeKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HowToPlay_AllTextAboveTheCloseButton()
        {
            yield return StartAdventure();
            PauseMenuUI.Instance.Open();
            yield return null;
            StoryPanelUI.Show("How to Play", PauseMenuUI.HelpText);
            yield return null;
            yield return null;
            yield return Shot("howtoplay.png", true);

            Transform panel = UnityEngine.Object.FindAnyObjectByType<StoryPanelUI>().transform.Find("StoryPanel");
            TextMeshProUGUI body = panel.Find("Body").GetComponent<TextMeshProUGUI>();
            RectTransform close = (RectTransform)panel.Find("Next_Btn");
            body.ForceMeshUpdate();
            Assert.IsFalse(body.isTextOverflowing, "The whole help text fits its box.");
            Assert.Less(Top(close), Bottom((RectTransform)body.transform) + 0.01f, "The Close button sits below the text, not over it.");
            float textBottom = body.transform.TransformPoint(new Vector3(0f, body.textBounds.min.y, 0f)).y;
            Assert.Greater(textBottom, Top(close) - 0.01f, "The last line (Saving) ends above the Close button.");

            StoryPanelUI.CloseIfOpen();
            PauseMenuUI.Instance.Resume();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ThroneRoomGrid_FloorShowsThrough()
        {
            yield return StartAdventure();
            SceneLoader.Instance.LoadScene("Zone_7_ThroneRoom");
            yield return null;
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();

            DungeonRoomController king = null;
            foreach (DungeonRoomController room in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                if (room.IsKingRoom) king = room;
            }
            Assert.IsNotNull(king, "The Gargoyle King's arena.");
            king.TriggerEncounter();
            float until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until && (TurnManager.Instance == null || !TurnManager.Instance.IsCombatActive)) yield return null;
            Assert.IsTrue(TurnManager.Instance != null && TurnManager.Instance.IsCombatActive, "The King's fight starts.");

            // Wait for the hero's turn, when the move range lights up
            until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until && !AnyReachable()) yield return null;
            DialogueSkip();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("throne_grid.png", false);

            CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
            if (follow != null)
            {
                follow.SetZoom(0.25f, true);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("throne_grid_close.png", false);
            }

            GridTile sample = null;
            foreach (GridTile tile in UnityEngine.Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None))
            {
                Renderer r = tile.GetComponentInChildren<Renderer>();
                if (r != null && r.enabled) { sample = tile; break; }
            }
            Assert.IsNotNull(sample, "The arena has visible tiles.");
            Assert.GreaterOrEqual(sample.GetComponentInChildren<Renderer>().sharedMaterial.renderQueue, 3000, "Tiles draw see-through over the floor.");
        }

        #region Helpers

        private static bool AnyReachable()
        {
            foreach (GridTile tile in UnityEngine.Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None))
            {
                if (tile.CurrentHighlight == TileHighlightType.Reachable) return true;
            }
            return false;
        }

        private static void DialogueSkip()
        {
            if (Dialogue.DialogueController.Instance != null && Dialogue.DialogueController.Instance.IsInDialogue)
            {
                Dialogue.DialogueController.Instance.EndDialogue();
            }
        }

        private IEnumerator Shot(string file, bool withUi)
        {
            if (string.IsNullOrEmpty(shots)) yield break;
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) yield break;

            const int w = 1920, h = 1080;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;

            // Overlay canvases are not in Camera.Render(): draw them through the camera for this shot
            var switched = new List<Canvas>();
            if (withUi)
            {
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1f + (1000 - Mathf.Clamp(canvas.sortingOrder, 0, 999)) * 0.001f;
                    switched.Add(canvas);
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(shots, prefix + file), tex.EncodeToPNG());
            cam.targetTexture = previous;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);

            foreach (Canvas canvas in switched)
            {
                if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }

        private static IEnumerator StartAdventure()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            for (int i = 0; i < 10; i++) yield return null;
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            CharacterClassSO cls = LoadClass("Character_Warrior_SirRoland");
            Assert.IsTrue(menu != null && cls != null, "Main menu and the warrior class load.");
            menu.SelectCharacterClass(cls);
            for (int i = 0; i < 5; i++) yield return null;
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            yield return null;
        }

        private static IEnumerator WaitForLoad()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
        }

        private static float Bottom(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[0].y;
        }

        private static float Top(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1].y;
        }

        private static CharacterClassSO LoadClass(string assetName)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{assetName}.asset");
#else
            return null;
#endif
        }

        #endregion
    }
}
