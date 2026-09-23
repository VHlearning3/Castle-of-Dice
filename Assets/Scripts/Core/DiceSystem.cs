using System;
using UnityEngine;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Thread-safe core dice engine implementing D20 rules and general dice rolling mechanics.
    /// Supports standard D20 rolls, Advantage, Disadvantage, Critical Success/Failure evaluations,
    /// and dispatches roll events to listeners (e.g., UI animations, combat logs, audio triggers).
    /// Can be utilized statically via <see cref="RollD20"/> or via the singleton <see cref="Instance"/>.
    /// </summary>
    public sealed class DiceSystem
    {
        #region Singleton & Thread-Safety

        private static readonly Lazy<DiceSystem> s_instance = new Lazy<DiceSystem>(() => new DiceSystem());

        /// <summary>
        /// Thread-safe singleton instance for systems utilizing dependency injection or instance references.
        /// </summary>
        public static DiceSystem Instance => s_instance.Value;

        private static readonly object s_rngLock = new object();
        private static System.Random s_random = new System.Random();

        /// <summary>
        /// Private constructor ensuring singleton instantiation.
        /// </summary>
        private DiceSystem()
        {
        }

        #endregion

        #region Events

        /// <summary>
        /// Invoked every time a D20 roll is finalized and evaluated.
        /// Useful for driving 3D/2D dice animations (<c>DiceUIController</c>), playing audio, and combat logs.
        /// </summary>
#nullable enable
        public static event Action<DiceResult>? OnDiceRolled;
#nullable restore

        #endregion

        #region Core D20 Mechanics

        /// <summary>
        /// Rolls a 20-sided die (D20), applies character bonuses, evaluates against a target DC/AC,
        /// and applies Advantage or Disadvantage rules.
        /// </summary>
        /// <param name="bonus">The modifier added to the raw roll (e.g., +3 Strength bonus).</param>
        /// <param name="targetDC">The target Difficulty Class or enemy Armor Class to meet or exceed.</param>
        /// <param name="advantage">Specifies whether the roll has Advantage, Disadvantage, or None.</param>
        /// <returns>A finalized <see cref="DiceResult"/> containing the roll outcome and critical evaluation.</returns>
        public static DiceResult RollD20(int bonus, int targetDC, AdvantageType advantage = AdvantageType.None)
        {
            int firstRoll = RollDice(20);
            int secondRoll = 0;
            int chosenRawRoll;

            switch (advantage)
            {
                case AdvantageType.Advantage:
                    secondRoll = RollDice(20);
                    chosenRawRoll = Math.Max(firstRoll, secondRoll);
                    break;

                case AdvantageType.Disadvantage:
                    secondRoll = RollDice(20);
                    chosenRawRoll = Math.Min(firstRoll, secondRoll);
                    break;

                case AdvantageType.None:
                default:
                    secondRoll = firstRoll;
                    chosenRawRoll = firstRoll;
                    break;
            }

            DiceResult result = new DiceResult(
                rawRoll: chosenRawRoll,
                bonus: bonus,
                targetDC: targetDC,
                advantageUsed: advantage,
                firstRoll: firstRoll,
                secondRoll: secondRoll
            );

            // Notify subscribers (UI, sound effects, combat log)
            NotifyDiceRolled(result);

            return result;
        }

        /// <summary>
        /// Evaluates an explicit raw roll value against a target DC without rolling a new random number.
        /// Useful for deterministic testing and physical 3D dice physics results.
        /// </summary>
        /// <param name="rawRoll">The raw die result to evaluate (clamped to 1..20).</param>
        /// <param name="bonus">Modifier added to the raw roll.</param>
        /// <param name="targetDC">Target Difficulty Class or Armor Class.</param>
        /// <param name="advantage">The advantage mode used, if applicable.</param>
        /// <param name="triggerEvent">Whether to trigger the <see cref="OnDiceRolled"/> event.</param>
        /// <returns>A finalized <see cref="DiceResult"/>.</returns>
        public static DiceResult EvaluateRoll(int rawRoll, int bonus, int targetDC, AdvantageType advantage = AdvantageType.None, bool triggerEvent = true)
        {
            int clampedRoll = Math.Max(1, Math.Min(20, rawRoll));
            DiceResult result = new DiceResult(clampedRoll, bonus, targetDC, advantage, clampedRoll, clampedRoll);

            if (triggerEvent)
            {
                NotifyDiceRolled(result);
            }

            return result;
        }

        #endregion

        #region General Dice Helpers

        /// <summary>
        /// Rolls an N-sided die, generating a uniform integer between 1 and <paramref name="sides"/> (inclusive).
        /// Thread-safe for multi-threaded invocations.
        /// </summary>
        /// <param name="sides">Number of sides on the die (must be >= 1).</param>
        /// <returns>An integer in the range [1, sides].</returns>
        public static int RollDice(int sides)
        {
            if (sides < 1)
            {
                Debug.LogWarning($"[DiceSystem] Attempted to roll a die with invalid number of sides: {sides}. Defaulting to 1.");
                return 1;
            }

            lock (s_rngLock)
            {
                return s_random.Next(1, sides + 1);
            }
        }

        /// <summary>
        /// Convenience helper to roll a standard 6-sided die (d6), commonly used for poison or basic damage ticks.
        /// </summary>
        /// <returns>An integer between 1 and 6.</returns>
        public static int RollD6() => RollDice(6);

        /// <summary>
        /// Convenience helper to roll a raw, un-evaluated 20-sided die (d20).
        /// </summary>
        /// <returns>An integer between 1 and 20.</returns>
        public static int RollD20Raw() => RollDice(20);

        /// <summary>
        /// Rolls multiple dice of the same type and computes their sum plus an optional flat bonus.
        /// Example: 2d6 + 3 damage for an ability attack.
        /// </summary>
        /// <param name="diceCount">The number of dice to roll (must be >= 1).</param>
        /// <param name="sides">The number of sides per die.</param>
        /// <param name="bonus">Flat bonus added to the cumulative total.</param>
        /// <returns>The sum of all dice plus bonus.</returns>
        public static int RollDamage(int diceCount, int sides, int bonus = 0)
        {
            if (diceCount <= 0) return Math.Max(0, bonus);

            int sum = bonus;
            for (int i = 0; i < diceCount; i++)
            {
                sum += RollDice(sides);
            }

            return sum;
        }

        #endregion

        #region Seed & Testing Management

        /// <summary>
        /// Sets a specific random seed for deterministic rolls (ideal for unit testing and replays).
        /// </summary>
        /// <param name="seed">Deterministic seed value.</param>
        public static void SetSeed(int seed)
        {
            lock (s_rngLock)
            {
                s_random = new System.Random(seed);
            }
        }

        /// <summary>
        /// Resets the internal random generator to a standard time-seeded instance.
        /// </summary>
        public static void ResetRandom()
        {
            lock (s_rngLock)
            {
                s_random = new System.Random();
            }
        }

        /// <summary>
        /// Safely clears all subscribers from <see cref="OnDiceRolled"/>.
        /// Useful during scene unloading or test teardowns to prevent lingering references.
        /// </summary>
        public static void ClearSubscribers()
        {
            OnDiceRolled = null;
        }

        #endregion

        #region Instance / Singleton Facade Methods

        /// <summary>
        /// Instance wrapper for <see cref="RollD20"/>.
        /// </summary>
        public DiceResult Roll(int bonus, int targetDC, AdvantageType advantage = AdvantageType.None)
        {
            return RollD20(bonus, targetDC, advantage);
        }

        /// <summary>
        /// Instance wrapper for <see cref="RollDice(int)"/>.
        /// </summary>
        public int Roll(int sides)
        {
            return RollDice(sides);
        }

        #endregion

        #region Private Utilities

        private static void NotifyDiceRolled(DiceResult result)
        {
            try
            {
                // Self-healing: Ensure DiceUIController is awake and presenting the animated roll modal
                DiceUIController ui = DiceUIController.Instance;
                if (ui != null)
                {
                    if (!ui.gameObject.activeInHierarchy)
                    {
                        ui.gameObject.SetActive(true);
                    }
                    ui.ShowDiceRoll(result);
                }

                OnDiceRolled?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiceSystem] Exception thrown during OnDiceRolled invocation: {ex}");
            }
        }

        #endregion
    }
}
