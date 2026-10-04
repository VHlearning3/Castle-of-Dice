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

        [Tooltip("Damage dice rolled on a hit, e.g. 1 for \"1d8\". 0 = the ability deals only its flat Base Value.")]
        [Min(0)]
        [SerializeField] private int damageDiceCount = 0;

        [Tooltip("Sides of each damage die, e.g. 8 for \"1d8\".")]
        [Min(0)]
        [SerializeField] private int damageDiceSides = 0;

        [Tooltip("Adds the caster's primary attribute bonus (STR/INT/AGI) to damage, as in \"1d8 + 3\".")]
        [SerializeField] private bool addsAttributeToDamage = false;

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

        /// <summary>Number of damage dice rolled on a hit (0 = flat damage only).</summary>
        public int DamageDiceCount => damageDiceCount;

        /// <summary>Sides of each damage die.</summary>
        public int DamageDiceSides => damageDiceSides;

        /// <summary>Whether the caster's primary attribute bonus is added to damage.</summary>
        public bool AddsAttributeToDamage => addsAttributeToDamage;

        /// <summary>Whether a hit with this ability deals any damage.</summary>
        public bool DealsDamage => baseValue > 0 || HasDamageDice;

        private bool HasDamageDice => damageDiceCount > 0 && damageDiceSides > 0;

        /// <summary>Whether an attack roll vs target AC is required.</summary>
        public bool RequiresCheck => requiresCheck;

        /// <summary>Status effect condition inflicted upon success.</summary>
        public StatusEffectType AppliedEffect => appliedEffect;

        /// <summary>Duration in turns for the applied status effect.</summary>
        public int EffectDurationTurns => effectDurationTurns;

        /// <summary>Animator trigger parameter name.</summary>
        public string AnimationTriggerName => animationTriggerName;

        /// <summary>
        /// Teleports (Blink, Shadow Step) target an empty tile, not a unit, even though their assets use SingleTarget.
        /// </summary>
        public bool TargetsEmptyTile =>
            !string.IsNullOrEmpty(abilityID) &&
            (abilityID.IndexOf("blink", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             abilityID.IndexOf("shadow_step", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             abilityID.IndexOf("shadowstep", System.StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Suffix that marks an ability upgraded to Rank 2 at level-up.</summary>
        public const string Rank2Suffix = "_rank2";

        /// <summary>Whether this is a Rank 2 copy made at level-up.</summary>
        public bool IsRank2 => !string.IsNullOrEmpty(abilityID) && abilityID.EndsWith(Rank2Suffix, StringComparison.Ordinal);

        /// <summary>The ability's ID without the Rank 2 suffix (animator triggers use this).</summary>
        public string BaseAbilityID => IsRank2 ? abilityID.Substring(0, abilityID.Length - Rank2Suffix.Length) : abilityID;

        #endregion

        #region Public Methods

        /// <summary>
        /// What Rank 2 adds to this ability, as shown on the level-up screen and the ability card.
        /// Every Rank 2 gets +3 potency (damage or healing); the defensive and movement abilities,
        /// which deal no damage, get their own upgrade instead.
        /// </summary>
        public string GetRank2Summary()
        {
            string id = BaseAbilityID.ToLowerInvariant();
            if (id.Contains("shield_block")) return "+6 AC instead of +4";
            if (id.Contains("mana_shield")) return "Absorbs 2 hits instead of 1";
            if (id.Contains("blink")) return "+2 range and Advantage on the next attack";
            if (id.Contains("shadow_step") || id.Contains("shadowstep")) return "+2 range";
            if (id.Contains("smoke_bomb")) return "5x5 cloud and Blind lasts 1 turn longer";
            if (id.Contains("iron_will")) return "Heals 3 more HP";
            return "+3 damage";
        }

        /// <summary>
        /// Builds the Rank 2 copy of this ability: +3 potency, plus the extra reach or area the
        /// movement and control abilities get (see <see cref="GetRank2Summary"/>).
        /// </summary>
        public AbilitySO CreateRank2()
        {
            string id = abilityID.ToLowerInvariant();
            bool teleport = id.Contains("blink") || id.Contains("shadow_step") || id.Contains("shadowstep");
            bool smoke = id.Contains("smoke_bomb");

            AbilitySO rank2 = CreateInstance<AbilitySO>();
            rank2.Initialize(
                abilityID + Rank2Suffix,
                $"{abilityName} [Rank 2]",
                $"{description}\n<color=#4ade80>[Rank 2] {GetRank2Summary()}</color>",
                targetType,
                teleport ? range + 2 : range,
                smoke ? areaOfEffectRadius + 1 : areaOfEffectRadius,
                baseValue + 3,
                requiresCheck,
                appliedEffect,
                smoke ? effectDurationTurns + 1 : effectDurationTurns,
                animationTriggerName,
                abilityIcon,
                damageDiceCount,
                damageDiceSides,
                addsAttributeToDamage
            );
            rank2.name = name + " [Rank 2]";
            return rank2;
        }

        /// <summary>
        /// Initializes the ability parameters programmatically (used by editor generators or unit tests).
        /// </summary>
        public void Initialize(
            string id,
            string name,
            string desc,
            AbilityTargetType target,
            int abilityRange,
            int aoeRadius,
            int value,
            bool checkRequired,
            StatusEffectType effect,
            int duration,
            string animTrigger,
            Sprite icon = null,
            int diceCount = 0,
            int diceSides = 0,
            bool addAttribute = false)
        {
            abilityID = id;
            abilityName = name;
            description = desc;
            targetType = target;
            range = abilityRange;
            areaOfEffectRadius = aoeRadius;
            baseValue = value;
            requiresCheck = checkRequired;
            appliedEffect = effect;
            effectDurationTurns = duration;
            animationTriggerName = animTrigger;
            abilityIcon = icon;
            damageDiceCount = Mathf.Max(0, diceCount);
            damageDiceSides = Mathf.Max(0, diceSides);
            addsAttributeToDamage = addAttribute;
        }

        /// <summary>
        /// Rolls this ability's damage: damage dice + Base Value (+ attribute bonus when the ability uses it)
        /// + any flat bonus such as the blacksmith's weapon upgrade. Criticals are doubled by the caller.
        /// </summary>
        public int RollDamage(int attributeBonus, int flatBonus = 0)
        {
            int total = baseValue + flatBonus;
            if (HasDamageDice)
            {
                total += DiceSystem.RollDamage(damageDiceCount, damageDiceSides);
            }
            if (addsAttributeToDamage)
            {
                total += attributeBonus;
            }
            return Mathf.Max(0, total);
        }

        /// <summary>
        /// Human-readable damage formula for tooltips, e.g. "1d8 + 4" for a +3 attribute and +1 weapon bonus.
        /// </summary>
        public string GetDamageFormula(int attributeBonus, int flatBonus = 0)
        {
            int flat = baseValue + flatBonus + (addsAttributeToDamage ? attributeBonus : 0);
            if (!HasDamageDice)
            {
                return flat.ToString();
            }

            string dice = damageDiceCount + "d" + damageDiceSides;
            if (flat > 0) return dice + " + " + flat;
            if (flat < 0) return dice + " - " + (-flat);
            return dice;
        }

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
