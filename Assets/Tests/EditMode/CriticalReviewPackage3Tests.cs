using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Critical review, package 3 ("tactical combat") and the combat extras: cooldowns, opportunity attacks,
    /// the balance pass, the King's telegraphed earthquake and gaze, Malakor the caster, cover and ground
    /// effects, initiative, levels 4-5 and difficulty.
    /// </summary>
    [TestFixture]
    public class CriticalReviewPackage3Tests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GameManager gameManager;
        private GridManager grid;
        private PlayerDataSO sessionData;
        private AbilityExecutor executor;

        [SetUp]
        public void SetUp()
        {
            sessionData = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerDataSO.Session = sessionData;

            gameManager = Track(new GameObject("P3_GameManager")).AddComponent<GameManager>();
            SetStatic(typeof(GameManager), "_instance", gameManager);

            grid = Track(new GameObject("P3_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            executor = CreateUnit<AbilityExecutor>("P3_Executor");
            SetStaticProperty(typeof(AbilityExecutor), "Instance", executor);

            RerollableRoll.Reset();
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

            foreach (TurnManager tm in Object.FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tm.gameObject);
            }
            foreach (EnemyUnit enemy in Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
            if (Dialogue.DialogueController.Instance != null) Object.DestroyImmediate(Dialogue.DialogueController.Instance.gameObject);
            SetStaticProperty(typeof(TurnManager), "Instance", null);
            SetStaticProperty(typeof(AbilityExecutor), "Instance", null);
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

        #region B1: cooldowns

        [Test]
        public void IronWill_CoolsDownForThreeTurns()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            int slot = FindSlot(hero, "warrior_iron_will");

            Assert.IsTrue(hero.UseAbility(slot, hero.GridPosition, executor));
            Assert.AreEqual(3, hero.GetCooldownRemaining(slot));

            for (int turn = 1; turn <= 2; turn++)
            {
                hero.ResetTurnFlags();
                hero.TickCooldowns();
                Assert.IsFalse(hero.CanUseAbility(slot), $"Still recharging on turn {turn} after use.");
            }

            hero.ResetTurnFlags();
            hero.TickCooldowns();
            Assert.IsTrue(hero.CanUseAbility(slot), "Ready again three turns later.");
        }

        [Test]
        public void Cooldowns_MatchTheReview()
        {
            Assert.AreEqual(3, AbilityCooldowns.GetCooldownTurns(LoadAbility("Ability_Warrior_IronWill")));
            Assert.AreEqual(3, AbilityCooldowns.GetCooldownTurns(LoadAbility("Ability_Mage_ManaShield")));
            Assert.AreEqual(2, AbilityCooldowns.GetCooldownTurns(LoadAbility("Ability_Rogue_SmokeBomb")));
            Assert.AreEqual(2, AbilityCooldowns.GetCooldownTurns(LoadAbility("Ability_Warrior_WarCry")));
            Assert.AreEqual(2, AbilityCooldowns.GetCooldownTurns(LoadAbility("Ability_Mage_Blink")));
            Assert.AreEqual(0, AbilityCooldowns.GetCooldownTurns(LoadAbility("Ability_Warrior_SwordSlash")));
        }

        #endregion

        #region B2: opportunity attacks

        [Test]
        public void WalkingAwayFromAnEnemy_ProvokesAFreeAttack_ButBlinkDoesNot()
        {
            PlayerUnit hero = CreateHero("Character_Mage_Elira", new Vector2Int(5, 5));
            EnemyUnit brute = CreateEnemy("Brute", new Vector2Int(6, 5), hp: 30, ac: 10, damage: 3, bonus: 100);
            StartCombat(hero, brute);

            List<EnemyUnit> attackers = new List<EnemyUnit>();
            hero.CollectOpportunityAttackers(new Vector2Int(5, 6), attackers);
            Assert.AreEqual(0, attackers.Count, "Stepping to another tile next to the brute is safe.");
            hero.CollectOpportunityAttackers(new Vector2Int(2, 5), attackers);
            Assert.AreEqual(1, attackers.Count);

            int hp = hero.CurrentHP;
            int blink = FindSlot(hero, "mage_blink");
            Assert.IsTrue(hero.UseAbility(blink, new Vector2Int(1, 5), executor));
            Assert.AreEqual(hp, hero.CurrentHP, "Blink teleports past the free attack.");

            // Walk back next to the brute, then walk away again
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));
            hero.WalkToTile(grid.GetTileAt(new Vector2Int(2, 5)));
            Assert.Less(hero.CurrentHP, hp, "Walking away gives the brute a free hit.");
        }

        #endregion

        #region B3: balance

        [Test]
        public void WarCry_RollsToHit()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            EnemyUnit wall = CreateEnemy("Iron Wall", new Vector2Int(6, 5), hp: 40, ac: 40, damage: 1, bonus: 0);
            StartCombat(hero, wall);

            int rolled = 0;
            System.Action<DiceResult> watch = r => rolled = r.rawRoll;
            DiceSystem.OnDiceRolled += watch;
            int slot = FindSlot(hero, "warrior_war_cry");
            try
            {
                Assert.IsTrue(hero.UseAbility(slot, hero.GridPosition, executor));
            }
            finally
            {
                DiceSystem.OnDiceRolled -= watch;
            }
            Assert.Greater(rolled, 0, "War Cry rolls to hit.");
            Assume.That(rolled, Is.LessThan(20), "A natural 20 always hits.");
            Assert.AreEqual(40, wall.CurrentHP, "A missed War Cry deals no damage.");
            Assert.AreEqual(new Vector2Int(6, 5), wall.GridPosition, "A missed War Cry pushes no one.");
        }

        [Test]
        public void Fireball_AddsIntelligence_AndBackstabHasNoFreeAdvantage()
        {
            Assert.IsTrue(LoadAbility("Ability_Mage_Fireball").AddsAttributeToDamage);

            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo", new Vector2Int(5, 5));
            EnemyUnit target = CreateEnemy("Target", new Vector2Int(6, 5), hp: 80, ac: 10, damage: 1, bonus: 0);
            StartCombat(rogue, target);

            AdvantageType seen = AdvantageType.Advantage;
            System.Action<DiceResult> watch = r => seen = r.advantageUsed;
            DiceSystem.OnDiceRolled += watch;
            try
            {
                rogue.UseAbility(FindSlot(rogue, "rogue_backstab"), target.GridPosition, executor);
            }
            finally
            {
                DiceSystem.OnDiceRolled -= watch;
            }
            Assert.AreEqual(AdvantageType.None, seen, "Backstab only gets Advantage out of a Shadow Step.");
        }

        [Test]
        public void Bosses_HaveMoreHealth_AndDiceDamage()
        {
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("King");
            king.InitializeUnit();
            Assert.Greater(king.MaxHP, 60);
            Assert.IsTrue(king.HasDamageDice);

            CursedCommanderBoss commander = CreateUnit<CursedCommanderBoss>("Commander");
            commander.InitializeUnit();
            Assert.Greater(commander.MaxHP, 50);
            Assert.IsTrue(commander.HasDamageDice);

            ShadowMageMalakorBoss malakor = CreateUnit<ShadowMageMalakorBoss>("Malakor");
            malakor.InitializeUnit();
            Assert.Greater(malakor.MaxHP, 40);
            Assert.IsTrue(malakor.HasDamageDice);
        }

        #endregion

        #region B4: the King's earthquake and gaze

        [Test]
        public void Earthquake_IsMarkedFirst_StrikesNextTurn_AndSparesTheKing()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("King");
            king.InitializeUnit();
            king.MoveToTile(grid.GetTileAt(new Vector2Int(6, 6)));
            StartCombat(hero, king);

            int heroHp = hero.CurrentHP;
            king.ExecuteGroundStomp(grid);
            Assert.IsTrue(king.IsQuakePending, "The first stomp only marks the ground.");
            Assert.IsTrue(grid.GetTileAt(new Vector2Int(5, 5)).HazardWarning, "The hero's tile is marked red.");
            Assert.AreEqual(heroHp, hero.CurrentHP);

            int kingHp = king.CurrentHP;
            king.ExecuteGroundStomp(grid);
            Assert.IsFalse(king.IsQuakePending);
            Assert.Less(hero.CurrentHP, heroHp, "The hero stayed in the marked area and was hit.");
            Assert.AreEqual(kingHp, king.CurrentHP, "The earthquake never hurts the King.");
            Assert.IsFalse(grid.GetTileAt(new Vector2Int(5, 5)).HazardWarning);
        }

        [Test]
        public void Earthquake_MissesAHeroWhoStepsOut()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("King");
            king.InitializeUnit();
            king.MoveToTile(grid.GetTileAt(new Vector2Int(9, 9)));
            StartCombat(hero, king);

            king.ExecuteGroundStomp(grid);
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(1, 1)));
            int hp = hero.CurrentHP;
            king.ExecuteGroundStomp(grid);
            Assert.AreEqual(hp, hero.CurrentHP);
        }

        [Test]
        public void PetrifyingGaze_FailedSave_LosesTheNextMove()
        {
            PlayerUnit hero = CreateHero("Character_Mage_Elira", new Vector2Int(5, 5));
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("King");
            king.InitializeUnit();
            king.EnterStoneForm();

            SeedSoRollsAre(r => r == 1);
            king.CastPetrifyingGaze(hero);
            Assert.AreEqual(0, hero.MovementRange, "A failed CON save roots the hero.");
        }

        [Test]
        public void OtheliasRing_BreaksTheStoneArmorForTwoTurns()
        {
            GargoyleKingBoss king = CreateUnit<GargoyleKingBoss>("King");
            king.InitializeUnit();
            int firstPhaseAc = king.ArmorClass;
            king.EnterStoneForm();
            Assert.AreEqual(firstPhaseAc + GargoyleKingBoss.StoneFormArmorBonus, king.ArmorClass);

            Assert.IsTrue(king.BreakStoneArmorWithRing());
            Assert.AreEqual(firstPhaseAc, king.ArmorClass);
            Assert.IsFalse(king.BreakStoneArmorWithRing(), "The ring works once.");
        }

        #endregion

        #region B5: Malakor the caster

        [Test]
        public void Malakor_CastsFromRange_AndTeleportsAtMostEveryOtherTurn()
        {
            ShadowMageMalakorBoss malakor = CreateUnit<ShadowMageMalakorBoss>("Malakor");
            malakor.InitializeUnit();
            malakor.MoveToTile(grid.GetTileAt(new Vector2Int(6, 6)));
            Assert.AreEqual(ShadowMageMalakorBoss.ShadowBoltRange, malakor.AttackRange);

            malakor.TakeDamage(3);
            Vector2Int afterFirst = malakor.GridPosition;
            Assert.AreNotEqual(new Vector2Int(6, 6), afterFirst, "The first hit makes him blink away.");
            Assert.Greater(malakor.TeleportCooldown, 0);

            malakor.TakeDamage(3);
            Assert.AreEqual(afterFirst, malakor.GridPosition, "He cannot blink again right away.");

            foreach (EnemyUnit decoy in malakor.ActiveDecoys)
            {
                Assert.Greater(decoy.AttackRange, 1, "His mirror image also fires shadow bolts.");
            }
        }

        #endregion

        #region B6: cover and ground

        [Test]
        public void Cover_BlocksOrHardensRangedAttacks()
        {
            GridTile pillar = grid.GetTileAt(new Vector2Int(5, 5));
            pillar.IsWalkable = false;
            pillar.Cover = TileCover.Full;
            Assert.IsFalse(grid.HasLineOfSight(new Vector2Int(5, 2), new Vector2Int(5, 8)));

            pillar.Cover = TileCover.Half;
            Assert.IsTrue(grid.HasLineOfSight(new Vector2Int(5, 2), new Vector2Int(5, 8)));
            Assert.AreEqual(GridManager.HalfCoverArmorBonus, grid.GetCoverArmorBonus(new Vector2Int(5, 2), new Vector2Int(5, 8)));
            Assert.AreEqual(0, grid.GetCoverArmorBonus(new Vector2Int(4, 5), new Vector2Int(5, 6)), "Next to each other there is no cover.");
        }

        [Test]
        public void ExplosiveBarrel_BurnsEveryoneAround_AndLeavesFlames()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            EnemyUnit goblin = CreateEnemy("Goblin", new Vector2Int(7, 6), hp: 30, ac: 10, damage: 1, bonus: 0);
            StartCombat(hero, goblin);

            ExplosiveBarrel barrel = Track(new GameObject("Barrel")).AddComponent<ExplosiveBarrel>();
            barrel.PlaceOn(grid.GetTileAt(new Vector2Int(6, 6)), grid);
            Assert.IsFalse(grid.GetTileAt(new Vector2Int(6, 6)).IsWalkable);

            int heroHp = hero.CurrentHP;
            barrel.Explode();
            Assert.Less(hero.CurrentHP, heroHp);
            Assert.Less(goblin.CurrentHP, 30);
            Assert.AreEqual(TileTerrain.Burning, grid.GetTileAt(new Vector2Int(6, 6)).Terrain);
            Assert.IsTrue(grid.GetTileAt(new Vector2Int(6, 6)).IsWalkable);

            // Starting a turn in the flames burns
            int before = goblin.CurrentHP;
            CombatTerrain.ApplyTurnStart(goblin, grid);
            Assert.Less(goblin.CurrentHP, before);

            grid.TickTerrain();
            grid.TickTerrain();
            Assert.AreEqual(TileTerrain.None, grid.GetTileAt(new Vector2Int(6, 6)).Terrain, "The flames die out after 2 rounds.");
        }

        [Test]
        public void Ice_CanStopAUnitFromMoving()
        {
            EnemyUnit skater = CreateEnemy("Skater", new Vector2Int(4, 4), hp: 10, ac: 10, damage: 1, bonus: 0);
            CombatTerrain.FreezePlus(grid, new Vector2Int(4, 4), 2);
            Assert.AreEqual(TileTerrain.Ice, grid.GetTileAt(new Vector2Int(4, 5)).Terrain);

            SeedSoRollsAre(r => r == 1);
            CombatTerrain.ApplyTurnStart(skater, grid);
            Assert.AreEqual(0, skater.MovementRange);
        }

        #endregion

        #region B7: initiative

        [Test]
        public void Initiative_OrdersTheFight()
        {
            PlayerUnit hero = CreateHero("Character_Rogue_Corvo", new Vector2Int(2, 2));
            EnemyUnit a = CreateEnemy("A", new Vector2Int(8, 8), hp: 10, ac: 10, damage: 1, bonus: 0);
            EnemyUnit b = CreateEnemy("B", new Vector2Int(9, 9), hp: 10, ac: 10, damage: 1, bonus: 0);
            TurnManager tm = StartCombat(hero, a, b);

            IReadOnlyList<CombatUnit> order = tm.ActiveUnits;
            for (int i = 1; i < order.Count; i++)
            {
                Assert.GreaterOrEqual(tm.GetInitiative(order[i - 1]), tm.GetInitiative(order[i]), "Highest initiative acts first.");
            }
            Assert.AreEqual(3, TurnManager.GetInitiativeBonus(hero), "Corvo adds his DEX.");
        }

        #endregion

        #region B8: levels 4 and 5

        [Test]
        public void LevelFour_TeachesTheFifthAbility()
        {
            foreach (string cls in new[] { "Character_Warrior_SirRoland", "Character_Mage_Elira", "Character_Rogue_Corvo" })
            {
                PlayerUnit hero = CreateHero(cls, new Vector2Int(1, 1));
                Assert.AreEqual(4, hero.ActiveAbilities.Count);
                hero.Level = 4;
                Assert.AreEqual(5, hero.ActiveAbilities.Count, cls + " learns a fifth ability at level 4.");
                Assert.AreEqual(hero.CharacterClass.Level4Ability, hero.ActiveAbilities[4]);
                hero.Level = 5;
                Assert.AreEqual(5, hero.Level);
                Assert.AreEqual(5, hero.ActiveAbilities.Count);
                Object.DestroyImmediate(hero.gameObject);
            }
        }

        [Test]
        public void ArcaneChains_RootTheTarget()
        {
            PlayerUnit mage = CreateHero("Character_Mage_Elira", new Vector2Int(2, 2));
            mage.Level = 4;
            EnemyUnit target = CreateEnemy("Target", new Vector2Int(4, 2), hp: 80, ac: 1, damage: 1, bonus: 0);
            StartCombat(mage, target);

            SeedSoRollsAre(r => r > 1);
            Assert.IsTrue(mage.UseAbility(4, target.GridPosition, executor));
            Assert.AreEqual(0, target.MovementRange);
        }

        [Test]
        public void Retaliation_StrikesBackAtMeleeAttackers()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            warrior.Level = 4;
            EnemyUnit brute = CreateEnemy("Brute", new Vector2Int(6, 5), hp: 80, ac: 10, damage: 1, bonus: 0);
            StartCombat(warrior, brute);

            Assert.IsTrue(warrior.UseAbility(4, warrior.GridPosition, executor));
            Assert.IsTrue(warrior.StatusEffects.HasEffect(StatusEffectType.Retaliation));
            brute.MakeOpportunityAttack(warrior);
            Assert.Less(brute.CurrentHP, 80);
        }

        #endregion

        #region B9: difficulty

        [Test]
        public void Difficulty_ChangesArmorAndEnemyAim()
        {
            PlayerUnit hero = CreateHero("Character_Warrior_SirRoland", new Vector2Int(5, 5));
            EnemyUnit enemy = CreateEnemy("Enemy", new Vector2Int(8, 8), hp: 10, ac: 10, damage: 1, bonus: 2);
            int ac = hero.ArmorClass;
            int aim = enemy.AttackBonus;

            DifficultySettings.Current = DifficultyLevel.Easy;
            Assert.AreEqual(ac + 2, hero.ArmorClass);
            Assert.AreEqual(aim, enemy.AttackBonus);

            DifficultySettings.Current = DifficultyLevel.Hard;
            Assert.AreEqual(ac, hero.ArmorClass);
            Assert.AreEqual(aim + 2, enemy.AttackBonus);
        }

        #endregion

        #region Helpers

        private TurnManager StartCombat(params CombatUnit[] units)
        {
            TurnManager tm = Track(new GameObject("P3_TurnManager")).AddComponent<TurnManager>();
            SetStaticProperty(typeof(TurnManager), "Instance", tm);
            tm.StartCombat(new List<CombatUnit>(units), awardVictoryScrap: false);
            return tm;
        }

        private static AbilitySO LoadAbility(string name)
        {
            AbilitySO ability = AssetDatabase.LoadAssetAtPath<AbilitySO>($"Assets/Data/{name}.asset");
            Assert.IsNotNull(ability, name);
            return ability;
        }

        private static void SeedSoRollsAre(System.Func<int, bool> first)
        {
            for (int seed = 1; seed < 100000; seed++)
            {
                System.Random rng = new System.Random(seed);
                if (first(rng.Next(1, 21)))
                {
                    DiceSystem.SetSeed(seed);
                    return;
                }
            }
            Assert.Fail("No seed found.");
        }

        private PlayerUnit CreateHero(string classAsset, Vector2Int tile)
        {
            CharacterClassSO cls = AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{classAsset}.asset");
            Assert.IsNotNull(cls, classAsset);
            sessionData.SelectedClass = cls;
            sessionData.ResetData();
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero_" + classAsset);
            hero.InitializeUnit();
            hero.MoveToTile(grid.GetTileAt(tile));
            return hero;
        }

        private EnemyUnit CreateEnemy(string name, Vector2Int tile, int hp, int ac, int damage, int bonus)
        {
            EnemyUnit enemy = CreateUnit<EnemyUnit>(name);
            enemy.ConfigureStats(name, hp, ac, damage, bonus);
            enemy.InitializeUnit();
            enemy.MoveToTile(grid.GetTileAt(tile));
            return enemy;
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
