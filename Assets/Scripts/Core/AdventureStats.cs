using System;
using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Tallies of the current adventure shown on the ending's stats screen: combat turns the hero took,
    /// natural 20s the hero rolled and how many times the hero fell. Village brawls do not count.
    /// Saved with the game and cleared when a new adventure starts.
    /// </summary>
    public static class AdventureStats
    {
        /// <summary>Combat turns the hero has taken this adventure.</summary>
        public static int TurnsTaken { get; private set; }

        /// <summary>Natural 20s rolled by the hero (attacks, skill checks, lockpicking...).</summary>
        public static int NaturalTwenties { get; private set; }

        /// <summary>Fights the hero lost.</summary>
        public static int Deaths { get; private set; }

        /// <summary>Clears every tally for a new adventure.</summary>
        public static void Reset()
        {
            TurnsTaken = 0;
            NaturalTwenties = 0;
            Deaths = 0;
        }

        /// <summary>Restores the tallies from a save.</summary>
        public static void Restore(int turnsTaken, int naturalTwenties, int deaths)
        {
            TurnsTaken = Mathf.Max(0, turnsTaken);
            NaturalTwenties = Mathf.Max(0, naturalTwenties);
            Deaths = Mathf.Max(0, deaths);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            Reset();
            Unhook();
            TurnManager.OnUnitTurnStarted += HandleUnitTurnStarted;
            TurnManager.OnTurnStateChanged += HandleTurnStateChanged;
            DiceSystem.OnDiceRolled += HandleDiceRolled;
        }

        /// <summary>Stops counting (used by tests that drive the handlers directly).</summary>
        public static void Unhook()
        {
            TurnManager.OnUnitTurnStarted -= HandleUnitTurnStarted;
            TurnManager.OnTurnStateChanged -= HandleTurnStateChanged;
            DiceSystem.OnDiceRolled -= HandleDiceRolled;
        }

        /// <summary>Whether the running fight is a real one (a village brawl pays no scrap and is not tallied).</summary>
        private static bool IsRealFight => TurnManager.Instance == null || TurnManager.Instance.AwardsVictoryScrap;

        public static void HandleUnitTurnStarted(CombatUnit unit)
        {
            if (unit is PlayerUnit && IsRealFight) TurnsTaken++;
        }

        public static void HandleTurnStateChanged(TurnState state)
        {
            if (state == TurnState.Defeat && IsRealFight) Deaths++;
        }

        public static void HandleDiceRolled(DiceResult result)
        {
            if (!result.isCriticalSuccess) return;

            // Enemies roll only on their own turn; every other roll is the hero's
            TurnManager tm = TurnManager.Instance;
            if (tm != null && tm.IsCombatActive && tm.CurrentState == TurnState.EnemyTurn) return;

            NaturalTwenties++;
        }
    }
}
