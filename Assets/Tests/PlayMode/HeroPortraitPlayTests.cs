using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-10: Choose Your Hero gets a portrait of each hero above their card, the title moves above
    /// the portraits and the main menu title no longer shows through; Sir Roland's eye slits are plugged.
    /// With HERO_SHOTS_DIR set, saves a screenshot of the screen.
    /// </summary>
    public class HeroPortraitPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private float? musicBackup;
        private float? sfxBackup;

        [SetUp]
        public void BackupPrefs()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey) : (float?)null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey) : (float?)null;
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (musicBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.MusicVolumeKey, musicBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.MusicVolumeKey);
            if (sfxBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.SfxVolumeKey, sfxBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.SfxVolumeKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ChooseYourHero_ShowsPortraitsAboveCards_TitleAbovePortraits()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            for (int i = 0; i < 15; i++) yield return null;

            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "Zone 1 has the main menu.");
            menu.OpenClassSelection();
            for (int i = 0; i < 5; i++) yield return null;

            GameObject modal = GameObject.Find("ClassSelectionModal");
            Assert.IsNotNull(modal, "Choose Your Hero is open.");
            Assert.AreEqual(1f, modal.GetComponent<Image>().color.a, 0.001f, "The screen is opaque, so the menu title can't show through.");
            RectTransform title = (RectTransform)modal.transform.Find("CS_Title");

            foreach (string card in new[] { "Warrior_Card", "Mage_Card", "Rogue_Card" })
            {
                RectTransform cardRect = (RectTransform)modal.transform.Find(card);
                Transform portrait = cardRect.Find("Hero_Portrait");
                Assert.IsNotNull(portrait, $"{card} has a portrait.");
                Image img = portrait.GetComponent<Image>();
                Assert.IsNotNull(img.sprite, $"{card}'s portrait shows a picture.");

                Rect p = ScreenRect((RectTransform)portrait);
                Rect c = ScreenRect(cardRect);
                Rect t = ScreenRect(title);
                Assert.Greater(p.yMin, c.yMax - 0.5f, $"{card}'s portrait sits above the card.");
                Assert.Greater(t.yMin, p.yMax - 0.5f, "The title sits above the portraits.");
                Assert.LessOrEqual(t.yMax, Screen.height + 0.5f, "The title stays on screen.");
            }

            yield return Shot("ChooseYourHero.png");
        }

        [Test]
        public void SirRoland_EyeSlits_ArePlugged()
        {
#if UNITY_EDITOR
            GameObject hero = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Players/PlayerHero.prefab");
            HeroClassModels models = hero.GetComponent<HeroClassModels>();
            GameObject knight = (GameObject)new UnityEditor.SerializedObject(models).FindProperty("warriorModel").objectReferenceValue;
            MeshFilter sockets = null;
            foreach (MeshFilter mf in knight.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == "Knight_EyeSockets") sockets = mf;
            Assert.IsNotNull(sockets, "The knight wears the eye sockets.");
            Assert.AreEqual("mixamorig:Head", sockets.transform.parent.name, "They ride on the head bone.");
            Assert.Greater(sockets.sharedMesh.vertexCount, 0, "The sockets have geometry.");
#endif
        }

        private static Rect ScreenRect(RectTransform rt)
        {
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        private static IEnumerator Shot(string file)
        {
            string dir = Environment.GetEnvironmentVariable("HERO_SHOTS_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
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
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
            cam.targetTexture = previous;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            foreach (Canvas c in moved)
                if (c != null) c.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
