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
