using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.DevTools;
using CastleOfTheD20.Economy;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Editor Test Start (Vili, 2026-10-08): the game boots in the village and continues a test save straight into
    /// the chosen zone and spot with the chosen hero, level and gear; the real save slot stays as it was. With
    /// TESTSTART_SHOTS_DIR set, saves a screenshot of each arrival.
    /// </summary>
    public class EditorTestStartPlayTests
    {
        private static readonly string[] SavedKeys = { "CastleOfDice_SaveData", "CastleOfDice_MusicVolume", "CastleOfDice_SfxVolume" };
        private readonly string[] backup = new string[3];
        private readonly bool[] had = new bool[3];

        [SetUp]
        public void BackupSave()
        {
            for (int i = 0; i < SavedKeys.Length; i++)
            {
                had[i] = PlayerPrefs.HasKey(SavedKeys[i]);
                if (!had[i]) continue;
                backup[i] = i == 0 ? PlayerPrefs.GetString(SavedKeys[i]) : PlayerPrefs.GetFloat(SavedKeys[i]).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        [TearDown]
        public void RestoreSave()
        {
            EditorTestStart.End();
            Time.timeScale = 1f;
            for (int i = 0; i < SavedKeys.Length; i++)
            {
                if (!had[i]) PlayerPrefs.DeleteKey(SavedKeys[i]);
                else if (i == 0) PlayerPrefs.SetString(SavedKeys[i], backup[i]);
                else PlayerPrefs.SetFloat(SavedKeys[i], float.Parse(backup[i], System.Globalization.CultureInfo.InvariantCulture));
            }
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Mage_Level4_InTheLibrary_WithGear()
        {
            var setup = new EditorTestStartSetup
            {
                scene = "Zone_4_Library",
                spawn = "Spawn_From_Hall",
                hero = CharacterClassType.Mage,
                level = 4,
                levelPicks = new[] { EditorTestStartSetup.PickResilience, 0, EditorTestStartSetup.PickAttribute, EditorTestStartSetup.PickResilience },
                gold = 321,
                runesOfFate = 3,
                bladeTier = 2,
                armorTier = 1,
                firstBattleWon = true,
                commanderDefeated = true,
            };
            setup.itemIds.Add("potion_health_small");
            setup.itemCounts.Add(4);
            setup.itemIds.Add("scroll_embers");
            setup.itemCounts.Add(2);

            yield return Run(setup, "TestStart_Mage_L4_Library.png");

            PlayerUnit hero = Hero();
            Assert.AreEqual(CharacterClassType.Mage, hero.CharacterClass.ClassType);
            Assert.AreEqual(4, hero.Level);
            Assert.AreEqual(5, hero.MaxHPBonus, "one Hero's Resilience pick");
            Assert.AreEqual(1, hero.AttributeBonusModifier);
            Assert.IsTrue(hero.ActiveAbilities[0].IsRank2, "level 3 pick: Rank 2 of the first ability");
            Assert.AreEqual(5, hero.ActiveAbilities.Count, "level 4 adds the fifth ability");
            Assert.AreEqual(2, hero.WeaponDamageBonus);
            Assert.AreEqual(1, hero.ArmorClassBonus);
            Assert.AreEqual(hero.MaxHP, hero.CurrentHP, "full health");
            Assert.AreEqual(321, InventoryManager.Instance.CurrentGold);
            Assert.AreEqual(3, InventoryManager.Instance.RerollScrollCount);
            Assert.AreEqual(4, InventoryManager.Instance.GetItemCount(InventoryManager.Instance.FindItemByID("potion_health_small")));
            Assert.AreEqual(2, InventoryManager.Instance.GetItemCount(InventoryManager.Instance.FindItemByID("scroll_embers")));
            Assert.IsTrue(StoryFlags.Has(PlayerProgressionManager.FirstVictoryFlag));
            Assert.IsTrue(GameManager.Instance.IsCommanderDefeated);
            AssertNear("Spawn_From_Hall");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Rogue_Level5_InTheTowerMirrorRoom()
        {
            var setup = new EditorTestStartSetup
            {
                scene = "Zone_6_Tower",
                spawn = EditorTestStartSetup.MirrorRoomSpawn,
                hero = CharacterClassType.Rogue,
                level = 5,
                gold = 999,
            };

            yield return Run(setup, "TestStart_Rogue_L5_MirrorRoom.png");

            PlayerUnit hero = Hero();
            Assert.AreEqual(CharacterClassType.Rogue, hero.CharacterClass.ClassType);
            Assert.AreEqual(5, hero.Level);
            Assert.AreEqual(20, hero.MaxHPBonus, "four Hero's Resilience picks");
            Assert.IsTrue(TowerMirrorRoom.IsOpen, "the mirror room is open");
            MirrorTwinEncounter encounter = UnityEngine.Object.FindAnyObjectByType<MirrorTwinEncounter>(FindObjectsInactive.Include);
            Assert.IsNotNull(encounter, "the Mirror Twin waits in the room");
            Vector3 front = encounter.Mirror.transform.position + encounter.Mirror.WorldNormal * 2f;
            float toMirror = Flat(hero.transform.position - front);
            Debug.Log($"[EditorTestStartPlayTests] hero {toMirror:F1} m from the point in front of the mirror (trigger {encounter.TriggerDistance} m)");
            Assert.Greater(toMirror, encounter.TriggerDistance, "the hero arrives outside the twin's trigger range");
            Assert.Less(toMirror, encounter.TriggerDistance + 6f, "the hero arrives in the mirror room");
            Assert.IsFalse(encounter.IsPlaying, "the twin's cutscene waits for the hero to walk up");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Warrior_InTheVillage_OutsideTheTavern()
        {
            var setup = new EditorTestStartSetup
            {
                scene = TavernEntrance.VillageSceneName,
                spawn = TavernEntrance.SpawnInVillage,
                hero = CharacterClassType.Warrior,
                level = 2,
                gold = 75,
            };

            yield return Run(setup, "TestStart_Warrior_L2_Village.png");

            Assert.AreEqual(CharacterClassType.Warrior, Hero().CharacterClass.ClassType);
            Assert.AreEqual(2, Hero().Level);
            Assert.AreEqual(75, InventoryManager.Instance.CurrentGold);
            Assert.IsFalse(UI.MainMenuController.Instance.IsMenuOpen, "the main menu is out of the way");
            AssertNear(TavernEntrance.SpawnInVillage);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Warrior_InTheTavern()
        {
            var setup = new EditorTestStartSetup { scene = TavernEntrance.SceneName, hero = CharacterClassType.Warrior, level = 3 };

            yield return Run(setup, "TestStart_Warrior_L3_Tavern.png");

            Assert.AreEqual(GameLocation.Tavern, GameManager.Instance.CurrentLocation);
            Assert.AreEqual(3, Hero().Level);
        }

        private IEnumerator Run(EditorTestStartSetup setup, string shotName)
        {
            LogAssert.ignoreFailingMessages = true;
            string realBefore = PlayerPrefs.HasKey(SavedKeys[0]) ? PlayerPrefs.GetString(SavedKeys[0]) : null;

            SceneManager.LoadScene(TavernEntrance.VillageSceneName);
            yield return null;
            EditorTestStart.Begin(JsonUtility.ToJson(setup.BuildSave()), setup.spawn);

            float until = Time.realtimeSinceStartup + 90f;
            while (!EditorTestStart.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(EditorTestStart.IsReady, "the test start finished");
            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(setup.scene, SceneManager.GetActiveScene().name, "the hero is in the chosen zone");
            Assert.AreEqual(GamePlayMode.Exploration, GameManager.Instance.CurrentMode);

            // Saving during a test start writes the test slot only
            SaveSystem.SaveGame(Data.PlayerDataSO.Session, Hero());
            Assert.IsTrue(PlayerPrefs.HasKey(EditorTestStart.TestSaveKey));
            string realAfter = PlayerPrefs.HasKey(SavedKeys[0]) ? PlayerPrefs.GetString(SavedKeys[0]) : null;
            Assert.AreEqual(realBefore, realAfter, "the real save slot is untouched");

            string shots = Environment.GetEnvironmentVariable("TESTSTART_SHOTS_DIR");
            if (!string.IsNullOrEmpty(shots))
            {
                Directory.CreateDirectory(shots);
                yield return Shot(Path.Combine(shots, shotName));
            }
        }

        private static void AssertNear(string spawnName)
        {
            GameObject spawn = GameObject.Find(spawnName);
            Assert.IsNotNull(spawn, spawnName);
            Assert.Less(Flat(Hero().transform.position - spawn.transform.position), 3f, "the hero stands at " + spawnName);
        }

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>The game camera with the HUD (overlay canvases drawn onto it for one frame).</summary>
        private static IEnumerator Shot(string file)
        {
            yield return null;
            Camera cam = Camera.main;
            if (cam == null)
            {
                CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                cam = follow != null ? follow.GetComponent<Camera>() : UnityEngine.Object.FindAnyObjectByType<Camera>();
            }
            if (cam == null) yield break;
            DevTools.EncounterDebugOverlay.Visible = false;

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var overlay = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in canvases)
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                overlay.Add(c);
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
            }
            Canvas.ForceUpdateCanvases();

            const int w = 1600, h = 900;
            RenderTexture rt = new RenderTexture(w, h, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = prev;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);

            foreach (Canvas c in overlay)
            {
                if (c != null) c.renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
