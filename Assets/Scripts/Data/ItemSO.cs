using System;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// ScriptableObject defining an inventory item, equipment upgrade, potion, or quest artifact.
    /// Manages pricing for Blacksmith Baldur's shop economy, consumption logic, and combat stat modifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "CastleOfDice/Data/Item", order = 12)]
    public class ItemSO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Item Identity")]
        [Tooltip("Unique programmatic identifier for the item (e.g., 'potion_health_small', 'upgrade_sharpened_blade').")]
        [SerializeField] private string itemID = "item_id";

        [Tooltip("Display name shown in shops, inventory, and combat logs.")]
        [SerializeField] private string itemName = "New Item";

        [Tooltip("Flavor and mechanical description explaining usage, lore, and effects.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = "Item description and effect details.";

        [Tooltip("Inventory and shop slot icon sprite.")]
        [SerializeField] private Sprite itemIcon;

        [Header("Classification")]
        [Tooltip("Category of the item: Consumable, WeaponUpgrade, ArmorUpgrade, QuestItem, or ScrapMetal.")]
        [SerializeField] private ItemType itemType = ItemType.Consumable;

        [Header("Economy")]
        [Tooltip("Cost in gold to purchase this item from a vendor (e.g., 25 for Small Potion, 60 for Weapon, 100 for Armor).")]
        [Min(0)]
        [SerializeField] private int buyPriceGold = 25;

        [Tooltip("Gold earned when selling this item to Blacksmith Baldur (e.g., 10 for Scrap Metal).")]
        [Min(0)]
        [SerializeField] private int sellPriceGold = 10;

        [Header("Combat & Modifiers")]
        [Tooltip("Numeric magnitude of the item's primary effect (+1 permanent DMG, +1 permanent AC, or HP restored on consumption).")]
        [SerializeField] private int statBonusValue = 1;

        [Tooltip("If true, using this item consumes it from the player's inventory.")]
        [SerializeField] private bool isConsumable = true;

        #endregion

        #region Public Properties

        /// <summary>Unique item identifier string.</summary>
        public string ItemID => itemID;

        /// <summary>User-facing item name.</summary>
        public string ItemName => itemName;

        /// <summary>Lore and mechanical description.</summary>
        public string Description => description;

        /// <summary>Visual icon sprite.</summary>
        public Sprite ItemIcon => itemIcon;

        /// <summary>Item categorization type.</summary>
        public ItemType ItemType => itemType;

        /// <summary>Purchase price in gold from vendors.</summary>
        public int BuyPriceGold => buyPriceGold;

        /// <summary>Sale value in gold when sold to vendors.</summary>
        public int SellPriceGold => sellPriceGold;

        /// <summary>Stat enhancement magnitude (+1 damage, +1 AC, heal amount).</summary>
        public int StatBonusValue => statBonusValue;

        /// <summary>Whether this item is expended upon use.</summary>
        public bool IsConsumable => isConsumable;

        #endregion

        #region Validation

        private void OnValidate()
        {
            if (buyPriceGold < 0) buyPriceGold = 0;
            if (sellPriceGold < 0) sellPriceGold = 0;

            // Automatically set consumable flag based on ItemType if standard defaults apply
            if (itemType == ItemType.Consumable)
            {
                isConsumable = true;
            }
            else if (itemType == ItemType.WeaponUpgrade || itemType == ItemType.ArmorUpgrade || itemType == ItemType.ScrapMetal)
            {
                isConsumable = false;
            }
        }

        #endregion
    }
}
