using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.Audio;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Modal UI controller for milestone-based level advancement (Levels 2 & 3).
    /// Disables player exploration movement, presents exactly 3 upgrade choices,
    /// allows selecting which ability to upgrade to Rank 2, updates player stats,
    /// plays SFX and floating text, saves game state, and restores inputs upon completion.
    /// </summary>
    public class LevelUpUIController : MonoBehaviour
    {
        #region Singleton

        public static LevelUpUIController Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("UI Root Modal")]
        [SerializeField] private GameObject levelUpModalPanel;

        [Header("Headers")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text milestoneSubtitleText;

        [Header("Main Choice Cards")]
        [SerializeField] private Button resilienceCardButton;
        [SerializeField] private Button attributeCardButton;
        [SerializeField] private Button abilityCardButton;

        [Header("Card Labels")]
        [SerializeField] private TMP_Text resilienceTitleText;
        [SerializeField] private TMP_Text resilienceDescText;
        [SerializeField] private TMP_Text attributeTitleText;
        [SerializeField] private TMP_Text attributeDescText;
        [SerializeField] private TMP_Text abilityTitleText;
        [SerializeField] private TMP_Text abilityDescText;

        [Header("Ability Upgrade Submenu")]
        [SerializeField] private GameObject abilitySubmenuPanel;
        [SerializeField] private Button[] abilitySlotButtons = new Button[4];
        [SerializeField] private TMP_Text[] abilitySlotTexts = new TMP_Text[4];
        [SerializeField] private Button cancelSubmenuButton;

        [Header("Sprites")]
        [SerializeField] private Sprite panelDarkSprite;
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite buttonNormalSprite;

        #endregion

        #region Private State

        private int pendingMilestoneLevel = 2;
        private PlayerUnit cachedPlayerUnit;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadThemeSpritesIfMissing();
            EnsureUIHierarchy();

            if (levelUpModalPanel != null)
            {
                levelUpModalPanel.SetActive(false);
            }
            if (abilitySubmenuPanel != null)
            {
                abilitySubmenuPanel.SetActive(false);
            }
        }

        private void Start()
        {
            if (levelUpModalPanel != null)
            {
                levelUpModalPanel.SetActive(false);
            }
            if (abilitySubmenuPanel != null)
            {
                abilitySubmenuPanel.SetActive(false);
            }

            WireListeners();
        }

        #endregion

        #region Setup & Sprites

        private void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
        }

        private void WireListeners()
        {
            if (resilienceCardButton != null)
            {
                resilienceCardButton.onClick.RemoveListener(OnResilienceChosen);
                resilienceCardButton.onClick.AddListener(OnResilienceChosen);
            }

            if (attributeCardButton != null)
            {
                attributeCardButton.onClick.RemoveListener(OnAttributeChosen);
                attributeCardButton.onClick.AddListener(OnAttributeChosen);
            }

            if (abilityCardButton != null)
            {
                abilityCardButton.onClick.RemoveListener(OnAbilityUpgradeClicked);
                abilityCardButton.onClick.AddListener(OnAbilityUpgradeClicked);
            }

            if (cancelSubmenuButton != null)
            {
                cancelSubmenuButton.onClick.RemoveListener(OnCancelSubmenuClicked);
                cancelSubmenuButton.onClick.AddListener(OnCancelSubmenuClicked);
            }

            for (int i = 0; i < 4; i++)
            {
                int slotIndex = i;
                if (abilitySlotButtons != null && i < abilitySlotButtons.Length && abilitySlotButtons[i] != null)
                {
                    abilitySlotButtons[i].onClick.RemoveAllListeners();
                    abilitySlotButtons[i].onClick.AddListener(() => OnAbilitySlotSelected(slotIndex));
                }
            }
        }

        #endregion

        #region Public Modal API

        /// <summary>
        /// Displays the Milestone Level-Up Modal for target level (2 or 3).
        /// Suspends exploration input while open.
        /// </summary>
        public void ShowLevelUpModal(int newLevel, PlayerUnit player = null)
        {
            pendingMilestoneLevel = Mathf.Clamp(newLevel, 2, 3);
            cachedPlayerUnit = player != null ? player : FindAnyObjectByType<PlayerUnit>();

            EnsureUIHierarchy();

            // Suspend exploration input
            GameInput.SetExplorationInputEnabled(false);

            if (levelUpModalPanel != null)
            {
                levelUpModalPanel.SetActive(true);
            }

            if (abilitySubmenuPanel != null)
            {
                abilitySubmenuPanel.SetActive(false);
            }

            string milestoneName = (pendingMilestoneLevel == 2)
                ? "Castle Veteran"
                : "Arcane Crusher (Max Level)";

            if (titleText != null)
            {
                titleText.text = $"LEVEL UP! - LEVEL {pendingMilestoneLevel}";
            }

            if (milestoneSubtitleText != null)
            {
                milestoneSubtitleText.text = $"Milestone: {milestoneName}\nChoose one permanent hero upgrade:";
            }

            Debug.Log($"[LevelUpUIController] Level-up modal displayed for Level {pendingMilestoneLevel} ({milestoneName}).");
        }

        /// <summary>
        /// Closes the Level-Up modal and re-enables exploration input.
        /// </summary>
        public void CloseLevelUpModal()
        {
            if (levelUpModalPanel != null)
            {
                levelUpModalPanel.SetActive(false);
            }

            if (abilitySubmenuPanel != null)
            {
                abilitySubmenuPanel.SetActive(false);
            }

            // Restore exploration input
            GameInput.SetExplorationInputEnabled(true);
        }

        #endregion

        #region Option Handlers

        private void OnResilienceChosen()
        {
            Debug.Log("[LevelUpUIController] Player selected: Hero's Resilience (+5 Max HP & Full Heal).");

            if (cachedPlayerUnit != null)
            {
                cachedPlayerUnit.Level = pendingMilestoneLevel;
                cachedPlayerUnit.ApplyHeroResilience(5);
            }

            // PlayerUnit pushes the new bonus into the session progression store itself

            FinalizeLevelUpChoice("HERO'S RESILIENCE! +5 MAX HP");
        }

        private void OnAttributeChosen()
        {
            Debug.Log("[LevelUpUIController] Player selected: Attribute Bonus Growth (+1 Primary Attribute).");

            if (cachedPlayerUnit != null)
            {
                cachedPlayerUnit.Level = pendingMilestoneLevel;
                cachedPlayerUnit.AddAttributeBonus(1);
            }


            FinalizeLevelUpChoice("ATTRIBUTE BONUS +1! (D20 & DAMAGE)");
        }

        private void OnAbilityUpgradeClicked()
        {
            if (abilitySubmenuPanel == null) return;

            abilitySubmenuPanel.SetActive(true);

            IReadOnlyList<AbilitySO> abilities = cachedPlayerUnit != null ? cachedPlayerUnit.ActiveAbilities : null;

            for (int i = 0; i < 4; i++)
            {
                if (abilitySlotButtons != null && i < abilitySlotButtons.Length && abilitySlotButtons[i] != null)
                {
                    if (abilities != null && i < abilities.Count && abilities[i] != null)
                    {
                        AbilitySO ab = abilities[i];
                        bool isAlreadyRank2 = ab.AbilityName.Contains("[Rank 2]");

                        abilitySlotButtons[i].gameObject.SetActive(true);
                        abilitySlotButtons[i].interactable = !isAlreadyRank2;

                        if (abilitySlotTexts != null && i < abilitySlotTexts.Length && abilitySlotTexts[i] != null)
                        {
                            abilitySlotTexts[i].text = isAlreadyRank2
                                ? $"{ab.AbilityName}\n<color=#a1a1aa>(Already Rank 2)</color>"
                                : $"<b>{ab.AbilityName}</b>\nBase Power: {ab.BaseValue} -> <color=#4ade80><b>{ab.BaseValue + 3} (Rank 2)</b></color>";
                        }
                    }
                    else
                    {
                        abilitySlotButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void OnCancelSubmenuClicked()
        {
            if (abilitySubmenuPanel != null)
            {
                abilitySubmenuPanel.SetActive(false);
            }
        }

        private void OnAbilitySlotSelected(int slotIndex)
        {
            if (cachedPlayerUnit == null) return;

            string upgradedName = "Ability";
            if (slotIndex >= 0 && slotIndex < cachedPlayerUnit.ActiveAbilities.Count && cachedPlayerUnit.ActiveAbilities[slotIndex] != null)
            {
                upgradedName = cachedPlayerUnit.ActiveAbilities[slotIndex].AbilityName;
            }

            cachedPlayerUnit.Level = pendingMilestoneLevel;
            bool success = cachedPlayerUnit.UpgradeAbilityToRank2(slotIndex);

            if (success)
            {
                FinalizeLevelUpChoice($"{upgradedName} -> RANK 2!");
            }
        }

        private void FinalizeLevelUpChoice(string floatingFeedback)
        {
            Vector3 playerPos = cachedPlayerUnit != null ? cachedPlayerUnit.transform.position : Vector3.zero;

            // 1. Play Level-Up SFX
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(SFXClipType.LevelUp, playerPos);
            }

            // 2. Show 3D Floating text banner
            if (FloatingCombatText.Instance != null)
            {
                FloatingCombatText.Instance.ShowText(playerPos, "LEVEL UP!\n" + floatingFeedback, UITheme.CoinGold);
            }

            // 3. Save Game State
            SaveSystem.SaveGame(PlayerDataSO.Session, cachedPlayerUnit);

            // 4. Refresh HUD
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.UpdateHeroDisplay();
            }

            // 5. Close Modal & re-enable movement
            CloseLevelUpModal();
        }

        #endregion

        #region Dynamic UI Hierarchy Builder (1920x1080 Scaler)

        public void EnsureUIHierarchy()
        {
            if (levelUpModalPanel != null && resilienceCardButton != null) return;

            Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            LoadThemeSpritesIfMissing();

            // Root Overlay Panel
            Transform existingPanel = canvas.transform.Find("LevelUp_Modal_Panel");
            GameObject panelObj = existingPanel != null ? existingPanel.gameObject : new GameObject("LevelUp_Modal_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObj.transform.SetParent(canvas.transform, false);
            panelObj.SetActive(false);
            levelUpModalPanel = panelObj;

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            Image backdropImg = panelObj.GetComponent<Image>();
            backdropImg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f); // Deep fantasy slate

            // Central Card Container Box
            Transform centerBoxTr = panelObj.transform.Find("Center_Dialog_Card");
            GameObject centerBoxObj = centerBoxTr != null ? centerBoxTr.gameObject : new GameObject("Center_Dialog_Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            centerBoxObj.transform.SetParent(panelObj.transform, false);

            RectTransform centerRect = centerBoxObj.GetComponent<RectTransform>();
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.pivot = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = Vector2.zero;
            centerRect.sizeDelta = new Vector2(920f, 540f);

            Image centerBg = centerBoxObj.GetComponent<Image>();
            if (panelDarkSprite != null)
            {
                centerBg.sprite = panelDarkSprite;
                centerBg.type = Image.Type.Sliced;
                centerBg.color = Color.white;
            }
            else
            {
                centerBg.color = new Color(0.10f, 0.12f, 0.18f, 0.98f);
            }

            // Title
            Transform titleTr = centerBoxObj.transform.Find("Header_Title");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : new GameObject("Header_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(centerBoxObj.transform, false);
            RectTransform trTitle = titleObj.GetComponent<RectTransform>();
            trTitle.anchorMin = new Vector2(0.5f, 1f);
            trTitle.anchorMax = new Vector2(0.5f, 1f);
            trTitle.pivot = new Vector2(0.5f, 1f);
            trTitle.anchoredPosition = new Vector2(0f, -24f);
            trTitle.sizeDelta = new Vector2(800f, 44f);
            titleText = titleObj.GetComponent<TMP_Text>();
            titleText.text = "LEVEL UP! - LEVEL 2";
            titleText.fontSize = 28f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.85f, 0.35f, 1f);

            // Subtitle
            Transform subTr = centerBoxObj.transform.Find("Header_Subtitle");
            GameObject subObj = subTr != null ? subTr.gameObject : new GameObject("Header_Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(centerBoxObj.transform, false);
            RectTransform trSub = subObj.GetComponent<RectTransform>();
            trSub.anchorMin = new Vector2(0.5f, 1f);
            trSub.anchorMax = new Vector2(0.5f, 1f);
            trSub.pivot = new Vector2(0.5f, 1f);
            trSub.anchoredPosition = new Vector2(0f, -70f);
            trSub.sizeDelta = new Vector2(800f, 48f);
            milestoneSubtitleText = subObj.GetComponent<TMP_Text>();
            milestoneSubtitleText.text = "Milestone: Castle Veteran\nChoose one permanent hero upgrade:";
            milestoneSubtitleText.fontSize = 15f;
            milestoneSubtitleText.alignment = TextAlignmentOptions.Center;
            milestoneSubtitleText.color = UITheme.SoftText;

            // 3 Choice Cards Layout
            // Card 1: Hero's Resilience
            resilienceCardButton = CreateChoiceCard(
                centerBoxObj.transform,
                "Card_Resilience",
                new Vector2(-280f, -40f),
                "HERO'S RESILIENCE",
                "+5 Max HP\n\nPermanently increases maximum Hit Points and fully restores all health immediately.",
                out resilienceTitleText,
                out resilienceDescText
            );

            // Card 2: Attribute Bonus Growth
            attributeCardButton = CreateChoiceCard(
                centerBoxObj.transform,
                "Card_Attribute",
                new Vector2(0f, -40f),
                "ATTRIBUTE BONUS",
                "+1 Primary Attribute\n\nIncreases primary attribute bonus by 1 (+1 to all d20 checks and damage rolls).",
                out attributeTitleText,
                out attributeDescText
            );

            // Card 3: Ability Empowerment
            abilityCardButton = CreateChoiceCard(
                centerBoxObj.transform,
                "Card_Ability",
                new Vector2(280f, -40f),
                "ABILITY EMPOWERMENT",
                "Rank 2 Upgrade\n\nChoose one of your 4 starting abilities to elevate to Rank 2 (+3 base potency).",
                out abilityTitleText,
                out abilityDescText
            );

            // Submenu for choosing an ability
            CreateAbilitySubmenu(centerBoxObj.transform);

            WireListeners();
        }

        private Button CreateChoiceCard(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            string title,
            string desc,
            out TMP_Text outTitle,
            out TMP_Text outDesc)
        {
            Transform existing = parent.Find(name);
            GameObject cardObj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardObj.transform.SetParent(parent, false);

            RectTransform rt = cardObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(250f, 320f);

            Image img = cardObj.GetComponent<Image>();
            if (slotFrameSprite != null)
            {
                img.sprite = slotFrameSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.20f, 0.24f, 0.32f, 1f);
            }
            else
            {
                img.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            }
            img.raycastTarget = true;

            Button btn = cardObj.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = cb;

            // Card Title
            Transform titleTr = cardObj.transform.Find("Title");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform rtt = titleObj.GetComponent<RectTransform>();
            rtt.anchorMin = new Vector2(0f, 1f);
            rtt.anchorMax = new Vector2(1f, 1f);
            rtt.pivot = new Vector2(0.5f, 1f);
            rtt.anchoredPosition = new Vector2(0f, -18f);
            rtt.sizeDelta = new Vector2(-20f, 50f);
            outTitle = titleObj.GetComponent<TMP_Text>();
            outTitle.text = title;
            outTitle.fontSize = 16f;
            outTitle.fontStyle = FontStyles.Bold;
            outTitle.alignment = TextAlignmentOptions.Center;
            outTitle.color = new Color(1f, 0.88f, 0.4f, 1f);
            outTitle.raycastTarget = false;

            // Card Desc
            Transform descTr = cardObj.transform.Find("Desc");
            GameObject descObj = descTr != null ? descTr.gameObject : new GameObject("Desc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform rtd = descObj.GetComponent<RectTransform>();
            rtd.anchorMin = new Vector2(0f, 0f);
            rtd.anchorMax = new Vector2(1f, 1f);
            rtd.pivot = new Vector2(0.5f, 0.5f);
            rtd.anchoredPosition = new Vector2(0f, -30f);
            rtd.sizeDelta = new Vector2(-24f, -110f);
            outDesc = descObj.GetComponent<TMP_Text>();
            outDesc.text = desc;
            outDesc.fontSize = 13f;
            outDesc.alignment = TextAlignmentOptions.Center;
            outDesc.color = new Color(0.9f, 0.92f, 0.95f, 1f);
            outDesc.raycastTarget = false;

            // Bottom Select Tag
            Transform tagTr = cardObj.transform.Find("SelectTag");
            GameObject tagObj = tagTr != null ? tagTr.gameObject : new GameObject("SelectTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tagObj.transform.SetParent(cardObj.transform, false);
            RectTransform rtag = tagObj.GetComponent<RectTransform>();
            rtag.anchorMin = new Vector2(0f, 0f);
            rtag.anchorMax = new Vector2(1f, 0f);
            rtag.pivot = new Vector2(0.5f, 0f);
            rtag.anchoredPosition = new Vector2(0f, 12f);
            rtag.sizeDelta = new Vector2(-20f, 30f);
            TMP_Text tagText = tagObj.GetComponent<TMP_Text>();
            tagText.text = "[ SELECT ]";
            tagText.fontSize = 14f;
            tagText.fontStyle = FontStyles.Bold;
            tagText.alignment = TextAlignmentOptions.Center;
            tagText.color = new Color(0.4f, 0.85f, 1f, 1f);
            tagText.raycastTarget = false;

            return btn;
        }

        private void CreateAbilitySubmenu(Transform parent)
        {
            Transform subTr = parent.Find("Ability_Submenu_Panel");
            GameObject subObj = subTr != null ? subTr.gameObject : new GameObject("Ability_Submenu_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            subObj.transform.SetParent(parent, false);
            abilitySubmenuPanel = subObj;

            RectTransform rt = subObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image bg = subObj.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);
            bg.raycastTarget = true;

            // Header
            Transform hdrTr = subObj.transform.Find("Submenu_Header");
            GameObject hdrObj = hdrTr != null ? hdrTr.gameObject : new GameObject("Submenu_Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            hdrObj.transform.SetParent(subObj.transform, false);
            RectTransform rh = hdrObj.GetComponent<RectTransform>();
            rh.anchorMin = new Vector2(0.5f, 1f);
            rh.anchorMax = new Vector2(0.5f, 1f);
            rh.pivot = new Vector2(0.5f, 1f);
            rh.anchoredPosition = new Vector2(0f, -30f);
            rh.sizeDelta = new Vector2(700f, 40f);
            TMP_Text hText = hdrObj.GetComponent<TMP_Text>();
            hText.text = "SELECT ABILITY TO UPGRADE (RANK 2)";
            hText.fontSize = 20f;
            hText.fontStyle = FontStyles.Bold;
            hText.alignment = TextAlignmentOptions.Center;
            hText.color = new Color(1f, 0.85f, 0.4f, 1f);
            hText.raycastTarget = false;

            // 4 Ability Buttons (grid / row)
            for (int i = 0; i < 4; i++)
            {
                float posX = -285f + (i * 190f);
                Transform slotTr = subObj.transform.Find($"AbilitySlot_{i}");
                GameObject slotObj = slotTr != null ? slotTr.gameObject : new GameObject($"AbilitySlot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                slotObj.transform.SetParent(subObj.transform, false);

                RectTransform srt = slotObj.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(posX, 10f);
                srt.sizeDelta = new Vector2(175f, 220f);

                Image sImg = slotObj.GetComponent<Image>();
                if (slotFrameSprite != null)
                {
                    sImg.sprite = slotFrameSprite;
                    sImg.type = Image.Type.Sliced;
                }
                sImg.color = new Color(0.22f, 0.26f, 0.36f, 1f);
                sImg.raycastTarget = true;

                Button sBtn = slotObj.GetComponent<Button>();
                sBtn.targetGraphic = sImg;
                abilitySlotButtons[i] = sBtn;

                // Slot text
                Transform tTr = slotObj.transform.Find("Text");
                GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                tObj.transform.SetParent(slotObj.transform, false);
                RectTransform trt = tObj.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = new Vector2(-16f, -16f);
                trt.anchoredPosition = Vector2.zero;

                TMP_Text sText = tObj.GetComponent<TMP_Text>();
                sText.fontSize = 13f;
                sText.alignment = TextAlignmentOptions.Center;
                sText.color = Color.white;
                sText.raycastTarget = false;
                abilitySlotTexts[i] = sText;
            }

            // Cancel Button
            Transform canTr = subObj.transform.Find("Cancel_Button");
            GameObject canObj = canTr != null ? canTr.gameObject : new GameObject("Cancel_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            canObj.transform.SetParent(subObj.transform, false);

            RectTransform crt = canObj.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 30f);
            crt.sizeDelta = new Vector2(180f, 44f);

            Image cImg = canObj.GetComponent<Image>();
            cImg.color = new Color(0.4f, 0.15f, 0.15f, 1f);
            cImg.raycastTarget = true;

            cancelSubmenuButton = canObj.GetComponent<Button>();
            cancelSubmenuButton.targetGraphic = cImg;

            Transform cTextTr = canObj.transform.Find("Text");
            GameObject cTextObj = cTextTr != null ? cTextTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            cTextObj.transform.SetParent(canObj.transform, false);
            RectTransform ctr = cTextObj.GetComponent<RectTransform>();
            ctr.anchorMin = Vector2.zero;
            ctr.anchorMax = Vector2.one;
            ctr.sizeDelta = Vector2.zero;
            ctr.anchoredPosition = Vector2.zero;
            TMP_Text cText = cTextObj.GetComponent<TMP_Text>();
            cText.text = "Back";
            cText.fontSize = 15f;
            cText.fontStyle = FontStyles.Bold;
            cText.alignment = TextAlignmentOptions.Center;
            cText.color = Color.white;
            cText.raycastTarget = false;
        }

        #endregion
    }
}
