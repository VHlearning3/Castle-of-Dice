using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Displays detailed, beautifully styled tabletop D&D ability cards and tooltips
    /// when hovering over or selecting combat ability buttons.
    /// Features enlarged dark slate framed overlay with burnished gold border,
    /// dedicated framed ability icon slot, structured combat stats, and full description text
    /// formatted in clear English with zero clipping or text overflow.
    /// </summary>
    public class AbilityTooltipUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Serialized Fields

        [Header("Slot Identity")]
        [Tooltip("Ability slot index (0 to 3) this tooltip listener is attached to.")]
        [SerializeField] private int slotIndex = 0;

        #endregion

        #region Static Tooltip Overlay Components

        private static GameObject tooltipOverlayInstance;
        private static RectTransform tooltipRect;
        private static Image tooltipBgImage;
        private static Image tooltipIconImage;
        private static TMP_Text titleText;
        private static TMP_Text subtitleText;
        private static TMP_Text typeAndRangeText;
        private static TMP_Text formulaText;
        private static TMP_Text descriptionText;

        private static Sprite panelDarkSprite;
        private static Sprite slotFrameSprite;
        private static Sprite dividerGoldSprite;
        private static Sprite defaultIconSprite;

        #endregion

        /// <summary>
        /// Explicitly inject theme sprites from CombatUIController for runtime and WebGL compatibility.
        /// </summary>
        public static void SetThemeSprites(Sprite panelDark, Sprite slotFrame, Sprite dividerGold, Sprite defaultIcon)
        {
            if (panelDark != null) panelDarkSprite = panelDark;
            if (slotFrame != null) slotFrameSprite = slotFrame;
            if (dividerGold != null) dividerGoldSprite = dividerGold;
            if (defaultIcon != null) defaultIconSprite = defaultIcon;

            if (tooltipBgImage != null && panelDarkSprite != null)
            {
                tooltipBgImage.sprite = panelDarkSprite;
                tooltipBgImage.type = Image.Type.Sliced;
                tooltipBgImage.color = Color.white;
            }
        }

        public int SlotIndex
        {
            get => slotIndex;
            set => slotIndex = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltipForSlot(slotIndex, transform.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        /// <summary>
        /// Populates and displays the enlarged, styled ability card directly above the hovered button.
        /// </summary>
        public static void ShowTooltipForSlot(int slot, Vector3 screenWorldPos)
        {
            PlayerUnit player = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();
            if (player == null || player.ActiveAbilities == null || slot < 0 || slot >= player.ActiveAbilities.Count)
            {
                return;
            }

            AbilitySO ability = player.ActiveAbilities[slot];
            if (ability == null) return;

            EnsureTooltipOverlayExists();
            if (tooltipOverlayInstance == null) return;

            // 1. Ability Title & Subtitle
            if (titleText != null)
            {
                titleText.text = ability.AbilityName;
            }

            string archetypeStr = player.CharacterClass != null ? player.CharacterClass.ClassName : "Combat";
            string targetStr = ability.TargetType switch
            {
                AbilityTargetType.Self => "Self Buff",
                AbilityTargetType.SingleTarget => "Single Target",
                AbilityTargetType.Area3x3 => $"Area of Effect ({ability.AreaOfEffectRadius}x{ability.AreaOfEffectRadius})",
                _ => ability.TargetType.ToString()
            };

            if (subtitleText != null)
            {
                subtitleText.text = $"<color=#EBC76B>{archetypeStr} Ability</color>  •  <color=#94A3B8>{targetStr}</color>";
            }

            // 2. Framed Ability Icon
            if (tooltipIconImage != null)
            {
                Sprite iconToUse = ability.AbilityIcon;
                if (iconToUse == null && defaultIconSprite != null)
                {
                    iconToUse = defaultIconSprite;
                }

                if (iconToUse != null)
                {
                    tooltipIconImage.sprite = iconToUse;
                    tooltipIconImage.enabled = true;
                    tooltipIconImage.preserveAspect = true;
                }
                else
                {
                    tooltipIconImage.enabled = false;
                }
            }

            // 3. Range & Target Profile
            string rangeStr = ability.TargetType == AbilityTargetType.Self
                ? "Self (In-place)"
                : $"{ability.Range} Tile{(ability.Range > 1 ? "s" : "")}";

            if (typeAndRangeText != null)
            {
                typeAndRangeText.text = $"<color=#F6D578>Range:</color> {rangeStr}   |   <color=#F6D578>Targeting:</color> {targetStr}";
            }

            // 4. Attack Check & Damage / Potency Formula
            if (formulaText != null)
            {
                string checkStr = ability.RequiresCheck ? "d20 + Bonus >= Enemy AC" : "Automatic Success";
                string dmgStr;

                if (ability.DealsDamage)
                {
                    // Area spells do not use the blacksmith's weapon upgrade
                    bool usesWeapon = ability.TargetType == AbilityTargetType.SingleTarget;
                    int weaponBonus = usesWeapon ? player.WeaponDamageBonus : 0;
                    string bonusStr = weaponBonus > 0 ? $" (incl. +{weaponBonus} Blacksmith)" : "";
                    dmgStr = $"{ability.GetDamageFormula(player.PrimaryAttributeBonus, weaponBonus)} Damage{bonusStr}";
                }
                else
                {
                    dmgStr = "Defensive Stance / Status Buff";
                }

                formulaText.text = $"<color=#E67E22>Hit Check:</color> {checkStr}\n<color=#E74C3C>Potency:</color> {dmgStr}";
            }

            // 5. Full Ability Description (Generously wrapped, never clipped)
            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(ability.Description)
                    ? "A specialized combat maneuver honed in the dungeons of the Castle."
                    : ability.Description;
            }

            tooltipOverlayInstance.SetActive(true);
            tooltipOverlayInstance.transform.SetAsLastSibling();

            // Force layout recalculation so rect height fits all content lines dynamically
            if (tooltipRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

                float width = tooltipRect.rect.width > 0 ? tooltipRect.rect.width : 440f;
                float height = tooltipRect.rect.height > 0 ? tooltipRect.rect.height : 200f;
                float halfW = width * 0.5f;

                // Smart positioning: Center above button with clamp to prevent screen border clipping
                float clampedX = Mathf.Clamp(screenWorldPos.x, halfW + 16f, Screen.width - halfW - 16f);
                float targetY = screenWorldPos.y + 65f;

                if (targetY + height > Screen.height - 16f)
                {
                    targetY = Screen.height - height - 16f;
                }
                targetY = Mathf.Max(16f, targetY);

                tooltipRect.pivot = new Vector2(0.5f, 0f);
                tooltipRect.position = new Vector3(clampedX, targetY, 0f);
            }
        }

        /// <summary>
        /// Conceals the ability tooltip overlay.
        /// </summary>
        public static void HideTooltip()
        {
            if (tooltipOverlayInstance != null)
            {
                tooltipOverlayInstance.SetActive(false);
            }
        }

        private static void LoadThemeSprites()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (defaultIconSprite == null)
                defaultIconSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_Sword.png");
        }

        private static void EnsureTooltipOverlayExists()
        {
            if (tooltipOverlayInstance != null) return;

            LoadThemeSprites();

            Canvas canvas = null;
            var combatUI = UnityEngine.Object.FindAnyObjectByType<CombatUIController>();
            if (combatUI != null)
            {
                canvas = combatUI.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                var hud = UnityEngine.Object.FindAnyObjectByType<PlayerHUD>();
                if (hud != null)
                {
                    canvas = hud.GetComponentInParent<Canvas>();
                }
            }

            if (canvas == null)
            {
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var c in canvases)
                {
                    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas = c;
                        break;
                    }
                }
                if (canvas == null && canvases.Length > 0) canvas = canvases[0];
            }
            if (canvas == null) return;

            // 1. Root Overlay Box
            GameObject overlay = new GameObject("AbilityTooltipOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(canvas.transform, false);
            tooltipRect = overlay.GetComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(440f, 200f);
            tooltipRect.pivot = new Vector2(0.5f, 0f);

            tooltipBgImage = overlay.GetComponent<Image>();
            if (panelDarkSprite != null)
            {
                tooltipBgImage.sprite = panelDarkSprite;
                tooltipBgImage.type = Image.Type.Sliced;
                tooltipBgImage.color = Color.white;
            }
            else
            {
                tooltipBgImage.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            }
            tooltipBgImage.raycastTarget = false;

            // Layout & Content Size Fitter
            VerticalLayoutGroup mainLayout = overlay.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(16, 16, 14, 14);
            mainLayout.spacing = 6f;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;

            ContentSizeFitter csf = overlay.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement le = overlay.AddComponent<LayoutElement>();
            le.minHeight = 180f;

            // 2. Header Row (Framed Icon + Title & Subtitle Column)
            GameObject headerRow = new GameObject("Tooltip_Header_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            headerRow.transform.SetParent(overlay.transform, false);

            LayoutElement headerLE = headerRow.GetComponent<LayoutElement>();
            headerLE.minHeight = 48f;
            headerLE.preferredHeight = 48f;

            HorizontalLayoutGroup headerLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = false;
            headerLayout.childControlHeight = false;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            // 2A. Framed Ability Icon Slot
            GameObject iconSlot = new GameObject("Ability_Icon_Slot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            iconSlot.transform.SetParent(headerRow.transform, false);
            RectTransform slotRect = iconSlot.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(48f, 48f);

            LayoutElement slotLE = iconSlot.GetComponent<LayoutElement>();
            slotLE.minWidth = 48f;
            slotLE.minHeight = 48f;
            slotLE.preferredWidth = 48f;
            slotLE.preferredHeight = 48f;

            Image slotImg = iconSlot.GetComponent<Image>();
            if (slotFrameSprite != null)
            {
                slotImg.sprite = slotFrameSprite;
                slotImg.type = Image.Type.Sliced;
                slotImg.color = Color.white;
            }

            GameObject iconChild = new GameObject("Ability_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconChild.transform.SetParent(iconSlot.transform, false);
            RectTransform iconRect = iconChild.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = new Vector2(-10f, -10f);
            iconRect.anchoredPosition = Vector2.zero;

            tooltipIconImage = iconChild.GetComponent<Image>();
            tooltipIconImage.preserveAspect = true;
            tooltipIconImage.raycastTarget = false;

            // 2B. Title & Subtitle Column
            GameObject titleCol = new GameObject("Title_Column", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            titleCol.transform.SetParent(headerRow.transform, false);

            LayoutElement colLE = titleCol.GetComponent<LayoutElement>();
            colLE.preferredWidth = 320f;
            colLE.minHeight = 44f;

            VerticalLayoutGroup colLayout = titleCol.GetComponent<VerticalLayoutGroup>();
            colLayout.spacing = 2f;
            colLayout.childControlWidth = true;
            colLayout.childControlHeight = false;
            colLayout.childForceExpandWidth = true;
            colLayout.childForceExpandHeight = false;

            // Title
            GameObject titleObj = new GameObject("Tooltip_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(titleCol.transform, false);
            titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 18f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = UITheme.GoldAccent; // #F6D578
            titleText.alignment = TextAlignmentOptions.TopLeft;
            titleText.enableAutoSizing = false;
            titleText.raycastTarget = false;

            // Subtitle
            GameObject subObj = new GameObject("Tooltip_Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(titleCol.transform, false);
            subtitleText = subObj.GetComponent<TextMeshProUGUI>();
            subtitleText.fontSize = 11.5f;
            subtitleText.color = new Color(0.58f, 0.64f, 0.72f, 1f);
            subtitleText.alignment = TextAlignmentOptions.TopLeft;
            subtitleText.enableAutoSizing = false;
            subtitleText.raycastTarget = false;

            // 3. Ornate Filigree Gold Divider
            GameObject divObj = new GameObject("Tooltip_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            divObj.transform.SetParent(overlay.transform, false);
            LayoutElement divLE = divObj.GetComponent<LayoutElement>();
            divLE.minHeight = 3f;
            divLE.preferredHeight = 3f;

            Image divImg = divObj.GetComponent<Image>();
            if (dividerGoldSprite != null)
            {
                divImg.sprite = dividerGoldSprite;
                divImg.type = Image.Type.Simple;
                divImg.color = Color.white;
            }
            divImg.raycastTarget = false;

            // 4. Stats Container (Range & Hit Formula)
            GameObject statsObj = new GameObject("Stats_Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
            statsObj.transform.SetParent(overlay.transform, false);
            VerticalLayoutGroup statsLayout = statsObj.GetComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 3f;
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = false;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = false;

            // Range / Targeting
            GameObject typeObj = new GameObject("Tooltip_TypeRange", typeof(RectTransform), typeof(TextMeshProUGUI));
            typeObj.transform.SetParent(statsObj.transform, false);
            typeAndRangeText = typeObj.GetComponent<TextMeshProUGUI>();
            typeAndRangeText.fontSize = 12.5f;
            typeAndRangeText.color = UITheme.SoftText;
            typeAndRangeText.enableAutoSizing = false;
            typeAndRangeText.raycastTarget = false;

            // Formula / Potency
            GameObject formulaObj = new GameObject("Tooltip_Formula", typeof(RectTransform), typeof(TextMeshProUGUI));
            formulaObj.transform.SetParent(statsObj.transform, false);
            formulaText = formulaObj.GetComponent<TextMeshProUGUI>();
            formulaText.fontSize = 12.5f;
            formulaText.color = new Color(0.95f, 0.85f, 0.70f, 1f);
            formulaText.enableAutoSizing = false;
            formulaText.raycastTarget = false;

            // 5. Description Paragraph (Generous space, no truncation)
            GameObject descObj = new GameObject("Tooltip_Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(overlay.transform, false);
            descriptionText = descObj.GetComponent<TextMeshProUGUI>();
            descriptionText.fontSize = 12.5f;
            descriptionText.color = UITheme.ParchmentText; // #EDE6D8
            descriptionText.lineSpacing = 2f;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.enableAutoSizing = false;
            descriptionText.raycastTarget = false;

            tooltipOverlayInstance = overlay;
            tooltipOverlayInstance.SetActive(false);
        }
    }
}
