#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The ability card (hover tooltip on the combat bar) must describe what the ability really does.
    /// </summary>
    [TestFixture]
    public class AbilityTooltipTextTests
    {
        private static AbilitySO Make(string id, AbilityTargetType target, int range, int radius, bool check,
            StatusEffectType effect = StatusEffectType.None, int duration = 0, int dice = 0, int sides = 0, bool addAttribute = false)
        {
            AbilitySO ability = ScriptableObject.CreateInstance<AbilitySO>();
            ability.Initialize(id, id, "desc", target, range, radius, 0, check, effect, duration, "Attack",
                diceCount: dice, diceSides: sides, addAttribute: addAttribute);
            return ability;
        }

        [Test]
        public void AreaLabel_ShowsTheRealWidth()
        {
            AbilitySO fireball = Make("mage_fireball", AbilityTargetType.Area3x3, 4, 1, true, dice: 2, sides: 6, addAttribute: true);
            Assert.AreEqual("Area of Effect (3x3)", AbilityTooltipUI.GetTargetLabel(fireball));

            AbilitySO smoke = Make("rogue_smoke_bomb", AbilityTargetType.Area3x3, 3, 1, false, StatusEffectType.Blind, 1);
            Assert.AreEqual("Area of Effect (5x5)", AbilityTooltipUI.GetTargetLabel(smoke.CreateRank2()));
        }

        [Test]
        public void Rank2NonDamageAbilities_DoNotReadAsDamage()
        {
            AbilitySO ironWill = Make("warrior_iron_will", AbilityTargetType.Self, 0, 0, false).CreateRank2();
            string text = AbilityTooltipUI.BuildStatsText(ironWill, 3, 0, 30);
            StringAssert.Contains("Heals 12 HP", text); // 30% of 30 + Rank 2's 3
            StringAssert.DoesNotContain("Damage", text);

            AbilitySO shield = Make("warrior_shield_block", AbilityTargetType.Self, 0, 0, false, StatusEffectType.ShieldWall, 1).CreateRank2();
            text = AbilityTooltipUI.BuildStatsText(shield, 3, 0, 30);
            StringAssert.Contains("+6 AC", text);
            StringAssert.DoesNotContain("Damage", text);

            AbilitySO smoke = Make("rogue_smoke_bomb", AbilityTargetType.Area3x3, 3, 1, false, StatusEffectType.Blind, 1).CreateRank2();
            StringAssert.Contains("Blind for 2 turns", AbilityTooltipUI.BuildStatsText(smoke, 3, 0, 25));
        }

        [Test]
        public void WarCry_ShowsItsAttackRoll()
        {
            AbilitySO warCry = Make("warrior_war_cry", AbilityTargetType.Area3x3, 1, 1, false, dice: 1, sides: 4, addAttribute: true);
            string text = AbilityTooltipUI.BuildStatsText(warCry, 3, 0, 30);
            StringAssert.Contains("d20 + 3 vs. each enemy's AC", text);
            Assert.AreEqual("Around You", AbilityTooltipUI.GetRangeLabel(warCry));
        }

        [Test]
        public void Retaliation_IncludesTheBlacksmithBonus()
        {
            AbilitySO retaliation = Make("warrior_retaliation", AbilityTargetType.Self, 0, 0, false, StatusEffectType.Retaliation, 2, 1, 8, true);
            StringAssert.Contains("1d8 + 5 Damage to each melee attacker (incl. +2 Blacksmith)",
                AbilityTooltipUI.BuildStatsText(retaliation, 3, 2, 30));
        }

        [Test]
        public void SingleTileRange_IsSingular()
        {
            AbilitySO slash = Make("warrior_sword_slash", AbilityTargetType.SingleTarget, 1, 0, true, dice: 1, sides: 8, addAttribute: true);
            Assert.AreEqual("1 Tile", AbilityTooltipUI.GetRangeLabel(slash));
            StringAssert.Contains("d20 + 3 vs. target's AC", AbilityTooltipUI.BuildStatsText(slash, 3, 0, 30));
        }
    }
}
#endif
