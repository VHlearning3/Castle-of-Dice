using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The D&D rules Vili picked on 2026-10-06: proficiency and skills, saving throws on both sides, the action
    /// economy (bonus action, reaction, Dash / Dodge / Disengage), Death's Door, surprise, HP per level,
    /// surrender, Inspiration, the worn magic items, scrolls by choice and the new texts.
    /// </summary>
    [TestFixture]
    public class DndRulesTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GameManager gameManager;
        private GridManager grid;
        private InventoryManager inventory;
        private PlayerDataSO sessionData;

        [SetUp]
        public void SetUp()
        {
            sessionData = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerDataSO.Session = sessionData;

            gameManager = Track(new GameObject("Dnd_GameManager")).AddComponent<GameManager>();
            SetStatic(typeof(GameManager), "_instance", gameManager);

            grid = Track(new GameObject("Dnd_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            inventory = Track(new GameObject("Dnd_Inventory")).AddComponent<InventoryManager>();
            SetStaticProperty(typeof(InventoryManager), "Instance", inventory);
            inventory.RestoreFromSave(gold: 0, scrapMetal: 0, rerollScrolls: 0);

            StoryFlags.Clear();
            RerollableRoll.Reset();
            RerollableRoll.CanPauseOverride = () => false;
            DifficultySettings.Reset();
            DiceSystem.ResetRandom();
            PlayerExplorationMovement.SetSneaking(false);
        }

        [TearDown]
        public void TearDown()
        {
            RerollableRoll.Reset();
            RerollableRoll.CanPauseOverride = null;
            DifficultySettings.Reset();
            DiceSystem.ResetRandom();
            PlayerExplorationMovement.SetSneaking(false);

            StoryFlags.Clear();
            foreach (TurnManager tm in Object.FindObjectsByType<TurnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tm.gameObject);
            }
            SetStaticProperty(typeof(InventoryManager), "Instance", null);
            SetStaticProperty(typeof(TurnManager), "Instance", null);
            GridManager.Instance = null;
            SetStatic(typeof(GameManager), "_instance", null);

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
            PlayerDataSO.Session = null;
            if (sessionData != null) Object.DestroyImmediate(sessionData);
        }

        #region Proficiency, skills and saves

        [Test]
        public void Proficiency_AddsToAttacks_AndSetsTheSaveDC()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            Assert.AreEqual(2, HeroAttributes.GetProficiencyBonus(warrior));
            Assert.AreEqual(warrior.PrimaryAttributeBonus + 2, HeroAttributes.GetAttackBonus(warrior));
            Assert.AreEqual(8 + 2 + warrior.PrimaryAttributeBonus, HeroAttributes.GetSaveDC(warrior));

            warrior.Level = 5;
            Assert.AreEqual(3, HeroAttributes.GetProficiencyBonus(warrior), "Level 5 raises the proficiency bonus to +3.");
        }

        [Test]
        public void Skills_TrainedOnesAddProficiency_AndTheRogueHasExpertise()
        {
            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo");
            int dex = HeroAttributes.GetModifier(rogue, HeroAttribute.Dexterity);
            Assert.AreEqual(dex + 4, HeroAttributes.GetSkillBonus(rogue, HeroSkill.Stealth), "Expertise doubles the proficiency.");
            Assert.AreEqual(HeroAttributes.GetModifier(rogue, HeroAttribute.Wisdom) + 2, HeroAttributes.GetSkillBonus(rogue, HeroSkill.Perception));
            Assert.AreEqual(HeroAttributes.GetModifier(rogue, HeroAttribute.Intelligence), HeroAttributes.GetSkillBonus(rogue, HeroSkill.Arcana), "Untrained: the attribute only.");

            int persuasion = HeroAttributes.ResolveCheckBonus(rogue, "Persuasion Check (Charisma/Agility)", out string label);
            Assert.AreEqual(Mathf.Max(HeroAttributes.GetSkillBonus(rogue, HeroSkill.Persuasion), dex), persuasion, "The best of what the check names.");
            StringAssert.Contains("+", label);

            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            Assert.AreEqual(HeroAttributes.GetModifier(mage, HeroAttribute.Intelligence) + 2, HeroAttributes.ResolveCheckBonus(mage, "Arcana / Intelligence Check"));
        }

        [Test]
        public void Saves_UseClassProficiency_AndTheOathkeeperShield()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            int con = HeroAttributes.GetModifier(warrior, HeroAttribute.Constitution);
            Assert.AreEqual(con + 2, HeroAttributes.GetSaveBonus(warrior, HeroAttribute.Constitution), "The warrior is proficient in CON saves.");
            Assert.AreEqual(HeroAttributes.GetModifier(warrior, HeroAttribute.Wisdom), HeroAttributes.GetSaveBonus(warrior, HeroAttribute.Wisdom));

            int ac = warrior.ArmorClass;
            Assert.IsTrue(MagicItemPickup.Give(MagicItems.OathkeeperShieldId));
            Assert.AreEqual(ac + 1, warrior.ArmorClass, "The shield adds +1 AC.");
            Assert.AreEqual(con + 3, HeroAttributes.GetSaveBonus(warrior, HeroAttribute.Constitution), "...and +1 to every save.");
            Assert.AreEqual(1, HeroAttributes.GetDeathSaveBonus());

            MagicItems.SetWorn(MagicItems.OathkeeperShieldId, false);
            Assert.AreEqual(ac, warrior.ArmorClass, "Taken off, it does nothing.");
            Assert.IsFalse(MagicItemPickup.Give(MagicItems.OathkeeperShieldId), "An item is found only once.");
        }

        [Test]
        public void EnemySaves_FollowTheCreature()
        {
            EnemyUnit zombie = CreateUnit<EnemyUnit>("Zombie");
            zombie.ConfigureStats("Rotting Zombie", 22, 8, 4, 2);
            Assert.Greater(zombie.GetSaveBonus(HeroAttribute.Constitution), zombie.GetSaveBonus(HeroAttribute.Dexterity), "Zombies are tough and slow.");

            CultistCaster cultist = CreateUnit<CultistCaster>("Cultist");
            Assert.AreEqual(2, cultist.GetSaveBonus(HeroAttribute.Wisdom));
            Assert.AreEqual(12, cultist.PassivePerception);
        }

        [Test]
        public void Fireball_IsADexSaveForHalf()
        {
            AbilitySO fireball = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Mage_Fireball.asset");
            Assert.IsTrue(CombatRules.TryGetSave(fireball, out HeroAttribute save, out bool half));
            Assert.AreEqual(HeroAttribute.Dexterity, save);
            Assert.IsTrue(half);

            AbilitySO slash = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Warrior_SwordSlash.asset");
            Assert.IsFalse(CombatRules.TryGetSave(slash, out _, out _), "Sword Slash is an attack roll.");
        }

        [Test]
        public void HitChance_CountsNaturalOnesAndTwenties_AndAdvantage()
        {
            Assert.AreEqual(2, CombatRules.NeededRoll(30, 10), "A natural 1 always misses.");
            Assert.AreEqual(20, CombatRules.NeededRoll(0, 40), "A natural 20 always hits.");
            Assert.AreEqual(50, CombatRules.SuccessPercent(5, 16, AdvantageType.None));
            Assert.AreEqual(75, CombatRules.SuccessPercent(5, 16, AdvantageType.Advantage));
            Assert.AreEqual(25, CombatRules.SuccessPercent(5, 16, AdvantageType.Disadvantage));
            Assert.AreEqual(AdvantageType.None, CombatRules.Combine(true, true), "Advantage and Disadvantage cancel out.");

            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(6, 5)));
            string text = CombatUIController.BuildHitChanceText(warrior, warrior.GetAbility(0), skeleton, grid);
            StringAssert.Contains("%", text);
            StringAssert.Contains("AC 12", text);
        }

        #endregion

        #region Action economy

        [Test]
        public void BonusAction_IronWillAfterSwordSlash_AndPotionsUseIt()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            int slash = FindSlot(warrior, "warrior_sword_slash");
            int ironWill = FindSlot(warrior, "warrior_iron_will");
            Assert.AreEqual(ActionCost.BonusAction, warrior.GetAbilityCost(ironWill));

            warrior.HasActedThisTurn = true;
            Assert.IsFalse(warrior.CanUseAbility(slash), "The action is spent.");
            Assert.IsTrue(warrior.CanUseAbility(ironWill), "The bonus action is still free.");

            warrior.HasUsedBonusActionThisTurn = true;
            Assert.AreEqual(PotionUseCheck.ActionUsed, PotionQuickBar.CheckUse(1, true, 5, 30, true, true, warrior.HasUsedBonusActionThisTurn));
            warrior.ResetTurnFlags();
            Assert.IsFalse(warrior.HasUsedBonusActionThisTurn);
        }

        [Test]
        public void Dash_DoublesTheMove_OrGivesASecondOne()
        {
            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo");
            int move = rogue.MovementRange;
            Assert.AreEqual(ActionCost.BonusAction, rogue.GetMobilityActionCost(), "Cunning Action: the rogue dashes as a bonus action.");
            Assert.IsTrue(rogue.Dash());
            Assert.AreEqual(move * 2, rogue.MovementRange);
            Assert.IsTrue(rogue.HasUsedBonusActionThisTurn);
            Assert.IsFalse(rogue.HasActedThisTurn);

            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            warrior.HasMovedThisTurn = true;
            Assert.IsTrue(warrior.Dash());
            Assert.IsFalse(warrior.HasMovedThisTurn, "Dashing after a move gives a second move.");
            Assert.IsTrue(warrior.HasActedThisTurn, "The warrior's Dash is his action.");
        }

        [Test]
        public void Disengage_StopsFreeAttacks_AndDodgeGivesDisadvantage()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(6, 5)));
            TurnManager tm = StartFight(warrior, skeleton);

            List<EnemyUnit> attackers = new List<EnemyUnit>();
            warrior.CollectOpportunityAttackers(new Vector2Int(2, 5), attackers);
            Assert.AreEqual(1, attackers.Count);

            warrior.ResetTurnFlags();
            Assert.IsTrue(warrior.Disengage());
            warrior.CollectOpportunityAttackers(new Vector2Int(2, 5), attackers);
            Assert.AreEqual(0, attackers.Count, "Disengaged: walking away is safe.");

            warrior.ResetTurnFlags();
            Assert.IsTrue(warrior.Dodge());
            Assert.AreEqual(AdvantageType.Disadvantage, CombatRules.AttackAdvantage(skeleton, warrior), "Attacks against a dodging hero have Disadvantage.");
            warrior.BeginOwnTurn();
            Assert.AreEqual(AdvantageType.None, CombatRules.AttackAdvantage(skeleton, warrior), "Dodge ends when the hero's turn starts.");
            Assert.IsNotNull(tm);
        }

        [Test]
        public void FleeingEnemy_GivesTheHeroAReactionAttack_OncePerRound()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 200, 1, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(6, 5)));
            StartFight(warrior, skeleton);
            warrior.ResetTurnFlags();

            Assert.IsTrue(warrior.CanReactTo(skeleton, new Vector2Int(9, 5)));
            Assert.IsFalse(warrior.CanReactTo(skeleton, new Vector2Int(6, 6)), "Staying next to the hero provokes nothing.");

            int hp = skeleton.CurrentHP;
            SeedSoFirstRollIs(r => r >= 2 && r < 20);
            skeleton.WalkToTile(grid.GetTileAt(new Vector2Int(9, 5)));
            Assert.Less(skeleton.CurrentHP, hp, "The warrior strikes the fleeing skeleton (AC 1).");
            Assert.IsTrue(warrior.HasUsedReactionThisRound);

            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            Assert.IsNull(mage.ReactionWeapon, "The mage has no melee weapon for reactions.");
        }

        #endregion

        #region Death's Door, HP per level, surprise

        [Test]
        public void DeathsDoor_KeepsTheHeroUpOnce()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(9, 9)));
            StartFight(warrior, skeleton);

            SeedSoFirstRollIs(r => r >= 10);
            warrior.TakeDamage(warrior.CurrentHP + 10);
            Assert.IsTrue(warrior.IsAlive, "A successful death save keeps the hero standing.");
            Assert.GreaterOrEqual(warrior.CurrentHP, 1);
            Assert.IsTrue(warrior.DeathsDoorUsedThisFight);

            warrior.TakeDamage(warrior.CurrentHP + 10);
            Assert.IsFalse(warrior.IsAlive, "Only once per fight.");
        }

        [Test]
        public void DeathsDoor_FailedSave_LetsTheHeroFall()
        {
            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(9, 9)));
            StartFight(mage, skeleton);

            SeedSoFirstRollIs(r => r < 10 && r > 1);
            mage.TakeDamage(mage.CurrentHP + 3);
            Assert.IsFalse(mage.IsAlive);
        }

        [Test]
        public void EveryLevel_AddsHitPoints_ThatAreNotSavedAsABonus()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            int baseHP = warrior.MaxHP;
            warrior.TakeDamage(5);
            warrior.Level = 3;
            Assert.AreEqual(baseHP + 2 * PlayerUnit.HitPointsPerLevel(CharacterClassType.Warrior), warrior.MaxHP);
            Assert.AreEqual(warrior.MaxHP - 5, warrior.CurrentHP, "The new hit points are added to the current HP too.");
            Assert.AreEqual(0, warrior.MaxHPBonus, "Level HP follow from the level, not from saved bonuses.");

            sessionData.SyncFromPlayer(warrior);
            warrior.ApplyProgression(3, sessionData.MaxHPBonus, 0, 0, 0, null);
            Assert.AreEqual(baseHP + 12, warrior.MaxHP, "Reapplying the save does not stack them.");
        }

        [Test]
        public void SurprisedEnemy_LosesItsFirstTurn()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(9, 9)));

            TurnManager tm = CreateTurnManager();
            tm.SetSurprisedForNextFight(new List<CombatUnit> { skeleton });
            tm.StartCombat(new List<CombatUnit> { warrior, skeleton });
            Assert.IsTrue(tm.IsSurprised(skeleton) || tm.CurrentActiveUnit == warrior);

            // Whoever went first, the skeleton's first turn is skipped and the hero acts again
            if (tm.IsSurprised(skeleton)) tm.EndPlayerTurn();
            Assert.AreEqual(TurnState.PlayerTurn, tm.CurrentState);
            Assert.AreEqual(warrior, tm.CurrentActiveUnit);
            Assert.IsFalse(tm.IsSurprised(skeleton), "The surprise lasts one turn.");
        }

        [Test]
        public void SneakingHero_CanSurpriseARoom_AndAnAmbushCanSurpriseTheHero()
        {
            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            DungeonRoomController room = Track(new GameObject("Forest_Ambush_Trigger")).AddComponent<DungeonRoomController>();
            List<CombatUnit> fighters = new List<CombatUnit> { rogue, skeleton };

            PlayerExplorationMovement.SetSneaking(true);
            SeedSoFirstRollIs(r => r >= 8);
            List<CombatUnit> surprised = room.ResolveAwareness(rogue, fighters, out string message);
            CollectionAssert.Contains(surprised, skeleton, "Stealth beats the skeleton's passive Perception.");
            Assert.IsFalse(PlayerExplorationMovement.IsSneaking, "The fight is on: sneaking ends.");
            StringAssert.Contains("Stealth", message);

            Assert.IsTrue(room.IsAmbush);
            SeedSoFirstRollIs(r => r == 1);
            surprised = room.ResolveAwareness(rogue, fighters, out message);
            CollectionAssert.Contains(surprised, rogue, "A failed Perception check: the hero is caught off guard.");
        }

        #endregion

        #region Surrender, Inspiration, magic items, scrolls

        [Test]
        public void Cultist_MayYield_AndTheWitchYieldsToTheRing()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            CultistCaster cultist = CreateUnit<CultistCaster>("Cultist");
            cultist.InitializeUnit();
            cultist.MoveToTile(grid.GetTileAt(new Vector2Int(9, 9)));
            EnemyUnit other = CreateUnit<EnemyUnit>("Skeleton");
            other.ConfigureStats("Armored Skeleton Guard", 20, 12, 4, 2);
            other.MoveToTile(grid.GetTileAt(new Vector2Int(10, 10)));
            StartFight(warrior, cultist, other);

            EnemyUnit yielded = null;
            System.Action<EnemyUnit> handler = e => yielded = e;
            EnemyUnit.OnEnemyYielded += handler;
            try
            {
                SeedSoFirstRollIs(r => r < 10 && r > 1);
                cultist.TakeDamage(cultist.CurrentHP - 2);
                Assert.IsTrue(cultist.HasYielded, "Losing its nerve, the cultist yields.");
                Assert.IsFalse(cultist.IsAlive, "A yielded enemy leaves the fight.");
                Assert.AreEqual(cultist, yielded);
            }
            finally
            {
                EnemyUnit.OnEnemyYielded -= handler;
            }

            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton2");
            Assert.IsFalse(skeleton.CanSurrender, "Only the Witch and the cultists surrender.");
        }

        [Test]
        public void Inspiration_IsAFreeReroll_SpentBeforeTheRunes()
        {
            inventory.RestoreFromSave(0, 0, rerollScrolls: 1);
            Assert.AreEqual(1, RerollableRoll.AvailableRerolls);
            Assert.IsTrue(HeroicInspiration.Grant("test"));
            Assert.IsFalse(HeroicInspiration.Grant("again"), "At most one at a time.");
            Assert.AreEqual(2, RerollableRoll.AvailableRerolls);
            Assert.AreEqual("Use Inspiration", RerollableRoll.NextRerollLabel);

            RerollableRoll.CanPauseOverride = () => true;
            SeedSoRollsAre(r1 => r1 < 20, r2 => true);
            RerollableRoll.Roll(0, 30, AdvantageType.None, _ => { });
            Assert.IsTrue(RerollableRoll.Reroll());
            Assert.IsFalse(HeroicInspiration.Has, "Inspiration went first.");
            Assert.AreEqual(1, inventory.RerollScrollCount, "The rune is kept.");
            RerollableRoll.Reset();

            StoryFlags.Set(BossChoices.CommanderReleasedFlag);
            Assert.IsTrue(HeroicInspiration.Has, "Releasing the Commander inspires the hero.");
        }

        [Test]
        public void MagicItems_BootsAddMovement_AndTheCloakHidesFromAllButTheKing()
        {
            PlayerUnit warrior = CreateHero("Character_Warrior_SirRoland");
            int move = warrior.MovementRange;
            Assert.IsTrue(MagicItemPickup.Give(MagicItems.BootsOfStridingId));
            Assert.AreEqual(move + 1, warrior.MovementRange);

            DungeonRoomController room = Track(new GameObject("Room")).AddComponent<DungeonRoomController>();
            DungeonRoomController throne = Track(new GameObject("Throne")).AddComponent<DungeonRoomController>();
            throne.bossIdentifier = "GargoyleKing";
            Assert.IsFalse(room.HeroGoesUnnoticed);

            Assert.IsTrue(MagicItemPickup.Give(MagicItems.ShadowCloakId));
            Assert.IsTrue(room.HeroGoesUnnoticed, "Wearing the cloak, no one notices the hero.");
            Assert.IsFalse(throne.HeroGoesUnnoticed, "The Gargoyle King does.");
            Assert.IsTrue(MagicItems.HasStealthAdvantage);

            Assert.IsFalse(MagicItems.ToggleWorn(MagicItems.ShadowCloakId));
            Assert.IsFalse(room.HeroGoesUnnoticed, "Taken off, the hero can fight again.");
        }

        [Test]
        public void MagicItemAssets_AreWondrous_WithTheirOwnIcons()
        {
            foreach (string id in MagicItems.AllIds)
            {
                ItemSO item = inventory.FindItemByID(id);
                Assert.IsNotNull(item, id);
                Assert.AreEqual(ItemType.Wondrous, item.ItemType, id);
                Assert.IsNotNull(item.ItemIcon, id + " has an icon");
                Assert.IsFalse(string.IsNullOrEmpty(MagicItems.EffectText(id)));
            }
        }

        [Test]
        public void ScrollOfEmbers_IsReadOnPurpose_AndEnemiesSaveForHalf()
        {
            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            EnemyUnit skeleton = CreateUnit<EnemyUnit>("Skeleton");
            skeleton.ConfigureStats("Armored Skeleton Guard", 50, 12, 4, 2);
            skeleton.MoveToTile(grid.GetTileAt(new Vector2Int(9, 9)));
            TurnManager tm = StartFight(mage, skeleton);
            mage.ResetTurnFlags();

            Assert.IsFalse(CombatScrolls.CanRead(mage, CombatScrolls.EmbersScrollId), "No scroll, nothing to read.");
            inventory.AddItem(inventory.FindItemByID(CombatScrolls.EmbersScrollId), 1);
            Assert.AreEqual(1, CombatScrolls.Count(CombatScrolls.EmbersScrollId), "Starting a fight did not burn the scroll.");

            int hp = skeleton.CurrentHP;
            Assert.IsTrue(CombatScrolls.ReadEmbers(mage, tm.ActiveUnits));
            Assert.Less(skeleton.CurrentHP, hp);
            Assert.IsTrue(mage.HasActedThisTurn);
            Assert.AreEqual(0, CombatScrolls.Count(CombatScrolls.EmbersScrollId));
        }

        #endregion

        #region Texts and content

        [Test]
        public void CharacterSheet_ListsSkillsSavesAndActions()
        {
            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo");
            string skills = CharacterSheetUI.BuildSkillsText(rogue);
            StringAssert.Contains("Stealth", skills);
            StringAssert.Contains("+" + HeroAttributes.GetSkillBonus(rogue, HeroSkill.Stealth), skills);
            StringAssert.Contains("Save", CharacterSheetUI.BuildAttributesText(rogue));
            string actions = CharacterSheetUI.BuildActionsText(rogue);
            StringAssert.Contains("Bonus action", actions);
            StringAssert.Contains("Death's Door", actions);
            StringAssert.Contains("Save DC", CharacterSheetUI.BuildStatsLine(rogue));
        }

        [Test]
        public void Tooltips_ShowTheSaveAndTheCost()
        {
            AbilitySO fireball = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Mage_Fireball.asset");
            string text = AbilityTooltipUI.BuildStatsText(fireball, 3, 0, 20, 5, 13);
            StringAssert.Contains("DEX save vs. DC 13", text);
            StringAssert.Contains("Cost:", text);

            AbilitySO blink = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Mage_Blink.asset");
            StringAssert.Contains("Bonus action", AbilityTooltipUI.BuildStatsText(blink, 3, 0, 20, 5, 13));
        }

        [Test]
        public void SurrenderTalks_OfferTheirChoices()
        {
            DialogueNodeSO cultist = SurrenderDialogues.BuildCultistTalk("Curse Cultist");
            Assert.AreEqual(3, cultist.Options.Count);
            Assert.IsTrue(cultist.Options[0].RequiresCheck, "Intimidation is a skill check.");

            StoryFlags.Set(ShadowMageMalakorBoss.YieldedToRingFlag);
            DialogueNodeSO witch = SurrenderDialogues.BuildWitchTalk();
            Assert.AreEqual(2, witch.Options.Count);
            StringAssert.Contains("ring", witch.DialogueText);

            StoryFlags.Set(BossChoices.MalakorSparedFlag);
            Assert.AreEqual(true, BossChoices.ResolveMalakorAtThrone(), "A Witch who yielded to the ring keeps her word.");
        }

        [Test]
        public void CrownChoice_OffersTheEvilEnding()
        {
            DialogueNodeSO offer = ThroneRoomEnding.BuildCrownChoice(null, out DialogueNodeSO take);
            Assert.AreEqual(2, offer.Options.Count);
            Assert.AreEqual(take, offer.Options[1].NextNodeSuccess);
            StringAssert.Contains("Take the crown", offer.Options[1].OptionText);
            StringAssert.Contains("never die", ThroneRoomEnding.BuildEvilStatsText(null));
        }

        [Test]
        public void InvestigationSpots_OnePerCastleZone()
        {
            Assert.AreEqual(6, InvestigationSpawner.All.Length);
            HashSet<string> scenes = new HashSet<string>();
            foreach (InvestigationSpawner.Spot spot in InvestigationSpawner.All)
            {
                Assert.IsTrue(scenes.Add(spot.Scene), spot.Scene + " has one spot");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/Scenes/{spot.Scene}.unity"), spot.Scene);
                Assert.Greater(spot.DC, 9);
                Assert.IsFalse(string.IsNullOrEmpty(spot.Body));
            }
        }

        #endregion

        #region Helpers

        private TurnManager CreateTurnManager()
        {
            TurnManager tm = CreateUnit<TurnManager>("Dnd_TurnManager");
            SetStaticProperty(typeof(TurnManager), "Instance", tm);
            CreateUnit<AbilityExecutor>("Dnd_Executor");
            return tm;
        }

        private TurnManager StartFight(params CombatUnit[] units)
        {
            TurnManager tm = CreateTurnManager();
            tm.StartCombat(new List<CombatUnit>(units));
            return tm;
        }

        /// <summary>Picks a seed whose first d20 satisfies <paramref name="first"/>.</summary>
        private static void SeedSoFirstRollIs(System.Func<int, bool> first)
        {
            SeedSoRollsAre(first, _ => true);
        }

        private static void SeedSoRollsAre(System.Func<int, bool> first, System.Func<int, bool> second)
        {
            for (int seed = 1; seed < 100000; seed++)
            {
                System.Random rng = new System.Random(seed);
                int r1 = rng.Next(1, 21);
                int r2 = rng.Next(1, 21);
                if (first(r1) && second(r2))
                {
                    DiceSystem.SetSeed(seed);
                    return;
                }
            }
            Assert.Fail("No seed found.");
        }

        private PlayerUnit CreateHero(string classAsset)
        {
            CharacterClassSO cls = AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{classAsset}.asset");
            Assert.IsNotNull(cls, classAsset);
            sessionData.SelectedClass = cls;
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero_" + classAsset);
            hero.InitializeUnit();
            hero.MoveToTile(grid.GetTileAt(new Vector2Int(5, 5)));
            return hero;
        }

        private static int FindSlot(PlayerUnit hero, string id)
        {
            for (int i = 0; i < hero.ActiveAbilities.Count; i++)
            {
                if (hero.ActiveAbilities[i].BaseAbilityID == id) return i;
            }
            Assert.Fail($"{id} not found.");
            return -1;
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private T CreateUnit<T>(string name) where T : Component
        {
            GameObject go = Track(new GameObject(name));
            T component = go.AddComponent<T>();
            StatusEffectController effects = go.GetComponent<StatusEffectController>();
            if (effects != null) Invoke(effects, "Awake");
            Invoke(component, "Awake");
            return component;
        }

        private static void Invoke(object target, string method)
        {
            for (System.Type t = target.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (m != null)
                {
                    m.Invoke(target, null);
                    return;
                }
            }
        }

        private static void SetStatic(System.Type type, string field, object value)
        {
            FieldInfo f = type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"{type.Name}.{field} not found.");
            f.SetValue(null, value);
        }

        private static void SetStaticProperty(System.Type type, string property, object value)
        {
            PropertyInfo p = type.GetProperty(property, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            p?.SetValue(null, value);
        }

        #endregion
    }
}
