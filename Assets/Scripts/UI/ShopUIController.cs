using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// User Interface Controller for Blacksmith Baldur's Shop in Oakhaven.
    /// Displays current gold balances, offers one-click scrap metal selling,
    /// and manages the store shelf with buttons to buy potions, weapon sharpening, and runic armor.
    /// </summary>
    public class ShopUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Shop Window")]
        [Tooltip("Root GameObject of the blacksmith shop interface.")]
        [SerializeField] private GameObject shopPanel;

        [Header("Currency & Resource Trackers")]
        [Tooltip("Text displaying player's current gold balance.")]
        [SerializeField] private TMP_Text goldBalanceText;

        [Tooltip("Text displaying player's scrap metal pieces.")]
        [SerializeField] private TMP_Text scrapMetalText;

        [Header("Scrap Conversion")]
        [Tooltip("Button to convert all collected scrap metal into gold (1 Scrap = 10 Gold).")]
        [SerializeField] private Button sellAllScrapButton;

        [Tooltip("Label on the scrap button showing potential gold payout.")]
        [SerializeField] private TMP_Text sellScrapButtonLabel;

        [Header("Stock Items (Pre-configured or Dynamic)")]
        [Tooltip("Health potion item data.")]
        [SerializeField] private ItemSO healthPotionItem;

        [Tooltip("Weapon sharpening item data (+1 permanent DMG).")]
        [SerializeField] private ItemSO sharpenedBladeItem;

        [Tooltip("Runic armor item data (+1 permanent AC).")]
        [SerializeField] private ItemSO runicArmorItem;

        [Header("Purchase Buttons")]
        [SerializeField] private Button buyPotionButton;
        [SerializeField] private Button buyWeaponButton;
        [SerializeField] private Button buyArmorButton;

        [Header("Navigation")]
        [Tooltip("Button to exit the shop and resume exploration.")]
        [SerializeField] private Button exitShopButton;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (shopPanel != null)
            {
                shopPanel.SetActive(false);
            }

            if (sellAllScrapButton != null)
            {
                sellAllScrapButton.onClick.AddListener(OnSellAllScrapClicked);
            }

            if (buyPotionButton != null)
            {
                buyPotionButton.onClick.AddListener(() => OnBuyItemClicked(healthPotionItem, ShopManager.HEALTH_POTION_PRICE));
            }

            if (buyWeaponButton != null)
            {
                buyWeaponButton.onClick.AddListener(() => OnBuyItemClicked(sharpenedBladeItem, ShopManager.WEAPON_UPGRADE_PRICE));
            }

            if (buyArmorButton != null)
            {
                buyArmorButton.onClick.AddListener(() => OnBuyItemClicked(runicArmorItem, ShopManager.ARMOR_UPGRADE_PRICE));
            }

            if (exitShopButton != null)
            {
                exitShopButton.onClick.AddListener(CloseShop);
            }
        }

        private void OnEnable()
        {
            InventoryManager.OnGoldChanged += HandleGoldChanged;
            InventoryManager.OnScrapMetalChanged += HandleScrapMetalChanged;
            ShopManager.OnScrapConverted += HandleScrapConverted;
            ShopManager.OnItemPurchased += HandleItemPurchased;
        }

        private void OnDisable()
        {
            InventoryManager.OnGoldChanged -= HandleGoldChanged;
            InventoryManager.OnScrapMetalChanged -= HandleScrapMetalChanged;
            ShopManager.OnScrapConverted -= HandleScrapConverted;
            ShopManager.OnItemPurchased -= HandleItemPurchased;
        }

        #endregion

        #region Shop Open / Close

        /// <summary>
        /// Opens Blacksmith Baldur's store interface.
        /// </summary>
        public void OpenShop()
        {
            if (shopPanel != null)
            {
                shopPanel.SetActive(true);
            }

            GameManager.Instance?.SetMode(GamePlayMode.Shop);
            RefreshEconomyDisplay();
        }

        /// <summary>
        /// Closes the shop interface and returns to exploration mode.
        /// </summary>
        public void CloseShop()
        {
            if (shopPanel != null)
            {
                shopPanel.SetActive(false);
            }

            GameManager.Instance?.SetMode(GamePlayMode.Exploration);
        }

        #endregion

        #region Refresh Display

        public void RefreshEconomyDisplay()
        {
            InventoryManager inventory = InventoryManager.Instance;
            int currentGold = inventory != null ? inventory.CurrentGold : 0;
            int currentScrap = inventory != null ? inventory.ScrapMetalCount : 0;

            if (goldBalanceText != null)
            {
                goldBalanceText.text = $"{currentGold} Gold";
            }

            if (scrapMetalText != null)
            {
                scrapMetalText.text = $"{currentScrap} Scrap Ore";
            }

            if (sellScrapButtonLabel != null)
            {
                int potentialGold = currentScrap * ShopManager.SCRAP_TO_GOLD_RATE;
                sellScrapButtonLabel.text = currentScrap > 0
                    ? $"Sell All Scrap (+{potentialGold}g)"
                    : "No Scrap to Sell";
            }

            if (sellAllScrapButton != null)
            {
                sellAllScrapButton.interactable = currentScrap > 0;
            }

            // Update item purchase affordability
            if (buyPotionButton != null)
            {
                buyPotionButton.interactable = currentGold >= ShopManager.HEALTH_POTION_PRICE;
            }

            if (buyWeaponButton != null)
            {
                buyWeaponButton.interactable = currentGold >= ShopManager.WEAPON_UPGRADE_PRICE;
            }

            if (buyArmorButton != null)
            {
                buyArmorButton.interactable = currentGold >= ShopManager.ARMOR_UPGRADE_PRICE;
            }
        }

        #endregion

        #region Button Actions

        private void OnSellAllScrapClicked()
        {
            ShopManager.Instance?.ConvertScrapToGold(-1);
            RefreshEconomyDisplay();
        }

        private void OnBuyItemClicked(ItemSO item, int fallbackPrice)
        {
            if (item == null) return;

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            bool success = ShopManager.Instance?.BuyItem(item, player) ?? false;

            if (success)
            {
                RefreshEconomyDisplay();
            }
        }

        #endregion

        #region Event Handlers

        private void HandleGoldChanged(int newGold) => RefreshEconomyDisplay();
        private void HandleScrapMetalChanged(int newScrap) => RefreshEconomyDisplay();
        private void HandleScrapConverted(int scrap, int gold) => RefreshEconomyDisplay();
        private void HandleItemPurchased(ItemSO item) => RefreshEconomyDisplay();

        #endregion
    }
}
