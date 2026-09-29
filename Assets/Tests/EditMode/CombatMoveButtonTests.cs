#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The hero only moves on the combat grid after pressing the Move button, so a stray tile click
    /// (or a click that slips through the action bar) never spends the move.
    /// </summary>
    [TestFixture]
    public class CombatMoveButtonTests
    {
        private GameObject gridGo;
        private GridManager gridManager;
        private GameObject turnManagerGo;
        private GameObject playerGo;
        private PlayerUnit player;
        private GameObject uiGo;
        private CombatUIController combatUI;
        private MethodInfo handleTileClicked;

        private static readonly Vector2Int StartPos = new Vector2Int(2, 2);

        [SetUp]
        public void SetUp()
        {
            gridGo = new GameObject("Test_GridManager");
            gridManager = gridGo.AddComponent<GridManager>();
            GridManager.Instance = gridManager;
            gridManager.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            turnManagerGo = new GameObject("Test_TurnManager");
            turnManagerGo.AddComponent<TurnManager>();
            TurnManager.EnsureInstance();
            Assume.That(TurnManager.Instance, Is.Not.Null);
            Assume.That(TurnManager.Instance.CurrentState, Is.EqualTo(TurnState.PlayerTurn));

            playerGo = new GameObject("Test_PlayerUnit");
            playerGo.AddComponent<StatusEffectController>();
            player = playerGo.AddComponent<PlayerUnit>();
            player.InitializeUnit();
            CharacterClassSO warrior = ScriptableObject.CreateInstance<CharacterClassSO>();
            // One ability so the combat UI doesn't re-initialize the hero (and reset its turn flags) on every click
            var abilities = new List<AbilitySO> { ScriptableObject.CreateInstance<AbilitySO>() };
            warrior.Initialize(CharacterClassType.Warrior, "Sir Roland", "Frontline", 30, 14, 4, 3, abilities);
            player.SetCharacterClass(warrior);
            player.MoveToTile(gridManager.GetTileAt(StartPos));
            player.HasMovedThisTurn = false;
            player.HasActedThisTurn = false;

            uiGo = new GameObject("Test_CombatUI");
            combatUI = uiGo.AddComponent<CombatUIController>();

            handleTileClicked = typeof(CombatUIController).GetMethod("HandleTileClicked", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handleTileClicked, "CombatUIController.HandleTileClicked not found.");
        }

        [TearDown]
        public void TearDown()
        {
            if (uiGo != null) Object.DestroyImmediate(uiGo);
            if (playerGo != null) Object.DestroyImmediate(playerGo);
            if (turnManagerGo != null) Object.DestroyImmediate(turnManagerGo);
            if (gridGo != null) Object.DestroyImmediate(gridGo);
        }

        private void ClickTile(Vector2Int pos)
        {
            handleTileClicked.Invoke(combatUI, new object[] { gridManager.GetTileAt(pos) });
        }

        [Test]
        public void ClickingEmptyTile_WithoutMoveMode_DoesNotMoveHero()
        {
            Assert.IsFalse(combatUI.IsMoveModeActive, "Move mode must start off.");

            ClickTile(new Vector2Int(3, 2));

            Assert.AreEqual(StartPos, player.GridPosition, "Hero moved without pressing Move.");
            Assert.IsFalse(player.HasMovedThisTurn, "Move was spent without pressing Move.");
        }

        [Test]
        public void ClickingReachableTile_InMoveMode_MovesHeroAndEndsMoveMode()
        {
            combatUI.ToggleMoveMode();
            Assert.IsTrue(combatUI.IsMoveModeActive, "Move button should turn move mode on.");

            Vector2Int target = new Vector2Int(4, 2);
            ClickTile(target);

            Assert.AreEqual(target, player.GridPosition, "Hero should move to the clicked tile in move mode.");
            Assert.IsTrue(player.HasMovedThisTurn);
            Assert.IsFalse(combatUI.IsMoveModeActive, "Move mode should end after moving.");
        }

        [Test]
        public void PressingMoveTwice_CancelsMoveMode()
        {
            combatUI.ToggleMoveMode();
            combatUI.ToggleMoveMode();
            Assert.IsFalse(combatUI.IsMoveModeActive);

            ClickTile(new Vector2Int(2, 4));
            Assert.AreEqual(StartPos, player.GridPosition, "Cancelled move mode must not move the hero.");
        }

        [Test]
        public void MoveButton_DoesNothingAfterHeroHasMoved()
        {
            player.HasMovedThisTurn = true;
            combatUI.ToggleMoveMode();
            Assert.IsFalse(combatUI.IsMoveModeActive, "Move mode can't turn on once the move is spent.");
        }
    }
}
#endif
