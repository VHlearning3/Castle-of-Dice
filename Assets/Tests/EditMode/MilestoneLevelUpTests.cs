#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.World;

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
        private readonly List<GameObject> spawned = new List<GameObject>();

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
            StoryFlags.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StoryFlags.Clear();
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
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

        // Levels from four milestones (Vili, 2026-10-06): first battle won, three battles or a castle boss,
        // the hero's own quest, the Tower. Each gives one level once, in any order.

        private DungeonRoomController Room(string name)
        {
            GameObject go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<DungeonRoomController>();
        }

        [Test]
        public void FirstBattle_GivesLevel2_AndTheThirdBattleLevel3()
        {
            DungeonRoomController cellar = Room("Cellar_Room");
            progressionManager.HandleRoomCleared(cellar);
            Assert.AreEqual(2, progressionManager.CurrentLevel, "the first battle won");

            progressionManager.HandleRoomCleared(cellar);
            Assert.AreEqual(1, PlayerProgressionManager.BattlesWon, "the same room counts once");

            progressionManager.HandleRoomCleared(Room("Forest_Zombie"));
            Assert.AreEqual(2, progressionManager.CurrentLevel);
            progressionManager.HandleRoomCleared(Room("Forest_Wolves"));
            Assert.AreEqual(3, progressionManager.CurrentLevel, "three battles won");

            progressionManager.HandleRoomCleared(Room("Forest_Ambush"));
            progressionManager.HandleBossDefeated("CursedCommander");
            Assert.AreEqual(3, progressionManager.CurrentLevel, "more battles and a boss give nothing more");
        }

        [Test]
        public void CastleBoss_GivesTheSecondMilestoneOnce_AndTheKingNothing()
        {
            progressionManager.HandleBossDefeated("CursedCommander");
            Assert.AreEqual(2, progressionManager.CurrentLevel, "the Commander counts as the second milestone");
            progressionManager.HandleBossDefeated("ShadowMageMalakor");
            Assert.AreEqual(2, progressionManager.CurrentLevel, "the Witch does not give it again");

            progressionManager.HandleRoomCleared(Room("Courtyard_Boss"));
            Assert.AreEqual(3, progressionManager.CurrentLevel, "the boss fight is also the first battle won");

            progressionManager.HandleBossDefeated("GargoyleKing");
            Assert.AreEqual(3, progressionManager.CurrentLevel, "the King gives no level");
        }

        [Test]
        public void AllFourMilestones_ReachLevel5_InAnyOrder()
        {
            progressionManager.GrantBonusLevel("Hero quest");
            progressionManager.GrantBonusLevel("Tower");
            Assert.AreEqual(3, progressionManager.CurrentLevel);
            progressionManager.HandleRoomCleared(Room("Cellar_Room"));
            progressionManager.HandleBossDefeated("ShadowMageMalakor");
            Assert.AreEqual(PlayerUnit.MaxLevel, progressionManager.CurrentLevel);
            Assert.AreEqual(5, player.Level);
        }

        [Test]
        public void TwoLevelsAtOnce_EachGetTheirOwnChoice()
        {
            GameObject uiGo = new GameObject("LevelUpUI");
            spawned.Add(uiGo);
            UI.LevelUpUIController ui = uiGo.AddComponent<UI.LevelUpUIController>();
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(uiGo.transform);
            panel.SetActive(false);
            typeof(UI.LevelUpUIController).GetField("levelUpModalPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(ui, panel);

            ui.ShowLevelUpModal(2, player);
            Assert.IsTrue(ui.IsModalOpen);
            ui.ShowLevelUpModal(3, player);
            Assert.AreEqual(1, ui.QueuedLevelUps, "the second level waits for its own window");
            ui.CloseLevelUpModal();
        }
    }
}
#endif
