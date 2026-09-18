using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Manages active status effects, buffs, and debuffs for a CombatUnit.
    /// Handles per-turn ticks (e.g., Poison d6 damage), movement penalties (Frostbite),
    /// combat accuracy debuffs (Blindness), and defensive absorption (Mana Shield).
    /// </summary>
    [RequireComponent(typeof(CombatUnit))]
    public class StatusEffectController : MonoBehaviour
    {
        #region Private State

        private CombatUnit ownerUnit;

        // Tracks active effect -> remaining turns
        private readonly Dictionary<StatusEffectType, int> activeEffects = new Dictionary<StatusEffectType, int>();

        // Mana shield charges (default 1 charge per application)
        private int manaShieldCharges = 0;

        #endregion

        #region Events

        /// <summary>Fired when a new status effect is applied or refreshed.</summary>
        public event Action<StatusEffectType, int> OnEffectApplied;

        /// <summary>Fired when a status effect expires or is cleared.</summary>
        public event Action<StatusEffectType> OnEffectExpired;

        /// <summary>Fired when Mana Shield absorbs an incoming attack.</summary>
        public event Action OnManaShieldAbsorbed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ownerUnit = GetComponent<CombatUnit>();
        }

        #endregion

        #region Effect Management

        /// <summary>
        /// Applies or refreshes a status effect with the specified duration in combat rounds.
        /// </summary>
        public void ApplyEffect(StatusEffectType type, int durationTurns)
        {
            if (type == StatusEffectType.None || durationTurns <= 0) return;

            if (type == StatusEffectType.ManaShield)
            {
                manaShieldCharges = Mathf.Max(1, manaShieldCharges + 1);
            }

            if (activeEffects.ContainsKey(type))
            {
                // Refresh to higher duration
                activeEffects[type] = Mathf.Max(activeEffects[type], durationTurns);
            }
            else
            {
                activeEffects[type] = durationTurns;
            }

            Debug.Log($"[StatusEffect] {ownerUnit?.UnitName ?? name} gained {type} for {durationTurns} turn(s).");
            OnEffectApplied?.Invoke(type, activeEffects[type]);
        }

        /// <summary>
        /// Checks whether a specific status effect is currently active.
        /// </summary>
        public bool HasEffect(StatusEffectType type)
        {
            return activeEffects.TryGetValue(type, out int turns) && turns > 0;
        }

        /// <summary>
        /// Returns the remaining turn count for a given effect, or 0 if inactive.
        /// </summary>
        public int GetRemainingTurns(StatusEffectType type)
        {
            return activeEffects.TryGetValue(type, out int turns) ? turns : 0;
        }

        /// <summary>
        /// Manually removes an active effect.
        /// </summary>
        public void RemoveEffect(StatusEffectType type)
        {
            if (activeEffects.Remove(type))
            {
                if (type == StatusEffectType.ManaShield)
                {
                    manaShieldCharges = 0;
                }

                Debug.Log($"[StatusEffect] {type} expired on {ownerUnit?.UnitName ?? name}.");
                OnEffectExpired?.Invoke(type);
            }
        }

        /// <summary>
        /// Clears all active buffs and debuffs.
        /// </summary>
        public void ClearAllEffects()
        {
            var keys = new List<StatusEffectType>(activeEffects.Keys);
            foreach (var key in keys)
            {
                RemoveEffect(key);
            }
            manaShieldCharges = 0;
        }

        #endregion

        #region Turn Processing

        /// <summary>
        /// Processes effects that trigger at the beginning of this unit's turn.
        /// E.g., Poison d6 damage ticks.
        /// </summary>
        public void ProcessTurnStartEffects()
        {
            if (ownerUnit == null || !ownerUnit.IsAlive) return;

            // Poison damage: Roll 1d6 damage at start of turn
            if (HasEffect(StatusEffectType.Poison))
            {
                int poisonDamage = DiceSystem.RollD6();
                Debug.Log($"[StatusEffect] {ownerUnit.UnitName} suffers {poisonDamage} poison damage (1d6).");
                ownerUnit.TakeDamage(poisonDamage, isCritical: false);
            }
        }

        /// <summary>
        /// Processes end-of-turn countdowns and cleans up expired effects.
        /// </summary>
        public void ProcessTurnEndEffects()
        {
            List<StatusEffectType> expired = new List<StatusEffectType>();

            var keys = new List<StatusEffectType>(activeEffects.Keys);
            foreach (var key in keys)
            {
                activeEffects[key]--;
                if (activeEffects[key] <= 0)
                {
                    expired.Add(key);
                }
            }

            foreach (var key in expired)
            {
                RemoveEffect(key);
            }
        }

        #endregion

        #region Combat Modifiers

        /// <summary>
        /// Returns the modified movement range accounting for Frostbite (halves movement distance).
        /// </summary>
        public int GetEffectiveMovementRange(int baseMovement)
        {
            if (HasEffect(StatusEffectType.Frostbite))
            {
                return Mathf.Max(1, baseMovement / 2);
            }
            return baseMovement;
        }

        /// <summary>
        /// Determines if attack rolls suffer Disadvantage due to conditions like Blindness.
        /// </summary>
        public AdvantageType GetAttackRollAdvantageModifier()
        {
            if (HasEffect(StatusEffectType.Blind))
            {
                return AdvantageType.Disadvantage;
            }
            return AdvantageType.None;
        }

        /// <summary>
        /// Attempts to absorb an incoming attack using Mana Shield.
        /// Returns true if absorbed (damage cancelled), false otherwise.
        /// </summary>
        public bool ConsumeManaShield()
        {
            if (HasEffect(StatusEffectType.ManaShield) && manaShieldCharges > 0)
            {
                manaShieldCharges--;
                Debug.Log($"[StatusEffect] Mana Shield absorbed incoming attack on {ownerUnit?.UnitName ?? name}! Remaining charges: {manaShieldCharges}");
                OnManaShieldAbsorbed?.Invoke();

                if (manaShieldCharges <= 0)
                {
                    RemoveEffect(StatusEffectType.ManaShield);
                }

                return true;
            }

            return false;
        }

        #endregion
    }
}
