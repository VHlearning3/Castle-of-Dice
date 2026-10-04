using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Fixes from the critical review: a lost fight can be fought again, bosses start over on a retry,
    /// the Throne Room gate and the map agree, the expedition map is no longer baked into the village,
    /// the game has an ending, and the browser build pauses in a background tab.
    /// </summary>
    [TestFixture]
    public class ReviewFixesTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            gameManager = Track(new GameObject("ReviewTest_GameManager")).AddComponent<GameManager>();
            SetStatic(typeof(GameManager), "_instance", gameManager);

            GridManager grid = Track(new GameObject("ReviewTest_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            AdventureStats.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (DialogueController.Instance != null)
            {
                DialogueController.Instance.EndDialogue();
                Object.DestroyImmediate(DialogueController.Instance.gameObject);
            }
            foreach (TurnManager tm in Object.FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tm.gameObject);
            }
            SetStaticProperty(typeof(TurnManager), "Instance", null);
            GridManager.Instance = null;
            SetStatic(typeof(GameManager), "_instance", null);

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
            AdventureStats.Reset();
            GameInput.SetExplorationInputEnabled(true);
        }

        #region A1: a lost fight can be fought again

        [Test]
        public void LostFight_ResetEncounter_UnlocksDoors_AndTheFightStartsAgainOnEntry()
        {
            GameObject barrier = Track(new GameObject("Barrier"));
            EnemyUnit enemy = CreateUnit<EnemyUnit>("Skeleton");
            Vector3 start = new Vector3(3.2f, 0f, 3.2f);
            enemy.transform.position = start;

            DungeonRoomController room = Track(new GameObject("Room")).AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.exitBarriers.Add(barrier);
            room.roomEnemies.Add(enemy.gameObject);
            room.generateGridOnCombat = false;
            Invoke(room, "Awake");
            Invoke(room, "Start");

            room.BeginEncounter(null);
            Assert.AreEqual(RoomState.CombatActive, room.CurrentState);
            Assert.IsTrue(barrier.activeSelf, "Doors lock while the fight runs.");

            // The hero falls after the skeleton has moved and been hurt
            enemy.transform.position = new Vector3(-4f, 0f, 1f);
            enemy.TakeDamage(3);

            room.ResetEncounter();

            Assert.AreEqual(RoomState.Unexplored, room.CurrentState, "The room waits for the hero again.");
            Assert.IsFalse(barrier.activeSelf, "Doors open again after a defeat.");
            Assert.IsTrue(room.GetComponent<BoxCollider>().enabled, "The encounter trigger is armed again.");
            Assert.IsFalse(enemy.gameObject.activeSelf, "The hostile waits hidden, as before the first fight.");
            Assert.AreEqual(enemy.MaxHP, enemy.CurrentHP, "The hostile is back at full strength.");
            Assert.AreEqual(start.x, enemy.transform.position.x, 0.001f, "The hostile is back on its starting spot.");
            Assert.AreEqual(start.z, enemy.transform.position.z, 0.001f);

            room.BeginEncounter(null);
            Assert.AreEqual(RoomState.CombatActive, room.CurrentState, "Walking back in starts the fight again.");
            Assert.IsTrue(barrier.activeSelf);

            Invoke(room, "OnDestroy");
        }

        [Test]
        public void ResetEncounter_LeavesAClearedRoomCleared()
        {
            DungeonRoomController room = Track(new GameObject("Room")).AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.generateGridOnCombat = false;
            Invoke(room, "Awake");
            Invoke(room, "Start");

            room.BeginEncounter(null);
            room.OnCombatResolved();
            room.ResetEncounter();

            Assert.IsTrue(room.IsCleared);
            Invoke(room, "OnDestroy");
        }

        #endregion

        #region A2: bosses start over on a retry

        [Test]
        public void GargoyleKing_Revive_ReturnsToTheFirstPhase()
        {
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("Boss_GargoyleKing");
            king.InitializeUnit();
            int firstPhaseAc = king.ArmorClass;

            king.EnterStoneForm();
            Assert.IsTrue(king.IsStoneFormActive);
            Assert.AreEqual(firstPhaseAc + 3, king.ArmorClass);

            king.Die(); // Stone Form's Mana Shield would absorb a killing blow here
            Assert.IsFalse(king.IsAlive);

            king.Revive();

            Assert.IsTrue(king.IsAlive);
            Assert.IsFalse(king.IsStoneFormActive, "A retry faces the first-phase King.");
            Assert.AreEqual(firstPhaseAc, king.ArmorClass, "Stone Form's +3 AC must not carry into the retry.");
            Assert.AreEqual(king.MaxHP, king.CurrentHP);

            // He reaches Stone Form again at half health
            king.TakeDamage(king.MaxHP / 2);
            Assert.IsTrue(king.IsStoneFormActive);
        }

        [Test]
        public void CursedCommander_Revive_SummonsAgain_AndRemovesTheOldSkeleton()
        {
            CursedCommanderBoss commander = CreateUnit<CursedCommanderBoss>("Boss_CursedCommander");
            commander.InitializeUnit();
            commander.MoveToTile(GridManager.Instance.GetTileAt(new Vector2Int(6, 6)));

            commander.TakeDamage(commander.MaxHP / 2 + 1);
            Assert.IsTrue(commander.HasSpawnedAdds);
            EnemyUnit skeleton = FindSummon(commander);
            Assert.IsNotNull(skeleton, "The Commander summons a skeleton at half health.");
            spawned.Add(skeleton.gameObject);

            commander.TakeDamage(commander.MaxHP);
            commander.Revive();

            Assert.IsFalse(commander.HasSpawnedAdds, "Reinforcements come again on the retry.");
            Assert.IsTrue(skeleton == null || !skeleton.gameObject.activeInHierarchy, "The first attempt's skeleton is gone.");
            Assert.AreEqual(commander.MaxHP, commander.CurrentHP);
        }

        [Test]
        public void ShadowMageMalakor_Revive_CastsMirrorImageAgain()
        {
            ShadowMageMalakorBoss malakor = CreateUnit<ShadowMageMalakorBoss>("Boss_Malakor");
            malakor.InitializeUnit();
            malakor.MoveToTile(GridManager.Instance.GetTileAt(new Vector2Int(6, 6)));

            malakor.TakeDamage(1);
            Assert.AreEqual(1, malakor.ActiveDecoys.Count, "The first hit conjures a mirror image.");
            EnemyUnit decoy = malakor.ActiveDecoys[0];
            spawned.Add(decoy.gameObject);

            malakor.TakeDamage(malakor.MaxHP);
            malakor.Revive();

            Assert.IsTrue(decoy == null || !decoy.gameObject.activeInHierarchy, "The first attempt's illusion is gone.");
            Assert.AreEqual(0, malakor.ActiveDecoys.Count);

            malakor.TakeDamage(1);
            Assert.AreEqual(1, malakor.ActiveDecoys.Count, "The retry's first hit conjures a new mirror image.");
            spawned.Add(malakor.ActiveDecoys[0].gameObject);
        }

        [Test]
        public void EnemyRevive_MakesTheEnemyClickableAgain()
        {
            EnemyUnit enemy = CreateUnit<EnemyUnit>("Skeleton");
            BoxCollider col = enemy.gameObject.AddComponent<BoxCollider>();
            col.enabled = false; // as the death clip leaves it

            enemy.TakeDamage(enemy.MaxHP + 1);
            enemy.Revive();

            Assert.IsTrue(col.enabled);
        }

        #endregion

        #region A8: the Throne Room gate and the map agree

        [Test]
        public void ThroneRoom_OpensOnlyAfterBothWingBosses()
        {
            Assert.IsFalse(gameManager.CanEnterLocation(GameLocation.CrownHall));
            Assert.IsTrue(gameManager.CanEnterLocation(GameLocation.CastleHall));

            gameManager.NotifyBossDefeated("CursedCommander");
            Assert.IsFalse(gameManager.CanEnterLocation(GameLocation.CrownHall), "One wing boss is not enough.");

            gameManager.NotifyBossDefeated("ShadowMageMalakor");
            Assert.IsTrue(gameManager.CanEnterLocation(GameLocation.CrownHall));
        }

        [Test]
        public void Map_ShowsTheThroneRoomLockedExactlyWhenTheGateIs()
        {
            GameObject canvasGo = Track(new GameObject("Canvas", typeof(Canvas)));
            DungeonMapUIController map = new GameObject("Map").AddComponent<DungeonMapUIController>();
            map.transform.SetParent(canvasGo.transform, false);
            map.EnsureUIHierarchy();

            gameManager.SetLocation(GameLocation.CastleHall);
            gameManager.NotifyBossDefeated("CursedCommander");
            map.RefreshMapNodes();
            StringAssert.Contains("LOCKED", ThroneStatus(canvasGo), "In the hall with one wing boss down, the gate is still sealed.");

            gameManager.NotifyBossDefeated("ShadowMageMalakor");
            map.RefreshMapNodes();
            StringAssert.DoesNotContain("LOCKED", ThroneStatus(canvasGo));
            StringAssert.Contains("FINAL BOSS", ThroneStatus(canvasGo));
        }

        [Test]
        public void SealedGate_DoesNotTeleport()
        {
            DoorTeleporter door = Track(new GameObject("Door_Hall_ThroneRoom")).AddComponent<DoorTeleporter>();
            Transform landing = Track(new GameObject("Landing")).transform;
            landing.position = new Vector3(0f, 0f, 40f);
            door.Destination = landing;
            SetField(door, "destinationZone", "CrownHall");

            GameObject hero = Track(new GameObject("Hero"));
            hero.transform.position = Vector3.zero;

            SetStatic(typeof(DoorTeleporter), "s_lastGlobalTeleportTime", -100f);
            door.PerformTeleport(hero);
            Assert.AreEqual(0f, hero.transform.position.z, 0.001f, "The sealed gate keeps the hero in the hall.");
            Assert.IsNotNull(LockpickMinigameUI.Instance, "The hero is told why the gate will not open.");
            Track(LockpickMinigameUI.Instance.gameObject);

            gameManager.NotifyBossDefeated("CursedCommander");
            gameManager.NotifyBossDefeated("ShadowMageMalakor");
            SetStatic(typeof(DoorTeleporter), "s_lastGlobalTeleportTime", -100f);
            door.PerformTeleport(hero);
            if (Time.timeSinceLevelLoad >= 0.6f)
            {
                Assert.AreEqual(40f, hero.transform.position.z, 0.001f, "With both wing bosses down the gate lets the hero through.");
            }
        }

        #endregion

        #region A7: no map baked into the village

        [Test]
        public void VillageScene_HasNoBakedExpeditionMap()
        {
            string scene = File.ReadAllText("Assets/Scenes/Zone_1_VillageAndCellar.unity");
            StringAssert.DoesNotContain("m_Name: " + DungeonMapUIController.ModalName, scene,
                "The map is built at runtime; an old copy saved in the scene showed up with every card repeated.");
        }

        #endregion

        #region C1: the ending

        [Test]
        public void AdventureStats_CountHeroTurns_Natural20s_AndFalls()
        {
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero");
            EnemyUnit enemy = CreateUnit<EnemyUnit>("Skeleton");

            AdventureStats.HandleUnitTurnStarted(hero);
            AdventureStats.HandleUnitTurnStarted(enemy);
            AdventureStats.HandleDiceRolled(DiceSystem.EvaluateRoll(20, 0, 10, triggerEvent: false));
            AdventureStats.HandleDiceRolled(DiceSystem.EvaluateRoll(19, 0, 10, triggerEvent: false));
            AdventureStats.HandleTurnStateChanged(TurnState.Defeat);
            AdventureStats.HandleTurnStateChanged(TurnState.Victory);

            Assert.AreEqual(1, AdventureStats.TurnsTaken, "Only the hero's turns count.");
            Assert.AreEqual(1, AdventureStats.NaturalTwenties);
            Assert.AreEqual(1, AdventureStats.Deaths);
        }

        [Test]
        public void AdventureStats_IgnoreEnemyNatural20s()
        {
            TurnManager tm = CreateUnit<TurnManager>("TurnManager");
            SetStaticProperty(typeof(TurnManager), "Instance", tm);
            SetField(tm, "isCombatActive", true);
            SetField(tm, "currentState", TurnState.EnemyTurn);

            AdventureStats.HandleDiceRolled(DiceSystem.EvaluateRoll(20, 0, 10, triggerEvent: false));

            Assert.AreEqual(0, AdventureStats.NaturalTwenties);
        }

        [Test]
        public void SaveData_CarriesTheAdventureStats()
        {
            AdventureStats.Restore(42, 5, 2);
            var save = new PlayerSaveData
            {
                statTurnsTaken = AdventureStats.TurnsTaken,
                statNaturalTwenties = AdventureStats.NaturalTwenties,
                statDeaths = AdventureStats.Deaths
            };
            PlayerSaveData copy = JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(save));

            Assert.AreEqual(42, copy.statTurnsTaken);
            Assert.AreEqual(5, copy.statNaturalTwenties);
            Assert.AreEqual(2, copy.statDeaths);
        }

        [Test]
        public void EndingDialogue_IsOtheliaThankingTheHero()
        {
            DialogueNodeSO node = ThroneRoomEnding.BuildEndingDialogue(null);
            int lines = 0;
            bool namesHero = false;
            while (node != null)
            {
                Track(node);
                lines++;
                Assert.AreEqual(ThroneRoomEnding.OtheliaName, node.SpeakerName);
                Assert.AreEqual(1, node.Options.Count, "Each line has one reply so the conversation always moves on.");
                namesHero |= node.DialogueText.Contains("hero");
                node = node.Options[0].NextNodeSuccess;
            }

            Assert.AreEqual(3, lines);
            Assert.IsTrue(namesHero);
        }

        [Test]
        public void StatsScreen_ListsTurnsNatural20sGoldAndFalls()
        {
            AdventureStats.Restore(31, 4, 1);
            string text = ThroneRoomEnding.BuildStatsText(null);

            StringAssert.Contains("31", text);
            StringAssert.Contains("Natural 20s", text);
            StringAssert.Contains("Gold", text);
            StringAssert.Contains("fallen", text);
        }

        [Test]
        public void ThroneRoomScene_HasTheEndingWithOtheliaHidden()
        {
            string scene = File.ReadAllText(ThroneRoomScenePath);
            StringAssert.Contains("CastleOfTheD20.World.ThroneRoomEnding", scene);
            StringAssert.Contains("value: Ending_Othelia", scene);
            StringAssert.DoesNotContain("othelia: {fileID: 0}", scene, "The ending needs Othelia wired in.");
        }

        #endregion

        #region D3: background tab

        [Test]
        public void RunInBackground_IsOff()
        {
            Assert.IsFalse(PlayerSettings.runInBackground, "The browser build should pause when its tab is hidden.");
        }

        #endregion

        #region Helpers

        private const string ThroneRoomScenePath = "Assets/Scenes/Zone_7_ThroneRoom.unity";

        private static string ThroneStatus(GameObject canvasGo)
        {
            foreach (TMP_Text text in canvasGo.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "StatusBadge" && text.transform.parent.name == "Node_" + DungeonMapUIController.KeyCrownHall)
                {
                    return text.text;
                }
            }
            Assert.Fail("Throne Room card not found on the map.");
            return null;
        }

        private static EnemyUnit FindSummon(CursedCommanderBoss commander)
        {
            foreach (EnemyUnit unit in Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None))
            {
                if (unit != commander) return unit;
            }
            return null;
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private T CreateUnit<T>(string name) where T : Component
        {
            GameObject go = Track(new GameObject(name));
            T component = go.AddComponent<T>();

            // Edit Mode does not run Awake for AddComponent; invoke it like the engine would
            StatusEffectController effects = go.GetComponent<StatusEffectController>();
            if (effects != null) Invoke(effects, "Awake");
            Invoke(component, "Awake");
            return component;
        }

        private static void Invoke(object target, string method)
        {
            for (System.Type t = target.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (m != null)
                {
                    m.Invoke(target, null);
                    return;
                }
            }
        }

        private static void SetField(object target, string field, object value)
        {
            for (System.Type t = target.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (f != null)
                {
                    f.SetValue(target, value);
                    return;
                }
            }
            Assert.Fail($"Field {field} not found.");
        }

        private static void SetStatic(System.Type type, string field, object value)
        {
            FieldInfo f = type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{type.Name}.{field} not found.");
            f.SetValue(null, value);
        }

        private static void SetStaticProperty(System.Type type, string property, object value)
        {
            PropertyInfo p = type.GetProperty(property, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            p?.SetValue(null, value);
        }

        #endregion
    }
}
