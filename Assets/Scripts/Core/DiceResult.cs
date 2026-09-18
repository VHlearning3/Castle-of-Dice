using System;
using UnityEngine;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Represents the evaluation and outcome of a D20 dice roll.
    /// Encapsulates raw dice values, bonuses, target DC/AC thresholds, and critical condition flags.
    /// </summary>
    [Serializable]
    public struct DiceResult : IEquatable<DiceResult>
    {
        #region Public Fields

        /// <summary>
        /// The effective rolled value on the D20 die (1..20) after accounting for Advantage or Disadvantage.
        /// </summary>
        public int rawRoll;

        /// <summary>
        /// Flat modifier added to the raw roll (e.g., character attribute bonus, equipment buff, proficiency).
        /// </summary>
        public int bonus;

        /// <summary>
        /// Final calculated roll value: <see cref="rawRoll"/> + <see cref="bonus"/>.
        /// </summary>
        public int finalTotal;

        /// <summary>
        /// The Difficulty Class (DC) for skill checks or Armor Class (AC) of the target combatant.
        /// </summary>
        public int targetDC;

        /// <summary>
        /// Indicates whether the check or attack succeeded.
        /// Guaranteed true on a Natural 20 (Critical Success).
        /// Guaranteed false on a Natural 1 (Critical Failure).
        /// Otherwise true when <see cref="finalTotal"/> meets or exceeds <see cref="targetDC"/>.
        /// </summary>
        public bool isSuccess;

        /// <summary>
        /// True if the chosen raw roll is a natural 20.
        /// </summary>
        public bool isCriticalSuccess;

        /// <summary>
        /// True if the chosen raw roll is a natural 1.
        /// </summary>
        public bool isCriticalFail;

        /// <summary>
        /// The advantage condition applied when this roll was made.
        /// </summary>
        public AdvantageType advantageUsed;

        /// <summary>
        /// The first die roll result (identical to <see cref="rawRoll"/> if no advantage/disadvantage).
        /// </summary>
        public int firstRoll;

        /// <summary>
        /// The second die roll result when advantage or disadvantage is rolled.
        /// </summary>
        public int secondRoll;

        #endregion

        #region PascalCase Properties

        /// <summary>The natural rolled value chosen on the D20 die (1..20).</summary>
        public int RawRoll => rawRoll;

        /// <summary>Modifier bonus added to the raw roll.</summary>
        public int Bonus => bonus;

        /// <summary>The final total score (rawRoll + bonus).</summary>
        public int FinalTotal => finalTotal;

        /// <summary>The target DC or AC.</summary>
        public int TargetDC => targetDC;

        /// <summary>Whether the roll was a success.</summary>
        public bool IsSuccess => isSuccess;

        /// <summary>Whether the roll resulted in a Natural 20 Critical Success.</summary>
        public bool IsCriticalSuccess => isCriticalSuccess;

        /// <summary>Whether the roll resulted in a Natural 1 Critical Failure.</summary>
        public bool IsCriticalFail => isCriticalFail;

        /// <summary>The advantage modifier applied during the roll.</summary>
        public AdvantageType AdvantageUsed => advantageUsed;

        /// <summary>The first die value rolled.</summary>
        public int FirstRoll => firstRoll;

        /// <summary>The second die value rolled.</summary>
        public int SecondRoll => secondRoll;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a new <see cref="DiceResult"/> and automatically evaluates success and critical conditions.
        /// </summary>
        /// <param name="rawRoll">The effective raw D20 roll (1..20).</param>
        /// <param name="bonus">Flat bonus modifier added to the roll.</param>
        /// <param name="targetDC">Target Difficulty Class (DC) or Armor Class (AC).</param>
        /// <param name="advantageUsed">The advantage mode used during the roll.</param>
        /// <param name="firstRoll">The first die roll value (defaults to rawRoll if omitted or zero).</param>
        /// <param name="secondRoll">The second die roll value (defaults to rawRoll if omitted or zero).</param>
        public DiceResult(
            int rawRoll,
            int bonus,
            int targetDC,
            AdvantageType advantageUsed = AdvantageType.None,
            int firstRoll = 0,
            int secondRoll = 0)
        {
            this.rawRoll = rawRoll;
            this.bonus = bonus;
            this.finalTotal = rawRoll + bonus;
            this.targetDC = targetDC;
            this.advantageUsed = advantageUsed;

            this.firstRoll = firstRoll > 0 ? firstRoll : rawRoll;
            this.secondRoll = secondRoll > 0 ? secondRoll : rawRoll;

            // Core D20 Rule Evaluation:
            // 1. Natural 20 is always a critical success regardless of DC.
            // 2. Natural 1 is always a critical failure regardless of DC/bonus.
            // 3. Otherwise, check if final total meets or exceeds target DC.
            this.isCriticalSuccess = (rawRoll == 20);
            this.isCriticalFail = (rawRoll == 1);

            if (this.isCriticalSuccess)
            {
                this.isSuccess = true;
            }
            else if (this.isCriticalFail)
            {
                this.isSuccess = false;
            }
            else
            {
                this.isSuccess = (this.finalTotal >= targetDC);
            }
        }

        #endregion

        #region Formatting & Overrides

        /// <summary>
        /// Generates a readable summary of the roll result for combat logging and UI display.
        /// </summary>
        public override string ToString()
        {
            string critTag = isCriticalSuccess ? " [CRITICAL SUCCESS!]" : (isCriticalFail ? " [CRITICAL FAIL!]" : "");
            string advTag = advantageUsed switch
            {
                AdvantageType.Advantage => $" (Advantage: [{firstRoll}, {secondRoll}] => {rawRoll})",
                AdvantageType.Disadvantage => $" (Disadvantage: [{firstRoll}, {secondRoll}] => {rawRoll})",
                _ => ""
            };

            string outcomeTag = isSuccess ? "SUCCESS" : "FAILURE";
            return $"[D20 Roll: {rawRoll}{advTag} + Bonus: {bonus} = {finalTotal} vs DC: {targetDC} => {outcomeTag}{critTag}]";
        }

#nullable enable
        public bool Equals(DiceResult other)
        {
            return rawRoll == other.rawRoll &&
                   bonus == other.bonus &&
                   finalTotal == other.finalTotal &&
                   targetDC == other.targetDC &&
                   isSuccess == other.isSuccess &&
                   isCriticalSuccess == other.isCriticalSuccess &&
                   isCriticalFail == other.isCriticalFail &&
                   advantageUsed == other.advantageUsed &&
                   firstRoll == other.firstRoll &&
                   secondRoll == other.secondRoll;
        }

        public override bool Equals(object? obj) => obj is DiceResult other && Equals(other);
#nullable restore

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + rawRoll.GetHashCode();
                hash = hash * 31 + bonus.GetHashCode();
                hash = hash * 31 + finalTotal.GetHashCode();
                hash = hash * 31 + targetDC.GetHashCode();
                hash = hash * 31 + isSuccess.GetHashCode();
                hash = hash * 31 + isCriticalSuccess.GetHashCode();
                hash = hash * 31 + isCriticalFail.GetHashCode();
                hash = hash * 31 + (int)advantageUsed;
                return hash;
            }
        }

        public static bool operator ==(DiceResult left, DiceResult right) => left.Equals(right);
        public static bool operator !=(DiceResult left, DiceResult right) => !left.Equals(right);

        #endregion
    }
}
