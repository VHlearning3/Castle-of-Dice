using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Regression tests for the v2.6 spec audit: ability formulas, Shield Wall, boss add caps,
    /// and campaign progress that must survive a zone scene reload.
    /// </summary>
    [TestFixture]
    public class SpecAlignmentTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GridManager grid;
        private AbilityExecutor executor;
        private GameManager gameManager;
        private PlayerDataSO sessionData;

        [SetUp]
        public void SetUp()
        {
            DiceSystem.ClearSubscribers();
            sessionData = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerDataSO.Session = sessionData;

            grid = Track(new GameObject("Spec_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            executor = Track(new GameObject("Spec_AbilityExecutor")).AddComponent<AbilityExecutor>();

            gameManager = Track(new GameObject("Spec_GameManager")).AddComponent<GameManager>();
            SetStaticField(typeof(GameManager), "_instance", gameManager);
        }

        [TearDown]
        public void TearDown()
        {
            DiceSystem.ClearSubscribers();
            DiceSystem.ResetRandom();
            SetStaticField(typeof(GameManager), "_instance", null);
            SetStaticProperty(typeof(TurnManager), "Instance", null);
            GridManager.Instance = null;
            PlayerDataSO.Session = null;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
            if (sessionData != null) Object.DestroyImmediate(sessionData);
        }

        #region Ability Formulas

        [Test]
        public void SwordSlashDamage_Is1d8PlusAttributePlusWeapon()
        {
            AbilitySO slash = CreateAbility("warrior_sword_slash", AbilityTargetType.SingleTarget, 1, 0, dice: 1, sides: 8, addAttribute: true);

            Assert.AreEqual("1d8 + 4", slash.GetDamageFormula(attributeBonus: 3, flatBonus: 1));

            DiceSystem.SetSeed(42);
            for (int i = 0; i < 500; i++)
            {
                Assert.That(slash.RollDamage(attributeBonus: 3, flatBonus: 1), Is.InRange(5, 12));
            }
        }

        [Test]
        public void FireballDamage_Is2d6WithoutAttributeBonus()
        {
            AbilitySO fireball = CreateAbility("mage_fireball", AbilityTargetType.Area3x3, 4, 0, dice: 2, sides: 6, addAttribute: false);

            Assert.AreEqual("2d6", fireball.GetDamageFormula(attributeBonus: 3));

            DiceSystem.SetSeed(7);
            for (int i = 0; i < 500; i++)
            {
                Assert.That(fireball.RollDamage(attributeBonus: 3), Is.InRange(2, 12));
            }
        }

        [Test]
        public void RankTwoUpgrade_KeepsDiceAndAddsThree()
        {
            AbilitySO slash = CreateAbility("warrior_sword_slash", AbilityTargetType.SingleTarget, 1, 0, dice: 1, sides: 8, addAttribute: true);
            CharacterClassSO warrior = ScriptableObject.CreateInstance<CharacterClassSO>();
            Track(warrior);
            warrior.Initialize(CharacterClassType.Warrior, "Sir Roland", "", 30, 14, 4, 3, new List<AbilitySO> { slash });

            PlayerUnit hero = CreatePlayer(new Vector2Int(5, 5));
            hero.SetCharacterClass(warrior);
            Assert.IsTrue(hero.UpgradeAbilityToRank2(0));

            Assert.AreEqual("1d8 + 6", hero.ActiveAbilities[0].GetDamageFormula(attributeBonus: 3));
        }

        [Test]
        public void Backstab_DoublesOnlyAgainstBlindedTargets()
        {
            AbilitySO backstab = CreateAbility("rogue_backstab", AbilityTargetType.SingleTarget, 1, 5, dice: 0, sides: 0, addAttribute: false);
            PlayerUnit rogue = CreatePlayer(new Vector2Int(5, 5));

            int plainDamage = BackstabDamageAgainst(rogue, backstab, new Vector2Int(5, 6), blinded: false);
            int blindDamage = BackstabDamageAgainst(rogue, backstab, new Vector2Int(6, 6), blinded: true);

            if (plainDamage >= 0) Assert.AreEqual(5, plainDamage, "Backstab without blind or Shadow Step deals its normal damage.");
            if (blindDamage >= 0) Assert.AreEqual(10, blindDamage, "Backstab doubles against a blinded target.");
        }

        [Test]
        public void IronWill_HealsAndRemovesDebuffs()
        {
            AbilitySO ironWill = CreateAbility("warrior_iron_will", AbilityTargetType.Self, 0, 0, dice: 0, sides: 0, addAttribute: false);
            PlayerUnit warrior = CreatePlayer(new Vector2Int(5, 5));
            warrior.StatusEffects.ApplyEffect(StatusEffectType.Poison, 2);
            warrior.StatusEffects.ApplyEffect(StatusEffectType.Frostbite, 1);
            warrior.StatusEffects.ApplyEffect(StatusEffectType.Blind, 1);

            Assert.IsTrue(executor.ExecuteAbility(warrior, ironWill, warrior.GridPosition));

            Assert.IsFalse(warrior.StatusEffects.HasEffect(StatusEffectType.Poison));
            Assert.IsFalse(warrior.StatusEffects.HasEffect(StatusEffectType.Frostbite));
            Assert.IsFalse(warrior.StatusEffects.HasEffect(StatusEffectType.Blind));
        }

        [Test]
        public void WarCry_PushesAdjacentEnemyTwoTilesAndDamagesIt()
        {
            AbilitySO warCry = CreateAbility("warrior_war_cry", AbilityTargetType.Area3x3, 1, 3, dice: 0, sides: 0, addAttribute: false);
            PlayerUnit warrior = CreatePlayer(new Vector2Int(5, 5));
            EnemyUnit enemy = CreateEnemy("Spec_Enemy", new Vector2Int(6, 5), hp: 20, ac: 12);

            // War Cry rolls to hit since the critical review (B3): seed a plain hit (not a natural 20)
            for (int seed = 1; seed < 10000; seed++)
            {
                int roll = new System.Random(seed).Next(1, 21);
                if (roll >= 12 && roll < 20)
                {
                    DiceSystem.SetSeed(seed);
                    break;
                }
            }

            Assert.IsTrue(executor.ExecuteAbility(warrior, warCry, warrior.GridPosition));

            Assert.AreEqual(new Vector2Int(8, 5), enemy.GridPosition, "War Cry pushes up to 2 tiles when the way is clear.");
            Assert.AreEqual(17, enemy.CurrentHP);
        }

        [Test]
        public void Blink_ReachesSevenTiles()
        {
            AbilitySO blink = CreateAbility("mage_blink", AbilityTargetType.SingleTarget, 7, 0, dice: 0, sides: 0, addAttribute: false);
            PlayerUnit mage = CreatePlayer(new Vector2Int(1, 1));

            Assert.IsTrue(executor.ExecuteAbility(mage, blink, new Vector2Int(8, 1)));
            Assert.AreEqual(new Vector2Int(8, 1), mage.GridPosition);
            Assert.IsFalse(executor.ExecuteAbility(mage, blink, new Vector2Int(0, 1)), "8 tiles is beyond Blink's range.");
        }

        [Test]
        public void Teleports_TargetEmptyTilesInsteadOfUnits()
        {
            Assert.IsTrue(CreateAbility("mage_blink", AbilityTargetType.SingleTarget, 7, 0, 0, 0, false).TargetsEmptyTile);
            Assert.IsTrue(CreateAbility("rogue_shadow_step", AbilityTargetType.SingleTarget, 3, 0, 0, 0, false).TargetsEmptyTile);
            Assert.IsFalse(CreateAbility("mage_frostbite", AbilityTargetType.SingleTarget, 4, 0, 1, 6, true).TargetsEmptyTile);
        }

        [Test]
        public void Fireball_HitsEveryEnemyInTheThreeByThreeBlastButNotTheHero()
        {
            AbilitySO fireball = ScriptableObject.CreateInstance<AbilitySO>();
            Track(fireball);
            fireball.Initialize("mage_fireball", "Fireball", "", AbilityTargetType.Area3x3, 4, 1, 5, false,
                StatusEffectType.None, 0, "CastSpell", null, 0, 0, false);

            PlayerUnit mage = CreatePlayer(new Vector2Int(4, 5));
            EnemyUnit center = CreateEnemy("Spec_Center", new Vector2Int(5, 5), hp: 20, ac: 10);
            EnemyUnit corner = CreateEnemy("Spec_Corner", new Vector2Int(6, 6), hp: 20, ac: 10);
            EnemyUnit outside = CreateEnemy("Spec_Outside", new Vector2Int(7, 5), hp: 20, ac: 10);

            Assert.IsTrue(executor.ExecuteAbility(mage, fireball, new Vector2Int(5, 5)));

            Assert.AreEqual(15, center.CurrentHP, "The enemy on the target tile is burned.");
            Assert.AreEqual(15, corner.CurrentHP, "An enemy on a corner of the 3x3 area is burned too.");
            Assert.AreEqual(20, outside.CurrentHP, "Two tiles from the centre is outside the blast.");
            Assert.AreEqual(mage.MaxHP, mage.CurrentHP, "The mage standing in the blast is not hurt.");
        }

        [Test]
        public void MageAssets_FrostbiteSlowsTwoTurns_ManaShieldLastsThree()
        {
            AbilitySO frostbite = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Mage_Frostbite.asset");
            AbilitySO manaShield = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Mage_ManaShield.asset");
            Assert.IsNotNull(frostbite);
            Assert.IsNotNull(manaShield);
            Assert.AreEqual(2, frostbite.EffectDurationTurns);
            Assert.AreEqual(3, manaShield.EffectDurationTurns);
        }

        [Test]
        public void Frostbite_HalvesMovementForTwoOfTheTargetsTurns()
        {
            EnemyUnit enemy = CreateEnemy("Spec_Frozen", new Vector2Int(5, 5), hp: 20, ac: 10);
            int normal = enemy.MovementRange;
            enemy.StatusEffects.ApplyEffect(StatusEffectType.Frostbite, 2);

            Assert.AreEqual(Mathf.Max(1, normal / 2), enemy.MovementRange);
            enemy.StatusEffects.ProcessTurnEndEffects();
            Assert.AreEqual(Mathf.Max(1, normal / 2), enemy.MovementRange, "Still slowed on its second turn.");
            enemy.StatusEffects.ProcessTurnEndEffects();
            Assert.AreEqual(normal, enemy.MovementRange, "The chill wears off after two turns.");
        }

        #endregion

        #region Shield Wall

        [Test]
        public void ShieldWall_CountersOnlyWhenTheAttackMisses()
        {
            PlayerUnit warrior = CreatePlayer(new Vector2Int(5, 5));
            warrior.StatusEffects.ApplyEffect(StatusEffectType.ShieldWall, 1);

            for (int seed = 1; seed <= 20; seed++)
            {
                EnemyUnit enemy = CreateEnemy("Spec_Attacker_" + seed, new Vector2Int(5, 6), hp: 100, ac: 10);
                warrior.Revive();
                warrior.StatusEffects.ApplyEffect(StatusEffectType.ShieldWall, 1);

                DiceResult attack = default;
                System.Action<DiceResult> capture = r => attack = r;
                DiceSystem.OnDiceRolled += capture;
                DiceSystem.SetSeed(seed);
                enemy.ExecuteTurnAction(grid);
                DiceSystem.OnDiceRolled -= capture;

                if (attack.IsSuccess)
                {
                    Assert.AreEqual(100, enemy.CurrentHP, "A hit must not trigger the Shield Wall counterattack.");
                }
                else
                {
                    Assert.Less(enemy.CurrentHP, 100, "A miss against Shield Wall triggers a 1d6 counterattack.");
                }

                enemy.ClearTile();
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        #endregion

        #region Boss Adds

        [Test]
        public void CursedCommander_DoesNotSummonWhileHisGuardStands()
        {
            TurnManager turnManager = Track(new GameObject("Spec_TurnManager")).AddComponent<TurnManager>();
            SetStaticProperty(typeof(TurnManager), "Instance", turnManager);

            CursedCommanderBoss boss = Track(new GameObject("Spec_Commander")).AddComponent<CursedCommanderBoss>();
            boss.InitializeUnit();
            boss.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));
            EnemyUnit guard = CreateEnemy("Courtyard_Skeleton", new Vector2Int(4, 5), hp: 20, ac: 12);

            List<CombatUnit> units = GetField<List<CombatUnit>>(turnManager, "activeUnits");
            units.Add(boss);
            units.Add(guard);

            int before = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None).Length;
            boss.SpawnSkeletonReinforcements();
            int after = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None).Length;

            Assert.AreEqual(before, after, "Boss adds are capped at one: the standing guard already fills the slot.");
        }

        [Test]
        public void CursedCommander_SummonedSkeletonHasGuardStats()
        {
            CursedCommanderBoss boss = Track(new GameObject("Spec_Commander")).AddComponent<CursedCommanderBoss>();
            boss.InitializeUnit();
            boss.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));

            boss.SpawnSkeletonReinforcements();

            EnemyUnit summoned = null;
            foreach (EnemyUnit unit in Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None))
            {
                if (unit != boss && unit.name == "Armored Skeleton Guard") summoned = unit;
            }
            Assert.IsNotNull(summoned);
            Track(summoned.gameObject);
            Assert.AreEqual("Armored Skeleton Guard", summoned.UnitName);
            Assert.AreEqual(20, summoned.MaxHP);
        }

        [Test]
        public void Malakor_StandingDecoyWearsHisNameAndFillsTheCloneSlot()
        {
            EnemyUnit decoy = CreateEnemy("Shadow_Decoy", new Vector2Int(2, 2), hp: 18, ac: 12);
            decoy.SetDisplayName("Shadow Decoy");

            ShadowMageMalakorBoss boss = Track(new GameObject("Spec_Malakor")).AddComponent<ShadowMageMalakorBoss>();
            boss.InitializeUnit();
            boss.MoveToTile(grid.GetTileAt(new Vector2Int(6, 6)));

            Assert.AreEqual("Shadow Mage Malakor", decoy.UnitName, "Without Arcane Heresy the illusion must look like Malakor.");
            Assert.AreEqual(1, boss.ActiveDecoys.Count);

            boss.SpawnIllusionDecoys();
            Assert.AreEqual(1, boss.ActiveDecoys.Count, "Only one mirror image at a time.");
        }

        #endregion

        #region Campaign Progress Across Zone Reloads

        [Test]
        public void ClaimedRewards_RoundTripThroughCampaignProgress_AndResetOnNewGame()
        {
            gameManager.MarkRewardClaimed("Zone_6_Tower/GiantElixir");

            List<string> bosses = new List<string>();
            List<int> wings = new List<int>();
            List<string> rewards = new List<string>();
            gameManager.CaptureCampaignProgress(bosses, wings, rewards);
            CollectionAssert.Contains(rewards, "Zone_6_Tower/GiantElixir");

            gameManager.RestoreCampaignProgress(null, null);
            Assert.IsFalse(gameManager.IsRewardClaimed("Zone_6_Tower/GiantElixir"), "New Adventure clears claimed rewards.");

            gameManager.RestoreCampaignProgress(bosses, wings, rewards);
            Assert.IsTrue(gameManager.IsRewardClaimed("Zone_6_Tower/GiantElixir"));
        }

        [Test]
        public void BossRoom_StaysClearedWhenItsZoneLoadsAgain()
        {
            gameManager.RestoreCampaignProgress(new List<string> { "CursedCommander" }, null);

            GameObject bossGo = Track(new GameObject("Boss_CursedCommander"));
            DungeonRoomController room = Track(new GameObject("Courtyard_Encounter_Trigger")).AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = "CursedCommander";
            room.roomEnemies.Add(bossGo);

            InvokePrivate(room, "Start");
            try
            {
                Assert.IsTrue(room.IsCleared, "A defeated boss must not be fought again after a zone reload.");
                Assert.IsFalse(bossGo.activeSelf);

                room.TriggerEncounter();
                Assert.IsFalse(bossGo.activeSelf, "Walking into a cleared room starts no encounter.");
            }
            finally
            {
                InvokePrivate(room, "OnDestroy");
            }
        }

        [Test]
        public void GiantElixir_RecordsItsBonusOnce_AndStaysDrunkAfterReload()
        {
            PlayerUnit hero = CreatePlayer(new Vector2Int(5, 5));
            int baseMaxHP = hero.MaxHP;

            GiantElixirInteraction elixir = Track(new GameObject("GiantElixir")).AddComponent<GiantElixirInteraction>();
            elixir.Interact(hero);

            Assert.AreEqual(baseMaxHP + 30, hero.MaxHP);
            Assert.AreEqual(30, sessionData.MaxHPBonus, "The elixir's +30 must be recorded once, not twice.");

            // The Tower scene loads again: a fresh copy of the same elixir
            Object.DestroyImmediate(elixir.gameObject);
            GiantElixirInteraction reloaded = Track(new GameObject("GiantElixir")).AddComponent<GiantElixirInteraction>();
            InvokePrivate(reloaded, "Awake");

            Assert.IsTrue(reloaded.IsConsumed, "The elixir can only be drunk once per adventure.");
        }

        #endregion

        #region Helpers

        private int BackstabDamageAgainst(PlayerUnit rogue, AbilitySO backstab, Vector2Int pos, bool blinded)
        {
            EnemyUnit target = CreateEnemy("Spec_Target_" + pos, pos, hp: 100, ac: 10);
            if (blinded) target.StatusEffects.ApplyEffect(StatusEffectType.Blind, 1);

            DiceResult attack = default;
            System.Action<DiceResult> capture = r => attack = r;
            DiceSystem.OnDiceRolled += capture;
            executor.ExecuteAbility(rogue, backstab, pos);
            DiceSystem.OnDiceRolled -= capture;

            if (!attack.IsSuccess || attack.IsCriticalSuccess) return -1; // miss or crit: nothing to compare
            return 100 - target.CurrentHP;
        }

        private PlayerUnit CreatePlayer(Vector2Int pos)
        {
            GameObject go = Track(new GameObject("Spec_Player"));
            go.AddComponent<StatusEffectController>();
            PlayerUnit player = go.AddComponent<PlayerUnit>();
            player.InitializeUnit();
            player.MoveToTile(grid.GetTileAt(pos));
            return player;
        }

        private EnemyUnit CreateEnemy(string name, Vector2Int pos, int hp, int ac)
        {
            GameObject go = Track(new GameObject(name));
            go.AddComponent<StatusEffectController>();
            EnemyUnit enemy = go.AddComponent<EnemyUnit>();
            enemy.ConfigureStats(name, hp, ac, damage: 4, bonus: 2);
            enemy.InitializeUnit();
            enemy.MoveToTile(grid.GetTileAt(pos));
            return enemy;
        }

        private AbilitySO CreateAbility(string id, AbilityTargetType target, int range, int baseValue, int dice, int sides, bool addAttribute)
        {
            AbilitySO ability = ScriptableObject.CreateInstance<AbilitySO>();
            Track(ability);
            bool check = target != AbilityTargetType.Self && baseValue + dice > 0 && !id.Contains("war_cry");
            ability.Initialize(id, id, "", target, range, target == AbilityTargetType.Area3x3 ? 1 : 0, baseValue, check,
                StatusEffectType.None, 0, "Attack", null, dice, sides, addAttribute);
            return ability;
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    method.Invoke(target, null);
                    return;
                }
            }
            Assert.Fail($"Method '{methodName}' not found on {target.GetType().Name}.");
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void SetStaticField(System.Type type, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Static field '{fieldName}' not found on {type.Name}.");
            field.SetValue(null, value);
        }

        private static void SetStaticProperty(System.Type type, string propertyName, object value)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            property?.SetValue(null, value);
        }

        #endregion
    }
}
