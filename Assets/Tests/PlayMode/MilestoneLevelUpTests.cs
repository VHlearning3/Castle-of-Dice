#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class MilestoneLevelUpTests
    {
        private GameObject playerGo;
        private PlayerUnit player;
        private GameObject progGo;
        private PlayerProgressionManager progressionManager;
        private CharacterClassSO warriorClass;
        private AbilitySO slashAbility;

        [SetUp]
        public void SetUp()
        {
            slashAbility = ScriptableObject.CreateInstance<AbilitySO>();
            slashAbility.Initialize(
                "test_slash",
                "Slash",
                "Basic strike",
                AbilityTargetType.SingleTarget,
                1,
                0,
                6,
                true,
                StatusEffectType.None,
                0,
                "Attack"
            );

            warriorClass = ScriptableObject.CreateInstance<CharacterClassSO>();
            warriorClass.Initialize(
                CharacterClassType.Warrior,
                "Sir Roland",
                "Resilient frontline fighter",
                20,
                14,
                3,
                3,
                new List<AbilitySO> { slashAbility }
            );

            playerGo = new GameObject("Test_PlayerUnit");
            player = playerGo.AddComponent<PlayerUnit>();
            player.SetCharacterClass(warriorClass);
            player.Level = 1;

            progGo = new GameObject("Test_PlayerProgressionManager");
            progressionManager = progGo.AddComponent<PlayerProgressionManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (progGo != null) Object.DestroyImmediate(progGo);
            if (playerGo != null) Object.DestroyImmediate(playerGo);
            if (warriorClass != null) Object.DestroyImmediate(warriorClass);
            if (slashAbility != null) Object.DestroyImmediate(slashAbility);
        }

        [Test]
        public void ProgressionManager_StartsAtLevel1()
        {
            Assert.AreEqual(1, progressionManager.CurrentLevel, "Initial milestone level must be 1.");
        }

        [Test]
        public void ProgressionManager_AdvanceToMilestone2_FiresEvent()
        {
            int recordedOld = -1;
            int recordedNew = -1;
            PlayerProgressionManager.OnMilestoneReached += (oldLvl, newLvl) =>
            {
                recordedOld = oldLvl;
                recordedNew = newLvl;
            };

            progressionManager.AdvanceToMilestone(2, "Test Boss Defeated");

            Assert.AreEqual(2, progressionManager.CurrentLevel, "Milestone level should advance to 2.");
            Assert.AreEqual(1, recordedOld, "Old level in event should be 1.");
            Assert.AreEqual(2, recordedNew, "New level in event should be 2.");
            Assert.AreEqual(2, player.Level, "PlayerUnit level must be updated to 2.");
        }

        [Test]
        public void HeroResilience_IncreasesMaxHPAndHealsFully()
        {
            int initialMax = player.MaxHP;
            player.TakeDamage(10);
            Assert.AreEqual(10, player.CurrentHP);

            player.ApplyHeroResilience(5);

            Assert.AreEqual(initialMax + 5, player.MaxHP, "MaxHP must increase by exactly 5.");
            Assert.AreEqual(player.MaxHP, player.CurrentHP, "CurrentHP must be fully restored upon choosing Resilience.");
        }

        [Test]
        public void AttributeBonusGrowth_IncrementsPrimaryAttribute()
        {
            int initialBonus = player.PrimaryAttributeBonus;

            player.AddAttributeBonus(1);

            Assert.AreEqual(initialBonus + 1, player.PrimaryAttributeBonus,
                "PrimaryAttributeBonus must increase by exactly 1.");
        }

        [Test]
        public void UpgradeAbilityToRank2_BoostsBaseValueBy3()
        {
            Assert.AreEqual(6, player.ActiveAbilities[0].BaseValue);

            bool upgraded = player.UpgradeAbilityToRank2(0);
            Assert.IsTrue(upgraded, "Upgrade to Rank 2 should succeed for slot 0.");
            Assert.AreEqual(9, player.ActiveAbilities[0].BaseValue, "Rank 2 base value must be original + 3.");
            Assert.IsTrue(player.ActiveAbilities[0].AbilityName.Contains("[Rank 2]"), "Ability name must denote Rank 2.");
        }
    }
}
#endif
