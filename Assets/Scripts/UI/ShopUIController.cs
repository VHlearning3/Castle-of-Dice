using System;
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
    /// Handles safe button listeners, auto-discovery of unassigned UI components, and clean state transitions.
    /// </summary>
    public class ShopUIController : MonoBehaviour
    {
        #region Singleton

        private static ShopUIController instance;

        public static ShopUIController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var canvas in canvases)
                        {
                            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.name.IndexOf("ShopPanel", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    instance = child.gameObject.AddComponent<ShopUIController>();
                                    break;
                                }
                            }
                            if (instance != null) break;
                        }

                        if (instance == null)
                        {
                            GameObject go = new GameObject("ShopUIController");
                            instance = go.AddComponent<ShopUIController>();
                        }
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

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
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            AutoLocateComponents();
            EnsureStockItems();

            if (shopPanel != null)
            {
                shopPanel.SetActive(false);
            }

            SubscribeButtonListeners();
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

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            UnsubscribeButtonListeners();
        }

        #endregion

        #region UI Component Auto-Discovery

        /// <summary>
        /// Automatically locates unassigned UI components in children to prevent null reference errors.
        /// </summary>
        public void AutoLocateComponents()
        {
            // 1. Locate shopPanel
            if (shopPanel == null)
            {
                Transform panelTransform = transform.Find("ShopPanel")
                    ?? transform.Find("Panel")
                    ?? transform.Find("ShopWindow")
                    ?? transform.Find("Window");

                if (panelTransform != null)
                {
                    shopPanel = panelTransform.gameObject;
                }
                else
                {
                    // Search across all canvases in the scene (including inactive)
                    Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var canvas in canvases)
                    {
                        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                        {
                            string lower = child.name.ToLowerInvariant();
                            if (lower == "shoppanel" || lower == "shop_panel" || lower == "shopwindow" || lower == "blacksmithshop" || lower == "shop")
                            {
                                shopPanel = child.gameObject;
                                break;
                            }
                        }
                        if (shopPanel != null) break;
                    }
                }

                if (shopPanel == null)
                {
                    GameObject found = GameObject.Find("ShopPanel");
                    if (found != null) shopPanel = found;
                }
            }

            // Target search root: prefer shopPanel, otherwise search canvas or this transform
            Transform searchRoot = shopPanel != null ? shopPanel.transform : transform;
            if (searchRoot == transform && transform.childCount == 0)
            {
                Canvas mainCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
                if (mainCanvas != null) searchRoot = mainCanvas.transform;
            }

            // 2. Locate TMP_Text components
            TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in texts)
            {
                string lower = txt.name.ToLowerInvariant();
                if (goldBalanceText == null && (lower.Contains("gold") || lower.Contains("balance") || lower.Contains("coin") || lower.Contains("money")))
                {
                    goldBalanceText = txt;
                }
                else if (scrapMetalText == null && (lower.Contains("scrap") || lower.Contains("ore") || lower.Contains("metal")))
                {
                    scrapMetalText = txt;
                }
            }

            // 3. Locate Buttons
            Button[] buttons = searchRoot.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string lower = btn.name.ToLowerInvariant();

                if (sellAllScrapButton == null && (lower.Contains("scrap") || lower.Contains("sell") || lower.Contains("convert")))
                {
                    sellAllScrapButton = btn;
                    if (sellScrapButtonLabel == null)
                    {
                        sellScrapButtonLabel = btn.GetComponentInChildren<TMP_Text>(true);
                    }
                }
                else if (buyPotionButton == null && (lower.Contains("potion") || lower.Contains("heal")))
                {
                    buyPotionButton = btn;
                }
                else if (buyWeaponButton == null && (lower.Contains("weapon") || lower.Contains("blade") || lower.Contains("sword") || lower.Contains("sharp")))
                {
                    buyWeaponButton = btn;
                }
                else if (buyArmorButton == null && (lower.Contains("armor") || lower.Contains("shield") || lower.Contains("runic") || lower.Contains("defense")))
                {
                    buyArmorButton = btn;
                }
                else if (exitShopButton == null && (lower.Contains("exit") || lower.Contains("close") || lower.Contains("back") || lower.Contains("leave")))
                {
                    exitShopButton = btn;
                }
            }

            if (sellAllScrapButton != null && sellScrapButtonLabel == null)
            {
                sellScrapButtonLabel = sellAllScrapButton.GetComponentInChildren<TMP_Text>(true);
            }

            // Sanitize all shop text components and button labels
            TMP_Text[] allShopTexts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in allShopTexts)
            {
                if (t == null) continue;
                t.margin = Vector4.zero;
                t.enableAutoSizing = true;
                t.fontSizeMin = 12f;
                t.fontSizeMax = Mathf.Max(20f, t.fontSize);
                t.textWrappingMode = TextWrappingModes.Normal;
                t.raycastTarget = false;
            }
        }

        /// <summary>
        /// Generates or resolves fallback ItemSO definitions if stock fields are left empty in the Inspector.
        /// </summary>
        private void EnsureStockItems()
        {
            if (healthPotionItem == null)
            {
                healthPotionItem = ScriptableObject.CreateInstance<ItemSO>();
                healthPotionItem.Initialize(
                    id: "potion_health_small",
                    name: "Small Health Potion",
                    desc: "Restores 15 HP.",
                    type: ItemType.Consumable,
                    buyPrice: ShopManager.HEALTH_POTION_PRICE,
                    sellPrice: 10,
                    statBonus: 15,
                    consumable: true
                );
            }

            if (sharpenedBladeItem == null)
            {
                sharpenedBladeItem = ScriptableObject.CreateInstance<ItemSO>();
                sharpenedBladeItem.Initialize(
                    id: "upgrade_sharpened_blade",
                    name: "Sharpened Blade",
                    desc: "Permanently adds +1 to all attack damage.",
                    type: ItemType.WeaponUpgrade,
                    buyPrice: ShopManager.WEAPON_UPGRADE_PRICE,
                    sellPrice: 20,
                    statBonus: 1,
                    consumable: false
                );
            }

            if (runicArmorItem == null)
            {
                runicArmorItem = ScriptableObject.CreateInstance<ItemSO>();
                runicArmorItem.Initialize(
                    id: "upgrade_runic_armor",
                    name: "Runic Armor",
                    desc: "Permanently increases Armor Class by +1.",
                    type: ItemType.ArmorUpgrade,
                    buyPrice: ShopManager.ARMOR_UPGRADE_PRICE,
                    sellPrice: 35,
                    statBonus: 1,
                    consumable: false
                );
            }
        }

        #endregion

        #region Button Subscriptions

        private void SubscribeButtonListeners()
        {
            if (sellAllScrapButton != null)
            {
                sellAllScrapButton.onClick.RemoveListener(SellAllScrap);
                sellAllScrapButton.onClick.AddListener(SellAllScrap);
            }

            if (buyPotionButton != null)
            {
                buyPotionButton.onClick.RemoveListener(BuyPotion);
                buyPotionButton.onClick.AddListener(BuyPotion);
            }

            if (buyWeaponButton != null)
            {
                buyWeaponButton.onClick.RemoveListener(BuyWeapon);
                buyWeaponButton.onClick.AddListener(BuyWeapon);
            }

            if (buyArmorButton != null)
            {
                buyArmorButton.onClick.RemoveListener(BuyArmor);
                buyArmorButton.onClick.AddListener(BuyArmor);
            }

            if (exitShopButton != null)
            {
                exitShopButton.onClick.RemoveListener(CloseShop);
                exitShopButton.onClick.AddListener(CloseShop);
            }
        }

        private void UnsubscribeButtonListeners()
        {
            if (sellAllScrapButton != null)
            {
                sellAllScrapButton.onClick.RemoveListener(SellAllScrap);
            }

            if (buyPotionButton != null)
            {
                buyPotionButton.onClick.RemoveListener(BuyPotion);
            }

            if (buyWeaponButton != null)
            {
                buyWeaponButton.onClick.RemoveListener(BuyWeapon);
            }

            if (buyArmorButton != null)
            {
                buyArmorButton.onClick.RemoveListener(BuyArmor);
            }

            if (exitShopButton != null)
            {
                exitShopButton.onClick.RemoveListener(CloseShop);
            }
        }

        #endregion

        #region Shop Open / Close & State Transitions

        /// <summary>
        /// Opens Blacksmith Baldur's store interface, pauses exploration mode, and refreshes stock.
        /// </summary>
        public void OpenShop()
        {
            AutoLocateComponents();
            SubscribeButtonListeners();

            if (shopPanel != null)
            {
                shopPanel.SetActive(true);
                shopPanel.transform.SetAsLastSibling();
            }
            else
            {
                Debug.LogError("[ShopUIController] Cannot open shop: shopPanel could not be located in scene!");
            }

            GameManager.Instance?.SetMode(GamePlayMode.Shop);
            RefreshEconomyDisplay();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Debug.Log("[ShopUIController] Blacksmith Baldur's shop opened.");
        }

        /// <summary>
        /// Closes the shop interface and returns to free exploration mode.
        /// </summary>
        public void CloseShop()
        {
            if (shopPanel != null)
            {
                shopPanel.SetActive(false);
            }

            GameManager.Instance?.SetMode(GamePlayMode.Exploration);
            Debug.Log("[ShopUIController] Blacksmith Baldur's shop closed. Exploration resumed.");
        }

        #endregion

        #region Economy Actions

        /// <summary>
        /// Converts all available scrap metal pieces to gold coins at 1 Scrap = 10 Gold.
        /// </summary>
        public void SellAllScrap()
        {
            ShopManager sm = ShopManager.Instance;
            if (sm != null)
            {
                sm.ConvertScrapToGold(-1);
            }

            RefreshEconomyDisplay();
            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        /// <summary>
        /// Purchases a Health Potion if the player has sufficient gold.
        /// </summary>
        public void BuyPotion()
        {
            PerformPurchase(healthPotionItem, ShopManager.HEALTH_POTION_PRICE);
        }

        /// <summary>
        /// Purchases weapon sharpening (+1 permanent DMG) if the player has sufficient gold.
        /// </summary>
        public void BuyWeapon()
        {
            PerformPurchase(sharpenedBladeItem, ShopManager.WEAPON_UPGRADE_PRICE);
        }

        /// <summary>
        /// Purchases runic armor reinforcements (+1 permanent AC) if the player has sufficient gold.
        /// </summary>
        public void BuyArmor()
        {
            PerformPurchase(runicArmorItem, ShopManager.ARMOR_UPGRADE_PRICE);
        }

        private void PerformPurchase(ItemSO item, int fallbackPrice)
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

        #region Refresh Display

        /// <summary>
        /// Synchronizes gold count, scrap metal amount, potential payout label, and button interactable states.
        /// </summary>
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

            // Update item purchase button affordability
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

        #region Event Handlers

        private void HandleGoldChanged(int newGold) => RefreshEconomyDisplay();
        private void HandleScrapMetalChanged(int newScrap) => RefreshEconomyDisplay();
        private void HandleScrapConverted(int scrap, int gold) => RefreshEconomyDisplay();
        private void HandleItemPurchased(ItemSO item) => RefreshEconomyDisplay();

        #endregion
    }
}
