#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Units walk between combat grid tiles instead of teleporting. The tile is occupied right away so
    /// game logic is unchanged; outside Play Mode (these tests) the walk lands instantly.
    /// </summary>
    [TestFixture]
    public class GridWalkTests
    {
        private GameObject gridGo;
        private GridManager grid;
        private GameObject playerGo;
        private PlayerUnit player;
        private GameObject enemyGo;

        [SetUp]
        public void SetUp()
        {
            gridGo = new GameObject("Walk_GridManager");
            grid = gridGo.AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            playerGo = new GameObject("Walk_Player");
            playerGo.AddComponent<StatusEffectController>();
            player = playerGo.AddComponent<PlayerUnit>();
            player.InitializeUnit();
            player.MoveToTile(grid.GetTileAt(new Vector2Int(2, 2)));
        }

        [TearDown]
        public void TearDown()
        {
            if (enemyGo != null) Object.DestroyImmediate(enemyGo);
            if (playerGo != null) Object.DestroyImmediate(playerGo);
            if (gridGo != null) Object.DestroyImmediate(gridGo);
        }

        [Test]
        public void WalkToTile_OccupiesTileAndRunsArrivalCallback()
        {
            GridTile start = grid.GetTileAt(new Vector2Int(2, 2));
            GridTile destination = grid.GetTileAt(new Vector2Int(5, 3));
            bool arrived = false;

            player.WalkToTile(destination, () => arrived = true);

            Assert.IsTrue(arrived, "The arrival callback runs once the walk ends (instantly outside Play Mode).");
            Assert.IsFalse(player.IsWalking);
            Assert.AreEqual(destination.GridPosition, player.GridPosition);
            Assert.AreSame(player, destination.OccupyingUnit);
            Assert.IsFalse(start.IsOccupied, "The start tile is released.");

            Vector3 expected = grid.GetWorldPosition(destination.GridPosition);
            Assert.AreEqual(expected.x, player.transform.position.x, 0.001f);
            Assert.AreEqual(expected.z, player.transform.position.z, 0.001f);
        }

        [Test]
        public void WhenWalkFinished_RunsImmediatelyWhenStandingStill()
        {
            bool ran = false;
            player.WhenWalkFinished(() => ran = true);
            Assert.IsTrue(ran);
        }

        [Test]
        public void Enemy_WalksIntoReachThenAttacks()
        {
            enemyGo = new GameObject("Walk_Enemy");
            enemyGo.AddComponent<StatusEffectController>();
            EnemyUnit enemy = enemyGo.AddComponent<EnemyUnit>();
            enemy.InitializeUnit();
            enemy.ConfigureStats("Walk_Skeleton", 20, 12, 4, 2);
            enemy.MoveToTile(grid.GetTileAt(new Vector2Int(5, 2)));

            bool attacked = false;
            System.Action<Core.DiceResult> capture = r => attacked = true;
            Core.DiceSystem.OnDiceRolled += capture;
            try
            {
                enemy.ExecuteTurnAction(grid);
            }
            finally
            {
                Core.DiceSystem.OnDiceRolled -= capture;
            }

            Assert.LessOrEqual(grid.GetDistance(enemy.GridPosition, player.GridPosition), enemy.AttackRange, "The enemy walked next to the hero.");
            Assert.IsFalse(enemy.IsWalking);
            Assert.IsTrue(attacked, "The enemy attacks after its walk.");
        }
    }
}
#endif
