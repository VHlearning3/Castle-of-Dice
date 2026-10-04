using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// User Interface Controller for Blacksmith Baldur's Shop in Oakhaven Village.
    /// Displays current gold and scrap metal resources in ornate tabletop D&D pill badges and splits
    /// the stock into Buy and Sell tabs: potions, poison, the reroll rune and levelled blade/armor
    /// upgrades to buy; scrap and loot to sell. Every purchase or sale shows a message and plays a sound.
    /// Strictly preserves authentic CoinIcon.png and HealthPotionIcon.png assets.
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

        [Tooltip("Icon displaying player's gold purse.")]
        [SerializeField] private Image coinIconImage;

        [Tooltip("Icon displaying player's scrap ore pieces.")]
        [SerializeField] private Image scrapIconImage;

        [Header("Item Image Slots (Framed Small Icons)")]
        [Tooltip("Small framed image slot for Health Potion.")]
        [SerializeField] private Image potionIconImage;

        [Tooltip("Small framed image slot for Weapon Sharpening (+1 DMG).")]
        [SerializeField] private Image weaponIconImage;

        [Tooltip("Small framed image slot for Shield / Armor Reinforcement (+1 AC).")]
        [SerializeField] private Image armorIconImage;

        [Tooltip("Small framed image slot for Scrap Ore Conversion.")]
        [SerializeField] private Image scrapActionIconImage;

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

        [Tooltip("Optional top-right 'X' button to close the shop.")]
        [SerializeField] private Button closeCornerButton;

        [Header("Fantasy Theme Sprites")]
        [SerializeField] private Sprite panelDarkSprite;
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite dividerGoldSprite;
        [SerializeField] private Sprite pillBadgeSprite;
        [SerializeField] private Sprite buttonNormalSprite;
        [SerializeField] private Sprite buttonHoverSprite;
        [SerializeField] private Sprite buttonPressedSprite;

        [Header("Item & Currency Sprites")]
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite potionSprite;
        [SerializeField] private Sprite swordIconSprite;
        [SerializeField] private Sprite shieldIconSprite;
        [SerializeField] private Sprite scrapOreSprite;
        [SerializeField] private Sprite runeIconSprite;

        [Header("Buy / Sell Tabs")]
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button sellTabButton;

        [Tooltip("Line under the shelf: Baldur's greeting, or what just happened (bought, sold, not enough gold).")]
        [SerializeField] private TMP_Text feedbackText;

        #endregion

        #region Layout & Text Constants

        private const float ShelfTopOffset = 182f;
        private const float ShelfBottomOffset = 104f;
        private const float RowSpacing = 6f;
        private const float MaxRowHeight = 64f;
        private const float MinRowHeight = 48f;
        private const int MaxSellRows = 8;
        private const float FeedbackSeconds = 2.5f;

        private const string ScrapRowKey = "scrap";
        private const string IdleLine = "<i>\"Steel for coin, coin for steel. What'll it be?\"</i>  - Baldur";

        private static readonly Color RowTint = new Color(0.06f, 0.08f, 0.12f, 0.85f);
        private static readonly Color RowTintMaxed = new Color(0.10f, 0.09f, 0.05f, 0.85f);
        private static readonly Color PriceGold = new Color(1f, 0.85f, 0.40f, 1f);
        private static readonly Color PriceTooHigh = new Color(0.95f, 0.45f, 0.40f, 1f);
        private static readonly Color MutedText = new Color(0.65f, 0.70f, 0.78f, 1f);
        private static readonly Color PipFilled = new Color(1f, 0.82f, 0.32f, 1f);
        private static readonly Color PipEmpty = new Color(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color PoisonTint = new Color(0.55f, 1f, 0.45f, 1f);
        private static readonly Color FeedbackGood = new Color(0.60f, 0.92f, 0.55f, 1f);
        private static readonly Color FeedbackBad = new Color(0.95f, 0.50f, 0.45f, 1f);

        #endregion

        #region Stock Rows

        /// <summary>One card on the shelf: an item to buy, an item to sell, or the scrap salvage row.</summary>
        private sealed class ShopRow
        {
            public string Key;
            public ItemSO Item;
            public bool IsSellRow;
            public GameObject Root;
            public Image Background;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Desc;
            public Button Button;
            public TMP_Text ButtonLabel;
            public Image[] Pips;
        }

        private readonly List<ShopRow> buyRows = new List<ShopRow>(6);
        private readonly List<ShopRow> sellRows = new List<ShopRow>(MaxSellRows);
        private readonly List<ItemSO> sellableBuffer = new List<ItemSO>(MaxSellRows);
        private ShopRow scrapRow;
        private TMP_Text sellEmptyHint;
        private Transform shelfTransform;
        private bool showingSellTab;
        private float feedbackResetTime;

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

            LoadThemeSpritesIfMissing();
            AutoLocateComponents();
            EnsureStockItems();
            EnsureStyledHierarchy();

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
            InventoryManager.OnInventoryChanged += HandleInventoryChanged;
            InventoryManager.OnRerollScrollsChanged += HandleRerollScrollsChanged;
            ShopManager.OnScrapConverted += HandleScrapConverted;
            ShopManager.OnItemPurchased += HandleItemPurchased;
            ShopManager.OnItemSold += HandleItemSold;
        }

        private void OnDisable()
        {
            InventoryManager.OnGoldChanged -= HandleGoldChanged;
            InventoryManager.OnScrapMetalChanged -= HandleScrapMetalChanged;
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
            InventoryManager.OnRerollScrollsChanged -= HandleRerollScrollsChanged;
            ShopManager.OnScrapConverted -= HandleScrapConverted;
            ShopManager.OnItemPurchased -= HandleItemPurchased;
            ShopManager.OnItemSold -= HandleItemSold;
        }

        private void Update()
        {
            if (shopPanel == null || !shopPanel.activeInHierarchy) return;

            if (GameInput.GetKeyDown(KeyCode.Escape))
            {
                CloseShop();
                return;
            }

            if (GameInput.GetKeyDown(KeyCode.Tab))
            {
                if (showingSellTab) ShowBuyTab();
                else ShowSellTab();
            }

            if (feedbackResetTime > 0f && Time.unscaledTime >= feedbackResetTime)
            {
                feedbackResetTime = 0f;
                ShowIdleLine();
            }
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

        #region Theme Sprites & Auto-Discovery

        public void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (pillBadgeSprite == null)
                pillBadgeSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            if (buttonHoverSprite == null)
                buttonHoverSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            if (buttonPressedSprite == null)
                buttonPressedSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");

            if (coinSprite == null)
                coinSprite = UITheme.GetSprite("Assets/ICONSART/CoinIcon.png");
            if (potionSprite == null)
                potionSprite = UITheme.GetSprite("Assets/ICONSART/HealthPotionIcon.png");
            if (swordIconSprite == null)
                swordIconSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_Sword.png");
            if (shieldIconSprite == null)
                shieldIconSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_Shield.png");
            if (scrapOreSprite == null)
                scrapOreSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_ScrapOre.png");
            if (runeIconSprite == null)
                runeIconSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Mage.png");

#if UNITY_EDITOR
            if (healthPotionItem == null)
                healthPotionItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_Potion_Health.asset");
#endif
        }

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

            Transform searchRoot = shopPanel != null ? shopPanel.transform : transform;

            // 2. Locate TMP_Text components
            TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in texts)
            {
                string lower = txt.name.ToLowerInvariant();
                string parentLower = txt.transform.parent != null ? txt.transform.parent.name.ToLowerInvariant() : "";

                if (goldBalanceText == null && (lower.Contains("gold") || lower.Contains("balance") || lower.Contains("coin") || lower.Contains("money") || parentLower.Contains("gold")))
                {
                    goldBalanceText = txt;
                }
                else if (scrapMetalText == null && (lower.Contains("scrap") || lower.Contains("ore") || lower.Contains("metal") || parentLower.Contains("scrap")))
                {
                    scrapMetalText = txt;
                }
            }

            // 3. Locate Buttons
            Button[] buttons = searchRoot.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string lower = btn.name.ToLowerInvariant();
                string parentLower = btn.transform.parent != null ? btn.transform.parent.name.ToLowerInvariant() : "";

                // Tabs and the generated stock rows are wired by the controller itself
                if (lower.StartsWith("tab_") || lower.StartsWith("row_buy_") || lower.StartsWith("row_sell_")) continue;

                if (sellAllScrapButton == null && (lower.Contains("scrap") || lower.Contains("sell") || lower.Contains("convert") || parentLower.Contains("scrap")))
                {
                    sellAllScrapButton = btn;
                    if (sellScrapButtonLabel == null)
                    {
                        sellScrapButtonLabel = btn.GetComponentInChildren<TMP_Text>(true);
                    }
                }
                else if (buyPotionButton == null && (lower.Contains("potion") || lower.Contains("heal") || parentLower.Contains("potion")))
                {
                    buyPotionButton = btn;
                }
                else if (buyWeaponButton == null && (lower.Contains("weapon") || lower.Contains("blade") || lower.Contains("sword") || lower.Contains("sharp") || parentLower.Contains("sword") || parentLower.Contains("weapon")))
                {
                    buyWeaponButton = btn;
                }
                else if (buyArmorButton == null && (lower.Contains("armor") || lower.Contains("shield") || lower.Contains("runic") || lower.Contains("defense") || parentLower.Contains("shield") || parentLower.Contains("armor")))
                {
                    buyArmorButton = btn;
                }
                else if (closeCornerButton == null && (lower.Contains("close") || lower == "x" || lower.Contains("corner")))
                {
                    closeCornerButton = btn;
                }
                else if (exitShopButton == null && (lower.Contains("exit") || lower.Contains("leave") || lower.Contains("back")))
                {
                    exitShopButton = btn;
                }
            }

            if (sellAllScrapButton != null && sellScrapButtonLabel == null)
            {
                sellScrapButtonLabel = sellAllScrapButton.GetComponentInChildren<TMP_Text>(true);
            }

            // 4. Locate Images / Icons
            Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                string lower = img.name.ToLowerInvariant();
                if (coinIconImage == null && (lower.Contains("coin") || lower == "gold_icon"))
                {
                    coinIconImage = img;
                }
                else if (scrapIconImage == null && (lower.Contains("scrap_icon") || lower == "ore_icon"))
                {
                    scrapIconImage = img;
                }
                else if (potionIconImage == null && (lower.Contains("potion_icon") || lower == "potionimage"))
                {
                    potionIconImage = img;
                }
                else if (weaponIconImage == null && (lower.Contains("sword_icon") || lower.Contains("weapon_icon")))
                {
                    weaponIconImage = img;
                }
                else if (armorIconImage == null && (lower.Contains("shield_icon") || lower.Contains("armor_icon")))
                {
                    armorIconImage = img;
                }
                else if (scrapActionIconImage == null && (lower.Contains("scrap_action_icon") || lower.Contains("salvage_icon")))
                {
                    scrapActionIconImage = img;
                }
            }
        }

        /// <summary>
        /// Structures and styles the Blacksmith Baldur shop interface to match the authentic D&D HUD:
        /// 1. Dark slate 9-sliced panel with gold trim and rivets.
        /// 2. Header with engraved gold title, blacksmith subtitle, and filigree divider.
        /// 3. Resource trackers (Gold & Scrap) in pill badges with exact icons.
        /// 4. 4 distinct stock cards with framed 48x48 item image slots, item titles, descriptions, and styled Buy buttons.
        /// 5. Prominent Leave Shop footer button and top-right close 'X'.
        /// </summary>
        public void EnsureStyledHierarchy()
        {
            LoadThemeSpritesIfMissing();

            if (shopPanel == null)
            {
                shopPanel = gameObject;
            }

            // A. Style Root Shop Panel
            RectTransform panelRect = shopPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(780f, 740f);
            }

            Image panelImg = shopPanel.GetComponent<Image>();
            if (panelImg != null && panelDarkSprite != null)
            {
                panelImg.sprite = panelDarkSprite;
                panelImg.type = Image.Type.Sliced;
                panelImg.color = Color.white;
            }

            // B. Header Title
            Transform titleTr = shopPanel.transform.Find("HeaderText") ?? shopPanel.transform.Find("Shop_Title_Text");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : null;
            if (titleObj == null)
            {
                titleObj = new GameObject("Shop_Title_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(shopPanel.transform, false);
            }
            titleObj.name = "Shop_Title_Text";
            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 1f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -18f);
            tRect.sizeDelta = new Vector2(0f, 36f);

            TMP_Text tText = titleObj.GetComponent<TMP_Text>();
            if (tText != null)
            {
                tText.text = "BALDUR'S FORGE & WARES";
                tText.fontSize = 22f;
                tText.fontStyle = FontStyles.Bold;
                tText.color = UITheme.GoldAccent;
                tText.alignment = TextAlignmentOptions.Center;
                tText.enableAutoSizing = false;
                tText.raycastTarget = false;
            }

            // C. Header Subtitle
            Transform subTr = shopPanel.transform.Find("Shop_Subtitle_Text");
            GameObject subObj = subTr != null ? subTr.gameObject : null;
            if (subObj == null)
            {
                subObj = new GameObject("Shop_Subtitle_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                subObj.transform.SetParent(shopPanel.transform, false);
            }
            RectTransform sRect = subObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f);
            sRect.anchoredPosition = new Vector2(0f, -50f);
            sRect.sizeDelta = new Vector2(0f, 20f);

            TMP_Text subText = subObj.GetComponent<TMP_Text>();
            if (subText != null)
            {
                subText.text = "Blacksmith of Oakhaven Village - Arms, Armor & Field Supplies";
                subText.fontSize = 12f;
                subText.color = new Color(0.62f, 0.67f, 0.74f, 1f);
                subText.alignment = TextAlignmentOptions.Center;
                subText.enableAutoSizing = false;
                subText.raycastTarget = false;
            }

            // D. Top Header Divider
            Transform topDivTr = shopPanel.transform.Find("Top_Divider");
            GameObject topDivObj = topDivTr != null ? topDivTr.gameObject : null;
            if (topDivObj == null)
            {
                topDivObj = new GameObject("Top_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                topDivObj.transform.SetParent(shopPanel.transform, false);
            }
            RectTransform tdRect = topDivObj.GetComponent<RectTransform>();
            tdRect.anchorMin = new Vector2(0f, 1f);
            tdRect.anchorMax = new Vector2(1f, 1f);
            tdRect.pivot = new Vector2(0.5f, 1f);
            tdRect.anchoredPosition = new Vector2(0f, -74f);
            tdRect.sizeDelta = new Vector2(-48f, 4f);

            Image tdImg = topDivObj.GetComponent<Image>();
            if (tdImg != null && dividerGoldSprite != null)
            {
                tdImg.sprite = dividerGoldSprite;
                tdImg.type = Image.Type.Simple;
                tdImg.color = Color.white;
            }

            // E. Corner Close Button ('X')
            Transform closeTr = shopPanel.transform.Find("Close_Corner_Button");
            GameObject closeObj = closeTr != null ? closeTr.gameObject : null;
            if (closeObj == null)
            {
                closeObj = new GameObject("Close_Corner_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                closeObj.transform.SetParent(shopPanel.transform, false);
            }
            RectTransform cRect = closeObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(1f, 1f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.pivot = new Vector2(1f, 1f);
            cRect.anchoredPosition = new Vector2(-16f, -16f);
            cRect.sizeDelta = new Vector2(32f, 32f);

            Image cImg = closeObj.GetComponent<Image>();
            if (cImg != null && buttonNormalSprite != null)
            {
                cImg.sprite = buttonNormalSprite;
                cImg.type = Image.Type.Sliced;
                cImg.color = Color.white;
            }

            closeCornerButton = closeObj.GetComponent<Button>();
            Transform xTxtTr = closeObj.transform.Find("Text");
            GameObject xTxtObj = xTxtTr != null ? xTxtTr.gameObject : null;
            if (xTxtObj == null)
            {
                xTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                xTxtObj.transform.SetParent(closeObj.transform, false);
            }
            TMP_Text xText = xTxtObj.GetComponent<TMP_Text>();
            if (xText != null)
            {
                xText.text = "X";
                xText.fontSize = 15f;
                xText.fontStyle = FontStyles.Bold;
                xText.color = UITheme.GoldAccent;
                xText.alignment = TextAlignmentOptions.Center;
                xText.enableAutoSizing = false;
                xText.raycastTarget = false;
                RectTransform xtRect = xTxtObj.GetComponent<RectTransform>();
                xtRect.anchorMin = Vector2.zero;
                xtRect.anchorMax = Vector2.one;
                xtRect.anchoredPosition = Vector2.zero;
                xtRect.sizeDelta = Vector2.zero;
            }

            // F. Currency Bar (Pills for Gold and Scrap)
            Transform currTr = shopPanel.transform.Find("CurrencyContainer") ?? shopPanel.transform.Find("Currency_Bar");
            GameObject currObj = currTr != null ? currTr.gameObject : null;
            if (currObj == null)
            {
                currObj = new GameObject("Currency_Bar", typeof(RectTransform));
                currObj.transform.SetParent(shopPanel.transform, false);
            }
            else
            {
                currObj.name = "Currency_Bar";
            }

            RectTransform cbRect = currObj.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0f, 1f);
            cbRect.anchorMax = new Vector2(1f, 1f);
            cbRect.pivot = new Vector2(0.5f, 1f);
            cbRect.anchoredPosition = new Vector2(0f, -86f);
            cbRect.sizeDelta = new Vector2(-48f, 38f);

            // F1. Gold Pill Badge
            Transform goldPillTr = currObj.transform.Find("Gold_Pill");
            GameObject goldPillObj = goldPillTr != null ? goldPillTr.gameObject : null;
            if (goldPillObj == null)
            {
                goldPillObj = new GameObject("Gold_Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                goldPillObj.transform.SetParent(currObj.transform, false);
            }
            RectTransform gpRect = goldPillObj.GetComponent<RectTransform>();
            gpRect.anchorMin = new Vector2(0.5f, 0.5f);
            gpRect.anchorMax = new Vector2(0.5f, 0.5f);
            gpRect.pivot = new Vector2(1f, 0.5f);
            gpRect.anchoredPosition = new Vector2(-15f, 0f);
            gpRect.sizeDelta = new Vector2(170f, 34f);

            Image gpImg = goldPillObj.GetComponent<Image>();
            if (gpImg != null && pillBadgeSprite != null)
            {
                gpImg.sprite = pillBadgeSprite;
                gpImg.type = Image.Type.Sliced;
                gpImg.color = Color.white;
            }

            // Coin Icon
            Transform cIconTr = goldPillObj.transform.Find("Gold_Icon");
            GameObject cIconObj = cIconTr != null ? cIconTr.gameObject : null;
            if (cIconObj == null)
            {
                cIconObj = new GameObject("Gold_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cIconObj.transform.SetParent(goldPillObj.transform, false);
            }
            RectTransform ciRect = cIconObj.GetComponent<RectTransform>();
            ciRect.anchorMin = new Vector2(0f, 0.5f);
            ciRect.anchorMax = new Vector2(0f, 0.5f);
            ciRect.pivot = new Vector2(0f, 0.5f);
            ciRect.anchoredPosition = new Vector2(8f, 0f);
            ciRect.sizeDelta = new Vector2(22f, 22f);

            coinIconImage = cIconObj.GetComponent<Image>();
            if (coinIconImage != null)
            {
                if (coinSprite != null) coinIconImage.sprite = coinSprite;
                coinIconImage.preserveAspect = true;
                coinIconImage.color = Color.white;
            }

            // Gold Text
            if (goldBalanceText == null)
            {
                Transform gtTr = goldPillObj.transform.Find("Gold_Balance_Text") ?? goldPillObj.transform.Find("Gold_Text");
                if (gtTr != null) goldBalanceText = gtTr.GetComponent<TMP_Text>();
                if (goldBalanceText == null)
                {
                    GameObject gtObj = new GameObject("Gold_Balance_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    gtObj.transform.SetParent(goldPillObj.transform, false);
                    goldBalanceText = gtObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (goldBalanceText != null)
            {
                goldBalanceText.transform.SetParent(goldPillObj.transform, false);
                goldBalanceText.name = "Gold_Balance_Text";
                RectTransform gtRect = goldBalanceText.GetComponent<RectTransform>();
                gtRect.anchorMin = Vector2.zero;
                gtRect.anchorMax = Vector2.one;
                gtRect.pivot = new Vector2(0f, 0.5f);
                gtRect.anchoredPosition = new Vector2(36f, 0f);
                gtRect.sizeDelta = new Vector2(-42f, 0f);

                goldBalanceText.fontSize = 13.5f;
                goldBalanceText.fontStyle = FontStyles.Bold;
                goldBalanceText.color = UITheme.CoinGold; // #FFD700
                goldBalanceText.alignment = TextAlignmentOptions.MidlineLeft;
                goldBalanceText.enableAutoSizing = false;
                goldBalanceText.raycastTarget = false;
            }

            // F2. Scrap Metal Pill Badge
            Transform scrapPillTr = currObj.transform.Find("Scrap_Pill");
            GameObject scrapPillObj = scrapPillTr != null ? scrapPillTr.gameObject : null;
            if (scrapPillObj == null)
            {
                scrapPillObj = new GameObject("Scrap_Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                scrapPillObj.transform.SetParent(currObj.transform, false);
            }
            RectTransform spRect = scrapPillObj.GetComponent<RectTransform>();
            spRect.anchorMin = new Vector2(0.5f, 0.5f);
            spRect.anchorMax = new Vector2(0.5f, 0.5f);
            spRect.pivot = new Vector2(0f, 0.5f);
            spRect.anchoredPosition = new Vector2(15f, 0f);
            spRect.sizeDelta = new Vector2(170f, 34f);

            Image spImg = scrapPillObj.GetComponent<Image>();
            if (spImg != null && pillBadgeSprite != null)
            {
                spImg.sprite = pillBadgeSprite;
                spImg.type = Image.Type.Sliced;
                spImg.color = Color.white;
            }

            // Scrap Icon
            Transform sIconTr = scrapPillObj.transform.Find("Scrap_Icon");
            GameObject sIconObj = sIconTr != null ? sIconTr.gameObject : null;
            if (sIconObj == null)
            {
                sIconObj = new GameObject("Scrap_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sIconObj.transform.SetParent(scrapPillObj.transform, false);
            }
            RectTransform siRect = sIconObj.GetComponent<RectTransform>();
            siRect.anchorMin = new Vector2(0f, 0.5f);
            siRect.anchorMax = new Vector2(0f, 0.5f);
            siRect.pivot = new Vector2(0f, 0.5f);
            siRect.anchoredPosition = new Vector2(8f, 0f);
            siRect.sizeDelta = new Vector2(22f, 22f);

            scrapIconImage = sIconObj.GetComponent<Image>();
            if (scrapIconImage != null)
            {
                if (scrapOreSprite != null) scrapIconImage.sprite = scrapOreSprite;
                scrapIconImage.preserveAspect = true;
                scrapIconImage.color = Color.white;
            }

            // Scrap Text
            if (scrapMetalText == null)
            {
                Transform stTr = scrapPillObj.transform.Find("Scrap_Metal_Text") ?? scrapPillObj.transform.Find("Scrap_Text");
                if (stTr != null) scrapMetalText = stTr.GetComponent<TMP_Text>();
                if (scrapMetalText == null)
                {
                    GameObject stObj = new GameObject("Scrap_Metal_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    stObj.transform.SetParent(scrapPillObj.transform, false);
                    scrapMetalText = stObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (scrapMetalText != null)
            {
                scrapMetalText.transform.SetParent(scrapPillObj.transform, false);
                scrapMetalText.name = "Scrap_Metal_Text";
                RectTransform stRect = scrapMetalText.GetComponent<RectTransform>();
                stRect.anchorMin = Vector2.zero;
                stRect.anchorMax = Vector2.one;
                stRect.pivot = new Vector2(0f, 0.5f);
                stRect.anchoredPosition = new Vector2(36f, 0f);
                stRect.sizeDelta = new Vector2(-42f, 0f);

                scrapMetalText.fontSize = 13.5f;
                scrapMetalText.fontStyle = FontStyles.Bold;
                scrapMetalText.color = new Color(0.70f, 0.77f, 0.87f, 1f); // #B0C4DE
                scrapMetalText.alignment = TextAlignmentOptions.MidlineLeft;
                scrapMetalText.enableAutoSizing = false;
                scrapMetalText.raycastTarget = false;
            }

            // G. Buy / Sell tabs above the stock shelf
            EnsureTabBar();

            Transform shelfTr = shopPanel.transform.Find("ActionContainer") ?? shopPanel.transform.Find("Stock_Shelf_Container");
            GameObject shelfObj = shelfTr != null ? shelfTr.gameObject : null;
            if (shelfObj == null)
            {
                shelfObj = new GameObject("Stock_Shelf_Container", typeof(RectTransform));
                shelfObj.transform.SetParent(shopPanel.transform, false);
            }
            else
            {
                shelfObj.name = "Stock_Shelf_Container";
            }

            RectTransform shRect = shelfObj.GetComponent<RectTransform>();
            shRect.anchorMin = new Vector2(0f, 0f);
            shRect.anchorMax = new Vector2(1f, 1f);
            shRect.pivot = new Vector2(0.5f, 0.5f);
            shRect.offsetMin = new Vector2(32f, ShelfBottomOffset);
            shRect.offsetMax = new Vector2(-32f, -ShelfTopOffset);

            VerticalLayoutGroup vlg = shelfObj.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = shelfObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = RowSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            shelfTransform = shelfObj.transform;
            BuildBuyRows();
            BuildScrapRow();
            EnsureSellEmptyHint();

            EnsureFeedbackText();

            // H. Bottom Footer Divider
            Transform botDivTr = shopPanel.transform.Find("Bottom_Divider");
            GameObject botDivObj = botDivTr != null ? botDivTr.gameObject : null;
            if (botDivObj == null)
            {
                botDivObj = new GameObject("Bottom_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                botDivObj.transform.SetParent(shopPanel.transform, false);
            }
            RectTransform bdRect = botDivObj.GetComponent<RectTransform>();
            bdRect.anchorMin = new Vector2(0f, 0f);
            bdRect.anchorMax = new Vector2(1f, 0f);
            bdRect.pivot = new Vector2(0.5f, 0f);
            bdRect.anchoredPosition = new Vector2(0f, 98f);
            bdRect.sizeDelta = new Vector2(-48f, 4f);

            Image bdImg = botDivObj.GetComponent<Image>();
            if (bdImg != null && dividerGoldSprite != null)
            {
                bdImg.sprite = dividerGoldSprite;
                bdImg.type = Image.Type.Simple;
                bdImg.color = Color.white;
            }

            // I. Leave Shop Button
            if (exitShopButton == null)
            {
                Transform exTr = shopPanel.transform.Find("ExitShopButton") ?? shopPanel.transform.Find("LeaveShopButton") ?? shopPanel.transform.Find("CloseButton");
                if (exTr != null) exitShopButton = exTr.GetComponent<Button>();
                if (exitShopButton == null)
                {
                    GameObject exObj = new GameObject("ExitShopButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    exObj.transform.SetParent(shopPanel.transform, false);
                    exitShopButton = exObj.GetComponent<Button>();

                    GameObject exTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    exTxtObj.transform.SetParent(exObj.transform, false);
                }
            }

            if (exitShopButton != null)
            {
                exitShopButton.transform.SetParent(shopPanel.transform, false);
                exitShopButton.name = "ExitShopButton";
                RectTransform exRect = exitShopButton.GetComponent<RectTransform>();
                exRect.anchorMin = new Vector2(0.5f, 0f);
                exRect.anchorMax = new Vector2(0.5f, 0f);
                exRect.pivot = new Vector2(0.5f, 0f);
                exRect.anchoredPosition = new Vector2(0f, 16f);
                exRect.sizeDelta = new Vector2(210f, 38f);

                Image exImg = exitShopButton.GetComponent<Image>();
                if (exImg != null && buttonNormalSprite != null)
                {
                    exImg.sprite = buttonNormalSprite;
                    exImg.type = Image.Type.Sliced;
                    exImg.color = Color.white;
                }

                if (buttonHoverSprite != null && buttonPressedSprite != null)
                {
                    exitShopButton.transition = Selectable.Transition.SpriteSwap;
                    SpriteState ss = exitShopButton.spriteState;
                    ss.highlightedSprite = buttonHoverSprite;
                    ss.pressedSprite = buttonPressedSprite;
                    ss.selectedSprite = buttonHoverSprite;
                    exitShopButton.spriteState = ss;
                }

                TMP_Text exTxt = exitShopButton.GetComponentInChildren<TMP_Text>(true);
                if (exTxt != null)
                {
                    RectTransform extRect = exTxt.GetComponent<RectTransform>();
                    extRect.anchorMin = Vector2.zero;
                    extRect.anchorMax = Vector2.one;
                    extRect.anchoredPosition = Vector2.zero;
                    extRect.sizeDelta = Vector2.zero;

                    exTxt.text = "Leave Shop";
                    exTxt.fontSize = 15f;
                    exTxt.fontStyle = FontStyles.Bold;
                    exTxt.color = UITheme.ParchmentText;
                    exTxt.alignment = TextAlignmentOptions.Center;
                    exTxt.enableAutoSizing = false;
                    exTxt.raycastTarget = false;
                }
            }

            SubscribeButtonListeners();
            RefreshEconomyDisplay();
        }

        private void SetupItemRow(Transform parent, string rowName, string title, string desc, Sprite iconSprite, ref Button btn, ref Image iconSlotImage, string defaultBtnText)
        {
            Transform rowTr = parent.Find(rowName);
            GameObject rowObj = rowTr != null ? rowTr.gameObject : null;
            if (rowObj == null)
            {
                rowObj = new GameObject(rowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                rowObj.transform.SetParent(parent, false);
            }

            LayoutElement le = rowObj.GetComponent<LayoutElement>();
            le.minHeight = 68f;
            le.preferredHeight = 68f;
            le.flexibleWidth = 1f;

            Image rowBg = rowObj.GetComponent<Image>();
            if (rowBg != null)
            {
                // Subtle dark card tint
                rowBg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            }

            // 1. Framed Small Item Image Slot
            Transform frameTr = rowObj.transform.Find("Item_Slot_Frame");
            GameObject frameObj = frameTr != null ? frameTr.gameObject : null;
            if (frameObj == null)
            {
                frameObj = new GameObject("Item_Slot_Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameObj.transform.SetParent(rowObj.transform, false);
            }
            RectTransform fRect = frameObj.GetComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0f, 0.5f);
            fRect.anchorMax = new Vector2(0f, 0.5f);
            fRect.pivot = new Vector2(0f, 0.5f);
            fRect.anchoredPosition = new Vector2(10f, 0f);
            fRect.sizeDelta = new Vector2(48f, 48f);

            Image fImg = frameObj.GetComponent<Image>();
            if (fImg != null && slotFrameSprite != null)
            {
                fImg.sprite = slotFrameSprite;
                fImg.type = Image.Type.Sliced;
                fImg.color = Color.white;
            }

            // Item Icon inside Slot
            Transform iconTr = frameObj.transform.Find("Item_Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : null;
            if (iconObj == null)
            {
                iconObj = new GameObject("Item_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(frameObj.transform, false);
            }
            RectTransform iRect = iconObj.GetComponent<RectTransform>();
            iRect.anchorMin = Vector2.zero;
            iRect.anchorMax = Vector2.one;
            iRect.pivot = new Vector2(0.5f, 0.5f);
            iRect.anchoredPosition = Vector2.zero;
            iRect.sizeDelta = new Vector2(-12f, -12f);

            iconSlotImage = iconObj.GetComponent<Image>();
            if (iconSlotImage != null)
            {
                if (iconSprite != null) iconSlotImage.sprite = iconSprite;
                iconSlotImage.preserveAspect = true;
                iconSlotImage.color = Color.white;
            }

            // 2. Item Text Info (Title & Description)
            Transform textTr = rowObj.transform.Find("Item_Text_Info");
            GameObject textObj = textTr != null ? textTr.gameObject : null;
            if (textObj == null)
            {
                textObj = new GameObject("Item_Text_Info", typeof(RectTransform));
                textObj.transform.SetParent(rowObj.transform, false);
            }
            RectTransform tRect = textObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.pivot = new Vector2(0f, 0.5f);
            tRect.offsetMin = new Vector2(68f, 6f);
            tRect.offsetMax = new Vector2(-190f, -6f);

            // Title
            Transform titleTr = textObj.transform.Find("Item_Title");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : null;
            if (titleObj == null)
            {
                titleObj = new GameObject("Item_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(textObj.transform, false);
            }
            RectTransform tiRect = titleObj.GetComponent<RectTransform>();
            tiRect.anchorMin = new Vector2(0f, 0.5f);
            tiRect.anchorMax = new Vector2(1f, 1f);
            tiRect.pivot = new Vector2(0f, 1f);
            tiRect.offsetMin = Vector2.zero;
            tiRect.offsetMax = Vector2.zero;

            TMP_Text titleTxt = titleObj.GetComponent<TMP_Text>();
            if (titleTxt != null)
            {
                titleTxt.text = title;
                titleTxt.fontSize = 14.5f;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.color = UITheme.ParchmentText;
                titleTxt.alignment = TextAlignmentOptions.TopLeft;
                titleTxt.enableAutoSizing = false;
                titleTxt.raycastTarget = false;
            }

            // Description
            Transform descTr = textObj.transform.Find("Item_Desc");
            GameObject descObj = descTr != null ? descTr.gameObject : null;
            if (descObj == null)
            {
                descObj = new GameObject("Item_Desc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                descObj.transform.SetParent(textObj.transform, false);
            }
            RectTransform deRect = descObj.GetComponent<RectTransform>();
            deRect.anchorMin = new Vector2(0f, 0f);
            deRect.anchorMax = new Vector2(1f, 0.5f);
            deRect.pivot = new Vector2(0f, 0f);
            deRect.offsetMin = Vector2.zero;
            deRect.offsetMax = Vector2.zero;

            TMP_Text descTxt = descObj.GetComponent<TMP_Text>();
            if (descTxt != null)
            {
                descTxt.text = desc;
                descTxt.fontSize = 11.5f;
                descTxt.color = new Color(0.65f, 0.70f, 0.78f, 1f);
                descTxt.alignment = TextAlignmentOptions.TopLeft;
                descTxt.enableAutoSizing = false;
                descTxt.raycastTarget = false;
            }

            // 3. Purchase Button
            string buttonName = rowName switch
            {
                "Row_Potion" => "BuyPotionButton",
                "Row_Sword" => "BuyWeaponButton",
                "Row_Shield" => "BuyArmorButton",
                "Row_Scrap" => "SellAllScrapButton",
                _ => rowName + "_Button"
            };

            if (btn == null)
            {
                Transform btnTr = rowObj.transform.Find(buttonName) ?? rowObj.transform.Find("Action_Button");
                if (btnTr != null) btn = btnTr.GetComponent<Button>();
                if (btn == null)
                {
                    GameObject btnObj = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    btnObj.transform.SetParent(rowObj.transform, false);
                    btn = btnObj.GetComponent<Button>();

                    GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    txtObj.transform.SetParent(btnObj.transform, false);
                }
            }

            if (btn != null)
            {
                btn.name = buttonName;
                btn.transform.SetParent(rowObj.transform, false);
                RectTransform bRect = btn.GetComponent<RectTransform>();
                bRect.anchorMin = new Vector2(1f, 0.5f);
                bRect.anchorMax = new Vector2(1f, 0.5f);
                bRect.pivot = new Vector2(1f, 0.5f);
                bRect.anchoredPosition = new Vector2(-12f, 0f);
                bRect.sizeDelta = new Vector2(165f, 38f);

                Image bImg = btn.GetComponent<Image>();
                if (bImg != null && buttonNormalSprite != null)
                {
                    bImg.sprite = buttonNormalSprite;
                    bImg.type = Image.Type.Sliced;
                    bImg.color = Color.white;
                }

                if (buttonHoverSprite != null && buttonPressedSprite != null)
                {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    SpriteState ss = btn.spriteState;
                    ss.highlightedSprite = buttonHoverSprite;
                    ss.pressedSprite = buttonPressedSprite;
                    ss.selectedSprite = buttonHoverSprite;
                    btn.spriteState = ss;
                }

                TMP_Text bTxt = btn.GetComponentInChildren<TMP_Text>(true);
                if (bTxt != null)
                {
                    RectTransform btRect = bTxt.GetComponent<RectTransform>();
                    btRect.anchorMin = Vector2.zero;
                    btRect.anchorMax = Vector2.one;
                    btRect.anchoredPosition = Vector2.zero;
                    btRect.sizeDelta = Vector2.zero;

                    bTxt.text = defaultBtnText;
                    bTxt.fontSize = 13.5f;
                    bTxt.fontStyle = FontStyles.Bold;
                    bTxt.color = new Color(1f, 0.85f, 0.40f, 1f); // #FFD700
                    bTxt.alignment = TextAlignmentOptions.Center;
                    bTxt.enableAutoSizing = false;
                    bTxt.raycastTarget = false;

                    if (rowName == "Row_Scrap")
                    {
                        sellScrapButtonLabel = bTxt;
                    }
                }
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
            // Stock row buttons are wired once, when their rows are built (see WireRowButton)

            if (buyTabButton != null)
            {
                buyTabButton.onClick.RemoveListener(ShowBuyTab);
                buyTabButton.onClick.AddListener(ShowBuyTab);
            }

            if (sellTabButton != null)
            {
                sellTabButton.onClick.RemoveListener(ShowSellTab);
                sellTabButton.onClick.AddListener(ShowSellTab);
            }

            if (exitShopButton != null)
            {
                exitShopButton.onClick.RemoveListener(CloseShop);
                exitShopButton.onClick.AddListener(CloseShop);
            }

            if (closeCornerButton != null)
            {
                closeCornerButton.onClick.RemoveListener(CloseShop);
                closeCornerButton.onClick.AddListener(CloseShop);
            }
        }

        private void UnsubscribeButtonListeners()
        {
            if (buyTabButton != null)
            {
                buyTabButton.onClick.RemoveListener(ShowBuyTab);
            }

            if (sellTabButton != null)
            {
                sellTabButton.onClick.RemoveListener(ShowSellTab);
            }

            if (exitShopButton != null)
            {
                exitShopButton.onClick.RemoveListener(CloseShop);
            }

            if (closeCornerButton != null)
            {
                closeCornerButton.onClick.RemoveListener(CloseShop);
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
            EnsureStyledHierarchy();
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
            showingSellTab = false;
            ShowIdleLine();
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

        #region Tabs, Rows & Feedback Line

        /// <summary>
        /// Buy / Sell tab buttons between the currency bar and the shelf.
        /// </summary>
        private void EnsureTabBar()
        {
            Transform barTr = shopPanel.transform.Find("Tab_Bar");
            GameObject barObj = barTr != null ? barTr.gameObject : null;
            if (barObj == null)
            {
                barObj = new GameObject("Tab_Bar", typeof(RectTransform));
                barObj.transform.SetParent(shopPanel.transform, false);
            }

            RectTransform barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 1f);
            barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -134f);
            barRect.sizeDelta = new Vector2(360f, 36f);

            buyTabButton = EnsureTabButton(barObj.transform, "Tab_Buy", "BUY", -92f, buyTabButton);
            sellTabButton = EnsureTabButton(barObj.transform, "Tab_Sell", "SELL", 92f, sellTabButton);
        }

        private Button EnsureTabButton(Transform parent, string name, string label, float x, Button existing)
        {
            Button tabBtn = existing;
            if (tabBtn == null)
            {
                Transform tr = parent.Find(name);
                if (tr != null) tabBtn = tr.GetComponent<Button>();
            }

            if (tabBtn == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                tabBtn = obj.GetComponent<Button>();

                GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtObj.transform.SetParent(obj.transform, false);
            }

            RectTransform rect = tabBtn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(172f, 34f);

            Image img = tabBtn.GetComponent<Image>();
            if (img != null && buttonNormalSprite != null)
            {
                img.sprite = buttonNormalSprite;
                img.type = Image.Type.Sliced;
            }

            if (buttonHoverSprite != null && buttonPressedSprite != null)
            {
                tabBtn.transition = Selectable.Transition.SpriteSwap;
                SpriteState ss = tabBtn.spriteState;
                ss.highlightedSprite = buttonHoverSprite;
                ss.pressedSprite = buttonPressedSprite;
                ss.selectedSprite = buttonHoverSprite;
                tabBtn.spriteState = ss;
            }

            TMP_Text txt = tabBtn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                RectTransform tRect = txt.GetComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;
                tRect.anchoredPosition = Vector2.zero;
                tRect.sizeDelta = Vector2.zero;

                txt.text = label;
                txt.fontSize = 15f;
                txt.fontStyle = FontStyles.Bold;
                txt.characterSpacing = 6f;
                txt.alignment = TextAlignmentOptions.Center;
                txt.enableAutoSizing = false;
                txt.raycastTarget = false;
            }

            return tabBtn;
        }

        /// <summary>
        /// Highlights the active tab: bright frame and gold text; the other tab is dimmed.
        /// </summary>
        private void RefreshTabVisuals()
        {
            StyleTab(buyTabButton, !showingSellTab);
            StyleTab(sellTabButton, showingSellTab);
        }

        private static void StyleTab(Button tabBtn, bool active)
        {
            if (tabBtn == null) return;

            Image img = tabBtn.GetComponent<Image>();
            if (img != null)
            {
                img.color = active ? Color.white : new Color(0.45f, 0.47f, 0.52f, 0.9f);
            }

            TMP_Text txt = tabBtn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                txt.color = active ? PriceGold : MutedText;
            }
        }

        /// <summary>
        /// Builds the Buy tab: potions, poison, the reroll rune and the two levelled upgrades.
        /// The old scene rows (Row_Potion, Row_Sword, Row_Shield) are reused.
        /// </summary>
        private void BuildBuyRows()
        {
            buyRows.Clear();

            for (int i = 0; i < ShopManager.BuyStockOrder.Length; i++)
            {
                string itemId = ShopManager.BuyStockOrder[i];
                ShopRow row;
                switch (itemId)
                {
                    case ShopManager.SMALL_POTION_ID:
                        row = CreateRow("Row_Potion", ref buyPotionButton, ref potionIconImage);
                        break;
                    case ShopManager.SHARPENED_BLADE_ID:
                        row = CreateRow("Row_Sword", ref buyWeaponButton, ref weaponIconImage);
                        break;
                    case ShopManager.RUNIC_ARMOR_ID:
                        row = CreateRow("Row_Shield", ref buyArmorButton, ref armorIconImage);
                        break;
                    default:
                        Button noButton = null;
                        Image noIcon = null;
                        row = CreateRow("Row_Buy_" + itemId, ref noButton, ref noIcon);
                        break;
                }

                row.Key = itemId;
                row.Item = ResolveItem(itemId);
                row.IsSellRow = false;

                bool isUpgrade = itemId == ShopManager.SHARPENED_BLADE_ID || itemId == ShopManager.RUNIC_ARMOR_ID;
                if (isUpgrade)
                {
                    row.Pips = EnsureLevelPips(row.Root.transform);
                }

                row.Root.transform.SetSiblingIndex(i);
                WireRowButton(row);
                buyRows.Add(row);
            }
        }

        /// <summary>
        /// The scrap salvage row at the top of the Sell tab (1 scrap = 10 gold).
        /// </summary>
        private void BuildScrapRow()
        {
            scrapRow = CreateRow("Row_Scrap", ref sellAllScrapButton, ref scrapActionIconImage);
            scrapRow.Key = ScrapRowKey;
            scrapRow.IsSellRow = true;
            sellScrapButtonLabel = scrapRow.ButtonLabel;
            if (scrapRow.Icon != null && scrapOreSprite != null) scrapRow.Icon.sprite = scrapOreSprite;
            scrapRow.Root.transform.SetSiblingIndex(ShopManager.BuyStockOrder.Length);
            WireRowButton(scrapRow);
        }

        /// <summary>
        /// Muted line under the scrap row when the player carries nothing else Baldur buys.
        /// </summary>
        private void EnsureSellEmptyHint()
        {
            if (sellEmptyHint == null)
            {
                Transform tr = shelfTransform.Find("Sell_Empty_Hint");
                GameObject obj = tr != null ? tr.gameObject : null;
                if (obj == null)
                {
                    obj = new GameObject("Sell_Empty_Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
                    obj.transform.SetParent(shelfTransform, false);
                }
                sellEmptyHint = obj.GetComponent<TMP_Text>();
            }

            LayoutElement le = sellEmptyHint.GetComponent<LayoutElement>();
            if (le == null) le = sellEmptyHint.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 56f;
            le.preferredHeight = 56f;
            le.flexibleWidth = 1f;

            sellEmptyHint.text = "<i>Potions, poison vials and other loot you carry will show up here.\nQuest items stay with you.</i>";
            sellEmptyHint.fontSize = 13f;
            sellEmptyHint.color = MutedText;
            sellEmptyHint.alignment = TextAlignmentOptions.Center;
            sellEmptyHint.enableAutoSizing = false;
            sellEmptyHint.richText = true;
            sellEmptyHint.raycastTarget = false;
            sellEmptyHint.gameObject.SetActive(false);
        }

        /// <summary>
        /// Returns the pooled sell row at the given index, creating it the first time.
        /// </summary>
        private ShopRow GetSellRow(int index)
        {
            while (sellRows.Count <= index)
            {
                Button noButton = null;
                Image noIcon = null;
                ShopRow row = CreateRow("Row_Sell_" + sellRows.Count, ref noButton, ref noIcon);
                row.IsSellRow = true;
                WireRowButton(row);
                sellRows.Add(row);
            }
            return sellRows[index];
        }

        /// <summary>
        /// Creates (or finds) a styled row through SetupItemRow and caches its parts.
        /// </summary>
        private ShopRow CreateRow(string rowName, ref Button button, ref Image icon)
        {
            SetupItemRow(shelfTransform, rowName, "", "", null, ref button, ref icon, "");

            Transform rowTr = shelfTransform.Find(rowName);
            ShopRow row = new ShopRow
            {
                Root = rowTr.gameObject,
                Background = rowTr.GetComponent<Image>(),
                Icon = icon,
                Button = button
            };

            Transform titleTr = rowTr.Find("Item_Text_Info/Item_Title");
            Transform descTr = rowTr.Find("Item_Text_Info/Item_Desc");
            row.Title = titleTr != null ? titleTr.GetComponent<TMP_Text>() : null;
            row.Desc = descTr != null ? descTr.GetComponent<TMP_Text>() : null;
            row.ButtonLabel = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;

            if (row.Title != null) row.Title.richText = true;
            if (row.Desc != null)
            {
                row.Desc.richText = true;
                row.Desc.textWrappingMode = TextWrappingModes.Normal;
            }

            return row;
        }

        /// <summary>
        /// Three small squares next to an upgrade's title: one lit per level bought.
        /// </summary>
        private Image[] EnsureLevelPips(Transform rowTransform)
        {
            Transform pipsTr = rowTransform.Find("Level_Pips");
            GameObject pipsObj = pipsTr != null ? pipsTr.gameObject : null;
            if (pipsObj == null)
            {
                pipsObj = new GameObject("Level_Pips", typeof(RectTransform));
                pipsObj.transform.SetParent(rowTransform, false);
            }

            RectTransform pRect = pipsObj.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(1f, 0.5f);
            pRect.anchorMax = new Vector2(1f, 0.5f);
            pRect.pivot = new Vector2(1f, 0.5f);
            pRect.anchoredPosition = new Vector2(-190f, 11f);
            pRect.sizeDelta = new Vector2(3 * 14f + 2 * 5f, 14f);

            Image[] pips = new Image[ShopManager.MAX_UPGRADE_LEVEL];
            for (int i = 0; i < pips.Length; i++)
            {
                string pipName = "Pip_" + i;
                Transform pipTr = pipsObj.transform.Find(pipName);
                GameObject pipObj = pipTr != null ? pipTr.gameObject : null;
                if (pipObj == null)
                {
                    pipObj = new GameObject(pipName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    pipObj.transform.SetParent(pipsObj.transform, false);
                }

                RectTransform r = pipObj.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0f, 0.5f);
                r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(7f + i * 19f, 0f);
                r.sizeDelta = new Vector2(11f, 11f);
                r.localRotation = Quaternion.Euler(0f, 0f, 45f);

                Image pipImg = pipObj.GetComponent<Image>();
                pipImg.raycastTarget = false;
                pips[i] = pipImg;
            }

            return pips;
        }

        private void WireRowButton(ShopRow row)
        {
            if (row.Button == null) return;

            row.Button.onClick.RemoveAllListeners();
            ShopRow captured = row;
            row.Button.onClick.AddListener(() => OnRowButtonClicked(captured));
        }

        private void OnRowButtonClicked(ShopRow row)
        {
            if (row == null) return;

            if (row.Key == ScrapRowKey)
            {
                SellAllScrap();
            }
            else if (row.IsSellRow)
            {
                SellStockItem(row.Item);
            }
            else
            {
                BuyStockItem(row.Item);
            }
        }

        /// <summary>
        /// Text line between the shelf and the Leave button.
        /// </summary>
        private void EnsureFeedbackText()
        {
            if (feedbackText == null)
            {
                Transform tr = shopPanel.transform.Find("Shop_Feedback_Text");
                GameObject obj = tr != null ? tr.gameObject : null;
                if (obj == null)
                {
                    obj = new GameObject("Shop_Feedback_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    obj.transform.SetParent(shopPanel.transform, false);
                }
                feedbackText = obj.GetComponent<TMP_Text>();
            }

            RectTransform rect = feedbackText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 64f);
            rect.sizeDelta = new Vector2(-64f, 30f);

            feedbackText.fontSize = 14f;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.enableAutoSizing = false;
            feedbackText.richText = true;
            feedbackText.raycastTarget = false;
        }

        private void ShowIdleLine()
        {
            feedbackResetTime = 0f;
            if (feedbackText == null) return;
            feedbackText.text = IdleLine;
            feedbackText.color = MutedText;
        }

        private void ShowFeedback(string message, bool success)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
                feedbackText.color = success ? FeedbackGood : FeedbackBad;
            }
            feedbackResetTime = Time.unscaledTime + FeedbackSeconds;
        }

        private static void PlayShopSound(SFXClipType clip, float volume = 1f)
        {
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(clip, default, volume);
            }
        }

        /// <summary>Switches the shelf to the Buy tab.</summary>
        public void ShowBuyTab()
        {
            if (!showingSellTab) return;
            showingSellTab = false;
            PlayShopSound(SFXClipType.ButtonClick, 0.6f);
            RefreshEconomyDisplay();
        }

        /// <summary>Switches the shelf to the Sell tab.</summary>
        public void ShowSellTab()
        {
            if (showingSellTab) return;
            showingSellTab = true;
            PlayShopSound(SFXClipType.ButtonClick, 0.6f);
            RefreshEconomyDisplay();
        }

        /// <summary>Whether the Sell tab is showing.</summary>
        public bool IsSellTabActive => showingSellTab;

        #endregion

        #region Item Lookup & Row Text

        /// <summary>
        /// Finds an item by ID in the inventory catalog, falling back to the serialized stock fields.
        /// </summary>
        private ItemSO ResolveItem(string itemId)
        {
            InventoryManager inventory = InventoryManager.Instance;
            ItemSO item = inventory != null ? inventory.FindItemByID(itemId) : null;
            if (item != null) return item;

            switch (itemId)
            {
                case ShopManager.SMALL_POTION_ID: return healthPotionItem;
                case ShopManager.SHARPENED_BLADE_ID: return sharpenedBladeItem;
                case ShopManager.RUNIC_ARMOR_ID: return runicArmorItem;
                default: return null;
            }
        }

        private void ApplyIcon(ShopRow row)
        {
            if (row.Icon == null || row.Item == null) return;

            Sprite sprite = row.Item.ItemIcon;
            Color tint = Color.white;

            if (sprite == null)
            {
                switch (row.Item.ItemID)
                {
                    case ShopManager.POISON_VIAL_ID:
                        sprite = potionSprite;
                        tint = PoisonTint;
                        break;
                    case ShopManager.REROLL_RUNE_ID:
                        sprite = runeIconSprite;
                        break;
                    default:
                        if (row.Item.ItemType == ItemType.WeaponUpgrade) sprite = swordIconSprite;
                        else if (row.Item.ItemType == ItemType.ArmorUpgrade) sprite = shieldIconSprite;
                        else if (row.Item.ItemType == ItemType.Consumable) sprite = potionSprite;
                        else sprite = coinSprite;
                        break;
                }
            }

            row.Icon.sprite = sprite;
            row.Icon.color = tint;
            row.Icon.enabled = sprite != null;
        }

        /// <summary>
        /// Short, player-facing description of what buying the item does right now.
        /// </summary>
        private static string GetBuyDescription(ItemSO item, int level)
        {
            switch (item.ItemID)
            {
                case ShopManager.SMALL_POTION_ID:
                    return $"Restores {item.StatBonusValue} HP. Drink from the bottom-left slot or with [Q].";
                case ShopManager.GREATER_POTION_ID:
                    return $"Restores {item.StatBonusValue} HP. Drink from the bottom-left slot.";
                case ShopManager.POISON_VIAL_ID:
                    return $"Coats your blade when a fight starts: first hit deals +{item.StatBonusValue} damage.";
                case ShopManager.REROLL_RUNE_ID:
                    return "Reroll one failed d20 roll of your choice.";
            }

            bool isWeapon = item.ItemType == ItemType.WeaponUpgrade;
            string stat = isWeapon ? "DMG" : "AC";
            if (level >= ShopManager.MAX_UPGRADE_LEVEL)
            {
                return $"Baldur can't improve it further. <color=#F6D578>+{level} {stat}</color>";
            }
            return level == 0
                ? $"Permanent +1 {stat}."
                : $"Now <color=#F6D578>+{level} {stat}</color>. Next level: +{level + 1} {stat}.";
        }

        private static string GetBuyTitle(ItemSO item, int owned)
        {
            switch (item.ItemID)
            {
                case ShopManager.SHARPENED_BLADE_ID: return "Sharpen Blade";
                case ShopManager.RUNIC_ARMOR_ID: return "Reinforce Armor";
            }
            return owned > 0 ? $"{item.ItemName}  <color=#A6B3C7><size=80%>(you have {owned})</size></color>" : item.ItemName;
        }

        #endregion

        #region Economy Actions

        /// <summary>
        /// Converts all available scrap metal pieces to gold coins at 1 Scrap = 10 Gold.
        /// </summary>
        public void SellAllScrap()
        {
            int gold = 0;
            int scrapBefore = InventoryManager.Instance != null ? ShopManager.SellableScrap(InventoryManager.Instance.ScrapMetalCount) : 0;
            ShopManager sm = ShopManager.Instance;
            if (sm != null)
            {
                gold = sm.ConvertScrapToGold(-1);
            }

            if (gold > 0)
            {
                ShowFeedback($"Baldur melts down {scrapBefore} scrap: <b>+{gold} gold</b>.", true);
                PlayShopSound(SFXClipType.SwordHit, 0.8f);
            }
            else
            {
                ShowFeedback("You have no scrap to sell. Fights drop 2-10 scrap.", false);
            }

            RefreshEconomyDisplay();
            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        /// <summary>Buys a small health potion.</summary>
        public void BuyPotion() => BuyStockItem(ResolveItem(ShopManager.SMALL_POTION_ID));

        /// <summary>Buys the next blade level (+1 permanent DMG).</summary>
        public void BuyWeapon() => BuyStockItem(ResolveItem(ShopManager.SHARPENED_BLADE_ID));

        /// <summary>Buys the next armor level (+1 permanent AC).</summary>
        public void BuyArmor() => BuyStockItem(ResolveItem(ShopManager.RUNIC_ARMOR_ID));

        /// <summary>
        /// Buys one item, then reports the outcome on the feedback line with a sound.
        /// </summary>
        public void BuyStockItem(ItemSO item)
        {
            ShopManager sm = ShopManager.Instance;
            if (sm == null || item == null) return;

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            int price = ShopManager.GetBuyPrice(item, player);
            ShopPurchaseResult result = sm.TryBuyItem(item, player);

            switch (result)
            {
                case ShopPurchaseResult.Success:
                    if (ShopManager.IsUpgrade(item))
                    {
                        int level = ShopManager.GetUpgradeLevel(item, player);
                        string what = item.ItemType == ItemType.WeaponUpgrade ? $"Your blade now deals +{level} DMG" : $"Your armor now gives +{level} AC";
                        ShowFeedback($"<b>Clang!</b> {what}. (-{price}g)", true);
                        PlayShopSound(SFXClipType.SwordHit);
                    }
                    else
                    {
                        ShowFeedback($"Bought <b>{item.ItemName}</b> for {price} gold.", true);
                        PlayShopSound(SFXClipType.ButtonClick);
                    }
                    break;

                case ShopPurchaseResult.NotEnoughGold:
                    int gold = InventoryManager.Instance != null ? InventoryManager.Instance.CurrentGold : 0;
                    ShowFeedback($"Not enough gold. {item.ItemName} costs {price}g, you need {price - gold}g more.", false);
                    PlayShopSound(SFXClipType.CriticalFailure, 0.5f);
                    break;

                case ShopPurchaseResult.MaxLevel:
                    ShowFeedback("\"That's as fine as steel gets, friend.\"", false);
                    break;
            }

            RefreshEconomyDisplay();
        }

        /// <summary>
        /// Sells one of the item back to Baldur.
        /// </summary>
        public void SellStockItem(ItemSO item)
        {
            ShopManager sm = ShopManager.Instance;
            if (sm == null || item == null) return;

            if (sm.SellItem(item))
            {
                ShowFeedback($"Sold <b>{item.ItemName}</b> for {item.SellPriceGold} gold.", true);
                PlayShopSound(SFXClipType.ButtonClick);
            }

            RefreshEconomyDisplay();
        }

        #endregion

        #region Refresh Display

        /// <summary>
        /// Synchronizes the gold and scrap badges, the tab highlight and every row on the active tab.
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

            RefreshTabVisuals();

            int visibleRows = showingSellTab
                ? RefreshSellRows(inventory, currentScrap)
                : RefreshBuyRows(inventory, currentGold);

            // Hide the other tab's rows
            SetRowsActive(buyRows, !showingSellTab);
            if (scrapRow != null && scrapRow.Root != null) scrapRow.Root.SetActive(showingSellTab);
            if (sellEmptyHint != null)
            {
                bool showHint = showingSellTab && sellableBuffer.Count == 0;
                sellEmptyHint.gameObject.SetActive(showHint);
                if (showHint) sellEmptyHint.transform.SetAsLastSibling();
            }
            if (!showingSellTab) SetRowsActive(sellRows, false);

            ApplyRowHeights(visibleRows);
        }

        private int RefreshBuyRows(InventoryManager inventory, int currentGold)
        {
            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            int visible = 0;

            for (int i = 0; i < buyRows.Count; i++)
            {
                ShopRow row = buyRows[i];
                if (row.Item == null) row.Item = ResolveItem(row.Key);

                bool available = row.Item != null;
                row.Root.SetActive(available);
                if (!available) continue;
                visible++;

                ItemSO item = row.Item;
                bool isUpgrade = ShopManager.IsUpgrade(item);
                int level = isUpgrade ? ShopManager.GetUpgradeLevel(item, player) : 0;
                int price = ShopManager.GetBuyPrice(item, player);
                bool maxed = price < 0;
                bool affordable = !maxed && currentGold >= price;
                int owned = !isUpgrade && inventory != null ? inventory.GetItemCount(item) : 0;

                ApplyIcon(row);
                if (row.Title != null) row.Title.text = GetBuyTitle(item, owned);
                if (row.Desc != null) row.Desc.text = GetBuyDescription(item, level);
                if (row.Background != null) row.Background.color = maxed ? RowTintMaxed : RowTint;

                if (row.Pips != null)
                {
                    for (int p = 0; p < row.Pips.Length; p++)
                    {
                        row.Pips[p].color = p < level ? PipFilled : PipEmpty;
                    }
                }

                if (row.ButtonLabel != null)
                {
                    row.ButtonLabel.text = maxed ? "Max Level" : (isUpgrade ? $"Upgrade  {price}g" : $"Buy  {price}g");
                    row.ButtonLabel.color = maxed ? MutedText : (affordable ? PriceGold : PriceTooHigh);
                }

                // Unaffordable stays clickable so the player is told how much gold is missing
                if (row.Button != null) row.Button.interactable = !maxed;
            }

            return visible;
        }

        private int RefreshSellRows(InventoryManager inventory, int currentScrap)
        {
            int visible = 0;

            if (scrapRow != null)
            {
                visible++;
                int sellable = ShopManager.SellableScrap(currentScrap);
                int kept = currentScrap - sellable;
                int potentialGold = sellable * ShopManager.SCRAP_TO_GOLD_RATE;
                if (scrapRow.Title != null) scrapRow.Title.text = currentScrap > 0 ? $"Scrap Metal  <color=#A6B3C7><size=80%>(you have {currentScrap})</size></color>" : "Scrap Metal";
                if (scrapRow.Desc != null)
                {
                    scrapRow.Desc.text = kept > 0
                        ? $"Baldur pays {ShopManager.SCRAP_TO_GOLD_RATE} gold per piece. {kept} kept for his scrap request."
                        : $"Baldur pays {ShopManager.SCRAP_TO_GOLD_RATE} gold per piece. Fights drop 2-10 scrap.";
                }
                if (scrapRow.ButtonLabel != null)
                {
                    scrapRow.ButtonLabel.text = sellable > 0 ? $"Sell {sellable}  +{potentialGold}g" : (kept > 0 ? "Kept for request" : "No Scrap");
                    scrapRow.ButtonLabel.color = sellable > 0 ? PriceGold : MutedText;
                }
                if (scrapRow.Button != null) scrapRow.Button.interactable = sellable > 0;
                if (scrapRow.Background != null) scrapRow.Background.color = RowTint;
            }

            sellableBuffer.Clear();
            if (inventory != null)
            {
                foreach (KeyValuePair<ItemSO, int> entry in inventory.Items)
                {
                    if (entry.Value > 0 && ShopManager.IsSellable(entry.Key) && sellableBuffer.Count < MaxSellRows - 1)
                    {
                        sellableBuffer.Add(entry.Key);
                    }
                }
            }
            sellableBuffer.Sort(CompareItemsByName);

            for (int i = 0; i < sellableBuffer.Count; i++)
            {
                ShopRow row = GetSellRow(i);
                ItemSO item = sellableBuffer[i];
                row.Item = item;
                row.Key = item.ItemID;
                row.Root.SetActive(true);
                row.Root.transform.SetSiblingIndex(buyRows.Count + 1 + i);
                visible++;

                int owned = inventory.GetItemCount(item);
                ApplyIcon(row);
                if (row.Title != null) row.Title.text = $"{item.ItemName}  <color=#A6B3C7><size=80%>(you have {owned})</size></color>";
                if (row.Desc != null) row.Desc.text = $"Baldur buys it for {item.SellPriceGold} gold each.";
                if (row.Background != null) row.Background.color = RowTint;
                if (row.ButtonLabel != null)
                {
                    row.ButtonLabel.text = $"Sell  +{item.SellPriceGold}g";
                    row.ButtonLabel.color = PriceGold;
                }
                if (row.Button != null) row.Button.interactable = true;
            }

            for (int i = sellableBuffer.Count; i < sellRows.Count; i++)
            {
                sellRows[i].Item = null;
                sellRows[i].Root.SetActive(false);
            }

            return visible;
        }

        private static int CompareItemsByName(ItemSO a, ItemSO b)
        {
            return string.CompareOrdinal(a.ItemName, b.ItemName);
        }

        private static void SetRowsActive(List<ShopRow> rows, bool active)
        {
            if (active) return; // visible rows are switched on individually during refresh
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Root != null) rows[i].Root.SetActive(false);
            }
        }

        /// <summary>
        /// Shrinks rows when the tab holds more than fits at full height.
        /// </summary>
        private void ApplyRowHeights(int visibleRows)
        {
            if (shopPanel == null || visibleRows <= 0) return;

            RectTransform panelRect = shopPanel.GetComponent<RectTransform>();
            float panelHeight = panelRect != null ? panelRect.sizeDelta.y : 740f;
            float shelfHeight = panelHeight - ShelfTopOffset - ShelfBottomOffset;
            float height = Mathf.Clamp((shelfHeight - RowSpacing * (visibleRows - 1)) / visibleRows, MinRowHeight, MaxRowHeight);

            ApplyHeight(buyRows, height);
            ApplyHeight(sellRows, height);
            if (scrapRow != null) ApplyHeight(scrapRow, height);
        }

        private static void ApplyHeight(List<ShopRow> rows, float height)
        {
            for (int i = 0; i < rows.Count; i++) ApplyHeight(rows[i], height);
        }

        private static void ApplyHeight(ShopRow row, float height)
        {
            if (row.Root == null) return;
            LayoutElement le = row.Root.GetComponent<LayoutElement>();
            if (le == null) return;
            le.minHeight = height;
            le.preferredHeight = height;
        }

        #endregion

        #region Event Handlers

        private void HandleGoldChanged(int newGold) => RefreshIfOpen();
        private void HandleScrapMetalChanged(int newScrap) => RefreshIfOpen();
        private void HandleScrapConverted(int scrap, int gold) => RefreshIfOpen();
        private void HandleItemPurchased(ItemSO item) => RefreshIfOpen();
        private void HandleItemSold(ItemSO item) => RefreshIfOpen();
        private void HandleInventoryChanged() => RefreshIfOpen();
        private void HandleRerollScrollsChanged(int count) => RefreshIfOpen();

        // Gold and items change during fights too; only redraw the shelf while the shop is on screen
        private void RefreshIfOpen()
        {
            if (shopPanel != null && shopPanel.activeInHierarchy)
            {
                RefreshEconomyDisplay();
            }
        }

        #endregion
    }
}
