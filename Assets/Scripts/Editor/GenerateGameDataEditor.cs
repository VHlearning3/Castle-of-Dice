using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

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
                "- 3 Quests (Cellar Rats, Lost Signet Ring, Swamp Herbs)",
                "OK"
            );
        }

        private static void EnsureDataFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(DataFolderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets"))
                {
                    Directory.CreateDirectory(Application.dataPath);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.Refresh();
            }
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
