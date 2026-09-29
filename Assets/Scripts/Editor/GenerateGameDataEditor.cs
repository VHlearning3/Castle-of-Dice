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

        // Assets created during the current run. Existing assets keep their (possibly hand-tuned) values
        // unless s_resetExisting is set by the explicit "Reset" menu command.
        private static readonly HashSet<UnityEngine.Object> s_createdThisRun = new HashSet<UnityEngine.Object>();
        private static bool s_resetExisting;

        [MenuItem("CastleOfDice/Generate Missing Game Assets", false, 1)]
        public static void GenerateAllGameAssetsMenu()
        {
            GenerateAllGameAssets(true);
        }

        [MenuItem("CastleOfDice/Reset All Game Assets To Defaults", false, 2)]
        public static void ResetAllGameAssetsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset game data",
                "Overwrite every generated asset in Assets/Data with the default values from GenerateGameDataEditor? Manual Inspector edits will be lost.",
                "Reset", "Cancel"))
            {
                return;
            }

            s_resetExisting = true;
            try
            {
                GenerateAllGameAssets(true);
            }
            finally
            {
                s_resetExisting = false;
            }
        }

        private static bool ShouldInitialize(UnityEngine.Object asset)
        {
            return s_resetExisting || s_createdThisRun.Contains(asset);
        }

        public static void GenerateAllGameAssets(bool showDialog = false)
        {
            EnsureDataFolderExists();

            int assetCount = 0;

            // 1. Generate Items
            ItemSO potionHealth = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_Potion_Health.asset");
            if (ShouldInitialize(potionHealth)) potionHealth.Initialize(
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
            if (ShouldInitialize(sharpenedBlade)) sharpenedBlade.Initialize(
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
            if (ShouldInitialize(runicArmor)) runicArmor.Initialize(
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
            if (ShouldInitialize(scrapMetal)) scrapMetal.Initialize(
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
            if (ShouldInitialize(warriorSlash)) warriorSlash.Initialize(
                id: "warrior_sword_slash",
                name: "Sword Slash",
                desc: "Melee strike for 1d8 + STR damage. Half of the damage also cleaves an enemy standing next to you.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 0,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Attack",
                diceCount: 1,
                diceSides: 8,
                addAttribute: true
            );
            EditorUtility.SetDirty(warriorSlash);
            assetCount++;

            AbilitySO warriorShield = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_ShieldBlock.asset");
            if (ShouldInitialize(warriorShield)) warriorShield.Initialize(
                id: "warrior_shield_block",
                name: "Shield Block",
                desc: "Raise your shield for +4 Armor Class until your next turn, and strike back for 1d6 whenever an adjacent enemy misses you.",
                target: AbilityTargetType.Self,
                abilityRange: 0,
                aoeRadius: 0,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.ShieldWall,
                duration: 1,
                animTrigger: "Buff"
            );
            EditorUtility.SetDirty(warriorShield);
            assetCount++;

            AbilitySO warriorWarCry = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_WarCry.asset");
            if (ShouldInitialize(warriorWarCry)) warriorWarCry.Initialize(
                id: "warrior_war_cry",
                name: "War Cry",
                desc: "A 3x3 shockwave that pushes adjacent enemies back 1-2 tiles and deals 1d4 + STR damage.",
                target: AbilityTargetType.Area3x3,
                abilityRange: 1,
                aoeRadius: 1,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Buff",
                diceCount: 1,
                diceSides: 4,
                addAttribute: true
            );
            EditorUtility.SetDirty(warriorWarCry);
            assetCount++;

            AbilitySO warriorIronWill = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Warrior_IronWill.asset");
            if (ShouldInitialize(warriorIronWill)) warriorIronWill.Initialize(
                id: "warrior_iron_will",
                name: "Iron Will",
                desc: "Restore 30% of your maximum hit points and shake off poison, frostbite and blindness.",
                target: AbilityTargetType.Self,
                abilityRange: 0,
                aoeRadius: 0,
                value: 0,
                checkRequired: false,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Buff"
            );
            EditorUtility.SetDirty(warriorIronWill);
            assetCount++;

            // --- Mage Abilities ---
            AbilitySO mageFireball = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_Fireball.asset");
            if (ShouldInitialize(mageFireball)) mageFireball.Initialize(
                id: "mage_fireball",
                name: "Fireball",
                desc: "Hurl a sphere of flame up to 4 tiles, dealing 2d6 fire damage to everything in a 3x3 area.",
                target: AbilityTargetType.Area3x3,
                abilityRange: 4,
                aoeRadius: 1,
                value: 0,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "CastSpell",
                diceCount: 2,
                diceSides: 6,
                addAttribute: false
            );
            EditorUtility.SetDirty(mageFireball);
            assetCount++;

            AbilitySO mageFrostbite = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_Frostbite.asset");
            if (ShouldInitialize(mageFrostbite)) mageFrostbite.Initialize(
                id: "mage_frostbite",
                name: "Frostbite",
                desc: "A frost ray up to 4 tiles for 1d6 + INT damage that halves the target's movement for 1 turn.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 4,
                aoeRadius: 0,
                value: 0,
                checkRequired: true,
                effect: StatusEffectType.Frostbite,
                duration: 1,
                animTrigger: "CastSpell",
                diceCount: 1,
                diceSides: 6,
                addAttribute: true
            );
            EditorUtility.SetDirty(mageFrostbite);
            assetCount++;

            AbilitySO mageManaShield = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Mage_ManaShield.asset");
            if (ShouldInitialize(mageManaShield)) mageManaShield.Initialize(
                id: "mage_mana_shield",
                name: "Mana Shield",
                desc: "Conjure a barrier that completely absorbs the next attack that hits you.",
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
            if (ShouldInitialize(mageBlink)) mageBlink.Initialize(
                id: "mage_blink",
                name: "Blink",
                desc: "Instantly teleport to an unoccupied tile up to 7 tiles away without provoking attacks.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 7,
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
            if (ShouldInitialize(rogueBackstab)) rogueBackstab.Initialize(
                id: "rogue_backstab",
                name: "Backstab",
                desc: "Attack with Advantage for 2d6 + AGI damage, doubled if the target is blinded or you just used Shadow Step.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 0,
                checkRequired: true,
                effect: StatusEffectType.None,
                duration: 0,
                animTrigger: "Attack",
                diceCount: 2,
                diceSides: 6,
                addAttribute: true
            );
            EditorUtility.SetDirty(rogueBackstab);
            assetCount++;

            AbilitySO rogueSmokeBomb = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Rogue_SmokeBomb.asset");
            if (ShouldInitialize(rogueSmokeBomb)) rogueSmokeBomb.Initialize(
                id: "rogue_smoke_bomb",
                name: "Smoke Bomb",
                desc: "Toss a smoke canister up to 3 tiles, blinding everything in a 3x3 area for 1 turn (Disadvantage on attacks).",
                target: AbilityTargetType.Area3x3,
                abilityRange: 3,
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
            if (ShouldInitialize(roguePoisonDagger)) roguePoisonDagger.Initialize(
                id: "rogue_poison_dagger",
                name: "Poison Dagger",
                desc: "Slash with an envenomed blade for 1d4 + AGI damage, then 1d6 poison damage each turn for 2 turns.",
                target: AbilityTargetType.SingleTarget,
                abilityRange: 1,
                aoeRadius: 0,
                value: 0,
                checkRequired: true,
                effect: StatusEffectType.Poison,
                duration: 2,
                animTrigger: "Attack",
                diceCount: 1,
                diceSides: 4,
                addAttribute: true
            );
            EditorUtility.SetDirty(roguePoisonDagger);
            assetCount++;

            AbilitySO rogueLockpicking = GetOrCreateAsset<AbilitySO>($"{DataFolderPath}/Ability_Rogue_Lockpicking.asset");
            if (ShouldInitialize(rogueLockpicking)) rogueLockpicking.Initialize(
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
            if (ShouldInitialize(warriorClass)) warriorClass.Initialize(
                type: CharacterClassType.Warrior,
                name: "Sir Roland",
                lore: "Veteran of the royal guard who donned his ancestral plate armor to purge his fallen castle of undead abominations.",
                maxHp: 30,
                ac: 14,
                move: 4,
                bonus: 3,
                abilities: new List<AbilitySO> { warriorSlash, warriorShield, warriorWarCry, warriorIronWill }
            );
            EditorUtility.SetDirty(warriorClass);
            assetCount++;

            // --- Mage: Scholar Elira ---
            CharacterClassSO mageClass = GetOrCreateAsset<CharacterClassSO>($"{DataFolderPath}/Character_Mage_Elira.asset");
            if (ShouldInitialize(mageClass)) mageClass.Initialize(
                type: CharacterClassType.Mage,
                name: "Scholar Elira",
                lore: "Academy arcanist seeking to unravel the ancient curses and retrieve lost grimoires hidden in the castle's depths.",
                maxHp: 20,
                ac: 12,
                move: 3,
                bonus: 3,
                abilities: new List<AbilitySO> { mageFireball, mageFrostbite, mageManaShield, mageBlink }
            );
            EditorUtility.SetDirty(mageClass);
            assetCount++;

            // --- Rogue: Shadow-Corvo ---
            CharacterClassSO rogueClass = GetOrCreateAsset<CharacterClassSO>($"{DataFolderPath}/Character_Rogue_Corvo.asset");
            if (ShouldInitialize(rogueClass)) rogueClass.Initialize(
                type: CharacterClassType.Rogue,
                name: "Shadow-Corvo",
                lore: "Tavern-bred opportunist and lockpick specialist who knows the castle's secret passageways better than anyone.",
                maxHp: 25,
                ac: 13,
                move: 5,
                bonus: 3,
                abilities: new List<AbilitySO> { rogueBackstab, rogueSmokeBomb, roguePoisonDagger, rogueLockpicking }
            );
            EditorUtility.SetDirty(rogueClass);
            assetCount++;

            // 4. Generate 3 Quests
            QuestSO cellarRats = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quest_CellarRats.asset");
            if (ShouldInitialize(cellarRats)) cellarRats.Initialize(
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
            if (ShouldInitialize(lostSignet)) lostSignet.Initialize(
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
            if (ShouldInitialize(swampHerbs)) swampHerbs.Initialize(
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
            if (showDialog)
            {
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
            if (ShouldInitialize(cellarPests)) cellarPests.Initialize(
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
            if (ShouldInitialize(scrapQuest)) scrapQuest.Initialize(
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
            if (ShouldInitialize(baldurLore)) baldurLore.Initialize(
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
            if (ShouldInitialize(baldurQuests)) baldurQuests.Initialize(
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
            if (ShouldInitialize(baldurStart)) baldurStart.Initialize(
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
            if (ShouldInitialize(baldurIntro)) baldurIntro.Initialize(baldurStart.SpeakerName, baldurStart.DialogueText, baldurStart.SpeakerPortrait, baldurStart.IsExitNode);
            baldurIntro.SetOptions(new List<DialogueOption>(baldurStart.Options));
            EditorUtility.SetDirty(baldurIntro);

            DialogueNodeSO baldurRumor = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Baldur_Rumor.asset");
            if (ShouldInitialize(baldurRumor)) baldurRumor.Initialize(baldurLore.SpeakerName, baldurLore.DialogueText, baldurLore.SpeakerPortrait, baldurLore.IsExitNode);
            baldurRumor.SetOptions(new List<DialogueOption>(baldurLore.Options));
            EditorUtility.SetDirty(baldurRumor);

            // 3. Innkeeper Barnaby Dialogue Tree
            DialogueNodeSO barnabyAccepted = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Accepted.asset");
            if (ShouldInitialize(barnabyAccepted)) barnabyAccepted.Initialize(
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
            if (ShouldInitialize(barnabyDeclined)) barnabyDeclined.Initialize(
                speaker: "Innkeeper Barnaby",
                text: "Rats beneath you? Well, if my cellar collapses under rat tunnels, don't expect a warm hearth or cheap ale next time you visit!",
                portrait: null,
                isExit: true
            );
            barnabyDeclined.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(barnabyDeclined);
            assetCount++;

            DialogueNodeSO barnabyNegotiationSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Barnaby_Negotiation_Success.asset");
            if (ShouldInitialize(barnabyNegotiationSuccess)) barnabyNegotiationSuccess.Initialize(
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
            if (ShouldInitialize(barnabyNegotiationFail)) barnabyNegotiationFail.Initialize(
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
            if (ShouldInitialize(barnabyIntro)) barnabyIntro.Initialize(
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
            if (ShouldInitialize(itemSignetRing)) itemSignetRing.Initialize(
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
            if (ShouldInitialize(itemRerollRune)) itemRerollRune.Initialize(
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
            if (ShouldInitialize(questSignetRing)) questSignetRing.Initialize(
                id: "quest_lost_signet_ring",
                title: "The Lost Signet Ring",
                desc: "Search for Elder Othelia's ancestral signet ring in the courtyard ruins.",
                state: QuestState.NotStarted,
                reqAmount: 1,
                gold: 50,
                bonusGold: 0,
                reward: itemRerollRune
            );
            EditorUtility.SetDirty(questSignetRing);
            assetCount++;

            DialogueNodeSO otheliaAccepted = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Othelia_Accepted.asset");
            if (ShouldInitialize(otheliaAccepted)) otheliaAccepted.Initialize(
                speaker: "Elder Othelia",
                text: "Thank you, brave adventurer! My family's signet ring was lost in the lower courtyards when the guards fell. Beware the skeletons among the ruins!",
                portrait: null,
                isExit: false
            );
            otheliaAccepted.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] I shall search for the ring as soon as I enter the castle.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(otheliaAccepted);
            assetCount++;

            DialogueNodeSO otheliaLore = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Othelia_Lore.asset");
            if (ShouldInitialize(otheliaLore)) otheliaLore.Initialize(
                speaker: "Elder Othelia",
                text: "Before the curse, the Petrified King ruled these lands justly. But he sought immortality from the deep crags... and his heart turned to stone. Shadows consumed the fortress from within.",
                portrait: null,
                isExit: false
            );
            otheliaLore.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("A tragic fate. I can search for the signet ring on your behalf.", otheliaAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_lost_signet_ring]"),
                new DialogueOption("[Exit] Thank you for the knowledge, I shall be on my way.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(otheliaLore);
            assetCount++;

            DialogueNodeSO otheliaIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Othelia_Intro.asset");
            if (ShouldInitialize(otheliaIntro)) otheliaIntro.Initialize(
                speaker: "Elder Othelia",
                text: "Greetings, traveler. I am Elder Othelia. Oakhaven lived in peace until the ancient stone castle stirred with evil. If you venture to the castle gates, could you search for my family's lost signet ring?",
                portrait: null,
                isExit: false
            );
            otheliaIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("I will find the ring for you.", otheliaAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_lost_signet_ring]"),
                new DialogueOption("Tell me about the castle's history and the King.", otheliaLore, false, 10, "", null, ""),
                new DialogueOption("[Exit] I have urgent matters elsewhere.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(otheliaIntro);
            assetCount++;

            // 5. Herbalist Mirabel Items & Dialogue Tree (Yrttejä parantajalle)
            ItemSO itemSwampHerb = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_SwampHerb.asset");
            if (ShouldInitialize(itemSwampHerb)) itemSwampHerb.Initialize(
                id: "item_swamp_herb",
                name: "Castle Moat Blossom",
                desc: "Rare swamp flower that blooms only near the castle moat.",
                type: ItemType.QuestItem,
                buyPrice: 0,
                sellPrice: 5,
                statBonus: 0,
                consumable: false
            );
            EditorUtility.SetDirty(itemSwampHerb);
            assetCount++;

            ItemSO itemPoisonVial = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_PoisonVial.asset");
            if (ShouldInitialize(itemPoisonVial)) itemPoisonVial.Initialize(
                id: "item_poison_vial",
                name: "Poison Vial",
                desc: "Potent herbal extract that adds +5 bonus damage in the next combat encounter.",
                type: ItemType.Consumable,
                buyPrice: 30,
                sellPrice: 15,
                statBonus: 5,
                consumable: true
            );
            EditorUtility.SetDirty(itemPoisonVial);
            assetCount++;

            ItemSO itemGreaterPotion = GetOrCreateAsset<ItemSO>($"{DataFolderPath}/Item_GreaterPotion.asset");
            if (ShouldInitialize(itemGreaterPotion)) itemGreaterPotion.Initialize(
                id: "item_greater_potion",
                name: "Greater Health Potion",
                desc: "Concentrated healing draught. Restores 35 Hit Points.",
                type: ItemType.Consumable,
                buyPrice: 50,
                sellPrice: 25,
                statBonus: 35,
                consumable: true
            );
            EditorUtility.SetDirty(itemGreaterPotion);
            assetCount++;

            QuestSO questSwampHerbs = GetOrCreateAsset<QuestSO>($"{DataFolderPath}/Quests/Quest_SwampHerbs.asset");
            if (ShouldInitialize(questSwampHerbs)) questSwampHerbs.Initialize(
                id: "quest_swamp_herbs",
                title: "Herbs for the Healer",
                desc: "Gather 3 swamp flowers near the moat for Mirabel.",
                state: QuestState.NotStarted,
                reqAmount: 3,
                gold: 30,
                bonusGold: 20,
                reward: itemPoisonVial
            );
            EditorUtility.SetDirty(questSwampHerbs);
            assetCount++;

            DialogueNodeSO mirabelAccepted = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_Accepted.asset");
            if (ShouldInitialize(mirabelAccepted)) mirabelAccepted.Initialize(
                speaker: "Mirabel the Herbalist",
                text: "Splendid! Rare blue blossoms thrive along the moat. Three flowers will suffice for a potent brew. Beware the shadows lurking near the moat!",
                portrait: null,
                isExit: false
            );
            mirabelAccepted.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] I shall return with the blossoms as soon as I find them.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(mirabelAccepted);
            assetCount++;

            DialogueNodeSO mirabelCheckSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_NatureCheck_Success.asset");
            if (ShouldInitialize(mirabelCheckSuccess)) mirabelCheckSuccess.Initialize(
                speaker: "Mirabel the Herbalist",
                text: "You truly understand marsh flora! Since you possess such deep knowledge of nature, I shall brew you a Greater Health Potion instead of ordinary poison as your reward!",
                portrait: null,
                isExit: false
            );
            mirabelCheckSuccess.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Agreed. I will gather the swamp blossoms from the moat.", mirabelAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_swamp_herbs:bonus]")
            });
            EditorUtility.SetDirty(mirabelCheckSuccess);
            assetCount++;

            DialogueNodeSO mirabelCheckFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_NatureCheck_Fail.asset");
            if (ShouldInitialize(mirabelCheckFail)) mirabelCheckFail.Initialize(
                speaker: "Mirabel the Herbalist",
                text: "Your herb lore needs practice, but steel is steel. You'll receive a poison vial, provided you return the blossoms undamaged.",
                portrait: null,
                isExit: false
            );
            mirabelCheckFail.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("Fair enough. I will fetch the flowers.", mirabelAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_swamp_herbs]")
            });
            EditorUtility.SetDirty(mirabelCheckFail);
            assetCount++;

            DialogueNodeSO mirabelIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Mirabel_Intro.asset");
            if (ShouldInitialize(mirabelIntro)) mirabelIntro.Initialize(
                speaker: "Mirabel the Herbalist",
                text: "Hush... be quiet. Do you smell the bitter moss of the moat? I am Mirabel, herbalist of Oakhaven. I desperately need three swamp flowers from near the moat for my remedies, but the undead sentries make foraging far too perilous.",
                portrait: null,
                isExit: false
            );
            mirabelIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("I can retrieve the flowers from the moat for you.", mirabelAccepted, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_swamp_herbs]"),
                new DialogueOption(
                    "[DC 10 Nature Lore] Moat blossoms are an ancient remedy - I know how to harvest them without damaging the roots.",
                    mirabelCheckSuccess,
                    true,
                    10,
                    "Nature / Wisdom Check",
                    mirabelCheckFail,
                    ""
                ),
                new DialogueOption("[Exit] I'm not wallowing in mud for wild flowers.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            EditorUtility.SetDirty(mirabelIntro);
            assetCount++;

            // 6. Boss 1: Cursed Commander Dialogue (Siipi 1: Alapiha)
            DialogueNodeSO commanderSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Commander_Check_Success.asset");
            if (ShouldInitialize(commanderSuccess)) commanderSuccess.Initialize(
                speaker: "Cursed Commander",
                text: "An oath...? It echoes across centuries in my mind... A moment of hesitation! My armor splinters!",
                portrait: null,
                isExit: true
            );
            commanderSuccess.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(commanderSuccess);
            assetCount++;

            DialogueNodeSO commanderFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Commander_Check_Fail.asset");
            if (ShouldInitialize(commanderFail)) commanderFail.Initialize(
                speaker: "Cursed Commander",
                text: "Honor is dead, as am I! My blade shall taste your blood!",
                portrait: null,
                isExit: true
            );
            commanderFail.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(commanderFail);
            assetCount++;

            DialogueNodeSO commanderIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Commander_Intro.asset");
            if (ShouldInitialize(commanderIntro)) commanderIntro.Initialize(
                speaker: "Cursed Commander",
                text: "Who dares desecrate the castle watchtower? My blade has lain in the grave for centuries, but today it hungers for living blood once more!",
                portrait: null,
                isExit: false
            );
            commanderIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(
                    "[DC 13 Soldier's Honor] The oath of the royal guard still binds you! Remember your honor and serve the curse no longer!",
                    commanderSuccess,
                    true,
                    13,
                    "Honor / Persuasion Check",
                    commanderFail,
                    "SoldiersHonor"
                ),
                new DialogueOption("[Fight] Your words are hollow, undead fiend. Prepare for destruction!", commanderFail, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(commanderIntro);
            assetCount++;

            // 7. Boss 2: Shadow Mage Malakor Dialogue (Siipi 2: Kirjasto)
            DialogueNodeSO malakorSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Malakor_Check_Success.asset");
            if (ShouldInitialize(malakorSuccess)) malakorSuccess.Initialize(
                speaker: "Shadow Mage Malakor",
                text: "What?! How do you know the dispel formula for that incantation?! The phantom reflection shatters!",
                portrait: null,
                isExit: true
            );
            malakorSuccess.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(malakorSuccess);
            assetCount++;

            DialogueNodeSO malakorFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Malakor_Check_Fail.asset");
            if (ShouldInitialize(malakorFail)) malakorFail.Initialize(
                speaker: "Shadow Mage Malakor",
                text: "Blind fool! You shall never discern shadow from truth in my hall of mirrors!",
                portrait: null,
                isExit: true
            );
            malakorFail.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(malakorFail);
            assetCount++;

            DialogueNodeSO malakorIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/Malakor_Intro.asset");
            if (ShouldInitialize(malakorIntro)) malakorIntro.Initialize(
                speaker: "Shadow Mage Malakor",
                text: "Welcome to my arcane sanctum, mortal. Do you seek ancient secrets? Or merely your doom within my labyrinth of mirrors?",
                portrait: null,
                isExit: false
            );
            malakorIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(
                    "[DC 14 Arcane Rebuke] Your phantoms are rudimentary. I see through the refraction of your mirrors!",
                    malakorSuccess,
                    true,
                    14,
                    "Arcana / Intelligence Check",
                    malakorFail,
                    "ArcaneHeresy"
                ),
                new DialogueOption("[Fight] Illusions shatter before cold steel!", malakorFail, false, 10, "", null, "")
            });
            EditorUtility.SetDirty(malakorIntro);
            assetCount++;

            // 8. Boss 3: Gargoyle King Dialogue (Siipi 3: Kruununsali)
            DialogueNodeSO gargoyleSuccess = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/GargoyleKing_Check_Success.asset");
            if (ShouldInitialize(gargoyleSuccess)) gargoyleSuccess.Initialize(
                speaker: "The Gargoyle King",
                text: "P-petrified... prisoner?! Grraaaagh! My stony heart trembles!",
                portrait: null,
                isExit: true
            );
            gargoyleSuccess.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(gargoyleSuccess);
            assetCount++;

            DialogueNodeSO gargoyleFail = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/GargoyleKing_Check_Fail.asset");
            if (ShouldInitialize(gargoyleFail)) gargoyleFail.Initialize(
                speaker: "The Gargoyle King",
                text: "Insolent worm! The mountain stone and rockslides will crush you to dust!",
                portrait: null,
                isExit: true
            );
            gargoyleFail.SetOptions(new List<DialogueOption>());
            EditorUtility.SetDirty(gargoyleFail);
            assetCount++;

            DialogueNodeSO gargoyleIntro = GetOrCreateAsset<DialogueNodeSO>($"{dialogueFolder}/GargoyleKing_Intro.asset");
            if (ShouldInitialize(gargoyleIntro)) gargoyleIntro.Initialize(
                speaker: "The Gargoyle King",
                text: "My crown is eternal stone! This realm shall never crumble! Kneel before the Lord of Stone or become part of the fortress floor!",
                portrait: null,
                isExit: false
            );
            gargoyleIntro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(
                    "[DC 16 Intimidation] You are no king, but a petrified prisoner in your own tomb! Your reign ends now!",
                    gargoyleSuccess,
                    true,
                    16,
                    "Intimidation / Strength Check",
                    gargoyleFail,
                    "GargoyleKingIntimidated"
                ),
                new DialogueOption("[Fight] I shall smash your stone crown to pieces!", gargoyleFail, false, 10, "", null, "")
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
                s_createdThisRun.Add(asset);
            }
            return asset;
        }
    }
}
