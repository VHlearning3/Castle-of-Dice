using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Unity Editor utility script to generate and populate all core ScriptableObject assets
    /// for Castle of the D20 (Castle of Dice) in Assets/Data/.
    /// </summary>
    public static class GenerateGameDataEditor
    {
        private const string DataFolderPath = "Assets/Data";

        [MenuItem("CastleOfDice/Generate All Game Assets", false, 1)]
        public static void GenerateAllGameAssets()
        {
            EnsureDataFolderExists();

            int assetCount = 0;

            // 1. Generate Items
            ItemSO potionHealth = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_Potion_Health.asset");
            potionHealth.Initialize(
                id: "potion_health_small",
                name: "Small Health Potion",
                desc: "Restores 15 hit points when consumed during exploration or combat.",
                type: ItemType.Consumable,
                buyPrice: 25,
                sellPrice: 10,
                statBonus: 15,
                consumable: true
            );
            EditorUtility.SetDirty(potionHealth);
            assetCount++;

            ItemSO sharpenedBlade = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_SharpenedBlade.asset");
            sharpenedBlade.Initialize(
                id: "upgrade_sharpened_blade",
                name: "Sharpened Blade",
                desc: "Finely honed weapon forged by Blacksmith Baldur. Permanently adds +1 to all attack damage.",
                type: ItemType.WeaponUpgrade,
                buyPrice: 60,
                sellPrice: 20,
                statBonus: 1,
                consumable: false
            );
            EditorUtility.SetDirty(sharpenedBlade);
            assetCount++;

            ItemSO runicArmor = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_RunicArmor.asset");
            runicArmor.Initialize(
                id: "upgrade_runic_armor",
                name: "Runic Armor",
                desc: "Reinforced armor inscribed with protective runes. Permanently increases Armor Class by +1.",
                type: ItemType.ArmorUpgrade,
                buyPrice: 100,
                sellPrice: 35,
                statBonus: 1,
                consumable: false
            );
            EditorUtility.SetDirty(runicArmor);
            assetCount++;

            ItemSO scrapMetal = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_ScrapMetal.asset");
            scrapMetal.Initialize(
                id: "mat_scrap_metal",
                name: "Scrap Metal",
                desc: "Salvaged scrap ore gathered from dungeon ruins. Can be sold to Blacksmith Baldur for 10 gold.",
                type: ItemType.ScrapMetal,
                buyPrice: 0,
                sellPrice: 10,
                statBonus: 0,
                consumable: false
            );
            EditorUtility.SetDirty(scrapMetal);
            assetCount++;

            // 2. Generate Abilities (12 total: 4 Warrior, 4 Mage, 4 Rogue)
            // --- Warrior Abilities ---
            AbilitySO warriorSlash = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_SwordSlash.asset");
            warriorSlash.Initialize(
                id: "warrior_sword_slash",
                name: "Sword Slash",
                desc: "Standard melee strike dealing heavy slashing damage to an adjacent enemy.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 6,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Attack"
            );
            EditorUtility.SetDirty(warriorSlash);
            assetCount++;

            AbilitySO warriorShield = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_ShieldBlock.asset");
            warriorShield.Initialize(
                id: "warrior_shield_block",
                name: "Shield Block",
                desc: "Raise shield defensively, granting +3 Armor Class and damage mitigation until next turn.",
                target: AbilityTargetType.Self,
                abilityRange: 0,
                aoeRadius: 0,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.ManaShield,
                duration: 1,
                animTrigger: "Buff"
            );
            EditorUtility.SetDirty(warriorShield);
            assetCount++;

            AbilitySO warriorWarCry = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_WarCry.asset");
            warriorWarCry.Initialize(
                id: "warrior_war_cry",
                name: "War Cry",
                desc: "Unleash an intimidating shout that knocks back surrounding foes and bolsters resolve.",
                target: AbilityTargetType.Area3x3,
                abilityRange: 1,
                aoeRadius: 1,
                value: 3,
                checkRequired: false,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Buff"
            );
            EditorUtility.SetDirty(warriorWarCry);
            assetCount++;

            AbilitySO warriorIronWill = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_IronWill.asset");
            warriorIronWill.Initialize(
                id: "warrior_iron_will",
                name: "Iron Will",
                desc: "Channel inner fortitude to immediately restore 30% of maximum hit points.",
                target: AbilityTargetType.Self,
                abilityRange: 0,
                aoeRadius: 0,
                value: 10,
                checkRequired: false,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Buff"
            );
            EditorUtility.SetDirty(warriorIronWill);
            assetCount++;

            // --- Mage Abilities ---
            AbilitySO mageFireball = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_Fireball.asset");
            mageFireball.Initialize(
                id: "mage_fireball",
                name: "Fireball",
                desc: "Hurl an explosive sphere of arcane flame, dealing area damage across a 3x3 grid zone.",
                target: AbilityTargetType.Area3x3,
                abilityRange: 6,
                aoeRadius: 1,
                value: 8,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "CastSpell"
            );
            EditorUtility.SetDirty(mageFireball);
            assetCount++;

            AbilitySO mageFrostbite = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_Frostbite.asset");
            mageFrostbite.Initialize(
                id: "mage_frostbite",
                name: "Frostbite",
                desc: "Blast a target with chilling frost, inflicting frostbite and halving movement for 2 turns.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 5,
                aoeRadius: 0,
                value: 5,
                checkRequired: true,
                effect: StatusEffectType.Frostbite,
                duration: 2,
                animTrigger: "CastSpell"
            );
            EditorUtility.SetDirty(mageFrostbite);
            assetCount++;

            AbilitySO mageManaShield = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_ManaShield.asset");
            mageManaShield.Initialize(
                id: "mage_mana_shield",
                name: "Mana Shield",
                desc: "Conjure a protective barrier of pure arcane energy that absorbs incoming attacks.",
                target: AbilityTargetType.Self,
                abilityRange: 0,
                aoeRadius: 0,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.ManaShield,
                duration: 2,
                animTrigger: "Buff"
            );
            EditorUtility.SetDirty(mageManaShield);
            assetCount++;

            AbilitySO mageBlink = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_Blink.asset");
            mageBlink.Initialize(
                id: "mage_blink",
                name: "Blink",
                desc: "Instantly teleport to an unoccupied grid tile up to 5 tiles away without provoking attacks.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 5,
                aoeRadius: 0,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "CastSpell"
            );
            EditorUtility.SetDirty(mageBlink);
            assetCount++;

            // --- Rogue Abilities ---
            AbilitySO rogueBackstab = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Rogue_Backstab.asset");
            rogueBackstab.Initialize(
                id: "rogue_backstab",
                name: "Backstab",
                desc: "Strike from the shadows for devastating critical puncture damage.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 8,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Attack"
            );
            EditorUtility.SetDirty(rogueBackstab);
            assetCount++;

            AbilitySO rogueSmokeBomb = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Rogue_SmokeBomb.asset");
            rogueSmokeBomb.Initialize(
                id: "rogue_smoke_bomb",
                name: "Smoke Bomb",
                desc: "Toss a dense smoke canister, blinding all targets in a 3x3 area for 1 turn.",
                target: AbilityTargetType.Area3x3,
                abilityRange: 4,
                aoeRadius: 1,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.Blind,
                duration: 1,
                animTrigger: "Attack"
            );
            EditorUtility.SetDirty(rogueSmokeBomb);
            assetCount++;

            AbilitySO roguePoisonDagger = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Rogue_PoisonDagger.asset");
            roguePoisonDagger.Initialize(
                id: "rogue_poison_dagger",
                name: "Poison Dagger",
                desc: "Slash with an envenomed blade, dealing light damage and poisoning the victim for 3 turns.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 4,
                checkRequired: true,
                effect: StatusEffectType.Poison,
                duration: 3,
                animTrigger: "Attack"
            );
            EditorUtility.SetDirty(roguePoisonDagger);
            assetCount++;

            AbilitySO rogueLockpicking = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Rogue_Lockpicking.asset");
            rogueLockpicking.Initialize(
                id: "rogue_lockpick",
                name: "Lockpicking",
                desc: "Use nimble lockpicks to bypass locks on dungeon doors and treasure chests.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 0,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Interact"
            );
            EditorUtility.SetDirty(rogueLockpicking);
            assetCount++;

            // 3. Generate 3 CharacterClasses and assign respective 4 abilities
            // --- Warrior: Sir Roland ---
            CharacterClassSO warriorClass = GetOrCreateAsset<CharacterClassSO>($"{DataFolderPath}/Character_Warrior_SirRoland.asset");
            warriorClass.Initialize(
                type: CharacterClassType.Warrior,
                name: "Sir Roland",
                lore: "Veteran of the royal guard who donned his ancestral plate armor to purge his fallen castle of undead abominations.",
                maxHp: 35,
                ac: 15,
                move: 3,
                bonus: 3,
                abilities: new List<AbilitySO> { warriorSlash, warriorShield, warriorWarCry, warriorIronWill }
            );
            EditorUtility.SetDirty(warriorClass);
            assetCount++;

            // --- Mage: Scholar Elira ---
            CharacterClassSO mageClass = GetOrCreateAsset<CharacterClassSO>($"{DataFolderPath}/Character_Mage_Elira.asset");
            mageClass.Initialize(
                type: CharacterClassType.Mage,
                name: "Scholar Elira",
                lore: "Academy arcanist seeking to unravel the ancient curses and retrieve lost grimoires hidden in the castle's depths.",
                maxHp: 20,
                ac: 11,
                move: 3,
                bonus: 4,
                abilities: new List<AbilitySO> { mageFireball, mageFrostbite, mageManaShield, mageBlink }
            );
            EditorUtility.SetDirty(mageClass);
            assetCount++;

            // --- Rogue: Shadow-Corvo ---
            CharacterClassSO rogueClass = GetOrCreateAsset<CharacterClassSO>($"{DataFolderPath}/Character_Rogue_Corvo.asset");
            rogueClass.Initialize(
                type: CharacterClassType.Rogue,
                name: "Shadow-Corvo",
                lore: "Tavern-bred opportunist and lockpick specialist who knows the castle's secret passageways better than anyone.",
                maxHp: 25,
                ac: 13,
                move: 4,
                bonus: 4,
                abilities: new List<AbilitySO> { rogueBackstab, rogueSmokeBomb, roguePoisonDagger, rogueLockpicking }
            );
            EditorUtility.SetDirty(rogueClass);
            assetCount++;

            // 4. Generate 3 Quests
            QuestSO cellarRats = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quest_CellarRats.asset");
            cellarRats.Initialize(
                id: "CellarRats",
                title: "Cellar Infestation",
                desc: "Clear 3 giant cellar rats infesting the wine cellar beneath Barnaby's tavern.",
                state: QuestState.NotStarted,
                reqAmount: 3,
                gold: 30,
                bonusGold: 15,
                reward: potionHealth
            );
            EditorUtility.SetDirty(cellarRats);
            assetCount++;

            QuestSO lostSignet = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quest_LostSignetRing.asset");
            lostSignet.Initialize(
                id: "LostSignetRing",
                title: "The Lost Signet Ring",
                desc: "Search the courtyard ruins and recover the ancestral signet ring for Elder Othelia.",
                state: QuestState.NotStarted,
                reqAmount: 1,
                gold: 20,
                bonusGold: 10,
                reward: null
            );
            EditorUtility.SetDirty(lostSignet);
            assetCount++;

            QuestSO swampHerbs = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quest_SwampHerbs.asset");
            swampHerbs.Initialize(
                id: "SwampHerbs",
                title: "Herbs for Mirabel",
                desc: "Gather 3 marsh swamp herbs from the castle moat for herbalist Mirabel.",
                state: QuestState.NotStarted,
                reqAmount: 3,
                gold: 25,
                bonusGold: 15,
                reward: null
            );
            EditorUtility.SetDirty(swampHerbs);
            assetCount++;

            // 5. Generate Village Dialogues and Quests
            GenerateVillageDialoguesAndQuestsInternal(potionHealth, ref assetCount);

            // Save and refresh asset database
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GenerateGameDataEditor] Successfully generated and configured {assetCount} game data assets in '{DataFolderPath}'!");
            EditorUtility.DisplayDialog(
                "Castle of the D20",
                $"Successfully generated {assetCount} game data assets in '{DataFolderPath}'!\n\n" +
                "- 3 Character Classes (Sir Roland, Elira, Corvo) with assigned abilities\n" +
                "- 12 Abilities (4 per class)\n" +
                "- 4 Items (Potion, Sharpened Blade, Runic Armor, Scrap Metal)\n" +
                "- 4 Quests (Cellar Pests, Cellar Rats, Lost Signet Ring, Swamp Herbs)\n" +
                "- 7 Village Dialogue Nodes (Baldur and Barnaby trees with Persuasion checks)",
                "OK"
            );
        }

        [MenuItem("CastleOfDice/Generate Village Dialogues and Quests", false, 2)]
        public static void GenerateVillageDialoguesAndQuests()
        {
            EnsureFolderExists("Assets/Data");
            EnsureFolderExists("Assets/Data/Quests");
            EnsureFolderExists("Assets/Data/Dialogues");

            ItemSO potionHealth = AssetDatabase.LoadAssetAtPath<ItemSO>($"{DataFolderPath}/Item_Potion_Health.asset");
            int count = 0;
            GenerateVillageDialoguesAndQuestsInternal(potionHealth, ref count);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GenerateGameDataEditor] Generated {count} village dialogue and quest assets in Assets/Data/!");
            EditorUtility.DisplayDialog(
                "Castle of the D20",
                $"Successfully generated {count} village dialogue and quest assets!\n\n" +
                "- Quest_CellarPests.asset in Assets/Data/Quests/\n" +
                "- Baldur_Intro, Baldur_Rumor in Assets/Data/Dialogues/\n" +
                "- Barnaby_Intro, Barnaby_Negotiation_Success/Fail, Barnaby_Accepted, Barnaby_Declined in Assets/Data/Dialogues/",
                "OK"
            );
        }

        private static void GenerateVillageDialoguesAndQuestsInternal(ItemSO potionHealth, ref int assetCount)
        {
            EnsureFolderExists("Assets/Data");
            EnsureFolderExists("Assets/Data/Quests");
            EnsureFolderExists("Assets/Data/Dialogues");

            string questFolder = "Assets/Data/Quests";
            string dialogueFolder = "Assets/Data/Dialogues";

            // 1. Quest: Cellar Pests
            QuestSO cellarPests = GetOrCreateAsset<QuestSO>($"{questFolder}/Quest_CellarPests.asset");
            cellarPests.Initialize(
                id: "quest_cellar_pests",
                title: "Cellar Pests",
                desc: "Slay the 3 giant rats infesting Innkeeper Barnaby's cellar casks.",
                state: QuestState.NotStarted,
                reqAmount: 3,
                gold: 30,
                bonusGold: 15,
                reward: potionHealth
            );
            EditorUtility.SetDirty(cellarPests);
            assetCount++;

            // 2. Quest: Scrap for the Forge
            ItemSO sharpenedBlade = AssetDatabase.LoadAssetAtPath<ItemSO>($"{DataFolderPath}/Item_SharpenedBlade.asset");
            QuestSO scrapQuest = GetOrCreateAsset<QuestSO>($"{questFolder}/Quest_ScrapMetal.asset");
            scrapQuest.Initialize(
                id: "quest_scrap_metal",
                title: "Scrap for the Forge",
                desc: "Collect 5 pieces of scrap metal from the castle ruins for Blacksmith Baldur.",
                state: QuestState.NotStarted,
                reqAmount: 5,
                gold: 50,
                bonusGold: 25,
                reward: sharpenedBlade
            );
            EditorUtility.SetDirty(scrapQuest);
            assetCount++;

            // 3. Blacksmith Baldur Dialogue Tree (StartNode, LoreNode, QuestsNode)
            DialogueNodeSO baldurStart = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Baldur_StartNode.asset");
            DialogueNodeSO baldurLore = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Baldur_LoreNode.asset");
            DialogueNodeSO baldurQuests = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Baldur_QuestsNode.asset");

            // Lore Node
            baldurLore.Initialize(
                speaker: "Baldur the Smith",
                text: "The Cursed Commander wears ancient plate and wields a heavy shield. But centuries in the damp courtyard have rusted the armor joints at his knees. Aim for the greaves and he won't be able to deflect your blows! (Enemy AC reduced by 2 for first 2 rounds)",
                portrait: null,
                isExit: false
            );
            baldurLore.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Blacksmith] Good to know. Let me see what you have for sale.", null, false, 10, "", null, "[ACTION_OPEN_SHOP]"),
                new DialogueOption("[Back] Let me ask about something else.", baldurStart, false, 10, "", null, ""),
                new DialogueOption("[Exit] Thank you for the advice. Farewell.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(baldurLore);
            assetCount++;

            // Quests Node
            baldurQuests.Initialize(
                speaker: "Baldur the Smith",
                text: "The forge fires are starving for quality ore. The old watchtowers and courtyard are full of scrap metal from fallen sentries. Gather 5 pieces of Scrap Metal and bring them to me, and I'll pay you in gold and tempered steel!",
                portrait: null,
                isExit: false
            );
            baldurQuests.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Accept] I'll gather scrap metal from the castle ruins.", null, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_scrap_metal]"),
                new DialogueOption("[Blacksmith] I already have scrap metal to sell.", null, false, 10, "", null, "[ACTION_OPEN_SHOP]"),
                new DialogueOption("[Back] Let's speak of other matters.", baldurStart, false, 10, "", null, ""),
                new DialogueOption("[Exit] I have business elsewhere.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(baldurQuests);
            assetCount++;

            // Start Node (Initial Greeting with 4 distinct options)
            baldurStart.Initialize(
                speaker: "Baldur the Smith",
                text: "Greetings, traveler. You'd be a fool to face the castle's terrors with dull iron. Bring me salvage scrap from the ruins, and I'll temper steel that cuts bone. What do you need?",
                portrait: null,
                isExit: false
            );
            baldurStart.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Blacksmith] Show me your wares and forge (Open Shop).", null, false, 10, "", null, "[ACTION_OPEN_SHOP]"),
                new DialogueOption("[Lore] What do you know about the castle defences?", baldurLore, false, 10, "", null, "CommanderArmorWeakened"),
                new DialogueOption("[Quests] Do you have any extra work for me?", baldurQuests, false, 10, "", null, ""),
                new DialogueOption("[Exit] I'll keep my own weapons for now.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(baldurStart);
            assetCount++;

            // Backward compatibility aliases
            DialogueNodeSO baldurIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Baldur_Intro.asset");
            baldurIntro.Initialize(baldurStart.SpeakerName, baldurStart.DialogueText, baldurStart.SpeakerPortrait, baldurStart.IsExitNode);
            baldurIntro.SetOptions(new List<DialogueOption>(baldurStart.Options));
            EditorUtility.SetDirty(baldurIntro);

            DialogueNodeSO baldurRumor = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Baldur_Rumor.asset");
            baldurRumor.Initialize(baldurLore.SpeakerName, baldurLore.DialogueText, baldurLore.SpeakerPortrait, baldurLore.IsExitNode);
            baldurRumor.SetOptions(new List<DialogueOption>(baldurLore.Options));
            EditorUtility.SetDirty(baldurRumor);

            // 3. Innkeeper Barnaby Dialogue Tree
            DialogueNodeSO barnabyAccepted = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Accepted.asset");
            barnabyAccepted.Initialize(
                speaker: "Innkeeper Barnaby",
                text: "Bless you! The cellar hatch is right behind the counter. Watch your step, mind the teeth, and don't break the wine bottles!",
                portrait: null,
                isExit: false
            );
            barnabyAccepted.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] I'll get right on it.", null, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(barnabyAccepted);
            assetCount++;

            DialogueNodeSO barnabyDeclined = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Declined.asset");
            barnabyDeclined.Initialize(
                speaker: "Innkeeper Barnaby",
                text: "Rats beneath you? Well, if my cellar collapses under rat tunnels, don't expect a warm hearth or cheap ale next time you visit!",
                portrait: null,
                isExit: true
            );
            barnabyDeclined.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(barnabyDeclined);
            assetCount++;

            DialogueNodeSO barnabyNegotiationSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Negotiation_Success.asset");
            barnabyNegotiationSuccess.Initialize(
                speaker: "Innkeeper Barnaby",
                text: "Fine, fine! If it saves my vintage reserve, I'll pay 45 gold instead of 30! Just get down there and crush those vermin!",
                portrait: null,
                isExit: false
            );
            barnabyNegotiationSuccess.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Deal. I'll clear the cellar now.", barnabyAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_cellar_pests:bonus]"),
                new DialogueOption("On second thought, I have other business.", barnabyDeclined, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(barnabyNegotiationSuccess);
            assetCount++;

            DialogueNodeSO barnabyNegotiationFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Negotiation_Fail.asset");
            barnabyNegotiationFail.Initialize(
                speaker: "Innkeeper Barnaby",
                text: "You drive a hard bargain, stranger, but thirty gold and two healing draughts is every copper I can spare. Take it or leave my cellar to the rats!",
                portrait: null,
                isExit: false
            );
            barnabyNegotiationFail.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Very well, I'll accept thirty gold.", barnabyAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_cellar_pests]"),
                new DialogueOption("Then find yourself another exterminator.", barnabyDeclined, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(barnabyNegotiationFail);
            assetCount++;

            DialogueNodeSO barnabyIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Intro.asset");
            barnabyIntro.Initialize(
                speaker: "Innkeeper Barnaby",
                text: "Thank the gods, an adventurer! Dreadful screeching echoes from my cellar—giant rats are ruining my finest wine casks! Will you clear them out before the whole village dies of thirst?",
                portrait: null,
                isExit: false
            );
            barnabyIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("I'll purge your cellar right away.", barnabyAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_cellar_pests]"),
                new DialogueOption(
                    "[Persuasion DC 13] Fine wine isn't cheap. Danger like this deserves better compensation.",
                    barnabyNegotiationSuccess,
                    true,
                    13,
                    "Persuasion Check (Charisma/Agility)",
                    barnabyNegotiationFail,
                    ""
                ),
                new DialogueOption("Rats are beneath me. Find someone else.", barnabyDeclined, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(barnabyIntro);
            assetCount++;

            // 4. Elder Othelia Items & Dialogue Tree (Kadonnut perintökalleus)
            ItemSO itemSignetRing = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_SignetRing.asset");
            itemSignetRing.Initialize(
                id: "item_signet_ring",
                name: "Othelia's Signet Ring",
                desc: "Ancient golden seal bearing the noble crest of Othelia's ancestors. Lost in the castle courtyard.",
                type: ItemType.QuestItem,
                buyPrice: 0,
                sellPrice: 0,
                statBonus: 0,
                consumable: false
            );
            EditorUtility.SetDirty(itemSignetRing);
            assetCount++;

            ItemSO itemRerollRune = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_RerollRuneStone.asset");
            itemRerollRune.Initialize(
                id: "item_reroll_rune",
                name: "Rune of Fate (D20 Reroll)",
                desc: "Mystical rune stone of Oakhaven. Allows the bearer to invoke a critical D20 reroll.",
                type: ItemType.QuestItem,
                buyPrice: 0,
                sellPrice: 50,
                statBonus: 1,
                consumable: false
            );
            EditorUtility.SetDirty(itemRerollRune);
            assetCount++;

            QuestSO questSignetRing = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quests/Quest_LostSignetRing.asset");
            questSignetRing.Initialize(
                id: "quest_lost_signet_ring",
                title: "Kadonnut perintökalleus",
                desc: "Etsi Othelyan sukunsa sinettisormus linnan alapihan raunioista.",
                state: QuestState.NotStarted,
                reqAmount: 1,
                gold: 50,
                bonusGold: 0,
                reward: itemRerollRune
            );
            EditorUtility.SetDirty(questSignetRing);
            assetCount++;

            DialogueNodeSO otheliaAccepted = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Othelia_Accepted.asset");
            otheliaAccepted.Initialize(
                speaker: "Kylänvanhin Othelia",
                text: "Kiitos, urhea seikkailija! Sukuni sinettisormus katosi linnan alapihoille, kun vartijat kaatuivat. Varo luurankoja raunioissa!",
                portrait: null,
                isExit: false
            );
            otheliaAccepted.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] Etsin sormuksen heti kun pääsen linnaan.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(otheliaAccepted);
            assetCount++;

            DialogueNodeSO otheliaLore = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Othelia_Lore.asset");
            otheliaLore.Initialize(
                speaker: "Kylänvanhin Othelia",
                text: "Ennen kirousta Kivettymiskuningas hallitsi näitä maita oikeudenmukaisesti. Mutta hän tavoitteli kuolemattomuutta syvistä kallioista... ja hänen sydämensä muuttui kiveksi. Varjot nielaisivat linnan sisältäpäin.",
                portrait: null,
                isExit: false
            );
            otheliaLore.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Surullinen kohtalo. Voin etsiä sinettisormuksen puolestasi.", otheliaAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_lost_signet_ring]"),
                new DialogueOption("[Exit] Kiitos tiedosta, jatkan matkaani.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(otheliaLore);
            assetCount++;

            DialogueNodeSO otheliaIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Othelia_Intro.asset");
            otheliaIntro.Initialize(
                speaker: "Kylänvanhin Othelia",
                text: "Tervehdys, matkalainen. Olen kylänvanhin Othelia. Kivenkolo eli rauhassa, kunnes vanha kivilinna heräsi pahuuteen. Jos uskaltaudut linnan porteille, voisitko etsiä perheeni kadonneen sinettisormuksen?",
                portrait: null,
                isExit: false
            );
            otheliaIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Etsin sormuksen puolestasi.", otheliaAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_lost_signet_ring]"),
                new DialogueOption("Kerro minulle linnan menneisyydestä ja kuninkaasta.", otheliaLore, false, 10, "", null, ""),
                new DialogueOption("[Exit] Minulla on kiireitä muualla.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(otheliaIntro);
            assetCount++;

            // 5. Herbalist Mirabel Items & Dialogue Tree (Yrttejä parantajalle)
            ItemSO itemSwampHerb = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_SwampHerb.asset");
            itemSwampHerb.Initialize(
                id: "item_swamp_herb",
                name: "Castle Moat Blossom",
                desc: "Harvinainen suokukka, joka kasvaa vain linnan vallihaudan liepeillä.",
                type: ItemType.QuestItem,
                buyPrice: 0,
                sellPrice: 5,
                statBonus: 0,
                consumable: false
            );
            EditorUtility.SetDirty(itemSwampHerb);
            assetCount++;

            ItemSO itemPoisonVial = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_PoisonVial.asset");
            itemPoisonVial.Initialize(
                id: "item_poison_vial",
                name: "Myrkkypullo (Venom Vial)",
                desc: "Väkevä yrttiuute, joka lisää +5 vahinkoa seuraavaan taisteluun.",
                type: ItemType.Consumable,
                buyPrice: 30,
                sellPrice: 15,
                statBonus: 5,
                consumable: true
            );
            EditorUtility.SetDirty(itemPoisonVial);
            assetCount++;

            ItemSO itemGreaterPotion = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_GreaterPotion.asset");
            itemGreaterPotion.Initialize(
                id: "item_greater_potion",
                name: "Suuri Terveysjuoma",
                desc: "Tiivistetty parannusrohto. Palauttaa 35 kestopistettä.",
                type: ItemType.Consumable,
                buyPrice: 50,
                sellPrice: 25,
                statBonus: 35,
                consumable: true
            );
            EditorUtility.SetDirty(itemGreaterPotion);
            assetCount++;

            QuestSO questSwampHerbs = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quests/Quest_SwampHerbs.asset");
            questSwampHerbs.Initialize(
                id: "quest_swamp_herbs",
                title: "Yrttejä parantajalle",
                desc: "Kerää 3 suokukkaa vallihaudan liepeiltä Mirabelille.",
                state: QuestState.NotStarted,
                reqAmount: 3,
                gold: 30,
                bonusGold: 20,
                reward: itemPoisonVial
            );
            EditorUtility.SetDirty(questSwampHerbs);
            assetCount++;

            DialogueNodeSO mirabelAccepted = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_Accepted.asset");
            mirabelAccepted.Initialize(
                speaker: "Yrttiparantaja Mirabel",
                text: "Loistavaa! Vallihaudalla kukkii harvinainen sinikukka. Kolme kukkaa riittää mahtavaan seokseen. Varo vain vallihaudan liepeillä vaeltavia varjoja!",
                portrait: null,
                isExit: false
            );
            mirabelAccepted.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] Tuon kukat heti kun löydän ne.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(mirabelAccepted);
            assetCount++;

            DialogueNodeSO mirabelCheckSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_NatureCheck_Success.asset");
            mirabelCheckSuccess.Initialize(
                speaker: "Yrttiparantaja Mirabel",
                text: "Tunnistat siis suokasvien arvoituksen! Koska ymmärrät luontoa näin syvästi, keitän sinulle tavallisen myrkyn sijaan Suuren Terveysjuoman palkkioksi!",
                portrait: null,
                isExit: false
            );
            mirabelCheckSuccess.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Sovittu. Haen suokukat vallihaudalta.", mirabelAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_swamp_herbs:bonus]")
            });
            EditorUtility.SetDirty(mirabelCheckSuccess);
            assetCount++;

            DialogueNodeSO mirabelCheckFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_NatureCheck_Fail.asset");
            mirabelCheckFail.Initialize(
                speaker: "Yrttiparantaja Mirabel",
                text: "Kasvintuntemuksesi kaipaa vielä harjoitusta, mutta teräs on aina terästä. Saat myrkkypullon, kunhan tuot kukat ehjinä.",
                portrait: null,
                isExit: false
            );
            mirabelCheckFail.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Käy minulle. Haen kukat.", mirabelAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_swamp_herbs]")
            });
            EditorUtility.SetDirty(mirabelCheckFail);
            assetCount++;

            DialogueNodeSO mirabelIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_Intro.asset");
            mirabelIntro.Initialize(
                speaker: "Yrttiparantaja Mirabel",
                text: "Tss... hiljaa. Haistatko vallihaudan kitkerän sammaleen? Olen Mirabel, yrttiparantaja. Tarvitsisin kipeästi kolme suokukkaa linnan vallihaudan liepeiltä rohtojani varten, mutta epäkuolleet vartijat tekevät keräämisestä liian vaarallista.",
                portrait: null,
                isExit: false
            );
            mirabelIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Voin noutaa kukat vallihaudalta.", mirabelAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_swamp_herbs]"),
                new DialogueOption(
                    "[DC 10 Luontotieto] Suokukka on arkaaista rohtoa – osaan kerätä ne juuria vahingoittamatta.",
                    mirabelCheckSuccess,
                    true,
                    10,
                    "Nature / Wisdom Check",
                    mirabelCheckFail,
                    ""
                ),
                new DialogueOption("[Exit] En ryve mudassa kukkien vuoksi.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(mirabelIntro);
            assetCount++;

            // 6. Boss 1: Cursed Commander Dialogue (Siipi 1: Alapiha)
            DialogueNodeSO commanderSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Commander_Check_Success.asset");
            commanderSuccess.Initialize(
                speaker: "Kirottu Komentaja",
                text: "Vala...? Se kaikuu mielessäni vuosisatojen takaa... Hetken epäröintiä! Haarniskani halkeilee!",
                portrait: null,
                isExit: true
            );
            commanderSuccess.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(commanderSuccess);
            assetCount++;

            DialogueNodeSO commanderFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Commander_Check_Fail.asset");
            commanderFail.Initialize(
                speaker: "Kirottu Komentaja",
                text: "Kunnia on kuollut, kuten minäkin! Teräkseni maistaa vertasi!",
                portrait: null,
                isExit: true
            );
            commanderFail.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(commanderFail);
            assetCount++;

            DialogueNodeSO commanderIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Commander_Intro.asset");
            commanderIntro.Initialize(
                speaker: "Kirottu Komentaja",
                text: "Kuka uskaltaa häpäistä linnan vartiotornin? Minun miekkani on maannut haudassa vuosisatoja, mutta tänään se maistaa jälleen elävää verta!",
                portrait: null,
                isExit: false
            );
            commanderIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(
                    "[DC 13 Sotilaan kunnia] Vartijakaartin vala velvoittaa sinua yhä! Muista kunniasi äläkä palvele kirousta!",
                    commanderSuccess,
                    true,
                    13,
                    "Honor / Persuasion Check",
                    commanderFail,
                    "SoldiersHonor"
                ),
                new DialogueOption("[Taistelu] Puheesi ovat turhia, epäkuollut. Valmistaudu tuhoosi!", commanderFail, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(commanderIntro);
            assetCount++;

            // 7. Boss 2: Shadow Mage Malakor Dialogue (Siipi 2: Kirjasto)
            DialogueNodeSO malakorSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Malakor_Check_Success.asset");
            malakorSuccess.Initialize(
                speaker: "Varjomaagi Malakor",
                text: "Mitä?! Mistä tiedät tuon loitsun purkukaavan?! Harhakuva hajoaa!",
                portrait: null,
                isExit: true
            );
            malakorSuccess.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(malakorSuccess);
            assetCount++;

            DialogueNodeSO malakorFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Malakor_Check_Fail.asset");
            malakorFail.Initialize(
                speaker: "Varjomaagi Malakor",
                text: "Sokea typerys! Et koskaan erota varjoa totuudesta peilieni salissa!",
                portrait: null,
                isExit: true
            );
            malakorFail.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(malakorFail);
            assetCount++;

            DialogueNodeSO malakorIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Malakor_Intro.asset");
            malakorIntro.Initialize(
                speaker: "Varjomaagi Malakor",
                text: "Tervetuloa arkaaniseen tutkimuskammiooni, kuolevainen. Etsitkö salaisuuksia? Vai vain omaa kuolemaasi peilieni labyrintissa?",
                portrait: null,
                isExit: false
            );
            malakorIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(
                    "[DC 14 Arkaaninen herja] Harhasi ovat alkeellisia. Tunnen peiliheijastuksesi taitteen!",
                    malakorSuccess,
                    true,
                    14,
                    "Arcana / Intelligence Check",
                    malakorFail,
                    "ArcaneHeresy"
                ),
                new DialogueOption("[Taistelu] Harhat särkyvät teräksen voimalla!", malakorFail, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(malakorIntro);
            assetCount++;

            // 8. Boss 3: Gargoyle King Dialogue (Siipi 3: Kruununsali)
            DialogueNodeSO gargoyleSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/GargoyleKing_Check_Success.asset");
            gargoyleSuccess.Initialize(
                speaker: "Kivettymiskuningas",
                text: "K-kivettymä... vanki?! Grraaaagh! Kivinen sydämeni järkkyy!",
                portrait: null,
                isExit: true
            );
            gargoyleSuccess.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(gargoyleSuccess);
            assetCount++;

            DialogueNodeSO gargoyleFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/GargoyleKing_Check_Fail.asset");
            gargoyleFail.Initialize(
                speaker: "Kivettymiskuningas",
                text: "Pikkusieluinen madonruoka! Kallio ja kivivyöryt murskaavat sinut!",
                portrait: null,
                isExit: true
            );
            gargoyleFail.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(gargoyleFail);
            assetCount++;

            DialogueNodeSO gargoyleIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/GargoyleKing_Intro.asset");
            gargoyleIntro.Initialize(
                speaker: "Kivettymiskuningas",
                text: "Kruununi on ikuista kiveä! Tämä valtakunta ei koskaan murene! Polvistu kiven herran edessä tai muutu osaksi linnoituksen lattiaa!",
                portrait: null,
                isExit: false
            );
            gargoyleIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(
                    "[DC 16 Pelottelu] Sinä et ole kuningas, vaan kivettynyt vanki omassa haudassasi! Aikasi on ohi!",
                    gargoyleSuccess,
                    true,
                    16,
                    "Intimidation / Strength Check",
                    gargoyleFail,
                    "GargoyleKingIntimidated"
                ),
                new DialogueOption("[Taistelu] Hakkaan kivisen kruunusi sirpaleiksi!", gargoyleFail, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(gargoyleIntro);
            assetCount++;

            Debug.Log($"[GenerateGameDataEditor] Successfully created and verified {assetCount} game assets (Items, Abilities, Quests, Characters, Dialogues).");
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);

                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolderExists(parent);
                }

                AssetDatabase.CreateFolder(parent, folderName);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureDataFolderExists()
        {
            EnsureFolderExists(DataFolderPath);
        }

        private static T GetOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            return asset;
        }
    }
}
