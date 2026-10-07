using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
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
    /// Vili, 2026-10-07: long dialogue choices were cut off with "...", the Cursed Commander's checks came back
    /// after his answer, Baldur's shop choice did nothing the first time, and the knight and the mage could
    /// reach the Rogue's Hidden Trail through the Library's side door. Each test starts a New Adventure.
    /// With DIALOGUE_SHOTS_DIR set, saves review screenshots (UI included).
    /// </summary>
    public class DialogueShopDoorPlayTests
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
            shots = Environment.GetEnvironmentVariable("DIALOGUE_SHOTS_DIR");
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue) DialogueController.Instance.EndDialogue();
            ShopUIController shop = UnityEngine.Object.FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
            if (shop != null && shop.gameObject.activeInHierarchy) shop.CloseShop();
            DiceSystem.ResetRandom();

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
        public IEnumerator CommanderDialogue_WrapsLongChoices_AndDoesNotRepeatTheCheck()
        {
            yield return StartAdventure("Character_Warrior_SirRoland");
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
            PlayerUnit hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();
            room.BeginEncounter(hero);
            yield return Frames(30);
            Assert.IsTrue(DialogueController.Instance.IsInDialogue, "The Commander greets the hero first.");

            yield return WaitSeconds(3f);
            List<Button> options = ActiveOptionButtons();
            yield return Shot("Commander_Intro_Choices.png");

            Button duel = Find(options, "[Warrior]");
            Assert.IsNotNull(duel, "The knight gets the duel choice.");
            TMP_Text duelText = duel.GetComponentInChildren<TMP_Text>();
            duelText.ForceMeshUpdate();
            Assert.IsFalse(duelText.isTextTruncated, "The whole duel choice is readable, not cut off with '...'.");
            Assert.Greater(((RectTransform)duel.transform).rect.height, 50f, "The long choice wraps onto a second line and its button grows.");
            foreach (Button b in options)
            {
                TMP_Text t = b.GetComponentInChildren<TMP_Text>();
                t.ForceMeshUpdate();
                Assert.IsFalse(t.isTextTruncated, $"Choice cut off: {t.text}");
            }

            // Win the honour duel check: a seed whose first d20 is high
            int seed = 0;
            for (int s = 1; s < 500; s++)
            {
                DiceSystem.SetSeed(s);
                if (DiceSystem.RollD20Raw() >= 19) { seed = s; break; }
            }
            DiceSystem.SetSeed(seed);
            duel.onClick.Invoke();

            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                DialogueNodeSO node = DialogueController.Instance.CurrentNode;
                if (node != null && node.DialogueText.StartsWith("A duel")) break;
                yield return null;
            }
            Assert.IsTrue(DialogueController.Instance.CurrentNode != null && DialogueController.Instance.CurrentNode.DialogueText.StartsWith("A duel"),
                "The Commander accepts the duel.");
            yield return Frames(20);
            yield return WaitSeconds(2f);
            yield return Shot("Commander_DuelAccepted_Choices.png");

            List<Button> after = ActiveOptionButtons();
            Assert.IsNull(Find(after, "Check"), "The checks are not offered again after the Commander's answer.");
            Assert.IsNull(Find(after, "[Warrior]"), "The duel choice is not offered again.");
            Assert.AreEqual(1, after.Count, "Only [Continue] is left.");
            StringAssert.Contains("Continue", after[0].GetComponentInChildren<TMP_Text>().text);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Baldur_ShopChoice_OpensTheShopTheFirstTime()
        {
            yield return StartAdventure("Character_Warrior_SirRoland");
            PlayerUnit hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

            VillageNPC baldur = null;
            foreach (VillageNPC npc in UnityEngine.Object.FindObjectsByType<VillageNPC>(FindObjectsSortMode.None))
            {
                if (npc.IsBlacksmith) baldur = npc;
            }
            Assert.IsNotNull(baldur, "Baldur stands in Oakhaven.");
            ShopUIController shop = UnityEngine.Object.FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
            Assert.IsNotNull(shop, "Zone 1 has the shop window.");
            Assert.IsFalse(shop.gameObject.activeInHierarchy, "The shop starts closed.");

            baldur.Interact(hero);
            yield return Frames(20);
            Assert.IsTrue(DialogueController.Instance.IsInDialogue, "Baldur talks first.");
            yield return Shot("Baldur_Greeting.png");

            Button shopChoice = Find(ActiveOptionButtons(), "[Blacksmith]");
            Assert.IsNotNull(shopChoice, "Baldur's greeting offers his shop.");
            shopChoice.onClick.Invoke();
            yield return Frames(20);

            yield return Shot("Baldur_ShopChoice_FirstClick.png");
            Assert.IsFalse(DialogueController.Instance.IsInDialogue, "The conversation closes.");
            Assert.IsTrue(shop.gameObject.activeInHierarchy, "The very first shop choice opens Baldur's shop.");
            Assert.AreEqual(GamePlayMode.Shop, GameManager.Instance.CurrentMode);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LibrarySideDoor_IsLockedForTheKnight()
        {
            yield return StartAdventure("Character_Warrior_SirRoland");
            yield return GoToLibrarySideDoor();
            DoorTeleporter door = SideDoor();
            PlayerUnit hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

            door.PerformTeleport(hero.gameObject);
            yield return Frames(5);
            yield return Shot("Library_SideDoor_Knight.png");

            Assert.IsFalse(SceneLoader.Instance.IsLoading, "The knight cannot go through the side door.");
            Assert.AreEqual("Zone_4_Library", SceneManager.GetActiveScene().name);
            Assert.IsTrue(ToastShowing("Only a rogue could slip through"), "The locked door says why.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LibrarySideDoor_StillOpensForTheRogue_BothWays()
        {
            yield return StartAdventure("Character_Rogue_Corvo");
            yield return GoToLibrarySideDoor();
            PlayerUnit hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

            SideDoor().PerformTeleport(hero.gameObject);
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            Assert.AreEqual("Zone_2.5_SecretForestPath", SceneManager.GetActiveScene().name, "The rogue slips onto the Hidden Trail.");
            yield return Shot("HiddenTrail_Rogue_FromLibrary.png");

            DoorTeleporter back = null;
            foreach (DoorTeleporter d in UnityEngine.Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None))
            {
                if (d.TargetSceneName == "Zone_4_Library") back = d;
            }
            Assert.IsNotNull(back, "The trail has its door back to the Library.");
            yield return WaitSeconds(1.5f);
            hero = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();
            back.PerformTeleport(hero.gameObject);
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual("Zone_4_Library", SceneManager.GetActiveScene().name, "And back into the Library.");
        }

        #region Helpers

        private IEnumerator GoToLibrarySideDoor()
        {
            SceneLoader.Instance.LoadScene("Zone_4_Library", SecretForestPath.LibrarySpawnFromTrail);
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            Assert.AreEqual("Zone_4_Library", SceneManager.GetActiveScene().name);
            yield return WaitSeconds(1.5f);
        }

        private static DoorTeleporter SideDoor()
        {
            foreach (DoorTeleporter d in UnityEngine.Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None))
            {
                if (SecretForestPath.IsScene(d.TargetSceneName)) return d;
            }
            Assert.Fail("The Library has its secret side door.");
            return null;
        }

        private static bool ToastShowing(string fragment)
        {
            foreach (TMP_Text t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (t.isActiveAndEnabled && t.text != null && t.text.Contains(fragment)) return true;
            }
            return false;
        }

        private static List<Button> ActiveOptionButtons()
        {
            List<Button> result = new List<Button>();
            DialogueUIController ui = DialogueUIController.Instance;
            if (ui == null) return result;
            foreach (Button b in ui.GetComponentsInChildren<Button>(false))
            {
                TMP_Text t = b.GetComponentInChildren<TMP_Text>();
                if (t == null || t.text.StartsWith("Continue >")) continue;
                result.Add(b);
            }
            return result;
        }

        private static Button Find(List<Button> buttons, string fragment)
        {
            foreach (Button b in buttons)
            {
                TMP_Text t = b.GetComponentInChildren<TMP_Text>();
                if (t != null && t.text.Contains(fragment)) return b;
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

        #endregion
    }
}
