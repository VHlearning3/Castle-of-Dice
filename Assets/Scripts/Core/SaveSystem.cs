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
        public const int CurrentVersion = 3;

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

        // Version 3: inventory items, quests, campaign progress and location
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public List<string> questIds = new List<string>();
        public List<int> questStates = new List<int>();
        public List<int> questProgress = new List<int>();
        public List<string> defeatedBosses = new List<string>();
        public List<int> clearedWings = new List<int>();
        /// <summary>Zone scene the save was made in (empty = unknown).</summary>
        public string sceneName = "";
        /// <summary>One-time world rewards already taken (chests, Giant's Elixir). Missing in older v3 saves.</summary>
        public List<string> claimedRewards = new List<string>();
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
                InventoryManager.Instance.CaptureItems(save.itemIds, save.itemCounts);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.CaptureState(save.questIds, save.questStates, save.questProgress);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CaptureCampaignProgress(save.defeatedBosses, save.clearedWings, save.claimedRewards);
            }

            save.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

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
                InventoryManager.Instance.RestoreItems(save.itemIds, save.itemCounts);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.RestoreState(save.questIds, save.questStates, save.questProgress);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestoreCampaignProgress(save.defeatedBosses, save.clearedWings, save.claimedRewards);
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

            // Version 1-2 saves have no v3 lists; JsonUtility leaves them null or empty
            if (save.itemIds == null) save.itemIds = new List<string>();
            if (save.itemCounts == null) save.itemCounts = new List<int>();
            if (save.questIds == null) save.questIds = new List<string>();
            if (save.questStates == null) save.questStates = new List<int>();
            if (save.questProgress == null) save.questProgress = new List<int>();
            if (save.defeatedBosses == null) save.defeatedBosses = new List<string>();
            if (save.clearedWings == null) save.clearedWings = new List<int>();
            if (save.sceneName == null) save.sceneName = "";
            if (save.claimedRewards == null) save.claimedRewards = new List<string>();

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
