using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// Implements Blacksmith Baldur's trading post mechanics in Oakhaven (Kivenkolo).
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
        public const int HEALTH_POTION_PRICE = 25;
        public const int WEAPON_UPGRADE_PRICE = 60;
        public const int ARMOR_UPGRADE_PRICE = 100;

        #endregion

        #region Serialized Fields

        [Header("Shop Inventory Catalog")]
        [Tooltip("Standard stock offered by Blacksmith Baldur.")]
        [SerializeField] private List<ItemSO> shopCatalog = new List<ItemSO>();

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

            int available = inventory.ScrapMetalCount;
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

        #endregion

        #region Purchasing

        /// <summary>
        /// Purchases an item from the shop catalog.
        /// Deducts gold, adds item to inventory, or applies permanent bonuses directly if targeted.
        /// </summary>
        public bool BuyItem(ItemSO item, PlayerUnit targetPlayer = null)
        {
            if (item == null) return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogError("[ShopManager] InventoryManager instance not found.");
                return false;
            }

            int price = item.BuyPriceGold;
            if (!inventory.RemoveGold(price))
            {
                Debug.LogWarning($"[ShopManager] Cannot buy {item.ItemName}: Not enough gold (Requires {price} Gold).");
                return false;
            }

            // If purchasing a permanent equipment upgrade and a player unit is targeted, apply immediately
            if (targetPlayer != null && !item.IsConsumable)
            {
                if (item.ItemType == ItemType.WeaponUpgrade)
                {
                    targetPlayer.AddWeaponDamageBonus(item.StatBonusValue);
                    Debug.Log($"[ShopManager] Baldur sharpened {targetPlayer.UnitName}'s weapon (+{item.StatBonusValue} DMG)!");
                }
                else if (item.ItemType == ItemType.ArmorUpgrade)
                {
                    targetPlayer.AddArmorClassBonus(item.StatBonusValue);
                    Debug.Log($"[ShopManager] Baldur reinforced {targetPlayer.UnitName}'s armor (+{item.StatBonusValue} AC)!");
                }
                else
                {
                    inventory.AddItem(item, 1);
                }
            }
            else
            {
                inventory.AddItem(item, 1);
            }

            Debug.Log($"[ShopManager] Purchased {item.ItemName} for {price} Gold.");
            OnItemPurchased?.Invoke(item);

            return true;
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

            int payout = item.SellPriceGold;
            inventory.RemoveItem(item, 1);
            inventory.AddGold(payout);

            Debug.Log($"[ShopManager] Sold {item.ItemName} for {payout} Gold.");
            OnItemSold?.Invoke(item);

            return true;
        }

        #endregion
    }
}
