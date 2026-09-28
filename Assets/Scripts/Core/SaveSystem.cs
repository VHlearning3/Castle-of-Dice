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
        public int currentLevel = 1;
        public int maxHPBonus = 0;
        public int attributeBonusModifier = 0;
        public int permanentWeaponDamageBonus = 0;
        public int permanentArmorClassBonus = 0;
        public List<int> upgradedAbilityIndices = new List<int>();
        public int gold = 0;
        public int scrapMetal = 0;
    }

    /// <summary>
    /// Persistent Save System utilizing PlayerPrefs JSON serialization.
    /// Zero file-system IO ensures complete reliability across WebGL, Windows Standalone, and Editor.
    /// </summary>
    public static class SaveSystem
    {
        private const string SaveKey = "CastleOfDice_SaveData";

        /// <summary>
        /// Saves current hero state, inventory, and progression.
        /// </summary>
        public static void SaveGame(PlayerDataSO dataSO = null, PlayerUnit player = null)
        {
            PlayerSaveData save = new PlayerSaveData();

            if (player != null)
            {
                save.currentLevel = player.Level;
                save.permanentWeaponDamageBonus = player.WeaponDamageBonus;
                save.permanentArmorClassBonus = player.ArmorClassBonus;
            }
            else if (dataSO != null)
            {
                save.currentLevel = dataSO.CurrentLevel;
                save.maxHPBonus = dataSO.MaxHPBonus;
                save.attributeBonusModifier = dataSO.AttributeBonusModifier;
                save.permanentWeaponDamageBonus = dataSO.WeaponDamageBonus;
                save.permanentArmorClassBonus = dataSO.ArmorClassBonus;
                save.upgradedAbilityIndices = new List<int>(dataSO.UpgradedAbilityIndices);
            }

            if (dataSO != null)
            {
                save.maxHPBonus = dataSO.MaxHPBonus;
                save.attributeBonusModifier = dataSO.AttributeBonusModifier;
                save.upgradedAbilityIndices = new List<int>(dataSO.UpgradedAbilityIndices);
            }

            if (InventoryManager.Instance != null)
            {
                save.gold = InventoryManager.Instance.CurrentGold;
                save.scrapMetal = InventoryManager.Instance.ScrapMetalCount;
            }

            string json = JsonUtility.ToJson(save, true);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
            Debug.Log($"[SaveSystem] Game saved successfully! Level: {save.currentLevel}, Gold: {save.gold}, Scrap: {save.scrapMetal}");
        }

        /// <summary>
        /// Loads saved progression and populates PlayerDataSO or PlayerUnit.
        /// </summary>
        public static PlayerSaveData LoadGame(PlayerDataSO targetSO = null, PlayerUnit targetPlayer = null)
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                Debug.Log("[SaveSystem] No existing save data found.");
                return null;
            }

            string json = PlayerPrefs.GetString(SaveKey);
            if (string.IsNullOrEmpty(json)) return null;

            PlayerSaveData save = JsonUtility.FromJson<PlayerSaveData>(json);
            if (save == null) return null;

            if (targetSO != null)
            {
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
            }

            if (targetPlayer != null)
            {
                targetPlayer.Level = save.currentLevel;
                if (save.maxHPBonus > 0) targetPlayer.ApplyHeroResilience(save.maxHPBonus);
                if (save.attributeBonusModifier > 0) targetPlayer.AddAttributeBonus(save.attributeBonusModifier);
                if (save.permanentWeaponDamageBonus > 0) targetPlayer.AddWeaponDamageBonus(save.permanentWeaponDamageBonus);
                if (save.permanentArmorClassBonus > 0) targetPlayer.AddArmorClassBonus(save.permanentArmorClassBonus);

                foreach (int slot in save.upgradedAbilityIndices)
                {
                    targetPlayer.UpgradeAbilityToRank2(slot);
                }
            }

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddGold(save.gold - InventoryManager.Instance.CurrentGold);
                InventoryManager.Instance.AddScrapMetal(save.scrapMetal - InventoryManager.Instance.ScrapMetalCount);
            }

            Debug.Log($"[SaveSystem] Game loaded successfully! Level: {save.currentLevel}");
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
