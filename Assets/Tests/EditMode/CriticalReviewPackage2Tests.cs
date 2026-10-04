using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
    /// Critical review, package 2 ("promises that work"): the Rune of Reroll really replaces a failed
    /// hero roll, Corvo has Shadow Step, and Rank 2 upgrades change every ability they are offered for.
    /// </summary>
    [TestFixture]
    public class CriticalReviewPackage2Tests
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

            gameManager = Track(new GameObject("P2_GameManager")).AddComponent<GameManager>();
            SetStatic(typeof(GameManager), "_instance", gameManager);

            grid = Track(new GameObject("P2_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            inventory = Track(new GameObject("P2_Inventory")).AddComponent<InventoryManager>();
            SetStaticProperty(typeof(InventoryManager), "Instance", inventory);
            inventory.RestoreFromSave(gold: 0, scrapMetal: 0, rerollScrolls: 0);

            RerollableRoll.Reset();
            RerollableRoll.CanPauseOverride = () => true;
            DifficultySettings.Reset();
            DiceSystem.ResetRandom();
        }

        [TearDown]
        public void TearDown()
        {
            RerollableRoll.Reset();
            RerollableRoll.CanPauseOverride = null;
            DifficultySettings.Reset();
            DiceSystem.ResetRandom();

            StoryFlags.Clear();
            SaveSystem.ClearPendingPosition();
            SetStatic(typeof(QuestManager), "instance", null);
            foreach (TurnManager tm in Object.FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tm.gameObject);
            }
            if (DialogueController.Instance != null) Object.DestroyImmediate(DialogueController.Instance.gameObject);
            SetStaticProperty(typeof(InventoryManager), "Instance", null);
            SetStaticProperty(typeof(TurnManager), "Instance", null);
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

        #region A3: the Rune of Reroll changes the outcome

        [Test]
        public void HeroFailure_WithScroll_WaitsForTheChoice_AndContinueKeepsTheFailure()
        {
            inventory.RestoreFromSave(0, 0, rerollScrolls: 1);
            SeedSoFirstRollIs(r1 => r1 < 20);

            int calls = 0;
            DiceResult final = default;
            RerollableRoll.Roll(0, 30, AdvantageType.None, r => { calls++; final = r; });

            Assert.AreEqual(0, calls, "A failed hero roll with a scroll in the bag waits for the player.");
            Assert.IsTrue(RerollableRoll.IsAwaitingDecision);

            RerollableRoll.Accept();
            Assert.AreEqual(1, calls);
            Assert.IsFalse(final.isSuccess);
            Assert.AreEqual(1, inventory.RerollScrollCount, "Continue keeps the scroll.");
        }

        [Test]
        public void HeroFailure_Rerolled_UsesTheNewRoll_AndSpendsTheScroll()
        {
            inventory.RestoreFromSave(0, 0, rerollScrolls: 1);
            SeedSoRollsAre(r1 => r1 < 20, r2 => r2 == 20);

            DiceResult final = default;
            int calls = 0;
            RerollableRoll.Roll(0, 30, AdvantageType.None, r => { calls++; final = r; });
            Assert.IsTrue(RerollableRoll.Reroll());

            Assert.AreEqual(1, calls, "The outcome runs once, with the rerolled result.");
            Assert.AreEqual(20, final.rawRoll);
            Assert.IsTrue(final.isSuccess);
            Assert.AreEqual(0, inventory.RerollScrollCount);
        }

        [Test]
        public void HeroSuccess_AndEnemyRolls_NeverPause()
        {
            inventory.RestoreFromSave(0, 0, rerollScrolls: 2);

            int calls = 0;
            SeedSoFirstRollIs(r1 => r1 > 1); // a natural 1 always fails
            RerollableRoll.Roll(0, -50, AdvantageType.None, r => calls++);
            Assert.AreEqual(1, calls, "A success resolves at once.");

            EnemyUnit enemy = CreateUnit<EnemyUnit>("Enemy");
            SeedSoFirstRollIs(r1 => r1 < 20);
            AbilityExecutor.RollToHit(enemy, 0, 30, AdvantageType.None, r => calls++);
            Assert.AreEqual(2, calls, "An enemy's miss never stops on the reroll choice.");
            Assert.IsFalse(RerollableRoll.IsAwaitingDecision);
        }

        [Test]
        public void HeroFailure_WithoutScroll_ResolvesAtOnce()
        {
            SeedSoFirstRollIs(r1 => r1 < 20);
            int calls = 0;
            RerollableRoll.Roll(0, 30, AdvantageType.None, r => calls++);
            Assert.AreEqual(1, calls);
            Assert.IsFalse(RerollableRoll.IsAwaitingDecision);
        }

        [Test]
        public void SwordSlash_DamageComesFromTheRerolledHit()
        {
            inventory.RestoreFromSave(0, 0, rerollScrolls: 1);
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit enemy = CreateUnit<EnemyUnit>("Target");
            enemy.ConfigureStats("Target", hp: 200, ac: 30, damage: 1, bonus: 0);
            enemy.InitializeUnit();
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));
            enemy.MoveToTile(grid.GetTileAt(new Vector2Int(6, 5)));
            AbilityExecutor executor = CreateUnit<AbilityExecutor>("Executor");

            SeedSoRollsAre(r1 => r1 < 20, r2 => r2 == 20);
            Assert.IsTrue(executor.ExecuteAbility(hero, hero.GetAbility(0), enemy.GridPosition));
            Assert.AreEqual(200, enemy.CurrentHP, "No damage while the failed roll waits on the choice.");

            RerollableRoll.Reroll();
            Assert.Less(enemy.CurrentHP, 200, "The rerolled natural 20 hits.");
        }

        [Test]
        public void EasyDifficulty_GivesOneFreeRerollPerFight()
        {
            DifficultySettings.Current = DifficultyLevel.Easy;
            DifficultySettings.OnCombatStarted();
            Assert.AreEqual(1, RerollableRoll.AvailableRerolls);

            SeedSoFirstRollIs(r1 => r1 < 20);
            RerollableRoll.Roll(0, 30, AdvantageType.None, r => { });
            Assert.IsTrue(RerollableRoll.Reroll());
            RerollableRoll.Accept();

            Assert.AreEqual(0, RerollableRoll.AvailableRerolls, "The free reroll is spent for this fight.");
            DifficultySettings.OnCombatStarted();
            Assert.AreEqual(1, RerollableRoll.AvailableRerolls, "The next fight brings it back.");
        }

        #endregion

        #region A4: Corvo has Shadow Step

        [Test]
        public void Corvo_HasFourCombatAbilities_IncludingShadowStep()
        {
            PlayerUnit hero = CreateHero("Character_Rogue_Corvo");
            Assert.AreEqual(4, hero.ActiveAbilities.Count);

            bool hasShadowStep = false;
            foreach (AbilitySO ability in hero.ActiveAbilities)
            {
                Assert.IsFalse(ability.AbilityID.Contains("lockpick"), "Lockpicking stays an exploration passive.");
                if (ability.AbilityID == "rogue_shadow_step") hasShadowStep = true;
            }
            Assert.IsTrue(hasShadowStep);
        }

        #endregion

        #region A5: Rank 2 changes every ability

        [Test]
        public void Rank2ShieldWall_GivesSixAc()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            int slot = FindSlot(hero, "warrior_shield_block");
            int baseAc = hero.ArmorClass;
            Assert.IsTrue(hero.UpgradeAbilityToRank2(slot));

            AbilityExecutor executor = CreateUnit<AbilityExecutor>("Executor");
            executor.ExecuteAbility(hero, hero.GetAbility(slot), hero.GridPosition);
            Assert.AreEqual(baseAc + StatusEffectController.ShieldWallRank2ArmorBonus, hero.ArmorClass);
        }

        [Test]
        public void Rank2ManaShield_AbsorbsTwoHits()
        {
            PlayerUnit hero = CreateHero("Character_Mage_Elira");
            int slot = FindSlot(hero, "mage_mana_shield");
            hero.UpgradeAbilityToRank2(slot);

            AbilityExecutor executor = CreateUnit<AbilityExecutor>("Executor");
            executor.ExecuteAbility(hero, hero.GetAbility(slot), hero.GridPosition);
            int hp = hero.CurrentHP;
            hero.TakeDamage(5);
            hero.TakeDamage(5);
            Assert.AreEqual(hp, hero.CurrentHP, "Both hits are absorbed.");
            hero.TakeDamage(5);
            Assert.AreEqual(hp - 5, hero.CurrentHP, "The third hit lands.");
        }

        [Test]
        public void Rank2Blink_SmokeBomb_AndShadowStep_ReachFarther()
        {
            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            AbilitySO blink = mage.GetAbility(FindSlot(mage, "mage_blink"));
            AbilitySO blink2 = blink.CreateRank2();
            Assert.AreEqual(blink.Range + 2, blink2.Range);
            Assert.AreEqual("mage_blink", blink2.BaseAbilityID, "Elira's blink clip still plays at Rank 2.");

            AbilitySO smoke = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Rogue_SmokeBomb.asset");
            AbilitySO smoke2 = smoke.CreateRank2();
            Assert.AreEqual(smoke.AreaOfEffectRadius + 1, smoke2.AreaOfEffectRadius);
            Assert.AreEqual(smoke.EffectDurationTurns + 1, smoke2.EffectDurationTurns);

            AbilitySO step = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Rogue_ShadowStep.asset");
            Assert.AreEqual(step.Range + 2, step.CreateRank2().Range);

            Object.DestroyImmediate(blink2);
            Object.DestroyImmediate(smoke2);
        }

        #endregion

        #region A6: dialogue bonuses survive scene changes and saves

        [Test]
        public void BaldursLoreBonus_SurvivesANewDialogueController()
        {
            StoryFlags.Clear();
            DialogueController first = Track(new GameObject("Dialogue_Village")).AddComponent<DialogueController>();
            first.RegisterCombatDebuff("CommanderArmorWeakened");
            Object.DestroyImmediate(first.gameObject);
            SetStatic(typeof(DialogueController), "instance", null);

            DialogueController second = Track(new GameObject("Dialogue_Courtyard")).AddComponent<DialogueController>();
            Assert.IsTrue(second.HasCombatDebuff("CommanderArmorWeakened"), "The village bonus is waiting in the Courtyard.");
            Assert.IsTrue(second.ConsumeCombatDebuff("CommanderArmorWeakened"));
            Assert.IsFalse(StoryFlags.Has("CommanderArmorWeakened"));
        }

        #endregion

        #region A9: saves keep health, position, flags and difficulty

        [Test]
        public void Save_KeepsHealthPositionFlagsAndDifficulty()
        {
            WithSaveBackup(() =>
            {
                PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
                hero.TakeDamage(9);
                int hp = hero.CurrentHP;
                hero.transform.position = new Vector3(3f, 1f, -2f);
                StoryFlags.Clear();
                StoryFlags.Set("CommanderArmorWeakened");
                DifficultySettings.Current = DifficultyLevel.Hard;

                SaveSystem.SaveGame(sessionData, hero);

                StoryFlags.Clear();
                DifficultySettings.Reset();
                sessionData.CurrentHP = -1;

                PlayerSaveData save = SaveSystem.LoadGame(sessionData, null);
                Assert.IsNotNull(save);
                Assert.AreEqual(hp, sessionData.CurrentHP);
                Assert.IsTrue(StoryFlags.Has("CommanderArmorWeakened"));
                Assert.AreEqual(DifficultyLevel.Hard, DifficultySettings.Current);
                Assert.IsTrue(save.hasPosition);
                Assert.AreEqual(3f, save.posX, 0.001f);
                Assert.AreEqual(-2f, save.posZ, 0.001f);

                PlayerUnit nextHero = CreateHero("Character_Warrior_SirRoland");
                Assert.AreEqual(hp, nextHero.CurrentHP, "The hero comes back with the HP they had.");
                SaveSystem.ClearPendingPosition();
            });
        }

        [Test]
        public void Health_CarriesFromZoneToZone()
        {
            PlayerUnit hero = CreateHero("Character_Rogue_Corvo");
            hero.TakeDamage(7);
            int hp = hero.CurrentHP;
            sessionData.SyncFromPlayer(hero);

            PlayerUnit nextZoneHero = CreateHero("Character_Rogue_Corvo");
            Assert.AreEqual(hp, nextZoneHero.CurrentHP);
        }

        [Test]
        public void NewAdventure_DeletesTheOldSave()
        {
            WithSaveBackup(() =>
            {
                SaveSystem.SaveGame(sessionData, null);
                Assert.IsTrue(SaveSystem.HasSavedGame());
                StoryFlags.Set("MalakorSpared");

                PlayerProgressionManager progression = Track(new GameObject("P2_Progression")).AddComponent<PlayerProgressionManager>();
                progression.ResetForNewGame();

                Assert.IsFalse(SaveSystem.HasSavedGame(), "Continue cannot bring the old run back.");
                Assert.IsFalse(StoryFlags.Has("MalakorSpared"));
            });
        }

        #endregion

        #region A10: quest items and quest scrap are not for sale

        [Test]
        public void QuestItems_CannotBeSold()
        {
            Assert.IsFalse(ShopManager.IsSellable(AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SwampHerb.asset")));
            Assert.IsFalse(ShopManager.IsSellable(AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_Consumable_GiantElixir.asset")));
            Assert.IsTrue(ShopManager.IsSellable(AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_Potion_Health.asset")));
        }

        [Test]
        public void SellingScrap_KeepsWhatBaldursRequestNeeds()
        {
            QuestManager quests = CreateQuestManager();
            QuestSO scrapQuest = AssetDatabase.LoadAssetAtPath<QuestSO>("Assets/Data/Quests/Quest_ScrapMetal.asset");
            quests.RegisterQuest(scrapQuest);
            Assert.IsTrue(quests.StartQuest(scrapQuest.QuestID));

            ShopManager shop = Track(new GameObject("P2_Shop")).AddComponent<ShopManager>();
            inventory.AddScrapMetal(scrapQuest.RequiredAmount + 2);
            quests.RefreshTrackedObjectives();
            shop.ConvertScrapToGold(-1);
            quests.RefreshTrackedObjectives();

            Assert.AreEqual(scrapQuest.RequiredAmount, inventory.ScrapMetalCount, "Only the spare scrap is sold.");
            Assert.AreEqual(scrapQuest.RequiredAmount, quests.GetQuestProgress(scrapQuest.QuestID), "Quest progress stays.");
            Invoke(quests, "OnDisable");
        }

        #endregion

        #region A11: the spec's numbers

        [Test]
        public void CellarQuest_AsksForTheTwoRatsTheCellarFights()
        {
            QuestSO pests = AssetDatabase.LoadAssetAtPath<QuestSO>("Assets/Data/Quests/Quest_CellarPests.asset");
            Assert.AreEqual(2, pests.RequiredAmount);
            StringAssert.Contains("2 giant rats", pests.Description);
        }

        [Test]
        public void CursedCommander_CallsHisGuardInAtHalfHealth()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland");
            hero.transform.position = grid.GetWorldPosition(new Vector2Int(2, 2));

            CursedCommanderBoss commander = CreateUnit<CursedCommanderBoss>("Boss_CursedCommander");
            commander.InitializeUnit();
            commander.transform.position = grid.GetWorldPosition(new Vector2Int(8, 8));
            EnemyUnit guard = CreateUnit<EnemyUnit>("Courtyard_Skeleton");
            guard.ConfigureStats("Armored Skeleton Guard", hp: 20, ac: 12, damage: 4, bonus: 2);
            guard.transform.position = grid.GetWorldPosition(new Vector2Int(9, 8));

            DungeonRoomController room = Track(new GameObject("Courtyard_Room")).AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = "CursedCommander";
            room.generateGridOnCombat = false;
            room.roomEnemies.Add(commander.gameObject);
            room.roomEnemies.Add(guard.gameObject);
            Invoke(room, "Awake");
            Invoke(room, "Start");

            TurnManager tm = Track(new GameObject("P2_TurnManager")).AddComponent<TurnManager>();
            SetStaticProperty(typeof(TurnManager), "Instance", tm);
            CreateUnit<AbilityExecutor>("P2_Executor");

            room.BeginEncounter(hero);
            Assert.IsTrue(tm.IsCombatActive);
            Assert.IsFalse(guard.gameObject.activeSelf, "The guard waits out of sight when the fight starts.");
            Assert.IsFalse(Contains(tm.ActiveUnits, guard));
            Assert.AreEqual(1, commander.ReserveCount);

            commander.TakeDamage(commander.MaxHP / 2 + 1);
            Assert.IsTrue(guard.gameObject.activeSelf, "At half health the Commander calls his guard in.");
            Assert.IsTrue(Contains(tm.ActiveUnits, guard));
            Assert.AreEqual(0, commander.ReserveCount);

            Invoke(room, "OnDestroy");
        }

        [Test]
        public void SkillChecks_UseTheAttributeTheyName()
        {
            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            HeroAttribute intimidation = HeroAttributes.ResolveCheckAttribute(mage, "Intimidation / Strength Check");
            Assert.AreEqual(HeroAttribute.Strength, intimidation);
            Assert.Less(HeroAttributes.GetModifier(mage, intimidation), mage.PrimaryAttributeBonus, "Elira does not scare people with her Intelligence.");
            Assert.AreEqual(HeroAttribute.Intelligence, HeroAttributes.ResolveCheckAttribute(mage, "Arcana / Intelligence Check"));

            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo");
            Assert.AreEqual(HeroAttribute.Dexterity, HeroAttributes.ResolveCheckAttribute(rogue, "Persuasion Check (Charisma/Agility)"), "Corvo picks his better approach.");
        }

        #endregion

        #region C2: the main quest

        [Test]
        public void MainQuest_FollowsTheThreeBosses()
        {
            QuestManager quests = CreateQuestManager();
            Assert.AreEqual(QuestState.InProgress, quests.GetQuestState(MainQuest.QuestId), "The main quest is active from the start.");
            StringAssert.Contains("Commander", quests.GetQuest(MainQuest.QuestId).ObjectiveSummary);

            gameManager.NotifyBossDefeated("CursedCommander");
            Assert.AreEqual(1, quests.GetQuestProgress(MainQuest.QuestId));
            StringAssert.Contains("Malakor", quests.GetQuest(MainQuest.QuestId).ObjectiveSummary);

            gameManager.NotifyBossDefeated("ShadowMageMalakor");
            StringAssert.Contains("King", quests.GetQuest(MainQuest.QuestId).ObjectiveSummary);

            gameManager.NotifyBossDefeated("GargoyleKing");
            Assert.AreEqual(QuestState.Completed, quests.GetQuestState(MainQuest.QuestId));
            Invoke(quests, "OnDisable");
        }

        #endregion

        #region Helpers

        private QuestManager CreateQuestManager()
        {
            QuestManager quests = Track(new GameObject("P2_Quests")).AddComponent<QuestManager>();
            FieldInfo database = typeof(QuestManager).GetField("questDatabase", BindingFlags.Instance | BindingFlags.NonPublic);
            database.SetValue(quests, new List<QuestSO> { AssetDatabase.LoadAssetAtPath<QuestSO>("Assets/Data/Quests/Quest_CellarPests.asset") });
            SetStatic(typeof(QuestManager), "instance", quests);
            Invoke(quests, "Awake");
            Invoke(quests, "OnEnable");
            return quests;
        }

        private static bool Contains(IReadOnlyList<CombatUnit> units, CombatUnit unit)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == unit) return true;
            }
            return false;
        }

        private static void WithSaveBackup(System.Action body)
        {
            const string saveKey = "CastleOfDice_SaveData";
            string backup = PlayerPrefs.HasKey(saveKey) ? PlayerPrefs.GetString(saveKey) : null;
            try
            {
                body();
            }
            finally
            {
                if (backup != null) PlayerPrefs.SetString(saveKey, backup);
                else PlayerPrefs.DeleteKey(saveKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Picks a seed whose first d20 satisfies <paramref name="first"/>.</summary>
        private static void SeedSoFirstRollIs(System.Func<int, bool> first)
        {
            SeedSoRollsAre(first, _ => true);
        }

        /// <summary>Picks a seed whose first two d20s satisfy the given tests.</summary>
        private static void SeedSoRollsAre(System.Func<int, bool> first, System.Func<int, bool> second)
        {
            for (int seed = 1; seed < 100000; seed++)
            {
                System.Random rng = new System.Random(seed);
                int r1 = rng.Next(1, 21);
                int r2 = rng.Next(1, 21);
                if (first(r1) && second(r2))
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
            Assert.IsNotNull(cls, classAsset);
            sessionData.SelectedClass = cls;
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero");
            hero.InitializeUnit();
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));
            return hero;
        }

        private static int FindSlot(PlayerUnit hero, string id)
        {
            for (int i = 0; i < hero.ActiveAbilities.Count; i++)
            {
                if (hero.ActiveAbilities[i].BaseAbilityID == id) return i;
            }
            Assert.Fail($"{id} not found.");
            return -1;
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
