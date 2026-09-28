using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Save data DTO serialized to JSON and persisted in PlayerPrefs for WebGL compatibility.
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        /// <summary>Current save format. Version 1 saves (no field) load with defaults for new fields.</summary>
        public const int CurrentVersion = 2;

        public int saveVersion = CurrentVersion;
        public int currentLevel = 1;
        public int maxHPBonus = 0;
        public int attributeBonusModifier = 0;
        public int permanentWeaponDamageBonus = 0;
        public int permanentArmorClassBonus = 0;
        public List<int> upgradedAbilityIndices = new List<int>();
        public int gold = 0;
        public int scrapMetal = 0;
        public int rerollScrolls = 1;
        /// <summary>Chosen hero class (-1 = not recorded, e.g. version 1 saves).</summary>
        public int characterClass = -1;
    }

    /// <summary>
    /// Persistent Save System utilizing PlayerPrefs JSON serialization.
    /// Zero file-system IO ensures complete reliability across WebGL, Windows Standalone, and Editor.
    /// Progression is stored as bonuses relative to the class baseline and applied absolutely,
    /// so saving and loading repeatedly never stacks upgrades.
    /// </summary>
    public static class SaveSystem
    {
        private const string SaveKey = "CastleOfDice_SaveData";

        /// <summary>
        /// Saves current hero state, inventory, and progression.
        /// </summary>
        public static void SaveGame(PlayerDataSO dataSO = null, PlayerUnit player = null)
        {
            if (dataSO == null)
            {
                dataSO = PlayerUnit.ProgressionData;
            }

            // The live hero is the source of truth; refresh the data store from it first
            if (dataSO != null && player != null)
            {
                dataSO.SyncFromPlayer(player);
            }

            PlayerSaveData save = new PlayerSaveData();

            if (dataSO != null)
            {
                save.currentLevel = dataSO.CurrentLevel;
                save.maxHPBonus = dataSO.MaxHPBonus;
                save.attributeBonusModifier = dataSO.AttributeBonusModifier;
                save.permanentWeaponDamageBonus = dataSO.WeaponDamageBonus;
                save.permanentArmorClassBonus = dataSO.ArmorClassBonus;
                save.upgradedAbilityIndices = new List<int>(dataSO.UpgradedAbilityIndices);
            }
            else if (player != null)
            {
                save.currentLevel = player.Level;
                save.maxHPBonus = player.MaxHPBonus;
                save.attributeBonusModifier = player.AttributeBonusModifier;
                save.permanentWeaponDamageBonus = player.WeaponDamageBonus;
                save.permanentArmorClassBonus = player.ArmorClassBonus;
                player.GetUpgradedAbilitySlots(save.upgradedAbilityIndices);
            }

            if (player != null && player.CharacterClass != null)
            {
                save.characterClass = (int)player.CharacterClass.ClassType;
            }

            if (InventoryManager.Instance != null)
            {
                save.gold = InventoryManager.Instance.CurrentGold;
                save.scrapMetal = InventoryManager.Instance.ScrapMetalCount;
                save.rerollScrolls = InventoryManager.Instance.RerollScrollCount;
            }

            string json = JsonUtility.ToJson(save, true);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
            Debug.Log($"[SaveSystem] Game saved successfully! Level: {save.currentLevel}, Gold: {save.gold}, Scrap: {save.scrapMetal}");
        }

        /// <summary>
        /// Loads saved progression into the PlayerDataSO and applies it (absolutely) to the hero and inventory.
        /// Returns null when no save exists.
        /// </summary>
        public static PlayerSaveData LoadGame(PlayerDataSO targetSO = null, PlayerUnit targetPlayer = null)
        {
            PlayerSaveData save = PeekSave();
            if (save == null)
            {
                Debug.Log("[SaveSystem] No existing save data found.");
                return null;
            }

            if (targetSO == null)
            {
                targetSO = PlayerUnit.ProgressionData;
            }

            if (targetSO != null)
            {
                targetSO.ResetData();
                targetSO.CurrentLevel = save.currentLevel;
                targetSO.MaxHPBonus = save.maxHPBonus;
                targetSO.AttributeBonusModifier = save.attributeBonusModifier;
                targetSO.WeaponDamageBonus = save.permanentWeaponDamageBonus;
                targetSO.ArmorClassBonus = save.permanentArmorClassBonus;
                targetSO.Gold = save.gold;
                targetSO.ScrapMetal = save.scrapMetal;

                foreach (int slot in save.upgradedAbilityIndices)
                {
                    targetSO.RegisterUpgradedAbility(slot);
                }

                if (targetPlayer != null)
                {
                    targetSO.ApplyToPlayer(targetPlayer);
                }
            }
            else if (targetPlayer != null)
            {
                targetPlayer.ApplyProgression(save.currentLevel, save.maxHPBonus, save.attributeBonusModifier,
                    save.permanentWeaponDamageBonus, save.permanentArmorClassBonus, save.upgradedAbilityIndices);
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RestoreFromSave(save.gold, save.scrapMetal, save.rerollScrolls);
            }

            Debug.Log($"[SaveSystem] Game loaded successfully! Level: {save.currentLevel}");
            return save;
        }

        /// <summary>
        /// Reads and migrates the stored save without applying it. Returns null if none exists or it is corrupt.
        /// </summary>
        public static PlayerSaveData PeekSave()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return null;

            string json = PlayerPrefs.GetString(SaveKey);
            if (string.IsNullOrEmpty(json)) return null;

            PlayerSaveData save;
            try
            {
                save = JsonUtility.FromJson<PlayerSaveData>(json);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"[SaveSystem] Save data is corrupt and was ignored: {e.Message}");
                return null;
            }
            if (save == null) return null;

            if (save.saveVersion < 2 || json.IndexOf("\"saveVersion\"", StringComparison.Ordinal) < 0)
            {
                // Version 1 saves predate the reroll scroll / class fields
                save.saveVersion = 1;
                save.rerollScrolls = Mathf.Max(save.rerollScrolls, 1);
                save.characterClass = -1;
            }

            return save;
        }

        /// <summary>
        /// Returns true if valid save data exists in PlayerPrefs.
        /// </summary>
        public static bool HasSavedGame()
        {
            return PlayerPrefs.HasKey(SaveKey);
        }

        /// <summary>
        /// Clears saved game data from PlayerPrefs.
        /// </summary>
        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Debug.Log("[SaveSystem] Save data cleared.");
        }
    }
}
