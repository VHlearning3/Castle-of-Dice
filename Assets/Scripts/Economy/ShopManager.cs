using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// Implements Blacksmith Baldur's trading post mechanics in Oakhaven.
    /// Handles scrap metal conversion (1 scrap = 10 gold), item purchasing (potions, weapons, armor),
    /// and selling loot back for gold.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        #region Singleton

        private static ShopManager instance;

        public static ShopManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ShopManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ShopManager");
                        instance = go.AddComponent<ShopManager>();
                        Debug.Log("[ShopManager] Auto-created ShopManager GameObject in scene.");
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Constants

        public const int SCRAP_TO_GOLD_RATE = 10;
        public const int HEALTH_POTION_PRICE = 15;
        public const int GREATER_POTION_PRICE = 25;
        public const int POISON_VIAL_PRICE = 30;
        public const int REROLL_RUNE_PRICE = 75;

        /// <summary>Highest level Baldur can raise the blade (+DMG) or the armor (+AC) to.</summary>
        public const int MAX_UPGRADE_LEVEL = 3;

        /// <summary>Price of the next +1, indexed by the current upgrade level (0 -> 50g, 1 -> 75g, 2 -> 100g).</summary>
        private static readonly int[] UpgradePriceByLevel = { 50, 75, 100 };

        /// <summary>First upgrade price (kept for older callers).</summary>
        public const int WEAPON_UPGRADE_PRICE = 50;

        /// <summary>First upgrade price (kept for older callers).</summary>
        public const int ARMOR_UPGRADE_PRICE = 50;

        public const string SMALL_POTION_ID = "potion_health_small";
        public const string GREATER_POTION_ID = "item_greater_potion";
        public const string POISON_VIAL_ID = "item_poison_vial";
        public const string REROLL_RUNE_ID = "item_reroll_rune";
        public const string SHARPENED_BLADE_ID = "upgrade_sharpened_blade";
        public const string RUNIC_ARMOR_ID = "upgrade_runic_armor";

        /// <summary>Buy-tab stock order, by ItemID.</summary>
        public static readonly string[] BuyStockOrder =
        {
            SMALL_POTION_ID, GREATER_POTION_ID, POISON_VIAL_ID, REROLL_RUNE_ID, SHARPENED_BLADE_ID, RUNIC_ARMOR_ID
        };

        #endregion

        #region Events

        /// <summary>Fired when scrap metal is converted into gold: (scrapUsed, goldEarned).</summary>
        public static event Action<int, int> OnScrapConverted;

        /// <summary>Fired when an item is purchased from the shop.</summary>
        public static event Action<ItemSO> OnItemPurchased;

        /// <summary>Fired when an item is sold back to the shop.</summary>
        public static event Action<ItemSO> OnItemSold;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Scrap Conversion

        /// <summary>
        /// Converts scrap metal pieces to gold coins at Blacksmith Baldur's 1 Scrap = 10 Gold rate.
        /// </summary>
        /// <param name="scrapCount">Number of scrap pieces to convert (pass -1 to convert all available scrap).</param>
        /// <returns>Total gold coins received.</returns>
        public int ConvertScrapToGold(int scrapCount = -1)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogError("[ShopManager] InventoryManager instance not found.");
                return 0;
            }

            // Scrap an accepted scrap quest still needs stays in the bag, so selling never undoes its progress
            int available = SellableScrap(inventory.ScrapMetalCount);
            int toConvert = (scrapCount < 0 || scrapCount > available) ? available : scrapCount;

            if (toConvert <= 0)
            {
                Debug.Log("[ShopManager] No scrap metal available for conversion.");
                return 0;
            }

            int goldEarned = toConvert * SCRAP_TO_GOLD_RATE;
            inventory.RemoveScrapMetal(toConvert);
            inventory.AddGold(goldEarned);

            Debug.Log($"[ShopManager] Baldur hammered {toConvert} scrap metal into {goldEarned} Gold!");
            OnScrapConverted?.Invoke(toConvert, goldEarned);

            return goldEarned;
        }

        /// <summary>
        /// Scrap Baldur buys out of <paramref name="carried"/>: everything above what accepted scrap quests
        /// still need (critical review A10).
        /// </summary>
        public static int SellableScrap(int carried)
        {
            int reserved = QuestManager.Instance != null ? QuestManager.Instance.GetScrapReservedForQuests() : 0;
            return Mathf.Max(0, carried - reserved);
        }

        #endregion

        #region Pricing & Upgrade Levels

        /// <summary>
        /// Price of the next upgrade at the given current level, or -1 when the upgrade is maxed.
        /// </summary>
        public static int GetUpgradePrice(int currentLevel)
        {
            if (currentLevel < 0) currentLevel = 0;
            if (currentLevel >= MAX_UPGRADE_LEVEL) return -1;
            return UpgradePriceByLevel[currentLevel];
        }

        /// <summary>
        /// Whether the item is a permanent blade or armor upgrade with tiered pricing.
        /// </summary>
        public static bool IsUpgrade(ItemSO item)
        {
            return item != null && (item.ItemType == ItemType.WeaponUpgrade || item.ItemType == ItemType.ArmorUpgrade);
        }

        /// <summary>
        /// Current upgrade level (0..3) for a weapon or armor upgrade item. Reads the hero when one is
        /// given, otherwise the session progression data.
        /// </summary>
        public static int GetUpgradeLevel(ItemSO item, PlayerUnit player)
        {
            if (!IsUpgrade(item)) return 0;

            int bonus;
            if (player != null)
            {
                bonus = item.ItemType == ItemType.WeaponUpgrade ? player.WeaponDamageBonus : player.ArmorClassBonus;
            }
            else
            {
                PlayerDataSO data = PlayerDataSO.Session;
                if (data == null) return 0;
                bonus = item.ItemType == ItemType.WeaponUpgrade ? data.WeaponDamageBonus : data.ArmorClassBonus;
            }

            return Mathf.Clamp(bonus, 0, MAX_UPGRADE_LEVEL);
        }

        /// <summary>
        /// Gold needed to buy the item right now: tiered for upgrades (-1 when maxed), the item's own price otherwise.
        /// </summary>
        public static int GetBuyPrice(ItemSO item, PlayerUnit player)
        {
            if (item == null) return -1;
            if (IsUpgrade(item)) return GetUpgradePrice(GetUpgradeLevel(item, player));
            return item.BuyPriceGold;
        }

        /// <summary>
        /// Whether Baldur takes this item in the Sell tab. Quest items, scrap (sold in its own row)
        /// and worthless items are refused.
        /// </summary>
        public static bool IsSellable(ItemSO item)
        {
            if (item == null || item.SellPriceGold <= 0) return false;
            return item.ItemType != ItemType.QuestItem && item.ItemType != ItemType.ScrapMetal;
        }

        #endregion

        #region Purchasing

        /// <summary>
        /// Purchases an item from the shop. Deducts gold and either adds the item to the inventory or,
        /// for blade/armor upgrades, raises the permanent bonus (capped at MAX_UPGRADE_LEVEL).
        /// </summary>
        public bool BuyItem(ItemSO item, PlayerUnit targetPlayer = null)
        {
            return TryBuyItem(item, targetPlayer) == ShopPurchaseResult.Success;
        }

        /// <summary>
        /// Same as BuyItem, but reports why a purchase was refused so the UI can tell the player.
        /// </summary>
        public ShopPurchaseResult TryBuyItem(ItemSO item, PlayerUnit targetPlayer = null)
        {
            if (item == null) return ShopPurchaseResult.InvalidItem;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogError("[ShopManager] InventoryManager instance not found.");
                return ShopPurchaseResult.InvalidItem;
            }

            // Auto-locate PlayerUnit if targetPlayer not explicitly provided
            if (targetPlayer == null)
            {
                targetPlayer = FindAnyObjectByType<PlayerUnit>();
            }

            int price = GetBuyPrice(item, targetPlayer);
            if (price < 0)
            {
                Debug.Log($"[ShopManager] {item.ItemName} is already at the maximum level.");
                return ShopPurchaseResult.MaxLevel;
            }

            if (!inventory.RemoveGold(price))
            {
                Debug.Log($"[ShopManager] Cannot buy {item.ItemName}: Not enough gold (Requires {price} Gold).");
                return ShopPurchaseResult.NotEnoughGold;
            }

            if (IsUpgrade(item))
            {
                ApplyUpgrade(item, targetPlayer);
            }
            else
            {
                inventory.AddItem(item, 1);
            }

            Debug.Log($"[ShopManager] Purchased {item.ItemName} for {price} Gold.");
            OnItemPurchased?.Invoke(item);
            return ShopPurchaseResult.Success;
        }

        private static void ApplyUpgrade(ItemSO item, PlayerUnit targetPlayer)
        {
            bool isWeapon = item.ItemType == ItemType.WeaponUpgrade;

            if (targetPlayer != null)
            {
                if (isWeapon)
                {
                    targetPlayer.AddWeaponDamageBonus(1);
                    Debug.Log($"[ShopManager] Baldur sharpened {targetPlayer.UnitName}'s weapon (+1 DMG)!");
                }
                else
                {
                    targetPlayer.AddArmorClassBonus(1);
                    Debug.Log($"[ShopManager] Baldur reinforced {targetPlayer.UnitName}'s armor (+1 AC)!");
                }
                return;
            }

            // No hero in the scene: store the upgrade in the session data so the next hero picks it up.
            PlayerDataSO data = PlayerDataSO.Session;
            if (data == null) return;
            if (isWeapon) data.WeaponDamageBonus += 1;
            else data.ArmorClassBonus += 1;
        }

        #endregion

        #region Selling

        /// <summary>
        /// Sells an item from the player's inventory back to the shop in exchange for its sell value.
        /// </summary>
        public bool SellItem(ItemSO item)
        {
            if (item == null) return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null || !inventory.HasItem(item, 1))
            {
                Debug.LogWarning($"[ShopManager] Cannot sell {item.ItemName}: Item not found in inventory.");
                return false;
            }

            if (!IsSellable(item))
            {
                Debug.Log($"[ShopManager] Baldur won't take {item.ItemName}.");
                return false;
            }

            int payout = item.SellPriceGold;
            inventory.RemoveItem(item, 1);
            inventory.AddGold(payout);

            Debug.Log($"[ShopManager] Sold {item.ItemName} for {payout} Gold.");
            OnItemSold?.Invoke(item);

            return true;
        }

        #endregion
    }

    /// <summary>Outcome of a shop purchase attempt.</summary>
    public enum ShopPurchaseResult
    {
        Success,
        NotEnoughGold,
        MaxLevel,
        InvalidItem
    }
}
