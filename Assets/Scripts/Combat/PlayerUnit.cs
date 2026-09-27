using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Represents the player-controlled hero in combat.
    /// Manages class identity (Warrior, Mage, Rogue), active ability slots, permanent gear bonuses,
    /// and ability activation on the battlefield grid.
    /// </summary>
    public class PlayerUnit : CombatUnit
    {
        #region Serialized Fields

        [Header("Class Data")]
        [Tooltip("ScriptableObject containing base stats, lore, and 4 class abilities.")]
        [SerializeField] private CharacterClassSO characterClass;

        [Header("Equipment & Permanent Modifiers")]
        [Tooltip("Permanent weapon damage bonus purchased from Blacksmith Baldur (+1 per upgrade).")]
        [SerializeField] private int permanentWeaponDamageBonus = 0;

        [Tooltip("Permanent armor defense bonus purchased from Blacksmith Baldur (+1 AC per upgrade).")]
        [SerializeField] private int permanentArmorClassBonus = 0;

        #endregion

        #region Private State

        private readonly List<AbilitySO> activeAbilities = new List<AbilitySO>(4);
        private int primaryAttributeBonus = 3;
        private bool hasActedThisTurn = false;
        private bool hasMovedThisTurn = false;

        #endregion

        #region Public Properties

        /// <summary>Assigned hero class ScriptableObject.</summary>
        public CharacterClassSO CharacterClass => characterClass;

        /// <summary>Active 4-slot ability loadout.</summary>
        public IReadOnlyList<AbilitySO> ActiveAbilities => activeAbilities;

        /// <summary>Permanent weapon bonus (+1 damage).</summary>
        public int WeaponDamageBonus => permanentWeaponDamageBonus;

        /// <summary>Permanent armor bonus (+1 AC).</summary>
        public int ArmorClassBonus => permanentArmorClassBonus;

        /// <summary>
        /// Total effective Armor Class including class baseline and permanent gear bonuses.
        /// </summary>
        public override int ArmorClass => base.ArmorClass + permanentArmorClassBonus;

        /// <summary>Primary attribute modifier (+2 to +5) added to D20 checks.</summary>
        public int PrimaryAttributeBonus => primaryAttributeBonus;

        /// <summary>Whether the player has used their combat action this turn.</summary>
        public bool HasActedThisTurn
        {
            get => hasActedThisTurn;
            set => hasActedThisTurn = value;
        }

        /// <summary>Whether the player has moved on the grid this turn.</summary>
        public bool HasMovedThisTurn
        {
            get => hasMovedThisTurn;
            set => hasMovedThisTurn = value;
        }

        #endregion

        #region Initialization

        public override void InitializeUnit()
        {
            if (characterClass != null)
            {
                unitName = characterClass.CharacterName;
                maxHP = characterClass.BaseMaxHealth;
                currentHP = maxHP;
                armorClass = characterClass.BaseArmorClass;
                movementRange = characterClass.BaseMovementRange;
                primaryAttributeBonus = characterClass.PrimaryAttributeBonus;

                activeAbilities.Clear();
                if (characterClass.StartingAbilities != null)
                {
                    foreach (var ability in characterClass.StartingAbilities)
                    {
                        if (ability != null)
                        {
                            activeAbilities.Add(ability);
                        }
                    }
                }
            }

            ResetTurnFlags();
            base.InitializeUnit();
        }

        /// <summary>
        /// Applies a new CharacterClassSO dynamically (e.g. during hero selection screen).
        /// </summary>
        public void SetCharacterClass(CharacterClassSO newClass)
        {
            characterClass = newClass;
            InitializeUnit();
        }

        #endregion

        #region Level & Progression Upgrades

        [Header("Progression & Milestone")]
        [Tooltip("Current hero level: 1 (Starting), 2 (Castle Veteran), 3 (Arcane Crusher).")]
        [SerializeField] private int level = 1;

        /// <summary>Current hero milestone level (1..3).</summary>
        public int Level
        {
            get => level;
            set => level = Mathf.Clamp(value, 1, 3);
        }

        /// <summary>
        /// Hero's Resilience: Increases Max HP by 5 and fully restores HP.
        /// </summary>
        public void ApplyHeroResilience(int hpIncrease = 5)
        {
            // 1. Increase the max cap
            maxHP += hpIncrease;

            // 2. Use the base class Heal method to fill the health.
            // Heal() automatically clamps the value, logs the action, and calls NotifyHealthChanged().
            Heal(maxHP);

            Debug.Log($"[PlayerUnit] Hero's Resilience chosen! Max HP increased by {hpIncrease} to {maxHP}.");
        }
        /// <summary>
        /// Attribute Bonus Growth: Adds permanent bonus (+1) to primary attribute (d20 checks & damage).
        /// </summary>
        public void AddAttributeBonus(int amount = 1)
        {
            primaryAttributeBonus += amount;
            Debug.Log($"[PlayerUnit] Primary Attribute Bonus increased by {amount}. New bonus: +{primaryAttributeBonus}");
        }

        /// <summary>
        /// Ability Empowerment (Rank 2): Upgrades the ability in the specified slot (0..3)
        /// with +3 potency and marked as Rank 2.
        /// </summary>
        public bool UpgradeAbilityToRank2(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= activeAbilities.Count || activeAbilities[slotIndex] == null)
            {
                Debug.LogWarning($"[PlayerUnit] Cannot upgrade ability slot {slotIndex}: invalid index or empty slot.");
                return false;
            }

            AbilitySO original = activeAbilities[slotIndex];
            if (original.AbilityName.Contains("[Rank 2]"))
            {
                Debug.LogWarning($"[PlayerUnit] Ability {original.AbilityName} is already Rank 2.");
                return false;
            }

            AbilitySO rank2 = ScriptableObject.CreateInstance<AbilitySO>();
            rank2.Initialize(
                original.AbilityID + "_rank2",
                $"{original.AbilityName} [Rank 2]",
                $"{original.Description}\n<color=#4ade80>[Rank 2] Potency +3</color>",
                original.TargetType,
                original.Range,
                original.AreaOfEffectRadius,
                original.BaseValue + 3,
                original.RequiresCheck,
                original.AppliedEffect,
                original.EffectDurationTurns,
                original.AnimationTriggerName,
                original.AbilityIcon
            );

            activeAbilities[slotIndex] = rank2;
            Debug.Log($"[PlayerUnit] Upgraded slot {slotIndex} ({rank2.AbilityName}) to Rank 2! New BaseValue: {rank2.BaseValue}");
            return true;
        }

        #endregion

        #region Permanent Upgrades

        /// <summary>
        /// Increases permanent weapon damage (purchased from Blacksmith Baldur).
        /// </summary>
        public void AddWeaponDamageBonus(int amount)
        {
            permanentWeaponDamageBonus += amount;
            Debug.Log($"[PlayerUnit] Weapon damage bonus increased by {amount}. Total bonus: +{permanentWeaponDamageBonus}");
        }

        /// <summary>
        /// Increases permanent Armor Class (purchased from Blacksmith Baldur).
        /// </summary>
        public void AddArmorClassBonus(int amount)
        {
            permanentArmorClassBonus += amount;
            Debug.Log($"[PlayerUnit] Armor Class bonus increased by {amount}. Total AC: {ArmorClass}");
        }

        #endregion

        #region Ability Execution

        /// <summary>
        /// Checks if an ability slot can be used.
        /// </summary>
        public bool CanUseAbility(int slotIndex)
        {
            if (hasActedThisTurn) return false;
            return slotIndex >= 0 && slotIndex < activeAbilities.Count && activeAbilities[slotIndex] != null;
        }

        /// <summary>
        /// Returns the ability mapped to the specified action slot (0..3).
        /// </summary>
        public AbilitySO GetAbility(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < activeAbilities.Count)
            {
                return activeAbilities[slotIndex];
            }
            return null;
        }

        /// <summary>
        /// Executes an ability from slot index targeting a specified grid position.
        /// </summary>
        public bool UseAbility(int slotIndex, Vector2Int targetGridPos, AbilityExecutor executor)
        {
            if (!CanUseAbility(slotIndex))
            {
                Debug.LogWarning($"[PlayerUnit] Cannot use ability in slot {slotIndex}. Action already taken or slot empty.");
                return false;
            }

            AbilitySO ability = activeAbilities[slotIndex];
            if (executor == null)
            {
                executor = AbilityExecutor.Instance;
            }

            if (executor == null)
            {
                Debug.LogError("[PlayerUnit] No AbilityExecutor available to resolve ability.");
                return false;
            }

            bool success = executor.ExecuteAbility(this, ability, targetGridPos);
            if (success)
            {
                hasActedThisTurn = true;
            }

            return success;
        }

        /// <summary>
        /// Resets turn action and movement flags when the player's turn begins.
        /// </summary>
        public void ResetTurnFlags()
        {
            hasActedThisTurn = false;
            hasMovedThisTurn = false;
        }

        #endregion
    }
}
