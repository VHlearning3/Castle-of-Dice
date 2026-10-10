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
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-10: the dialogue box showed a gold hooded placeholder for everyone. Each speaker now has a
    /// face rendered from their own model. Talks to Baldur, Othelia, Pip, the ghost and the Cursed Commander and
    /// checks the portrait slot shows that speaker's face. With PORTRAIT_SHOTS_DIR set, saves screenshots.
    /// </summary>
    public class DialoguePortraitPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private float? musicBackup;
        private float? sfxBackup;
        private string shots;

        // The editor's PlayerPrefs are shared with the main project: New Adventure clears the save slot
        [SetUp]
        public void BackupPrefs()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey) : (float?)null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey) : (float?)null;
            shots = Environment.GetEnvironmentVariable("PORTRAIT_SHOTS_DIR");
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue) DialogueController.Instance.EndDialogue();
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (musicBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.MusicVolumeKey, musicBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.MusicVolumeKey);
            if (sfxBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.SfxVolumeKey, sfxBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.SfxVolumeKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator Speakers_ShowTheirOwnFace_InThePortraitSlot()
        {
            yield return StartAdventure("Character_Rogue_Corvo");
            PlayerUnit hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

            VillageNPC baldur = null, othelia = null;
            foreach (VillageNPC npc in UnityEngine.Object.FindObjectsByType<VillageNPC>(FindObjectsSortMode.None))
            {
                if (npc.IsBlacksmith) baldur = npc;
                else if (npc.NpcName != null && npc.NpcName.Contains("Othelia")) othelia = npc;
            }
            Assert.IsNotNull(baldur, "Baldur stands in Oakhaven.");
            Assert.IsNotNull(othelia, "Othelia stands in Oakhaven.");

            baldur.Interact(hero);
            yield return ExpectFace("Baldur", "Portrait_Baldur.png");
            DialogueController.Instance.EndDialogue();
            yield return Frames(10);

            othelia.Interact(hero);
            yield return ExpectFace("Othelia", "Portrait_Othelia.png");
            DialogueController.Instance.EndDialogue();
            yield return Frames(10);

            SceneLoader.Instance.LoadScene("Zone_5_CastleHall");
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();
            foreach (ScriptedNpc npc in UnityEngine.Object.FindObjectsByType<ScriptedNpc>(FindObjectsSortMode.None))
            {
                npc.Interact(hero);
                yield return ExpectFace(npc.NpcKind == ScriptedNpc.Kind.TrappedMerchant ? "Pip" : "GhostAldric",
                    npc.NpcKind == ScriptedNpc.Kind.TrappedMerchant ? "Portrait_Pip.png" : "Portrait_GhostAldric.png");
                DialogueController.Instance.EndDialogue();
                yield return Frames(10);
            }

            SceneLoader.Instance.LoadScene("Zone_3_CastleCourtyard");
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            DungeonRoomController room = null;
            foreach (DungeonRoomController r in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                if (r.bossIdentifier == BossChoices.CommanderBossId) room = r;
            }
            Assert.IsNotNull(room, "The Courtyard has the Commander's room.");
            room.BeginEncounter(UnityEngine.Object.FindAnyObjectByType<PlayerUnit>());
            yield return Frames(30);
            yield return ExpectFace("CursedCommander", "Portrait_CursedCommander.png");
        }

        private IEnumerator ExpectFace(string key, string file)
        {
            yield return Frames(20);
            Assert.IsTrue(DialogueController.Instance.IsInDialogue, $"{key} starts talking.");
            yield return WaitSeconds(2.5f);
            Image portrait = PortraitImage();
            Assert.IsNotNull(portrait, "The dialogue box has its portrait slot.");
            Assert.IsNotNull(portrait.sprite, "The slot shows a picture.");
            Assert.AreEqual(key, portrait.sprite.name, $"{DialogueController.Instance.CurrentNode.SpeakerName} shows their own face.");
            yield return Shot(file);
        }

        private static Image PortraitImage()
        {
            DialogueUIController ui = DialogueUIController.Instance;
            if (ui == null) return null;
            foreach (Image img in ui.GetComponentsInChildren<Image>(false))
            {
                if (img.name == "Speaker_Portrait") return img;
            }
            return null;
        }

        private IEnumerator StartAdventure(string classAsset)
        {
            LogAssert.ignoreFailingMessages = true;
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
    }
}
