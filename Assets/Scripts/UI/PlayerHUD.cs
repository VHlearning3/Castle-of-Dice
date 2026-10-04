using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Data;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Professional tabletop D&D adventure Heads-Up Display (HUD).
    /// Pinned to the screen during exploration and tactical combat.
    /// Manages:
    /// 1. Top-Left Hero Status Card: Crest emblem, name, class/level, Armor Class badge,
    ///    ruby health vitality bar with smooth animation & damage flash,
    ///    integrated Quick Potion slot with count badge & [Q] hotkey, Gold Purse pill,
    ///    and the hero stats block (to-hit, weapon, AC, move, effects) from CombatStatsHUD.
    /// 2. Top-Center Zone & Campaign Banner: Shows active atmospheric location, with the
    ///    CombatStatsHUD enemy cards below it while a battle is running.
    /// 3. Top-Right Quest Tracker Card: Displays active quest name, objectives, and hint steps.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        #region Singleton

        private static PlayerHUD instance;

        public static PlayerHUD Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include);
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Serialized Fields - Core HUD

        [Header("Health Bar (Hero Vitality)")]
        [Tooltip("Slider displaying the hero's current hit point percentage.")]
        [SerializeField] private Slider healthSlider;

        [Tooltip("Text display formatted as 'HP: 25 / 30'.")]
        [SerializeField] private TMP_Text healthText;

        [Tooltip("Image component used for health bar fill (for damage flash & color lerp).")]
        [SerializeField] private Image sliderFillImage;

        [Header("Hero Identity & Stats")]
        [Tooltip("Text display for Hero Name (e.g. 'Sir Roland').")]
        [SerializeField] private TMP_Text heroNameText;

        [Tooltip("Text display for Hero Class & Level (e.g. 'Warrior • Level 1').")]
        [SerializeField] private TMP_Text heroClassText;

        [Tooltip("Text display for Armor Class badge (e.g. 'AC 14').")]
        [SerializeField] private TMP_Text heroACText;

        [Tooltip("Image display for Hero Class Crest emblem.")]
        [SerializeField] private Image heroCrestImage;

        [Tooltip("Legacy text or icon display for Hero Class Crest (kept for compatibility).")]
        [SerializeField] private TMP_Text heroCrestText;

        [Header("Gold Counter (Purse)")]
        [Tooltip("Text display indicating available gold coins in the player's pouch.")]
        [SerializeField] private TMP_Text goldCounterText;

        [Tooltip("Image holding the authentic user-provided CoinIcon.png.")]
        [SerializeField] private Image coinIconImage;

        [Header("Scrap Metal Counter (MasterSpec §6)")]
        [Tooltip("Text display indicating available scrap metal pieces in the player's pack (e.g. '8 kpl').")]
        [SerializeField] private TMP_Text scrapCounterText;

        [Tooltip("Image displaying the authentic UI_Icon_ScrapOre.png.")]
        [SerializeField] private Image scrapIconImage;

        [Tooltip("Sprite asset for the scrap ore icon in HUD.")]
        [SerializeField] private Sprite scrapOreSprite;

        [Header("Quick Potion Hotbar")]
        [Tooltip("Button allowing immediate consumption of a Health Potion.")]
        [SerializeField] private Button quickPotionButton;

        [Tooltip("Label displaying the number of remaining potions (e.g., 'x2').")]
        [SerializeField] private TMP_Text potionCountText;

        [Tooltip("Image holding the authentic user-provided HealthPotionIcon.png.")]
        [SerializeField] private Image potionIconImage;

        [Tooltip("Potion item definition used for quick consumption.")]
        [SerializeField] private ItemSO healthPotionItem;

        [Header("Active Quest Tracker")]
        [Tooltip("Text component displaying active quest title and objective count (e.g. 'Cellar Rats: 2/3').")]
        [SerializeField] private TMP_Text activeQuestSummaryText;

        [Tooltip("Text component displaying quest header (e.g. 'QUEST OBJECTIVES').")]
        [SerializeField] private TMP_Text questHeaderText;

        [Header("Zone & Location Banner")]
        [Tooltip("Text component displaying current atmospheric zone name (e.g. 'Oakhaven Village').")]
        [SerializeField] private TMP_Text zoneTitleText;

        [Tooltip("Text component displaying campaign chapter or subtitle.")]
        [SerializeField] private TMP_Text zoneSubtitleText;

        [Header("Fantasy UI Theme Sprites")]
        [SerializeField] private Sprite panelDarkSprite;
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite barTrackSprite;
        [SerializeField] private Sprite barFillRubySprite;
        [SerializeField] private Sprite pillBadgeSprite;
        [SerializeField] private Sprite dividerGoldSprite;
        [SerializeField] private Sprite crestPlateSprite;
        [SerializeField] private Sprite crestWarriorSprite;
        [SerializeField] private Sprite crestMageSprite;
        [SerializeField] private Sprite crestRogueSprite;

        [Tooltip("The user-provided CoinIcon.png shown in the gold purse.")]
        [SerializeField] private Sprite coinSprite;

        [Tooltip("The user-provided HealthPotionIcon.png shown in the bottom-left flask slots.")]
        [SerializeField] private Sprite potionSprite;

        #endregion

        #region Layout Constants

        /// <summary>Gold purse position inside the Hero_Status_Card: top-right, beside the hero's name.</summary>
        public static readonly Vector2 GoldPursePosition = new Vector2(300f, -12f);
        public static readonly Vector2 GoldPurseSize = new Vector2(146f, 32f);

        /// <summary>Right edge of the MAP (M) button, measured from the screen's right edge, left of the quest card.</summary>
        public const float MapButtonRightEdge = -436f;

        #endregion

        #region Private State

        private PlayerUnit trackedPlayer;
        private CombatStatsHUD combatStatsHUD;
        private float targetHPValue;
        private float hpLerpSpeed = 8f;
        private Color normalRubyColor = new Color(0.85f, 0.18f, 0.15f, 1f);
        private Color damageFlashColor = new Color(1.0f, 0.45f, 0.40f, 1f);
        private float damageFlashTimer = 0f;
        private int lastSeenHP = -1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // Back in the village the scene brings a second copy of the whole persistent Canvas (HUD,
                // map, dialogue, shop...). Drop all of it, not just this HUD, so no stale panel ever draws on
                // top of the live UI (Destroy lands before the frame renders).
                GameObject duplicateRoot = transform.root.gameObject;
                if (duplicateRoot != instance.transform.root.gameObject)
                {
                    Destroy(duplicateRoot);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }

            instance = this;
            if (transform.root != null)
            {
                DontDestroyOnLoad(transform.root.gameObject);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }

            LoadThemeSpritesIfMissing();
            AutoLocateComponents();
            EnsureStyledHierarchy();

            if (quickPotionButton != null)
            {
                quickPotionButton.onClick.RemoveListener(OnQuickPotionClicked);
                quickPotionButton.onClick.AddListener(OnQuickPotionClicked);
            }
        }

        private void OnEnable()
        {
            InventoryManager.OnGoldChanged += HandleGoldChanged;
            InventoryManager.OnScrapMetalChanged += HandleScrapMetalChanged;
            InventoryManager.OnInventoryChanged += HandleInventoryChanged;
            QuestManager.OnQuestProgressUpdated += HandleQuestProgressUpdated;
            QuestManager.OnQuestStateUpdated += HandleQuestStateUpdated;
            GameManager.OnLocationChanged += HandleLocationChanged;

            LocatePlayer();
            RefreshAllHUD();
        }

        private void OnDisable()
        {
            InventoryManager.OnGoldChanged -= HandleGoldChanged;
            InventoryManager.OnScrapMetalChanged -= HandleScrapMetalChanged;
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
            QuestManager.OnQuestProgressUpdated -= HandleQuestProgressUpdated;
            QuestManager.OnQuestStateUpdated -= HandleQuestStateUpdated;
            GameManager.OnLocationChanged -= HandleLocationChanged;

            if (trackedPlayer != null)
            {
                trackedPlayer.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (quickPotionButton != null)
            {
                quickPotionButton.onClick.RemoveListener(OnQuickPotionClicked);
            }
        }

        private void Start()
        {
            RefreshAllHUD();
        }

        private void Update()
        {
            // 1. Smoothly interpolate health slider value
            if (healthSlider != null && Mathf.Abs(healthSlider.value - targetHPValue) > 0.05f)
            {
                healthSlider.value = Mathf.MoveTowards(healthSlider.value, targetHPValue, Time.deltaTime * hpLerpSpeed * Mathf.Max(1f, healthSlider.maxValue));
            }

            // 2. Handle health damage flash decay
            if (damageFlashTimer > 0f)
            {
                damageFlashTimer -= Time.deltaTime * 3.5f;
                if (sliderFillImage != null)
                {
                    sliderFillImage.color = Color.Lerp(normalRubyColor, damageFlashColor, damageFlashTimer);
                }
            }

            // 3. Low health warning subtle pulse (HP < 25%)
            if (trackedPlayer != null && trackedPlayer.MaxHP > 0 && damageFlashTimer <= 0f)
            {
                float hpRatio = (float)trackedPlayer.CurrentHP / trackedPlayer.MaxHP;
                if (hpRatio <= 0.25f && hpRatio > 0f && sliderFillImage != null)
                {
                    float pulse = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
                    sliderFillImage.color = Color.Lerp(normalRubyColor, new Color(1f, 0.25f, 0.25f, 1f), pulse * 0.6f);
                }
            }

            // 4. Quick Potion Hotkey [Q] (exploration and combat; never while talking or shopping)
            if (GameInput.IsQuickPotionHotkeyPressed())
            {
                GameManager gm = GameManager.Instance;
                if (gm == null || gm.CurrentMode == GamePlayMode.Exploration || gm.CurrentMode == GamePlayMode.Combat)
                {
                    OnQuickPotionClicked();
                }
            }
        }

        #endregion

        #region Theme Sprites Loader

        public void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (barTrackSprite == null)
                barTrackSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Bar_Track.png");
            if (barFillRubySprite == null)
                barFillRubySprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Bar_Fill_Ruby.png");
            if (pillBadgeSprite == null)
                pillBadgeSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (crestPlateSprite == null)
                crestPlateSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Plate.png");
            if (crestWarriorSprite == null)
                crestWarriorSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Warrior.png");
            if (crestMageSprite == null)
                crestMageSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Mage.png");
            if (crestRogueSprite == null)
                crestRogueSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Rogue.png");

            if (scrapOreSprite == null)
                scrapOreSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_ScrapOre.png");
            if (coinSprite == null)
                coinSprite = UITheme.GetSprite("Assets/ICONSART/CoinIcon.png");
            if (potionSprite == null)
                potionSprite = UITheme.GetSprite("Assets/ICONSART/HealthPotionIcon.png");

#if UNITY_EDITOR
            if (healthPotionItem == null)
                healthPotionItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_Potion_Health.asset");
#endif
        }

        #endregion

        #region Auto-Locate Setup

        private void AutoLocateComponents()
        {
            // Auto-locate slider & fill
            if (healthSlider == null)
            {
                healthSlider = GetComponentInChildren<Slider>(true);
            }
            if (healthSlider != null && sliderFillImage == null)
            {
                Image[] imgs = healthSlider.GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    if (img.name.ToLowerInvariant().Contains("fill"))
                    {
                        sliderFillImage = img;
                        break;
                    }
                }
            }

            // Auto-locate texts
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in texts)
            {
                string lower = txt.name.ToLowerInvariant();
                if (healthText == null && (lower.Contains("hp") || lower.Contains("health")))
                {
                    healthText = txt;
                }
                else if (goldCounterText == null && (lower.Contains("gold") || lower.Contains("money") || lower.Contains("coin")))
                {
                    goldCounterText = txt;
                }
                else if (scrapCounterText == null && (lower.Contains("scrap") || lower.Contains("romu") || lower.Contains("ore")))
                {
                    scrapCounterText = txt;
                }
                else if (potionCountText == null && (lower.Contains("potioncount") || lower.Contains("count") || lower.Contains("qty")))
                {
                    potionCountText = txt;
                }
                else if (activeQuestSummaryText == null && (lower.Contains("quest") || lower.Contains("objective") || lower.Contains("tracker") || lower.Contains("summary")))
                {
                    activeQuestSummaryText = txt;
                }
                else if (heroNameText == null && lower.Contains("heroname"))
                {
                    heroNameText = txt;
                }
                else if (heroClassText == null && lower.Contains("heroclass"))
                {
                    heroClassText = txt;
                }
                else if (heroACText == null && lower.Contains("heroac"))
                {
                    heroACText = txt;
                }
                else if (zoneTitleText == null && lower.Contains("zonetitle"))
                {
                    zoneTitleText = txt;
                }
                else if (zoneSubtitleText == null && lower.Contains("zonesubtitle"))
                {
                    zoneSubtitleText = txt;
                }
            }

            // Auto-locate quick potion button
            if (quickPotionButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    string lower = btn.name.ToLowerInvariant();
                    if (lower.Contains("potion") || lower.Contains("heal") || lower.Contains("hotbar"))
                    {
                        quickPotionButton = btn;
                        break;
                    }
                }
            }
        }

        #endregion

        #region Hierarchy Construction & Professional Styling

        /// <summary>
        /// Restructures and styles the HUD components into an ornate, responsive tabletop D&D layout:
        /// 1. Top-Left Hero Vitality Card (Class crest, Hero Name, Level, AC, Ruby HP Bar, Quick Potion slot, Gold Purse).
        /// 2. Top-Center Zone Atmosphere Banner (Current location indicator).
        /// 3. Top-Right Quest Log Card (Objectives and guidance).
        /// Strictly preserves the user's authentic CoinIcon.png and HealthPotionIcon.png assets.
        /// </summary>
        public void EnsureStyledHierarchy()
        {
            LoadThemeSpritesIfMissing();

            RectTransform hudRect = GetComponent<RectTransform>();
            if (hudRect != null)
            {
                hudRect.anchorMin = new Vector2(0f, 1f);
                hudRect.anchorMax = new Vector2(1f, 1f);
                hudRect.pivot = new Vector2(0.5f, 1f);
                hudRect.anchoredPosition = Vector2.zero;
                hudRect.sizeDelta = new Vector2(0f, 180f);
            }

            // ========================================================
            // 1. TOP-LEFT: HERO STATUS CARD
            // ========================================================
            Transform heroCardTr = transform.Find("Hero_Status_Card");
            GameObject heroCardObj;
            if (heroCardTr == null)
            {
                heroCardObj = new GameObject("Hero_Status_Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                heroCardObj.transform.SetParent(transform, false);
            }
            else
            {
                heroCardObj = heroCardTr.gameObject;
            }

            RectTransform heroCardRect = heroCardObj.GetComponent<RectTransform>();
            heroCardRect.anchorMin = new Vector2(0f, 1f);
            heroCardRect.anchorMax = new Vector2(0f, 1f);
            heroCardRect.pivot = new Vector2(0f, 1f);
            heroCardRect.anchoredPosition = new Vector2(24f, -18f);
            heroCardRect.sizeDelta = new Vector2(460f, CombatStatsHUD.HeroCardHeight);

            Image heroCardBg = heroCardObj.GetComponent<Image>();
            if (panelDarkSprite != null)
            {
                heroCardBg.sprite = panelDarkSprite;
                heroCardBg.type = Image.Type.Sliced;
                heroCardBg.color = Color.white;
            }
            else
            {
                heroCardBg.color = new Color(0.08f, 0.10f, 0.15f, 0.94f);
            }

            // 1A. Hero Class Crest / Portrait Box (Heraldic Image Crest)
            Transform crestTr = heroCardObj.transform.Find("Hero_Crest_Box");
            GameObject crestObj = crestTr != null ? crestTr.gameObject : new GameObject("Hero_Crest_Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            crestObj.transform.SetParent(heroCardObj.transform, false);
            RectTransform crestRect = crestObj.GetComponent<RectTransform>();
            crestRect.anchorMin = new Vector2(0f, 1f);
            crestRect.anchorMax = new Vector2(0f, 1f);
            crestRect.pivot = new Vector2(0f, 1f);
            crestRect.anchoredPosition = new Vector2(16f, -14f);
            crestRect.sizeDelta = new Vector2(62f, 62f);
            heroCrestImage = crestObj.GetComponent<Image>();
            if (crestWarriorSprite != null)
            {
                heroCrestImage.sprite = crestWarriorSprite;
                heroCrestImage.type = Image.Type.Simple;
                heroCrestImage.preserveAspect = true;
            }
            else if (crestPlateSprite != null)
            {
                heroCrestImage.sprite = crestPlateSprite;
                heroCrestImage.type = Image.Type.Sliced;
            }

            // Hide or clear legacy text icon to prevent missing glyph warnings
            Transform crestIconTr = crestObj.transform.Find("Crest_Icon_Text");
            if (crestIconTr != null)
            {
                heroCrestText = crestIconTr.GetComponent<TMP_Text>();
                if (heroCrestText != null)
                {
                    heroCrestText.text = "";
                }
                crestIconTr.gameObject.SetActive(false);
            }

            // 1B. Hero Armor Class Badge
            Transform acBadgeTr = heroCardObj.transform.Find("Hero_AC_Badge");
            GameObject acBadgeObj = acBadgeTr != null ? acBadgeTr.gameObject : new GameObject("Hero_AC_Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            acBadgeObj.transform.SetParent(heroCardObj.transform, false);
            RectTransform acBadgeRect = acBadgeObj.GetComponent<RectTransform>();
            acBadgeRect.anchorMin = new Vector2(0f, 1f);
            acBadgeRect.anchorMax = new Vector2(0f, 1f);
            acBadgeRect.pivot = new Vector2(0f, 1f);
            acBadgeRect.anchoredPosition = new Vector2(14f, -84f);
            acBadgeRect.sizeDelta = new Vector2(66f, 26f);
            Image acBadgeBg = acBadgeObj.GetComponent<Image>();
            if (pillBadgeSprite != null)
            {
                acBadgeBg.sprite = pillBadgeSprite;
                acBadgeBg.type = Image.Type.Sliced;
            }

            Transform acTxtTr = acBadgeObj.transform.Find("Hero_AC_Text");
            GameObject acTxtObj = acTxtTr != null ? acTxtTr.gameObject : new GameObject("Hero_AC_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            acTxtObj.transform.SetParent(acBadgeObj.transform, false);
            heroACText = acTxtObj.GetComponent<TMP_Text>();
            heroACText.text = "AC --";
            heroACText.fontSize = 11.5f;
            heroACText.fontStyle = FontStyles.Bold;
            heroACText.alignment = TextAlignmentOptions.Center;
            heroACText.color = new Color(0.96f, 0.85f, 0.50f, 1f); // Warm gold
            RectTransform acTxtRect = acTxtObj.GetComponent<RectTransform>();
            acTxtRect.anchorMin = Vector2.zero;
            acTxtRect.anchorMax = Vector2.one;
            acTxtRect.sizeDelta = Vector2.zero;

            // 1C. Hero Identity Header (Name & Class)
            Transform nameTr = heroCardObj.transform.Find("Hero_Name_Text");
            GameObject nameObj = nameTr != null ? nameTr.gameObject : new GameObject("Hero_Name_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(heroCardObj.transform, false);
            heroNameText = nameObj.GetComponent<TMP_Text>();
            heroNameText.text = "Sir Roland";
            heroNameText.fontSize = 17f;
            heroNameText.fontStyle = FontStyles.Bold;
            heroNameText.color = new Color(1.0f, 0.96f, 0.88f, 1f); // Antique parchment
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(92f, -14f);
            nameRect.sizeDelta = new Vector2(GoldPursePosition.x - 92f - 6f, 22f); // gold purse sits to the right
            heroNameText.overflowMode = TextOverflowModes.Ellipsis;

            Transform classTr = heroCardObj.transform.Find("Hero_Class_Text");
            GameObject classObj = classTr != null ? classTr.gameObject : new GameObject("Hero_Class_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            classObj.transform.SetParent(heroCardObj.transform, false);
            heroClassText = classObj.GetComponent<TMP_Text>();
            heroClassText.text = "Warrior • Level 1";
            heroClassText.fontSize = 11.5f;
            heroClassText.color = new Color(0.82f, 0.68f, 0.35f, 1f); // Warm gold
            RectTransform classRect = classObj.GetComponent<RectTransform>();
            classRect.anchorMin = new Vector2(0f, 1f);
            classRect.anchorMax = new Vector2(0f, 1f);
            classRect.pivot = new Vector2(0f, 1f);
            classRect.anchoredPosition = new Vector2(92f, -34f);
            classRect.sizeDelta = new Vector2(GoldPursePosition.x - 92f - 6f, 18f);

            // 1D. Health Bar Slider
            Transform hpContainer = transform.Find("HP_Container") ?? heroCardObj.transform.Find("HP_Container");
            if (hpContainer != null)
            {
                hpContainer.SetParent(heroCardObj.transform, false);
                RectTransform hpRect = hpContainer.GetComponent<RectTransform>();
                hpRect.anchorMin = new Vector2(0f, 1f);
                hpRect.anchorMax = new Vector2(0f, 1f);
                hpRect.pivot = new Vector2(0f, 1f);
                hpRect.anchoredPosition = new Vector2(92f, -54f);
                hpRect.sizeDelta = new Vector2(350f, 24f);

                // Style slider track and fill
                if (healthSlider != null)
                {
                    RectTransform slRect = healthSlider.GetComponent<RectTransform>();
                    slRect.anchorMin = Vector2.zero;
                    slRect.anchorMax = Vector2.one;
                    slRect.sizeDelta = Vector2.zero;
                    slRect.anchoredPosition = Vector2.zero;

                    Image trackImg = healthSlider.GetComponentInChildren<Image>(true);
                    if (trackImg != null && barTrackSprite != null)
                    {
                        trackImg.sprite = barTrackSprite;
                        trackImg.type = Image.Type.Sliced;
                        trackImg.color = Color.white;
                    }

                    if (sliderFillImage != null && barFillRubySprite != null)
                    {
                        sliderFillImage.sprite = barFillRubySprite;
                        sliderFillImage.type = Image.Type.Sliced;
                        sliderFillImage.color = normalRubyColor;
                    }
                }

                // Center HP text
                if (healthText != null)
                {
                    healthText.transform.SetParent(hpContainer, false);
                    RectTransform htRect = healthText.GetComponent<RectTransform>();
                    htRect.anchorMin = Vector2.zero;
                    htRect.anchorMax = Vector2.one;
                    htRect.sizeDelta = Vector2.zero;
                    htRect.anchoredPosition = Vector2.zero;
                    healthText.alignment = TextAlignmentOptions.Center;
                    healthText.fontSize = 12.5f;
                    healthText.fontStyle = FontStyles.Bold;
                    healthText.color = Color.white;
                }
            }

            // 1E. Quick Health Potion Slot Button
            Transform potionBtnTr = transform.Find("QuickPotion_Button") ?? heroCardObj.transform.Find("QuickPotion_Button");
            if (potionBtnTr != null)
            {
                potionBtnTr.SetParent(heroCardObj.transform, false);
                RectTransform pRect = potionBtnTr.GetComponent<RectTransform>();
                pRect.anchorMin = new Vector2(0f, 1f);
                pRect.anchorMax = new Vector2(0f, 1f);
                pRect.pivot = new Vector2(0f, 1f);
                pRect.anchoredPosition = new Vector2(92f, -86f);
                pRect.sizeDelta = new Vector2(38f, 38f);

                Image btnBg = potionBtnTr.GetComponent<Image>();
                if (btnBg != null && slotFrameSprite != null)
                {
                    btnBg.sprite = slotFrameSprite;
                    btnBg.type = Image.Type.Sliced;
                    btnBg.color = Color.white;
                }

                // Potion icon inside button (strictly preserve user's HealthPotionIcon.png)
                Transform iconTr = potionBtnTr.Find("Potion_Icon") ?? potionBtnTr.Find("Image");
                if (iconTr != null)
                {
                    potionIconImage = iconTr.GetComponent<Image>();
                    if (potionIconImage != null)
                    {
                        potionIconImage.preserveAspect = true;
                        potionIconImage.raycastTarget = false;
                        RectTransform iconRect = potionIconImage.GetComponent<RectTransform>();
                        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                        iconRect.pivot = new Vector2(0.5f, 0.5f);
                        iconRect.anchoredPosition = Vector2.zero;
                        iconRect.sizeDelta = new Vector2(28f, 28f);
                        iconRect.localScale = Vector3.one;
                    }
                }

                // Potion count badge
                Transform countBadgeTr = potionBtnTr.Find("Count_Badge");
                GameObject countBadgeObj = countBadgeTr != null ? countBadgeTr.gameObject : new GameObject("Count_Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                countBadgeObj.transform.SetParent(potionBtnTr, false);
                RectTransform cbRect = countBadgeObj.GetComponent<RectTransform>();
                cbRect.anchorMin = new Vector2(1f, 0f);
                cbRect.anchorMax = new Vector2(1f, 0f);
                cbRect.pivot = new Vector2(1f, 0f);
                cbRect.anchoredPosition = new Vector2(2f, -2f);
                cbRect.sizeDelta = new Vector2(22f, 16f);
                Image cbBg = countBadgeObj.GetComponent<Image>();
                if (pillBadgeSprite != null)
                {
                    cbBg.sprite = pillBadgeSprite;
                    cbBg.type = Image.Type.Sliced;
                }

                if (potionCountText != null)
                {
                    potionCountText.transform.SetParent(countBadgeObj.transform, false);
                    RectTransform pctRect = potionCountText.GetComponent<RectTransform>();
                    pctRect.anchorMin = Vector2.zero;
                    pctRect.anchorMax = Vector2.one;
                    pctRect.sizeDelta = Vector2.zero;
                    potionCountText.alignment = TextAlignmentOptions.Center;
                    potionCountText.fontSize = 10f;
                    potionCountText.fontStyle = FontStyles.Bold;
                    potionCountText.color = new Color(1.0f, 0.90f, 0.55f, 1f);
                }

                // Hotkey tag [Q]
                Transform qTagTr = potionBtnTr.Find("Hotkey_Tag");
                GameObject qTagObj = qTagTr != null ? qTagTr.gameObject : new GameObject("Hotkey_Tag", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                qTagObj.transform.SetParent(potionBtnTr, false);
                RectTransform qRect = qTagObj.GetComponent<RectTransform>();
                qRect.anchorMin = new Vector2(0f, 1f);
                qRect.anchorMax = new Vector2(0f, 1f);
                qRect.pivot = new Vector2(0f, 1f);
                qRect.anchoredPosition = new Vector2(-2f, 2f);
                qRect.sizeDelta = new Vector2(16f, 14f);
                TMP_Text qText = qTagObj.GetComponent<TMP_Text>();
                qText.text = "Q";
                qText.fontSize = 9f;
                qText.fontStyle = FontStyles.Bold;
                qText.alignment = TextAlignmentOptions.Center;
                qText.color = new Color(0.85f, 0.70f, 0.35f, 1f);
            }

            // 1F. Gold Purse (top-right of the card, beside the hero's name; built here when the scene has none)
            Transform goldContainer = transform.Find("Gold_Container") ?? heroCardObj.transform.Find("Gold_Container");
            if (goldContainer == null)
            {
                GameObject purseObj = new GameObject("Gold_Container", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                purseObj.transform.SetParent(heroCardObj.transform, false);
                goldContainer = purseObj.transform;

                GameObject coinObj = new GameObject("Gold_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                coinObj.transform.SetParent(goldContainer, false);
                coinObj.GetComponent<Image>().sprite = coinSprite;

                GameObject goldTextObj = new GameObject("Gold_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                goldTextObj.transform.SetParent(goldContainer, false);
                goldCounterText = goldTextObj.GetComponent<TMP_Text>();
                goldCounterText.textWrappingMode = TextWrappingModes.NoWrap;
                goldCounterText.text = "0 Gold";
            }
            if (goldContainer != null)
            {
                goldContainer.SetParent(heroCardObj.transform, false);
                RectTransform gRect = goldContainer.GetComponent<RectTransform>();
                gRect.anchorMin = new Vector2(0f, 1f);
                gRect.anchorMax = new Vector2(0f, 1f);
                gRect.pivot = new Vector2(0f, 1f);
                gRect.anchoredPosition = GoldPursePosition;
                gRect.sizeDelta = GoldPurseSize;

                Image gBg = goldContainer.GetComponent<Image>();
                if (gBg == null) gBg = goldContainer.gameObject.AddComponent<Image>();
                if (pillBadgeSprite != null)
                {
                    gBg.sprite = pillBadgeSprite;
                    gBg.type = Image.Type.Sliced;
                    gBg.color = Color.white;
                }

                // Find Coin Icon (supporting Gold_Icon, Coin_Icon, Image, or any child image)
                Transform coinImgTr = goldContainer.Find("Gold_Icon")
                    ?? goldContainer.Find("Coin_Icon")
                    ?? goldContainer.Find("Image");

                if (coinImgTr == null)
                {
                    Image[] childImgs = goldContainer.GetComponentsInChildren<Image>(true);
                    foreach (var ci in childImgs)
                    {
                        if (ci.gameObject != goldContainer.gameObject)
                        {
                            coinImgTr = ci.transform;
                            break;
                        }
                    }
                }

                if (coinImgTr != null)
                {
                    coinImgTr.name = "Gold_Icon";
                    coinIconImage = coinImgTr.GetComponent<Image>();
                    if (coinIconImage != null)
                    {
                        if (coinIconImage.sprite == null) coinIconImage.sprite = coinSprite;
                        coinIconImage.preserveAspect = true;
                        coinIconImage.raycastTarget = false;
                        RectTransform cRect = coinIconImage.GetComponent<RectTransform>();
                        cRect.anchorMin = new Vector2(0f, 0.5f);
                        cRect.anchorMax = new Vector2(0f, 0.5f);
                        cRect.pivot = new Vector2(0f, 0.5f);
                        cRect.anchoredPosition = new Vector2(8f, 0f);
                        cRect.sizeDelta = new Vector2(22f, 22f);
                        cRect.localScale = Vector3.one;
                    }
                }

                // Gold counter text (positioned cleanly to the right of the coin)
                if (goldCounterText == null)
                {
                    goldCounterText = goldContainer.GetComponentInChildren<TMP_Text>(true);
                }
                if (goldCounterText != null)
                {
                    goldCounterText.transform.SetParent(goldContainer, false);
                    RectTransform gtRect = goldCounterText.GetComponent<RectTransform>();
                    gtRect.anchorMin = new Vector2(0f, 0f);
                    gtRect.anchorMax = new Vector2(1f, 1f);
                    gtRect.pivot = new Vector2(0f, 0.5f);
                    gtRect.anchoredPosition = new Vector2(36f, 0f);
                    gtRect.sizeDelta = new Vector2(-42f, 0f);
                    gtRect.localScale = Vector3.one;
                    goldCounterText.alignment = TextAlignmentOptions.MidlineLeft;
                    goldCounterText.fontSize = 13f;
                    goldCounterText.fontStyle = FontStyles.Bold;
                    goldCounterText.color = new Color(0.98f, 0.82f, 0.20f, 1f); // Warm gold
                    goldCounterText.raycastTarget = false;
                }
            }

            // 1G. Scrap Metal Banner (MasterSpec Section 6 & UNITY_SETUP_GUIDE: Scrap Metal: 8 pcs)
            Transform scrapContainer = transform.Find("Scrap_Container") ?? heroCardObj.transform.Find("Scrap_Container");
            if (scrapContainer == null)
            {
                GameObject scObj = new GameObject("Scrap_Container", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                scObj.transform.SetParent(heroCardObj.transform, false);
                scrapContainer = scObj.transform;
            }
            else
            {
                scrapContainer.SetParent(heroCardObj.transform, false);
            }

            RectTransform scRect = scrapContainer.GetComponent<RectTransform>();
            scRect.anchorMin = new Vector2(0f, 1f);
            scRect.anchorMax = new Vector2(0f, 1f);
            scRect.pivot = new Vector2(0f, 1f);
            scRect.anchoredPosition = new Vector2(296f, -86f);
            scRect.sizeDelta = new Vector2(140f, 36f);

            Image scBg = scrapContainer.GetComponent<Image>();
            if (scBg == null) scBg = scrapContainer.gameObject.AddComponent<Image>();
            if (pillBadgeSprite != null)
            {
                scBg.sprite = pillBadgeSprite;
                scBg.type = Image.Type.Sliced;
                scBg.color = Color.white;
            }

            // Scrap Icon (using authentic UI_Icon_ScrapOre.png)
            Transform scrapImgTr = scrapContainer.Find("Scrap_Icon")
                ?? scrapContainer.Find("Ore_Icon")
                ?? scrapContainer.Find("Image");

            if (scrapImgTr == null)
            {
                GameObject sIconObj = new GameObject("Scrap_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sIconObj.transform.SetParent(scrapContainer, false);
                scrapImgTr = sIconObj.transform;
            }

            scrapImgTr.name = "Scrap_Icon";
            scrapIconImage = scrapImgTr.GetComponent<Image>();
            if (scrapIconImage != null)
            {
                if (scrapOreSprite != null) scrapIconImage.sprite = scrapOreSprite;
                scrapIconImage.preserveAspect = true;
                scrapIconImage.raycastTarget = false;
                RectTransform scIconRect = scrapIconImage.GetComponent<RectTransform>();
                scIconRect.anchorMin = new Vector2(0f, 0.5f);
                scIconRect.anchorMax = new Vector2(0f, 0.5f);
                scIconRect.pivot = new Vector2(0f, 0.5f);
                scIconRect.anchoredPosition = new Vector2(8f, 0f);
                scIconRect.sizeDelta = new Vector2(22f, 22f);
                scIconRect.localScale = Vector3.one;
            }

            // Scrap counter text (formatted as e.g. '8 kpl' per MasterSpec §6)
            Transform scrapTextTr = scrapContainer.Find("Scrap_Text");
            if (scrapTextTr == null)
            {
                GameObject sTextObj = new GameObject("Scrap_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                sTextObj.transform.SetParent(scrapContainer, false);
                scrapTextTr = sTextObj.transform;
            }

            scrapCounterText = scrapTextTr.GetComponent<TMP_Text>();
            if (scrapCounterText != null)
            {
                scrapCounterText.text = "0 kpl";
                scrapCounterText.alignment = TextAlignmentOptions.MidlineLeft;
                scrapCounterText.fontSize = 13f;
                scrapCounterText.fontStyle = FontStyles.Bold;
                scrapCounterText.color = UITheme.SoftText; // Metallic silver
                scrapCounterText.raycastTarget = false;
                RectTransform sctRect = scrapCounterText.GetComponent<RectTransform>();
                sctRect.anchorMin = new Vector2(0f, 0f);
                sctRect.anchorMax = new Vector2(1f, 1f);
                sctRect.pivot = new Vector2(0f, 0.5f);
                sctRect.anchoredPosition = new Vector2(36f, 0f);
                sctRect.sizeDelta = new Vector2(-42f, 0f);
                sctRect.localScale = Vector3.one;
            }

            // ========================================================
            // 2. TOP-CENTER: ZONE & LOCATION BANNER (Emoji-free, clean typography)
            // ========================================================
            Transform zoneBannerTr = transform.Find("Zone_Indicator_Banner");
            GameObject zoneBannerObj = zoneBannerTr != null ? zoneBannerTr.gameObject : new GameObject("Zone_Indicator_Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            zoneBannerObj.transform.SetParent(transform, false);
            RectTransform zbRect = zoneBannerObj.GetComponent<RectTransform>();
            zbRect.anchorMin = new Vector2(0.5f, 1f);
            zbRect.anchorMax = new Vector2(0.5f, 1f);
            zbRect.pivot = new Vector2(0.5f, 1f);
            zbRect.anchoredPosition = new Vector2(0f, -14f);
            zbRect.sizeDelta = new Vector2(340f, 50f);
            Image zbBg = zoneBannerObj.GetComponent<Image>();
            if (panelDarkSprite != null)
            {
                zbBg.sprite = panelDarkSprite;
                zbBg.type = Image.Type.Sliced;
                zbBg.color = new Color(1f, 1f, 1f, 0.94f);
            }

            Transform zTitleTr = zoneBannerObj.transform.Find("Zone_Title_Text");
            GameObject zTitleObj = zTitleTr != null ? zTitleTr.gameObject : new GameObject("Zone_Title_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            zTitleObj.transform.SetParent(zoneBannerObj.transform, false);
            zoneTitleText = zTitleObj.GetComponent<TMP_Text>();
            zoneTitleText.text = "Oakhaven Village";
            zoneTitleText.fontSize = 15f;
            zoneTitleText.fontStyle = FontStyles.Bold;
            zoneTitleText.alignment = TextAlignmentOptions.Center;
            zoneTitleText.color = new Color(1.0f, 0.94f, 0.82f, 1f);
            RectTransform ztRect = zTitleObj.GetComponent<RectTransform>();
            ztRect.anchorMin = new Vector2(0f, 0.42f);
            ztRect.anchorMax = new Vector2(1f, 1f);
            ztRect.sizeDelta = Vector2.zero;
            ztRect.anchoredPosition = Vector2.zero;

            Transform zSubTr = zoneBannerObj.transform.Find("Zone_Subtitle_Text");
            GameObject zSubObj = zSubTr != null ? zSubTr.gameObject : new GameObject("Zone_Subtitle_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            zSubObj.transform.SetParent(zoneBannerObj.transform, false);
            zoneSubtitleText = zSubObj.GetComponent<TMP_Text>();
            zoneSubtitleText.text = "Safe Haven • Starting Area";
            zoneSubtitleText.fontSize = 10.5f;
            zoneSubtitleText.alignment = TextAlignmentOptions.Center;
            zoneSubtitleText.color = new Color(0.80f, 0.65f, 0.32f, 1f);
            RectTransform zsRect = zSubObj.GetComponent<RectTransform>();
            zsRect.anchorMin = new Vector2(0f, 0f);
            zsRect.anchorMax = new Vector2(1f, 0.48f);
            zsRect.sizeDelta = Vector2.zero;
            zsRect.anchoredPosition = Vector2.zero;

            // ========================================================
            // 3. TOP-RIGHT: QUEST TRACKER CARD (Fixed: Zero text overlap)
            // ========================================================
            Transform questCardTr = transform.Find("Quest_Tracker_Card");
            GameObject questCardObj = questCardTr != null ? questCardTr.gameObject : new GameObject("Quest_Tracker_Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            questCardObj.transform.SetParent(transform, false);
            RectTransform qcRect = questCardObj.GetComponent<RectTransform>();
            qcRect.anchorMin = new Vector2(1f, 1f);
            qcRect.anchorMax = new Vector2(1f, 1f);
            qcRect.pivot = new Vector2(1f, 1f);
            qcRect.anchoredPosition = new Vector2(-24f, -18f);
            qcRect.sizeDelta = new Vector2(400f, 145f);
            Image qcBg = questCardObj.GetComponent<Image>();
            if (panelDarkSprite != null)
            {
                qcBg.sprite = panelDarkSprite;
                qcBg.type = Image.Type.Sliced;
                qcBg.color = Color.white;
            }

            // 3A. Header row (Clean text, no emojis)
            Transform qHeaderTr = questCardObj.transform.Find("Quest_Header_Text");
            GameObject qHeaderObj = qHeaderTr != null ? qHeaderTr.gameObject : new GameObject("Quest_Header_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            qHeaderObj.transform.SetParent(questCardObj.transform, false);
            questHeaderText = qHeaderObj.GetComponent<TMP_Text>();
            questHeaderText.text = "QUEST OBJECTIVES";
            questHeaderText.fontSize = 11.5f;
            questHeaderText.fontStyle = FontStyles.Bold;
            questHeaderText.characterSpacing = 1.5f;
            questHeaderText.alignment = TextAlignmentOptions.MidlineLeft;
            questHeaderText.color = new Color(0.92f, 0.78f, 0.38f, 1f); // Rich gold
            RectTransform qhRect = qHeaderObj.GetComponent<RectTransform>();
            qhRect.anchorMin = new Vector2(0f, 1f);
            qhRect.anchorMax = new Vector2(1f, 1f);
            qhRect.pivot = new Vector2(0f, 1f);
            qhRect.anchoredPosition = new Vector2(18f, -12f);
            qhRect.sizeDelta = new Vector2(-36f, 18f);

            // 3B. Gold Divider Line
            Transform dividerTr = questCardObj.transform.Find("Divider_Gold");
            GameObject dividerObj = dividerTr != null ? dividerTr.gameObject : new GameObject("Divider_Gold", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dividerObj.transform.SetParent(questCardObj.transform, false);
            RectTransform dRect = dividerObj.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0f, 1f);
            dRect.anchorMax = new Vector2(1f, 1f);
            dRect.pivot = new Vector2(0.5f, 1f);
            dRect.anchoredPosition = new Vector2(0f, -32f);
            dRect.sizeDelta = new Vector2(-36f, 4f);
            Image divImg = dividerObj.GetComponent<Image>();
            if (dividerGoldSprite != null)
            {
                divImg.sprite = dividerGoldSprite;
                divImg.type = Image.Type.Sliced;
            }

            // 3C. Quest Summary & Objectives Text (Starts BELOW the divider at y = -40, non-overlapping)
            Transform summaryTr = transform.Find("ActiveQuestSummaryText") ?? questCardObj.transform.Find("ActiveQuestSummaryText");
            if (summaryTr != null)
            {
                summaryTr.SetParent(questCardObj.transform, false);
                activeQuestSummaryText = summaryTr.GetComponent<TMP_Text>();
                RectTransform sRect = summaryTr.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0f, 0f);
                sRect.anchorMax = new Vector2(1f, 1f);
                sRect.pivot = new Vector2(0f, 1f);
                sRect.anchoredPosition = new Vector2(18f, -40f);
                sRect.sizeDelta = new Vector2(-36f, -48f);
                sRect.localScale = Vector3.one;
                if (activeQuestSummaryText != null)
                {
                    activeQuestSummaryText.alignment = TextAlignmentOptions.TopLeft;
                    activeQuestSummaryText.fontSize = 12f;
                    activeQuestSummaryText.enableAutoSizing = false;
                    activeQuestSummaryText.lineSpacing = -2f;
                    activeQuestSummaryText.paragraphSpacing = 3f;
                    activeQuestSummaryText.color = new Color(0.96f, 0.94f, 0.90f, 1f);
                    activeQuestSummaryText.textWrappingMode = TextWrappingModes.Normal;
                    activeQuestSummaryText.richText = true;
                }
            }

            // 3D. Attach and initialize QuestHUDUIController
            QuestHUDUIController questHUD = questCardObj.GetComponent<QuestHUDUIController>();
            if (questHUD == null)
            {
                questHUD = questCardObj.AddComponent<QuestHUDUIController>();
            }
            questHUD.AutoLocateOrBuildHierarchy();

            // ========================================================
            // 4. COMBAT STATS: hero stats block + enemy cards under the zone banner
            // ========================================================
            combatStatsHUD = GetComponent<CombatStatsHUD>();
            if (combatStatsHUD == null)
            {
                combatStatsHUD = gameObject.AddComponent<CombatStatsHUD>();
            }
            combatStatsHUD.Build(heroCardRect, panelDarkSprite, pillBadgeSprite, barTrackSprite, barFillRubySprite, dividerGoldSprite);
            if (trackedPlayer != null)
            {
                combatStatsHUD.SetPlayer(trackedPlayer);
            }

            // ========================================================
            // 5. BOTTOM-LEFT: FLASK SLOTS (small and large health potion)
            // ========================================================
            // Parented to the canvas: this HUD's own rect is only the top strip of the screen
            RectTransform canvasRect = transform.parent as RectTransform;
            PotionQuickBar.EnsureBar(canvasRect != null ? canvasRect : hudRect, slotFrameSprite, pillBadgeSprite, potionSprite);

            // ========================================================
            // 6. MAP (M) BUTTON: left of the quest card, clear of its header
            // ========================================================
            Transform mapBtnTr = transform.Find("Button_Map_Toggle");
            if (mapBtnTr != null)
            {
                RectTransform mbRect = mapBtnTr.GetComponent<RectTransform>();
                mbRect.anchorMin = new Vector2(1f, 1f);
                mbRect.anchorMax = new Vector2(1f, 1f);
                mbRect.pivot = new Vector2(1f, 1f);
                mbRect.anchoredPosition = new Vector2(MapButtonRightEdge, -18f);
            }
        }

        #endregion

        #region Player Tracking

        private void LocatePlayer()
        {
            if (trackedPlayer == null)
            {
                trackedPlayer = FindAnyObjectByType<PlayerUnit>(FindObjectsInactive.Include);
                if (trackedPlayer != null)
                {
                    trackedPlayer.OnHealthChanged -= HandleHealthChanged;
                    trackedPlayer.OnHealthChanged += HandleHealthChanged;
                }
            }

            if (trackedPlayer != null && combatStatsHUD != null)
            {
                combatStatsHUD.SetPlayer(trackedPlayer);
            }
        }

        #endregion

        #region Refresh All HUD

        /// <summary>
        /// Refreshes all elements of the persistent HUD.
        /// </summary>
        public void RefreshAllHUD()
        {
            LocatePlayer();

            // 1. Health Bar & Hero Identity
            if (trackedPlayer != null)
            {
                UpdateHealthDisplay(trackedPlayer.CurrentHP, trackedPlayer.MaxHP);
                UpdateHeroDisplay();
            }

            // 2. Gold Counter, Scrap Metal & Potions
            if (InventoryManager.Instance != null)
            {
                UpdateGoldDisplay(InventoryManager.Instance.CurrentGold);
                UpdateScrapDisplay(InventoryManager.Instance.ScrapMetalCount);
                UpdatePotionDisplay();
            }

            // 3. Zone & Location
            if (GameManager.Instance != null)
            {
                UpdateZoneDisplay(GameManager.Instance.CurrentLocation);
            }
            else
            {
                UpdateZoneDisplay(GameLocation.Village);
            }

            // 4. Quest Summary
            UpdateQuestSummaryText();
        }

        public void UpdateHeroDisplay()
        {
            if (trackedPlayer == null) return;

            // Name
            if (heroNameText != null)
            {
                heroNameText.text = !string.IsNullOrEmpty(trackedPlayer.UnitName) ? trackedPlayer.UnitName : "Sir Roland";
            }

            // Class & Level
            CharacterClassType classType = CharacterClassType.Warrior;
            if (heroClassText != null)
            {
                if (trackedPlayer.CharacterClass != null)
                {
                    classType = trackedPlayer.CharacterClass.ClassType;
                }
                int level = trackedPlayer.Level > 0 ? trackedPlayer.Level : 1;
                heroClassText.text = $"{classType} • Level {level}";
            }

            // Armor Class
            if (heroACText != null)
            {
                heroACText.text = $"AC {trackedPlayer.ArmorClass}";
            }

            // Crest Emblem Sprite
            if (heroCrestImage != null)
            {
                switch (classType)
                {
                    case CharacterClassType.Mage:
                        heroCrestImage.sprite = crestMageSprite != null ? crestMageSprite : crestPlateSprite;
                        break;
                    case CharacterClassType.Rogue:
                        heroCrestImage.sprite = crestRogueSprite != null ? crestRogueSprite : crestPlateSprite;
                        break;
                    default:
                        heroCrestImage.sprite = crestWarriorSprite != null ? crestWarriorSprite : crestPlateSprite;
                        break;
                }
            }
        }

        private void UpdateHealthDisplay(int currentHP, int maxHP)
        {
            if (healthSlider != null && maxHP > 0)
            {
                healthSlider.maxValue = maxHP;
                targetHPValue = Mathf.Clamp(currentHP, 0, maxHP);

                // Detect damage to trigger brief red flash
                if (lastSeenHP > 0 && currentHP < lastSeenHP)
                {
                    damageFlashTimer = 1f;
                }
                lastSeenHP = currentHP;
            }

            if (healthText != null)
            {
                healthText.text = $"HP: {currentHP} / {maxHP}";
            }
        }

        private void UpdateGoldDisplay(int goldAmount)
        {
            if (goldCounterText != null)
            {
                goldCounterText.text = $"{goldAmount} Gold";
            }
        }

        public void UpdateScrapDisplay(int scrapCount)
        {
            if (scrapCounterText != null)
            {
                scrapCounterText.text = $"{scrapCount} kpl";
            }
        }

        private void UpdatePotionDisplay()
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return;

            // Small and greater potions share the quick slot
            ItemSO smallPotion = healthPotionItem != null ? healthPotionItem : inventory.FindItemByID(ShopManager.SMALL_POTION_ID);
            int count = smallPotion != null ? inventory.GetItemCount(smallPotion) : 0;
            ItemSO greaterPotion = inventory.FindItemByID(ShopManager.GREATER_POTION_ID);
            if (greaterPotion != null) count += inventory.GetItemCount(greaterPotion);

            if (potionCountText != null)
            {
                potionCountText.text = $"x{count}";
            }

            if (quickPotionButton != null)
            {
                quickPotionButton.interactable = count > 0;
            }

            if (potionIconImage != null)
            {
                potionIconImage.color = count > 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.45f);
            }
        }

        /// <summary>
        /// Updates the top-center zone banner based on the active atmospheric location.
        /// </summary>
        public void UpdateZoneDisplay(GameLocation location)
        {
            if (zoneTitleText == null) return;

            switch (location)
            {
                case GameLocation.Village:
                    zoneTitleText.text = "Oakhaven Village";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Safe Haven • Starting Area";
                    break;
                case GameLocation.Forest:
                    zoneTitleText.text = "Whispering Woods";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "The Approach to Castle of Dice";
                    break;
                case GameLocation.Courtyard:
                    zoneTitleText.text = "Castle Courtyard";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Wing 1 • Cursed Commander's Domain";
                    break;
                case GameLocation.Library:
                    zoneTitleText.text = "Grand Archives";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Wing 2 • Shadow Mage Malakor";
                    break;
                case GameLocation.CrownHall:
                    zoneTitleText.text = "The Throne Room";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Wing 3 • Gargoyle King's Lair";
                    break;
                case GameLocation.CastleHall:
                    zoneTitleText.text = "The Great Hall";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Safe Haven Hub • Runestone Shrine";
                    break;
                case GameLocation.Tower:
                    zoneTitleText.text = "Treasure Tower";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Ancient Spire of Relics";
                    break;
                case GameLocation.Cellar:
                    zoneTitleText.text = "Village Wine Cellar";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Ruins Beneath Oakhaven";
                    break;
                default:
                    zoneTitleText.text = "Castle of Dice";
                    if (zoneSubtitleText != null) zoneSubtitleText.text = "Adventure Campaign";
                    break;
            }
        }

        /// <summary>
        /// Synchronizes the active quest summary text tracker on the HUD.
        /// Can be called directly or supplied with a custom status message.
        /// </summary>
        public void UpdateQuestSummaryText(string customText = null)
        {
            QuestHUDUIController.Instance?.RefreshQuestList();

            if (activeQuestSummaryText == null) return;

            if (!string.IsNullOrEmpty(customText))
            {
                activeQuestSummaryText.text = customText;
                return;
            }

            // Query QuestManager for active quest
            QuestManager qm = QuestManager.Instance;
            if (qm != null)
            {
                QuestSO activeQuest = qm.GetActiveQuest();
                if (activeQuest != null)
                {
                    int current = qm.GetQuestProgress(activeQuest.QuestID);
                    int required = activeQuest.RequiredAmount;
                    bool isDone = current >= required;

                    string statusColor = isDone ? "#2ECC71" : "#F1C40F";
                    string statusPrefix = isDone ? "[COMPLETE]" : "-";

                    activeQuestSummaryText.text =
                        $"<color=#FFF8DC><size=13.5><b>{activeQuest.QuestTitle}</b></size></color>\n" +
                        $"<color={statusColor}>{statusPrefix} {activeQuest.Description}: {current} / {required}</color>\n" +
                        $"<color=#A0AEC0><size=10.5>{(isDone ? "Return to the quest giver for reward!" : "Explore the area to complete objectives.")}</size></color>";
                    return;
                }
            }

            activeQuestSummaryText.text =
                "<color=#FFF8DC><size=13.5><b>Village Exploration</b></size></color>\n" +
                "<color=#E2E8F0>- Explore Oakhaven Village</color>\n" +
                "<color=#A0AEC0><size=10.5>Speak with Baldur at the Forge or Barnaby at the Tavern.</size></color>";
        }

        #endregion

        #region Hotbar Actions

        /// <summary>[Q]: drinks a small potion (a large one when the small ones are gone) under the potion rules.</summary>
        public void OnQuickPotionClicked()
        {
            LocatePlayer();
            PotionQuickBar.TryDrinkQuick(trackedPlayer);
            UpdatePotionDisplay();
        }

        /// <summary>
        /// The potion [Q] drinks: a small one first, a greater one when no small potions are left.
        /// </summary>
        public ItemSO ResolveQuickPotion(InventoryManager inventory)
        {
            if (inventory == null) return null;
            if (healthPotionItem != null && inventory.HasItem(healthPotionItem, 1)) return healthPotionItem;

            ItemSO greaterPotion = inventory.FindItemByID(ShopManager.GREATER_POTION_ID);
            return greaterPotion != null && inventory.HasItem(greaterPotion, 1) ? greaterPotion : null;
        }

        #endregion

        #region Event Listeners

        private void HandleHealthChanged(int current, int max)
        {
            UpdateHealthDisplay(current, max);

            // The hero adopts its class (name, crest) in InitializeUnit, which can run after this HUD's
            // Start; that re-init also raises a health change, so refresh the identity here too.
            UpdateHeroDisplay();
        }

        private void HandleGoldChanged(int newGold)
        {
            UpdateGoldDisplay(newGold);
        }

        private void HandleScrapMetalChanged(int newScrap)
        {
            UpdateScrapDisplay(newScrap);
        }

        private void HandleInventoryChanged()
        {
            UpdatePotionDisplay();
        }

        private void HandleLocationChanged(GameLocation location)
        {
            UpdateZoneDisplay(location);
        }

        private void HandleQuestProgressUpdated(string questID, int current, int required)
        {
            QuestManager qm = QuestManager.Instance;
            QuestSO quest = qm != null ? qm.GetQuest(questID) : null;
            string title = quest != null ? quest.QuestTitle : questID;

            UpdateQuestSummaryText();
        }

        private void HandleQuestStateUpdated(string questID, QuestState state)
        {
            UpdateQuestSummaryText();
        }

        #endregion
    }
}
