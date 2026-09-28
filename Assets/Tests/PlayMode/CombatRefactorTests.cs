#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class CombatRefactorTests
    {
        private GameObject gridGo;
        private GridManager gridManager;
        private GameObject playerGo;
        private PlayerUnit player;
        private GameObject abilityExecutorGo;
        private AbilityExecutor abilityExecutor;
        private GameObject turnManagerGo;
        private TurnManager turnManager;

        [SetUp]
        public void SetUp()
        {
            gridGo = new GameObject("Test_GridManager");
            gridManager = gridGo.AddComponent<GridManager>();
            GridManager.Instance = gridManager;

            abilityExecutorGo = new GameObject("Test_AbilityExecutor");
            abilityExecutor = abilityExecutorGo.AddComponent<AbilityExecutor>();

            turnManagerGo = new GameObject("Test_TurnManager");
            turnManager = turnManagerGo.AddComponent<TurnManager>();

            playerGo = new GameObject("Test_PlayerUnit");
            playerGo.AddComponent<StatusEffectController>();
            player = playerGo.AddComponent<PlayerUnit>();
            player.InitializeUnit();
        }

        [TearDown]
        public void TearDown()
        {
            if (playerGo != null) Object.DestroyImmediate(playerGo);
            if (turnManagerGo != null) Object.DestroyImmediate(turnManagerGo);
            if (abilityExecutorGo != null) Object.DestroyImmediate(abilityExecutorGo);
            if (gridGo != null) Object.DestroyImmediate(gridGo);
        }

        #region 1. Battlefield Grid Expansion (12x12 & 1.6f tile size)

        [Test]
        public void GridManager_DefaultDimensionsAre12x12With1Point6TileSize()
        {
            Assert.AreEqual(12, gridManager.Width, "Default grid width must be 12 columns.");
            Assert.AreEqual(12, gridManager.Height, "Default grid height must be 12 rows.");
            Assert.AreEqual(1.6f, gridManager.TileSize, 0.01f, "Default tile size must be 1.6f.");
        }

        [Test]
        public void DungeonRoomController_DefaultsTo12x12CombatGrid()
        {
            GameObject roomGo = new GameObject("Test_Room");
            DungeonRoomController room = roomGo.AddComponent<DungeonRoomController>();

            Assert.AreEqual(12, room.gridWidth, "DungeonRoomController default gridWidth must be 12.");
            Assert.AreEqual(12, room.gridHeight, "DungeonRoomController default gridHeight must be 12.");
            Assert.AreEqual(1.6f, room.gridTileSize, 0.01f, "DungeonRoomController default gridTileSize must be 1.6f.");

            Object.DestroyImmediate(roomGo);
        }

        [Test]
        public void GridManager_GenerateGridAt_Generates144TilesWithCorrectDimensions()
        {
            gridManager.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            Assert.AreEqual(144, gridManager.Tiles.Count, "12x12 grid must generate exactly 144 tiles.");
            Assert.AreEqual(12, gridManager.Width);
            Assert.AreEqual(12, gridManager.Height);
            Assert.AreEqual(1.6f, gridManager.TileSize, 0.01f);
        }

        #endregion

        #region 2. Enemy Density & Boss Minions

        [Test]
        public void CursedCommanderBoss_SpawnsExactlyOneSkeletonAdd()
        {
            gridManager.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            GameObject bossGo = new GameObject("Test_CursedCommander");
            CursedCommanderBoss boss = bossGo.AddComponent<CursedCommanderBoss>();
            boss.InitializeUnit();
            boss.MoveToTile(gridManager.GetTileAt(new Vector2Int(5, 5)));

            boss.SpawnSkeletonReinforcements();

            Assert.IsTrue(boss.HasSpawnedAdds, "Boss must mark reinforcements as spawned.");

            // Verify only 1 skeleton was spawned
            EnemyUnit[] skeletons = Object.FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None);
            int minionCount = 0;
            foreach (var enemy in skeletons)
            {
                if (enemy != boss && enemy.name.Contains("Skeleton"))
                {
                    minionCount++;
                }
            }

            Assert.AreEqual(1, minionCount, "CursedCommander must spawn exactly 1 skeleton add (solo hero balance).");

            foreach (var enemy in skeletons)
            {
                if (enemy != boss) Object.DestroyImmediate(enemy.gameObject);
            }
            Object.DestroyImmediate(bossGo);
        }

        [Test]
        public void ShadowMageMalakorBoss_SpawnsExactlyOneMirrorDecoy()
        {
            gridManager.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            GameObject bossGo = new GameObject("Test_Malakor");
            ShadowMageMalakorBoss boss = bossGo.AddComponent<ShadowMageMalakorBoss>();
            boss.InitializeUnit();
            boss.MoveToTile(gridManager.GetTileAt(new Vector2Int(6, 6)));

            boss.SpawnIllusionDecoys();

            Assert.AreEqual(1, boss.ActiveDecoys.Count, "ShadowMageMalakor must summon exactly 1 decoy clone (solo hero balance).");

            foreach (var decoy in boss.ActiveDecoys)
            {
                if (decoy != null) Object.DestroyImmediate(decoy.gameObject);
            }
            Object.DestroyImmediate(bossGo);
        }

        [Test]
        public void DungeonRoomController_LimitsRegularEncounterToMaxTwoEnemiesOrOneElite()
        {
            gridManager.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            GameObject roomGo = new GameObject("Test_Room");
            DungeonRoomController room = roomGo.AddComponent<DungeonRoomController>();
            room.bossIdentifier = ""; // Regular encounter

            // Create 3 weaker enemies (MaxHP 15, damage 3)
            List<GameObject> enemies = new List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                GameObject eGo = new GameObject($"Weak_Enemy_{i}");
                EnemyUnit eu = eGo.AddComponent<EnemyUnit>();
                eu.InitializeUnit();
                eGo.transform.position = gridManager.GetWorldPosition(new Vector2Int(2 + i, 2));
                enemies.Add(eGo);
            }
            room.roomEnemies = enemies;

            room.TriggerEncounter();

            int activeCount = 0;
            foreach (var e in enemies)
            {
                if (e.activeSelf) activeCount++;
            }

            Assert.AreEqual(2, activeCount, "Regular encounter for solo hero must be capped to max 2 weaker enemies.");

            foreach (var e in enemies) Object.DestroyImmediate(e);
            Object.DestroyImmediate(roomGo);
        }

        #endregion

        #region 3. Solo Hero Movement Ranges

        [Test]
        public void HeroMovementRanges_Match12x12TacticalAdjustments()
        {
            // Warrior: 4 tiles
            CharacterClassSO warrior = ScriptableObject.CreateInstance<CharacterClassSO>();
            warrior.Initialize(CharacterClassType.Warrior, "Sir Roland", "Frontline", 35, 15, 4, 3, new List<AbilitySO>());
            player.SetCharacterClass(warrior);
            Assert.AreEqual(4, player.MovementRange, "Warrior (Sir Roland) base movement range must be 4 tiles on 12x12 grid.");

            // Wizard: 3 tiles
            CharacterClassSO wizard = ScriptableObject.CreateInstance<CharacterClassSO>();
            wizard.Initialize(CharacterClassType.Mage, "Scholar Elira", "Arcanist", 20, 11, 3, 4, new List<AbilitySO>());
            player.SetCharacterClass(wizard);
            Assert.AreEqual(3, player.MovementRange, "Wizard (Elira) base movement range must remain 3 tiles.");

            // Rogue: 5 tiles
            CharacterClassSO rogue = ScriptableObject.CreateInstance<CharacterClassSO>();
            rogue.Initialize(CharacterClassType.Rogue, "Shadow-Corvo", "Shadow", 25, 13, 5, 4, new List<AbilitySO>());
            player.SetCharacterClass(rogue);
            Assert.AreEqual(5, player.MovementRange, "Rogue (Corvo) base movement range must be 5 tiles on 12x12 grid.");

            Object.DestroyImmediate(warrior);
            Object.DestroyImmediate(wizard);
            Object.DestroyImmediate(rogue);
        }

        #endregion

        #region 4. Rogue Ability Rework — Shadow Step

        [Test]
        public void ShadowStep_TeleportsWithinThreeTilesAndAppliesAdvantageNextAttack()
        {
            gridManager.GenerateGridAt(new Vector3(500f, 500f, 500f), 12, 12, 1.6f);
            player.MoveToTile(gridManager.GetTileAt(new Vector2Int(5, 5)));

            Vector2Int targetPos = new Vector2Int(5, 8); // Distance = 3 tiles
            bool success = abilityExecutor.ExecuteShadowStep(player, targetPos, gridManager);

            Assert.IsTrue(success, "Shadow Step must execute successfully up to 3 tiles.");
            Assert.AreEqual(targetPos, player.GridPosition, "Player must be teleported to target tile.");
            Assert.IsTrue(player.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack),
                "Shadow Step must apply 1-turn AdvantageNextAttack status effect.");
            Assert.AreEqual(AdvantageType.Advantage, player.StatusEffects.GetAttackRollAdvantageModifier(),
                "Attack roll modifier must be Advantage when AdvantageNextAttack is active.");
        }

        [Test]
        public void ShadowStep_RejectsDistancesGreaterThanThree()
        {
            gridManager.GenerateGridAt(new Vector3(500f, 500f, 500f), 12, 12, 1.6f);
            player.MoveToTile(gridManager.GetTileAt(new Vector2Int(5, 5)));

            Vector2Int outOfRangePos = new Vector2Int(5, 9); // Distance = 4 tiles
            bool success = abilityExecutor.ExecuteShadowStep(player, outOfRangePos, gridManager);

            Assert.IsFalse(success, "Shadow Step must reject targets further than 3 tiles.");
            Assert.AreEqual(new Vector2Int(5, 5), player.GridPosition, "Player position must remain unchanged on failure.");
            Assert.IsFalse(player.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack),
                "Advantage effect must not be applied if Shadow Step fails.");
        }

        [Test]
        public void AdvantageNextAttack_IsConsumedAfterAttackRoll()
        {
            gridManager.GenerateGridAt(new Vector3(500f, 500f, 500f), 12, 12, 1.6f);
            player.MoveToTile(gridManager.GetTileAt(new Vector2Int(5, 5)));

            // Apply AdvantageNextAttack via Shadow Step
            abilityExecutor.ExecuteShadowStep(player, new Vector2Int(5, 6), gridManager);
            Assert.IsTrue(player.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack));

            // Create target enemy
            GameObject enemyGo = new GameObject("Test_Enemy");
            enemyGo.AddComponent<StatusEffectController>();
            EnemyUnit enemy = enemyGo.AddComponent<EnemyUnit>();
            enemy.InitializeUnit();
            enemy.MoveToTile(gridManager.GetTileAt(new Vector2Int(5, 7)));

            // Create strike ability
            AbilitySO slash = ScriptableObject.CreateInstance<AbilitySO>();
            slash.Initialize("test_slash", "Slash", "Basic strike", AbilityTargetType.SingleTarget, 1, 0, 6, true, StatusEffectType.None, 0, "Attack");

            // Execute attack
            bool attackResult = abilityExecutor.ExecuteAbility(player, slash, new Vector2Int(5, 7));
            Assert.IsTrue(attackResult);

            // Verify Advantage was consumed
            Assert.IsFalse(player.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack),
                "AdvantageNextAttack must be consumed immediately after resolving an attack roll.");

            Object.DestroyImmediate(slash);
            Object.DestroyImmediate(enemyGo);
        }

        [Test]
        public void PlayerUnit_ExcludesLockpickFromActiveCombatAbilities()
        {
            AbilitySO lockpickAbility = ScriptableObject.CreateInstance<AbilitySO>();
            lockpickAbility.Initialize("rogue_lockpick", "Lockpicking", "Pick locks", AbilityTargetType.SingleTarget, 1, 0, 0, true, StatusEffectType.None, 0, "Interact");

            AbilitySO shadowStep = ScriptableObject.CreateInstance<AbilitySO>();
            shadowStep.Initialize("rogue_shadow_step", "Shadow Step", "Teleport 3 tiles", AbilityTargetType.SingleTarget, 3, 0, 0, false, StatusEffectType.AdvantageNextAttack, 1, "Attack");

            CharacterClassSO rogueClass = ScriptableObject.CreateInstance<CharacterClassSO>();
            rogueClass.Initialize(CharacterClassType.Rogue, "Corvo", "Rogue", 25, 13, 5, 4,
                new List<AbilitySO> { shadowStep, lockpickAbility });

            player.SetCharacterClass(rogueClass);

            bool hasShadowStep = false;
            foreach (var ability in player.ActiveAbilities)
            {
                Assert.AreNotEqual("rogue_lockpick", ability.AbilityID,
                    "Active combat abilities must never contain Lockpicking.");
                if (ability == shadowStep)
                    hasShadowStep = true;
            }
            Assert.IsTrue(hasShadowStep,
                "Active combat abilities must include Shadow Step.");

            Object.DestroyImmediate(lockpickAbility);
            Object.DestroyImmediate(shadowStep);
            Object.DestroyImmediate(rogueClass);
        }

        #endregion
    }
}
#endif
