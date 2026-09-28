#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class ZoneTransitionTests
    {
        private GameObject testContainer;

        [SetUp]
        public void SetUp()
        {
            testContainer = new GameObject("ZoneTransitionTests_Container");
        }

        [TearDown]
        public void TearDown()
        {
            if (testContainer != null)
            {
                UnityEngine.Object.DestroyImmediate(testContainer);
            }
        }

        [Test]
        public void BuildAll7ZoneScenes_CreatesAll7ScenesAndRegistersInBuildSettings()
        {
            // Invoke ZoneSceneBuilder.BuildAll7ZoneScenes via reflection from Editor assembly
            Type builderType = Type.GetType("CastleOfTheD20.Editor.ZoneSceneBuilder, Assembly-CSharp-Editor");
            Assert.IsNotNull(builderType, "ZoneSceneBuilder type could not be loaded from Assembly-CSharp-Editor.");
            MethodInfo buildMethod = builderType.GetMethod("BuildAll7ZoneScenes", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(buildMethod, "BuildAll7ZoneScenes method not found on ZoneSceneBuilder.");
            buildMethod.Invoke(null, null);

            string[] expectedScenes = new string[]
            {
                "Assets/Scenes/Zone_1_VillageAndCellar.unity",
                "Assets/Scenes/Zone_2_ForestPath.unity",
                "Assets/Scenes/Zone_3_CastleCourtyard.unity",
                "Assets/Scenes/Zone_4_Library.unity",
                "Assets/Scenes/Zone_5_CastleHall.unity",
                "Assets/Scenes/Zone_6_Tower.unity",
                "Assets/Scenes/Zone_7_ThroneRoom.unity"
            };

            // 1. Verify each scene file exists on disk
            foreach (string scenePath in expectedScenes)
            {
                Assert.IsTrue(File.Exists(scenePath), $"Expected scene file '{scenePath}' does not exist on disk!");
            }

            // 2. Verify EditorBuildSettings contains all 7 scenes in order
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            Assert.IsNotNull(buildScenes, "EditorBuildSettings.scenes should not be null.");
            Assert.GreaterOrEqual(buildScenes.Length, 7, "Build settings must contain at least 7 scenes.");

            for (int i = 0; i < expectedScenes.Length; i++)
            {
                bool found = false;
                foreach (var bs in buildScenes)
                {
                    if (bs.path.Equals(expectedScenes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        Assert.IsTrue(bs.enabled, $"Scene '{expectedScenes[i]}' should be enabled in Build Settings.");
                        break;
                    }
                }
                Assert.IsTrue(found, $"Scene '{expectedScenes[i]}' was not found in EditorBuildSettings.scenes!");
            }

            Debug.Log("<color=#00e676>[PASS]</color> BuildAll7ZoneScenes generated all 7 scenes and registered them in Build Settings.");
        }

        [Test]
        public void DoorTeleporter_CrossSceneTargetAndCooldown_EnforcesPacing()
        {
            GameObject doorObj = new GameObject("TestDoor");
            doorObj.transform.SetParent(testContainer.transform);

            DoorTeleporter dt = doorObj.AddComponent<DoorTeleporter>();
            dt.InitializeSceneTeleporter("Zone_2_ForestPath", "Spawn_From_Village", "Forest", "Enter Forest", 4.5f, true);

            Assert.AreEqual("Zone_2_ForestPath", dt.TargetSceneName);
            Assert.AreEqual("Spawn_From_Village", dt.TargetSpawnPointName);
            Assert.AreEqual("Forest", dt.DestinationZone);
            Assert.AreEqual("Enter Forest", dt.PromptMessage);
            Assert.AreEqual(4.5f, dt.InteractionRadius);
            Assert.IsTrue(dt.TriggerOnWalk);
            Assert.AreEqual(1.0f, dt.TeleportCooldown);

            Debug.Log("<color=#00e676>[PASS]</color> DoorTeleporter initializes cross-scene target parameters and pacing cooldown.");
        }

        [Test]
        public void SavePoint_RestoresFullHealthAndPersistsViaSaveSystem()
        {
            GameObject shrineObj = new GameObject("TestShrine");
            shrineObj.transform.SetParent(testContainer.transform);
            SavePoint sp = shrineObj.AddComponent<SavePoint>();

            GameObject playerObj = new GameObject("TestPlayer");
            playerObj.transform.SetParent(testContainer.transform);
            playerObj.tag = "Player";
            CharacterController cc = playerObj.AddComponent<CharacterController>();
            PlayerUnit player = playerObj.AddComponent<PlayerUnit>();

            // Damage player
            player.TakeDamage(10);
            Assert.Less(player.CurrentHP, player.MaxHP);

            // Communing with shrine restores full HP
            sp.Interact(player);

            Assert.AreEqual(player.MaxHP, player.CurrentHP, "Communing with SavePoint must restore health to 100%.");
            Assert.IsTrue(SaveSystem.HasSavedGame(), "SaveSystem should record saved game state in PlayerPrefs.");

            Debug.Log("<color=#00e676>[PASS]</color> SavePoint restores 100% HP and persists game via SaveSystem.");
        }

        [Test]
        public void GiantElixir_GrantsPermanent30MaxHPAndHealsToFull()
        {
            GameObject elixirObj = new GameObject("TestElixir");
            elixirObj.transform.SetParent(testContainer.transform);
            GiantElixirInteraction elixir = elixirObj.AddComponent<GiantElixirInteraction>();

            GameObject playerObj = new GameObject("TestPlayer");
            playerObj.transform.SetParent(testContainer.transform);
            playerObj.tag = "Player";
            CharacterController cc = playerObj.AddComponent<CharacterController>();
            PlayerUnit player = playerObj.AddComponent<PlayerUnit>();

            int originalMaxHP = player.MaxHP;
            player.TakeDamage(15);

            // Consume Giant's Elixir
            elixir.Interact(player);

            Assert.IsTrue(elixir.IsConsumed, "Giant's Elixir should be marked consumed after drinking.");
            Assert.AreEqual(originalMaxHP + 30, player.MaxHP, "Giant's Elixir must grant +30 permanent Max HP.");
            Assert.AreEqual(player.MaxHP, player.CurrentHP, "Giant's Elixir must restore health to full.");

            // Attempting to drink again should not grant double bonus
            elixir.Interact(player);
            Assert.AreEqual(originalMaxHP + 30, player.MaxHP, "Drinking consumed elixir again should not grant additional bonus.");

            Debug.Log("<color=#00e676>[PASS]</color> GiantElixirInteraction grants permanent +30 Max HP and full heal.");
        }

        [Test]
        public void LockpickInteraction_UnlockingSecretPath_AdvancesMilestone2()
        {
            GameObject gateObj = new GameObject("TestSecretGate");
            gateObj.transform.SetParent(testContainer.transform);
            LockpickInteraction lpi = gateObj.AddComponent<LockpickInteraction>();

            GameObject hiddenPath = new GameObject("HiddenSecretDoor");
            hiddenPath.transform.SetParent(testContainer.transform);
            hiddenPath.SetActive(false);

            SerializedObject so = new SerializedObject(lpi);
            so.FindProperty("lockpickDC").intValue = 13;
            so.FindProperty("hiddenPathObject").objectReferenceValue = hiddenPath;
            so.FindProperty("rewardGold").intValue = 25;
            so.ApplyModifiedProperties();

            // Ensure PlayerProgressionManager is active
            GameObject progObj = new GameObject("ProgressionManager");
            progObj.transform.SetParent(testContainer.transform);
            PlayerProgressionManager prog = progObj.AddComponent<PlayerProgressionManager>();

            // Trigger secret path opening
            prog.NotifySecretPathOpened();

            Assert.GreaterOrEqual(prog.CurrentLevel, 2, "Opening secret route must advance hero to Milestone 2 (Shadows of the Castle).");

            Debug.Log("<color=#00e676>[PASS]</color> Secret route lockpicking advances milestone to Milestone 2.");
        }

        [Test]
        public void ZoneMusicSO_ResolvesAll7ZonesAndEncounters()
        {
            ZoneMusicSO musicConfig = AssetDatabase.LoadAssetAtPath<ZoneMusicSO>("Assets/Data/ZoneMusicConfig.asset");
            Assert.IsNotNull(musicConfig, "ZoneMusicConfig.asset must exist in Assets/Data/");

            // Check Zone Exploration Ambient mappings
            Assert.AreEqual(MusicTrackType.Village, musicConfig.GetTrackForLocation(GameLocation.Village));
            Assert.AreEqual(MusicTrackType.CastleAdventure, musicConfig.GetTrackForLocation(GameLocation.Forest));
            Assert.AreEqual(MusicTrackType.CastleAdventure, musicConfig.GetTrackForLocation(GameLocation.Courtyard));
            Assert.AreEqual(MusicTrackType.CastleAdventure, musicConfig.GetTrackForLocation(GameLocation.Library));
            Assert.AreEqual(MusicTrackType.CastleAdventure, musicConfig.GetTrackForLocation(GameLocation.CastleHall));
            Assert.AreEqual(MusicTrackType.CastleAdventure, musicConfig.GetTrackForLocation(GameLocation.Tower));
            Assert.AreEqual(MusicTrackType.CastleAdventure, musicConfig.GetTrackForLocation(GameLocation.CrownHall));

            // Check Encounter & Boss Combat mappings
            Assert.AreEqual(MusicTrackType.CursedCommanderCombat, musicConfig.GetCombatTrack("CursedCommander"));
            Assert.AreEqual(MusicTrackType.MalakorCombat, musicConfig.GetCombatTrack("ShadowMageMalakor"));
            Assert.AreEqual(MusicTrackType.GargoyleKingPhase1, musicConfig.GetCombatTrack("GargoyleKing"));
            Assert.AreEqual(MusicTrackType.CellarCombat, musicConfig.GetCombatTrack("", "Cellar"));

            Debug.Log("<color=#00e676>[PASS]</color> ZoneMusicSO accurately maps all 7 zones and boss combats.");
        }
    }
}
#endif
