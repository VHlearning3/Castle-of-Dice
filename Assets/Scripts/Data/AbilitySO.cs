using System;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// ScriptableObject defining an action or spell ability in Castle of the D20.
    /// Configures targeting profiles, range, D20 check requirements, damage/healing values,
    /// status effects, and visual animation triggers.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "CastleOfDice/Data/Ability", order = 10)]
    public class AbilitySO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Identity & Presentation")]
        [Tooltip("Unique programmatic identifier for the ability (e.g., 'warrior_sword_slash', 'mage_fireball').")]
        [SerializeField] private string abilityID = "ability_id";

        [Tooltip("User-facing display name shown in combat UI and tooltips.")]
        [SerializeField] private string abilityName = "New Ability";

        [Tooltip("Flavor and mechanical description displayed in ability toolbars and popups.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = "Ability description text.";

        [Tooltip("Square icon displayed on the 4-slot combat action bar.")]
        [SerializeField] private Sprite abilityIcon;

        [Header("Targeting & Range")]
        [Tooltip("Targeting pattern for the ability: SingleTarget, Area3x3, or Self.")]
        [SerializeField] private AbilityTargetType targetType = AbilityTargetType.SingleTarget;

        [Tooltip("Maximum grid tile distance from the caster to target cell.")]
        [Range(0, 15)]
        [SerializeField] private int range = 1;

        [Tooltip("Radius of affected tiles around the target tile. Set to 0 for single-target, 1 for a 3x3 square area.")]
        [Range(0, 5)]
        [SerializeField] private int areaOfEffectRadius = 0;

        [Header("Combat Resolution")]
        [Tooltip("Base numeric potency of the ability (e.g., base damage dealt, health restored, or shield charges).")]
        [SerializeField] private int baseValue = 5;

        [Tooltip("If true, requires a D20 attack/spell roll (d20 + attribute bonus) against the target's Armor Class (AC).")]
        [SerializeField] private bool requiresCheck = true;

        [Header("Status Effect & Animation")]
        [Tooltip("Secondary status condition applied on a successful hit (e.g., Poison, Frostbite, Blind, ManaShield).")]
        [SerializeField] private StatusEffectType appliedEffect = StatusEffectType.None;

        [Tooltip("Number of combat turns the applied status condition persists on the target.")]
        [Range(0, 10)]
        [SerializeField] private int effectDurationTurns = 0;

        [Tooltip("Mecanim Animator trigger parameter name executed during ability playback (e.g., 'Attack', 'CastSpell', 'Buff').")]
        [SerializeField] private string animationTriggerName = "Attack";

        #endregion

        #region Public Properties

        /// <summary>Unique string ID of this ability.</summary>
        public string AbilityID => abilityID;

        /// <summary>Display name for UI.</summary>
        public string AbilityName => abilityName;

        /// <summary>Ability mechanical and lore description.</summary>
        public string Description => description;

        /// <summary>Ability toolbar icon sprite.</summary>
        public Sprite AbilityIcon => abilityIcon;

        /// <summary>Target selection pattern (SingleTarget, Area3x3, Self).</summary>
        public AbilityTargetType TargetType => targetType;

        /// <summary>Maximum tile range on the combat grid.</summary>
        public int Range => range;

        /// <summary>Radius for area effects (0 = single tile, 1 = 3x3 box).</summary>
        public int AreaOfEffectRadius => areaOfEffectRadius;

        /// <summary>Base damage, heal, or shield value.</summary>
        public int BaseValue => baseValue;

        /// <summary>Whether an attack roll vs target AC is required.</summary>
        public bool RequiresCheck => requiresCheck;

        /// <summary>Status effect condition inflicted upon success.</summary>
        public StatusEffectType AppliedEffect => appliedEffect;

        /// <summary>Duration in turns for the applied status effect.</summary>
        public int EffectDurationTurns => effectDurationTurns;

        /// <summary>Animator trigger parameter name.</summary>
        public string AnimationTriggerName => animationTriggerName;

        #endregion

        #region Editor Validation

        private void OnValidate()
        {
            if (range < 0) range = 0;
            if (areaOfEffectRadius < 0) areaOfEffectRadius = 0;
            if (effectDurationTurns < 0) effectDurationTurns = 0;

            // Automatically set AoE radius if Area3x3 is selected and radius is still 0
            if (targetType == AbilityTargetType.Area3x3 && areaOfEffectRadius == 0)
            {
                areaOfEffectRadius = 1;
            }
            else if (targetType == AbilityTargetType.Self)
            {
                range = 0;
            }
        }

        #endregion
    }
}
