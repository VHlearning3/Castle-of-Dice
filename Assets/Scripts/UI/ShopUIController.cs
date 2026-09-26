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
    /// User Interface Controller for Blacksmith Baldur's Shop in Oakhaven / Kivenkolo Village.
    /// Displays current gold and scrap metal resources in ornate tabletop D&D pill badges,
    /// presents stock items in styled rows with framed item image slots, and handles one-click purchases.
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

        #region Theme Sprites & Auto-Discovery

        public void LoadThemeSpritesIfMissing()
        {
#if UNITY_EDITOR
            if (panelDarkSprite == null)
                panelDarkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (pillBadgeSprite == null)
                pillBadgeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            if (buttonHoverSprite == null)
                buttonHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            if (buttonPressedSprite == null)
                buttonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");

            if (coinSprite == null)
                coinSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONSART/CoinIcon.png");
            if (potionSprite == null)
                potionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONSART/HealthPotionIcon.png");
            if (swordIconSprite == null)
                swordIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_Sword.png");
            if (shieldIconSprite == null)
                shieldIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_Shield.png");
            if (scrapOreSprite == null)
                scrapOreSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_ScrapOre.png");

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
                panelRect.sizeDelta = new Vector2(760f, 560f);
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
                tText.color = new Color(0.965f, 0.835f, 0.47f, 1f);
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
                subText.text = "Blacksmith of Kivenkolo Village • Arms, Armor & Field Supplies";
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
                xText.color = new Color(0.965f, 0.835f, 0.47f, 1f);
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
                goldBalanceText.color = new Color(1f, 0.84f, 0f, 1f); // #FFD700
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

            // G. Stock Items Container (4 Distinct Rows)
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
            shRect.offsetMin = new Vector2(32f, 72f);
            shRect.offsetMax = new Vector2(-32f, -135f);

            VerticalLayoutGroup vlg = shelfObj.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = shelfObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Setup each of the 4 item rows with framed item image slots
            SetupItemRow(shelfObj.transform, "Row_Potion", "Small Health Potion", "Restores 15 Hit Points instantly.", potionSprite, ref buyPotionButton, ref potionIconImage, "Buy (15g)");
            SetupItemRow(shelfObj.transform, "Row_Sword", "Sharpen Blade (+1 DMG)", "Hones steel edge for permanent +1 attack damage.", swordIconSprite, ref buyWeaponButton, ref weaponIconImage, "Upgrade (50g)");
            SetupItemRow(shelfObj.transform, "Row_Shield", "Reinforce Shield (+1 AC)", "Tempers runic armor for permanent +1 Armor Class.", shieldIconSprite, ref buyArmorButton, ref armorIconImage, "Upgrade (50g)");
            SetupItemRow(shelfObj.transform, "Row_Scrap", "Scrap Metal Salvage", "Sell recovered ruin metal to Baldur at 10 Gold each.", scrapOreSprite, ref sellAllScrapButton, ref scrapActionIconImage, "Sell All (+50g)");

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
            bdRect.anchoredPosition = new Vector2(0f, 62f);
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
                    exTxt.color = new Color(0.93f, 0.90f, 0.85f, 1f);
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
                titleTxt.color = new Color(0.93f, 0.90f, 0.85f, 1f);
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
                _ => "Action_Button"
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

            if (closeCornerButton != null)
            {
                closeCornerButton.onClick.RemoveListener(CloseShop);
                closeCornerButton.onClick.AddListener(CloseShop);
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
                    ? $"Sell All (+{potentialGold}g)"
                    : "No Scrap";
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
