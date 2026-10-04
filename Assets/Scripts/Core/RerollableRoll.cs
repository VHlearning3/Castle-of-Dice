using System;
using UnityEngine;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// A hero's d20 check that the Rune of Reroll can change (MasterSpec §4.2). The roll is shown in the
    /// dice modal as usual. Only when it fails and the hero holds a scroll (or the difficulty's free
    /// reroll) does the modal stop on [Continue] / [Use Reroll Scroll]; the outcome callback then runs
    /// with whichever result stands, so damage and effects always come from the final roll.
    /// Every other roll (enemy attacks, successes, no scroll) resolves at once, in the same call.
    /// </summary>
    public static class RerollableRoll
    {
        private static Action<DiceResult> pendingOutcome;
        private static DiceResult pendingResult;

        /// <summary>True while a failed hero roll waits on the Continue / Reroll choice.</summary>
        public static bool IsAwaitingDecision => pendingOutcome != null;

        /// <summary>
        /// Lets tests (and builds without the dice modal) decide whether a pause can be shown.
        /// Null = only in Play Mode with a dice modal in the scene.
        /// </summary>
        public static Func<bool> CanPauseOverride;

        /// <summary>Rerolls the hero can spend right now: scrolls plus the difficulty's free reroll.</summary>
        public static int AvailableRerolls
        {
            get
            {
                int scrolls = InventoryManager.Instance != null ? InventoryManager.Instance.RerollScrollCount : 0;
                return scrolls + (DifficultySettings.FreeRerollAvailable ? 1 : 0);
            }
        }

        /// <summary>
        /// Rolls d20 + <paramref name="bonus"/> against <paramref name="targetDC"/> for the hero and hands the
        /// final result to <paramref name="onResolved"/> (right away unless the hero may reroll a failure).
        /// </summary>
        public static DiceResult Roll(int bonus, int targetDC, AdvantageType advantage, Action<DiceResult> onResolved, string checkTitle = null)
        {
            DiceResult result = DiceSystem.RollD20(bonus, targetDC, advantage);
            if (!string.IsNullOrEmpty(checkTitle) && DiceUIController.Instance != null && CanPause())
            {
                DiceUIController.Instance.ShowDiceRoll(result, checkTitle);
            }
            Offer(result, onResolved);
            return result;
        }

        private static void Offer(DiceResult result, Action<DiceResult> onResolved)
        {
            if (!result.isSuccess && AvailableRerolls > 0 && CanPause())
            {
                pendingOutcome = onResolved ?? (_ => { });
                pendingResult = result;
                DiceUIController ui = DiceUIController.Instance;
                if (ui != null) ui.AwaitRerollDecision();
                return;
            }

            onResolved?.Invoke(result);
        }

        private static bool CanPause()
        {
            if (CanPauseOverride != null) return CanPauseOverride();
            return Application.isPlaying && DiceUIController.Instance != null;
        }

        /// <summary>[Continue]: the shown (failed) result stands.</summary>
        public static void Accept()
        {
            if (pendingOutcome == null) return;
            Action<DiceResult> outcome = pendingOutcome;
            DiceResult result = pendingResult;
            pendingOutcome = null;
            outcome(result);
        }

        /// <summary>
        /// [Use Reroll Scroll]: spends the free reroll first, then a scroll, and rolls again with the same
        /// bonus, DC and advantage. The new roll can be rerolled again while scrolls remain.
        /// </summary>
        public static bool Reroll()
        {
            if (pendingOutcome == null) return false;

            bool spent = DifficultySettings.TryUseFreeReroll()
                || (InventoryManager.Instance != null && InventoryManager.Instance.ConsumeRerollScroll());
            if (!spent)
            {
                Accept();
                return false;
            }

            Action<DiceResult> outcome = pendingOutcome;
            DiceResult previous = pendingResult;
            pendingOutcome = null;
            Debug.Log($"[RerollableRoll] Rune of Reroll used on {previous}.");
            Roll(previous.bonus, previous.targetDC, previous.advantageUsed, outcome);
            return true;
        }

        /// <summary>Drops a waiting decision without running it (scene change, test teardown).</summary>
        public static void Reset()
        {
            pendingOutcome = null;
        }
    }
}
