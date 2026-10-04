using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Critical review content (C3-C10): letters in every zone, the Castle Hall peddler, ghost and vault, the
    /// Tower challenge, the secret gate for every class, the new enemies, class choices with consequences in
    /// the boss conversations, and per-zone music.
    /// </summary>
    [TestFixture]
    public class CriticalReviewContentTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GameManager gameManager;
        private GridManager grid;
        private InventoryManager inventory;
        private PlayerDataSO sessionData;

        [SetUp]
        public void SetUp()
        {
            sessionData = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerDataSO.Session = sessionData;

            gameManager = Track(new GameObject("C_GameManager")).AddComponent<GameManager>();
            SetStatic(typeof(GameManager), "_instance", gameManager);

            grid = Track(new GameObject("C_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            inventory = Track(new GameObject("C_Inventory")).AddComponent<InventoryManager>();
            SetStaticProperty(typeof(InventoryManager), "Instance", inventory);
            inventory.RestoreFromSave(gold: 0, scrapMetal: 0, rerollScrolls: 0);
            inventory.RestoreItems(null, null);

            StoryFlags.Clear();
            RerollableRoll.Reset();
            DiceSystem.ResetRandom();
        }

        [TearDown]
        public void TearDown()
        {
            StoryFlags.Clear();
            RerollableRoll.Reset();
            DiceSystem.ResetRandom();
            foreach (TurnManager tm in Object.FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tm.gameObject);
            }
            if (DialogueController.Instance != null) Object.DestroyImmediate(DialogueController.Instance.gameObject);
            SetStatic(typeof(DialogueController), "instance", null);
            SetStaticProperty(typeof(TurnManager), "Instance", null);
            SetStaticProperty(typeof(InventoryManager), "Instance", null);
            GridManager.Instance = null;
            SetStatic(typeof(GameManager), "_instance", null);
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
            PlayerDataSO.Session = null;
            if (sessionData != null) Object.DestroyImmediate(sessionData);
        }

        #region C3: story

        [Test]
        public void EveryZone_HasAtLeastTwoNotes()
        {
            string[] zones = { "village_", "othelia_", "forest_", "courtyard_", "library_", "hall_", "tower_", "throne_" };
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (string id in LoreTexts.AllIds)
            {
                foreach (string z in zones)
                {
                    if (!id.StartsWith(z)) continue;
                    string key = z == "othelia_" ? "village_" : z;
                    counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            }
            foreach (string z in new[] { "village_", "forest_", "courtyard_", "library_", "hall_", "tower_", "throne_" })
            {
                Assert.GreaterOrEqual(counts.TryGetValue(z, out int n) ? n : 0, 2, z + " needs 2 notes");
            }
            StringAssert.Contains("Gareth", LoreTexts.Get("courtyard_oath").Body, "The Commander was the King's guard captain.");
            StringAssert.Contains("will not die", LoreTexts.Get("library_journal").Body, "Malakor cast the curse at the King's request.");
        }

        [Test]
        public void ZoneScenes_CarryTheirNotesAndContent()
        {
            AssertNotes("Zone_2_ForestPath", 2);
            Assert.AreEqual(2, Object.FindObjectsByType<DungeonRoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "The Forest Path has a second encounter.");
            Assert.IsNotNull(Object.FindAnyObjectByType<CultistCaster>(FindObjectsInactive.Include));

            AssertNotes("Zone_3_CastleCourtyard", 2);
            AssertNotes("Zone_4_Library", 2);

            AssertNotes("Zone_5_CastleHall", 2);
            Assert.AreEqual(2, Object.FindObjectsByType<ScriptedNpc>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "Pip and the ghost guard.");
            Assert.IsNotNull(Object.FindAnyObjectByType<HallVault>(FindObjectsInactive.Include));

            AssertNotes("Zone_6_Tower", 2);
            TowerChallenge challenge = Object.FindAnyObjectByType<TowerChallenge>(FindObjectsInactive.Include);
            Assert.IsNotNull(challenge);
            Assert.AreEqual(3, challenge.Pillars.Count);
            Assert.AreEqual(2, challenge.SealedRewards.Count, "The elixir and the ring are sealed.");
            Assert.IsNotNull(challenge.GuardianRoom);
            Assert.AreEqual(3, Object.FindObjectsByType<PressurePlateTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            AssertNotes("Zone_7_ThroneRoom", 2);

            // Leave an empty scene behind so later tests don't build grids among the Throne Room's walls
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void ForestAmbush_BothEnemiesJoinTheFight()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Zone_2_ForestPath.unity", OpenSceneMode.Single);
            DungeonRoomController ambush = null;
            foreach (DungeonRoomController room in Object.FindObjectsByType<DungeonRoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (room.roomKey == "ForestAmbush") ambush = room;
            }
            Assert.IsNotNull(ambush);
            Assert.AreEqual(2, ambush.roomEnemies.Count);
            foreach (GameObject enemy in ambush.roomEnemies)
            {
                Assert.IsFalse(DungeonRoomController.IsElite(enemy.GetComponent<EnemyUnit>()), enemy.name + " must not count as elite, or it fights alone.");
            }
            Assert.AreEqual(2, ambush.CountEnemiesThatJoin(), "The skeleton archer and the curse cultist both fight.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void AssertNotes(string scene, int min)
        {
            EditorSceneManager.OpenScene($"Assets/Scenes/{scene}.unity", OpenSceneMode.Single);
            int notes = Object.FindObjectsByType<LoreNote>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.GreaterOrEqual(notes, min, scene);
        }

        #endregion

        #region C4: Castle Hall

        [Test]
        public void Pip_SellsScrolls_AndTurnsAwayAnEmptyPurse()
        {
            DialogueController dialogue = Track(new GameObject("C_Dialogue")).AddComponent<DialogueController>();
            SetStatic(typeof(DialogueController), "instance", dialogue);
            DialogueActionTrigger actions = Track(new GameObject("C_Actions")).AddComponent<DialogueActionTrigger>();

            DialogueNodeSO start = HubDialogues.BuildMerchant();
            dialogue.StartDialogue(start, null);
            DialogueOption warding = start.Options[0];
            StringAssert.Contains("Warding", warding.OptionText);

            Select(dialogue, actions, warding);
            Assert.AreEqual(warding.NextNodeFailure, dialogue.CurrentNode, "No gold: Pip's 'gold first' line.");
            Assert.AreEqual(0, inventory.GetItemCount(inventory.FindItemByID(CombatScrolls.WardingScrollId)));

            inventory.AddGold(100);
            Select(dialogue, actions, dialogue.CurrentNode.Options[0]);
            Assert.AreEqual(1, inventory.GetItemCount(inventory.FindItemByID(CombatScrolls.WardingScrollId)));
            Assert.AreEqual(40, inventory.CurrentGold);
            dialogue.EndDialogue();
        }

        [Test]
        public void ScrollOfWarding_ShieldsTheHeroWhenAFightStarts()
        {
            PlayerUnit hero = CreateHero("Character_Mage_Elira");
            inventory.AddItem(inventory.FindItemByID(CombatScrolls.WardingScrollId), 1);
            Assert.AreEqual(1, CombatScrolls.UseAtFightStart(hero, new List<CombatUnit> { hero }));
            Assert.AreEqual(2, hero.StatusEffects.ManaShieldCharges);
            Assert.AreEqual(0, CombatScrolls.UseAtFightStart(hero, new List<CombatUnit> { hero }), "Used up.");
        }

        [Test]
        public void GhostRiddle_OpensTheVault()
        {
            GameObject vaultObj = Track(new GameObject("Vault"));
            HallVault vault = vaultObj.AddComponent<HallVault>();
            vault.Door = Track(new GameObject("Door"));
            vault.RewardChest = Track(new GameObject("Chest"));
            vault.RewardChest.SetActive(false);

            DialogueNodeSO ghost = HubDialogues.BuildGhostGuard();
            DialogueOption answer = null;
            foreach (DialogueOption o in ghost.Options) if (o.OptionText.Contains("Footsteps")) answer = o;
            Assert.IsNotNull(answer);
            StringAssert.Contains("OPEN_VAULT", answer.CombatDebuffTag);

            HallVault.OpenInScene();
            Assert.IsTrue(vault.IsOpen);
            Assert.IsFalse(vault.Door.activeSelf);
            Assert.IsTrue(vault.RewardChest.activeSelf);
        }

        #endregion

        #region C5: the Tower

        [Test]
        public void TowerChallenge_NeedsRunesAndGuardian_ThenUnseals()
        {
            GameObject rootObj = Track(new GameObject("Challenge"));
            TowerChallenge challenge = rootObj.AddComponent<TowerChallenge>();
            for (int i = 0; i < 3; i++)
            {
                RunePillar pillar = CreateUnit<RunePillar>("Pillar_" + i);
                pillar.Challenge = challenge;
                challenge.Pillars.Add(pillar);
            }
            GiantElixirInteraction elixir = CreateUnit<GiantElixirInteraction>("Elixir");
            challenge.SealedRewards.Add(elixir);
            Invoke(challenge, "Start");
            Assert.IsFalse(elixir.IsInteractable, "Sealed at first.");

            // Pillars start on EYE; SUN, MOON, STAR is 1, 2 and 3 turns away
            for (int i = 0; i < 3; i++)
            {
                for (int turns = 0; turns <= i; turns++) challenge.Pillars[i].Interact(null);
            }
            Assert.IsTrue(challenge.RunesSolved);
            Assert.IsFalse(challenge.IsComplete, "The guardian still stands.");

            StoryFlags.Set(TowerChallenge.GuardianDefeatedFlag);
            challenge.TryComplete();
            Assert.IsTrue(challenge.IsComplete);
            Assert.IsTrue(elixir.IsInteractable, "Unsealed.");
        }

        [Test]
        public void PressurePlate_FailedDexSave_Hurts()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            PressurePlateTrap trap = Track(new GameObject("Plate")).AddComponent<PressurePlateTrap>();
            SeedFirstRoll(r => r == 1);
            int hp = hero.CurrentHP;
            trap.Spring(hero);
            Assert.Less(hero.CurrentHP, hp);
            Assert.IsTrue(trap.IsSprung);
        }

        [Test]
        public void MimicBite_GluesTheHero()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            MimicUnit mimic = CreateUnit<MimicUnit>("Mimic");
            mimic.InitializeUnit();
            mimic.MoveToTile(grid.GetTileAt(new Vector2Int(6, 5)));
            SeedFirstRoll(r => r == 20);
            Invoke(mimic, "PerformAttack", hero, null);
            Assert.AreEqual(0, hero.MovementRange);
        }

        #endregion

        #region C6, C7: Forest gate and new enemies

        [Test]
        public void SecretGate_GivesWayToTheWarriorsStrength()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            LockpickInteraction gate = Track(new GameObject("Gate")).AddComponent<LockpickInteraction>();
            GameObject passage = Track(new GameObject("Passage"));
            passage.SetActive(false);
            SerializedObject so = new SerializedObject(gate);
            so.FindProperty("hiddenPathObject").objectReferenceValue = passage;
            so.FindProperty("rewardGold").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(gate.OffersClassAlternative(warrior));
            SeedFirstRoll(r => r == 20);
            gate.TryClassAlternative(warrior);
            Assert.IsFalse(gate.IsLocked);
            Assert.IsTrue(passage.activeSelf);
        }

        [Test]
        public void Cultist_HealsAHurtAlly()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            CultistCaster cultist = CreateUnit<CultistCaster>("Cultist");
            cultist.InitializeUnit();
            cultist.MoveToTile(grid.GetTileAt(new Vector2Int(8, 8)));
            EnemyUnit archer = CreateUnit<EnemyUnit>("Archer");
            archer.ConfigureStats("Archer", 20, 12, 4, 2);
            archer.InitializeUnit();
            archer.MoveToTile(grid.GetTileAt(new Vector2Int(9, 8)));
            StartCombat(hero, cultist, archer);

            archer.TakeDamage(15);
            int hurt = archer.CurrentHP;
            Assert.IsTrue(cultist.TryHealAlly(grid));
            Assert.Greater(archer.CurrentHP, hurt);
        }

        [Test]
        public void RangedEnemy_ShootsFromAfar()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit archer = CreateUnit<EnemyUnit>("Archer");
            archer.ConfigureStats("Skeleton Archer", 16, 12, 5, 100);
            archer.ConfigureAttackRange(5);
            archer.InitializeUnit();
            archer.MoveToTile(grid.GetTileAt(new Vector2Int(9, 5)));
            StartCombat(hero, archer);

            int hp = hero.CurrentHP;
            SeedFirstRoll(r => r > 1); // a natural 1 always misses
            archer.ExecuteTurnAction(grid, null);
            Assert.AreEqual(new Vector2Int(9, 5), archer.GridPosition, "In range and in sight: no need to walk up.");
            Assert.Less(hero.CurrentHP, hp);
        }

        #endregion

        #region C8, C9: boss choices

        [Test]
        public void ClassOptions_AppearForTheRightHero()
        {
            Assert.IsTrue(HasOption(BossChoices.BuildOptions(BossChoices.CommanderBossId, CreateHero("Character_Warrior_SirRoland")), "[Warrior]"));
            Assert.IsTrue(HasOption(BossChoices.BuildOptions(BossChoices.MalakorBossId, CreateHero("Character_Mage_Elira")), "[Mage]"));
            Assert.IsTrue(HasOption(BossChoices.BuildOptions(BossChoices.KingBossId, CreateHero("Character_Rogue_Corvo")), "[Rogue]"));
            Assert.IsFalse(HasOption(BossChoices.BuildOptions(BossChoices.CommanderBossId, CreateHero("Character_Rogue_Corvo")), "[Warrior]"));
            Assert.IsTrue(HasOption(BossChoices.BuildOptions(BossChoices.CommanderBossId, CreateHero("Character_Mage_Elira")), "[Soldier's Honor]"), "Anyone can release the Commander.");
        }

        [Test]
        public void ReleasingTheCommander_EndsTheEncounterWithoutAFight()
        {
            DungeonRoomController room = Track(new GameObject("Courtyard")).AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = BossChoices.CommanderBossId;
            room.generateGridOnCombat = false;
            Invoke(room, "Awake");
            Invoke(room, "Start");

            Assert.IsFalse(BossChoices.ResolvedPeacefully(BossChoices.CommanderBossId));
            StoryFlags.Set(BossChoices.CommanderReleasedFlag);
            Assert.IsTrue(BossChoices.ResolvedPeacefully(BossChoices.CommanderBossId));

            room.ResolvePeacefully();
            Assert.IsTrue(room.IsCleared);
            Assert.IsTrue(gameManager.IsCommanderDefeated, "The campaign moves on.");
            StringAssert.Contains("Gareth", BossChoices.BuildEpilogue());
            Invoke(room, "OnDestroy");
        }

        [Test]
        public void StolenCrown_AndSparedMalakor_ChangeTheKingsFight()
        {
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("King");
            king.InitializeUnit();
            int ac = king.ArmorClass;

            StoryFlags.Set(BossChoices.CrownStolenFlag);
            StoryFlags.Set(BossChoices.MalakorSparedFlag);
            StoryFlags.Set(BossChoices.MalakorHelpedFlag);
            king.InitializeUnit();

            Assert.AreEqual(ac - GargoyleKingBoss.StolenCrownArmorPenalty, king.ArmorClass);
            Assert.AreEqual(king.MaxHP - GargoyleKingBoss.MalakorHelpDamage, king.CurrentHP, "Malakor's counter-rune hurts the King.");
            StringAssert.Contains("counter-rune", BossChoices.BuildEpilogue());
        }

        [Test]
        public void SparedMalakor_DecidesOnce()
        {
            StoryFlags.Set(BossChoices.MalakorSparedFlag);
            bool? first = BossChoices.ResolveMalakorAtThrone();
            Assert.IsNotNull(first);
            for (int i = 0; i < 5; i++) Assert.AreEqual(first, BossChoices.ResolveMalakorAtThrone());
        }

        [Test]
        public void SecondEncounter_KeepsItsOwnClearedRecord()
        {
            DungeonRoomController ambush = Track(new GameObject("Ambush")).AddComponent<DungeonRoomController>();
            ambush.roomLocation = "Forest";
            ambush.roomKey = "ForestAmbush";
            ambush.generateGridOnCombat = false;
            Invoke(ambush, "Awake");
            Invoke(ambush, "Start");
            ambush.BeginEncounter(null);
            ambush.OnCombatResolved();

            Assert.IsFalse(gameManager.IsWingCleared(GameLocation.Forest), "The zombie fight still waits.");
            Assert.IsTrue(gameManager.IsRewardClaimed(ambush.ClearedKey));
            Invoke(ambush, "OnDestroy");
        }

        #endregion

        #region C10: music

        [Test]
        public void ZoneMusic_FallsBackWhenNoSongIsGiven()
        {
            MusicManager music = Track(new GameObject("Music")).AddComponent<MusicManager>();
            Assert.IsNull(music.GetZoneExplorationClip(GameLocation.Village));
            Assert.IsNull(music.GetZoneExplorationClip(GameLocation.Forest), "No Explore_Forest song yet: the castle theme plays.");
        }

        #endregion

        #region Helpers

        private void Select(DialogueController dialogue, DialogueActionTrigger actions, DialogueOption option)
        {
            // Edit Mode runs no OnEnable, so hand the option to the action trigger like the event would
            System.Action<DialogueOption> relay = o => Invoke(actions, "HandleOptionSelected", o);
            EventInfo evt = typeof(DialogueController).GetEvent("OnOptionSelected");
            evt.AddEventHandler(null, relay);
            try
            {
                dialogue.SelectOption(option);
            }
            finally
            {
                evt.RemoveEventHandler(null, relay);
            }
        }

        private static bool HasOption(List<DialogueOption> options, string prefix)
        {
            foreach (DialogueOption o in options) if (o.OptionText.StartsWith(prefix)) return true;
            return false;
        }

        private TurnManager StartCombat(params CombatUnit[] units)
        {
            TurnManager tm = Track(new GameObject("C_TurnManager")).AddComponent<TurnManager>();
            SetStaticProperty(typeof(TurnManager), "Instance", tm);
            tm.StartCombat(new List<CombatUnit>(units), awardVictoryScrap: false);
            return tm;
        }

        private static void SeedFirstRoll(System.Func<int, bool> test)
        {
            for (int seed = 1; seed < 100000; seed++)
            {
                if (test(new System.Random(seed).Next(1, 21)))
                {
                    DiceSystem.SetSeed(seed);
                    return;
                }
            }
            Assert.Fail("No seed found.");
        }

        private PlayerUnit CreateHero(string classAsset)
        {
            CharacterClassSO cls = AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{classAsset}.asset");
            sessionData.SelectedClass = cls;
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero_" + classAsset);
            hero.InitializeUnit();
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));
            return hero;
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
            StatusEffectController effects = go.GetComponent<StatusEffectController>();
            if (effects != null) Invoke(effects, "Awake");
            Invoke(component, "Awake");
            return component;
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            for (System.Type t = target.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (m != null)
                {
                    m.Invoke(target, args.Length == 0 ? null : args);
                    return;
                }
            }
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
