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

        // Mana shield charges (one absorbed hit per application)
        private int manaShieldCharges = 0;

        // Effects the owner applied during its own turn. They skip that turn's end-of-turn countdown,
        // so a 1-turn buff (Shadow Step, Shield Wall, Mana Shield) lasts until the owner's next turn.
        private readonly HashSet<StatusEffectType> appliedDuringOwnTurn = new HashSet<StatusEffectType>();
        private readonly List<StatusEffectType> effectKeyBuffer = new List<StatusEffectType>();

        /// <summary>Armor Class bonus granted by Shield Wall.</summary>
        public const int ShieldWallArmorBonus = 4;

        /// <summary>Armor Class bonus granted by a Rank 2 Shield Wall.</summary>
        public const int ShieldWallRank2ArmorBonus = 6;

        // AC bonus of the Shield Wall currently raised (Rank 2 raises a sturdier one)
        private int shieldWallBonus = ShieldWallArmorBonus;

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
                manaShieldCharges = Mathf.Max(manaShieldCharges, 1);
            }
            if (type == StatusEffectType.ShieldWall && !activeEffects.ContainsKey(type))
            {
                shieldWallBonus = ShieldWallArmorBonus;
            }

            if (TurnManager.Instance != null && TurnManager.Instance.CurrentActiveUnit == ownerUnit)
            {
                appliedDuringOwnTurn.Add(type);
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
            AbilityVfx.ShowStatusAura(ownerUnit, type);
            OnEffectApplied?.Invoke(type, activeEffects[type]);
        }

        /// <summary>
        /// Raises Mana Shield with <paramref name="charges"/> absorbed hits (Rank 2 holds two).
        /// </summary>
        public void ApplyManaShield(int durationTurns, int charges)
        {
            ApplyEffect(StatusEffectType.ManaShield, durationTurns);
            if (HasEffect(StatusEffectType.ManaShield))
            {
                manaShieldCharges = Mathf.Max(1, charges);
            }
        }

        /// <summary>Hits the active Mana Shield can still absorb.</summary>
        public int ManaShieldCharges => HasEffect(StatusEffectType.ManaShield) ? manaShieldCharges : 0;

        /// <summary>
        /// Raises Shield Wall with the given AC bonus (+4, or +6 at Rank 2).
        /// </summary>
        public void ApplyShieldWall(int durationTurns, int armorBonus)
        {
            ApplyEffect(StatusEffectType.ShieldWall, durationTurns);
            if (HasEffect(StatusEffectType.ShieldWall))
            {
                shieldWallBonus = Mathf.Max(0, armorBonus);
            }
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
            appliedDuringOwnTurn.Remove(type);
            if (activeEffects.Remove(type))
            {
                if (type == StatusEffectType.ManaShield)
                {
                    manaShieldCharges = 0;
                }

                Debug.Log($"[StatusEffect] {type} expired on {ownerUnit?.UnitName ?? name}.");
                AbilityVfx.HideStatusAura(ownerUnit, type);
                OnEffectExpired?.Invoke(type);
            }
        }

        /// <summary>
        /// Clears all active buffs and debuffs.
        /// </summary>
        public void ClearAllEffects()
        {
            effectKeyBuffer.Clear();
            effectKeyBuffer.AddRange(activeEffects.Keys);
            foreach (var key in effectKeyBuffer)
            {
                RemoveEffect(key);
            }
            manaShieldCharges = 0;
            appliedDuringOwnTurn.Clear();
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
            effectKeyBuffer.Clear();
            effectKeyBuffer.AddRange(activeEffects.Keys);
            for (int i = 0; i < effectKeyBuffer.Count; i++)
            {
                StatusEffectType key = effectKeyBuffer[i];
                if (appliedDuringOwnTurn.Contains(key))
                {
                    continue; // freshly self-applied this turn: starts counting down next turn
                }

                activeEffects[key]--;
                if (activeEffects[key] <= 0)
                {
                    RemoveEffect(key);
                }
            }

            appliedDuringOwnTurn.Clear();
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
        /// Returns the temporary Armor Class bonus from active effects (Shield Wall).
        /// </summary>
        public int GetArmorClassBonus()
        {
            return HasEffect(StatusEffectType.ShieldWall) ? shieldWallBonus : 0;
        }

        /// <summary>
        /// Determines if attack rolls benefit from Advantage (e.g. Shadow Step) or suffer Disadvantage (Blindness).
        /// </summary>
        public AdvantageType GetAttackRollAdvantageModifier()
        {
            if (HasEffect(StatusEffectType.AdvantageNextAttack))
            {
                return AdvantageType.Advantage;
            }
            if (HasEffect(StatusEffectType.Blind))
            {
                return AdvantageType.Disadvantage;
            }
            return AdvantageType.None;
        }

        /// <summary>
        /// Consumes the AdvantageNextAttack status effect (if active) after an attack roll is resolved.
        /// Returns true if the effect was active and consumed.
        /// </summary>
        public bool ConsumeAdvantageNextAttack()
        {
            if (HasEffect(StatusEffectType.AdvantageNextAttack))
            {
                RemoveEffect(StatusEffectType.AdvantageNextAttack);
                Debug.Log($"[StatusEffect] Advantage on next attack consumed for {ownerUnit?.UnitName ?? name}.");
                return true;
            }
            return false;
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
                AbilityVfx.PlayManaShieldAbsorb(ownerUnit);

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
