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
    /// Displays detailed ability cards and tooltips when hovering or selecting combat ability buttons.
    /// Fulfills the "Ability textit" requirement from the notebook.
    /// </summary>
    public class AbilityTooltipUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Serialized Fields

        [Header("Slot Identity")]
        [Tooltip("Ability slot index (0 to 3) this tooltip listener is attached to.")]
        [SerializeField] private int slotIndex = 0;

        #endregion

        #region Static Tooltip Overlay

        private static GameObject tooltipOverlayInstance;
        private static TMP_Text titleText;
        private static TMP_Text typeAndRangeText;
        private static TMP_Text formulaText;
        private static TMP_Text descriptionText;
        private static RectTransform tooltipRect;

        #endregion

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

            // Populate ability card details
            if (titleText != null) titleText.text = ability.AbilityName;

            string targetStr = ability.TargetType switch
            {
                AbilityTargetType.Self => "Itseen (Self)",
                AbilityTargetType.SingleTarget => "Yksittäinen kohde (Single Target)",
                AbilityTargetType.Area3x3 => $"Alue (AOE: {ability.AreaOfEffectRadius}x{ability.AreaOfEffectRadius})",
                _ => ability.TargetType.ToString()
            };
            string rangeStr = ability.TargetType == AbilityTargetType.Self ? "-" : $"{ability.Range} ruutua";

            if (typeAndRangeText != null)
            {
                typeAndRangeText.text = $"<color=#F1C40F>Tyyppi:</color> {targetStr}   |   <color=#F1C40F>Kantama:</color> {rangeStr}";
            }

            if (formulaText != null)
            {
                string checkStr = ability.RequiresCheck ? "d20 + Bonus ≥ AC" : "Automaattinen";
                string dmgStr = ability.BaseValue > 0 ? $"{ability.BaseValue} vahinkoa" : "Puolustus / Tehoste";
                if (player.WeaponDamageBonus > 0 && ability.BaseValue > 0)
                {
                    dmgStr += $" (+{player.WeaponDamageBonus} seppä)";
                }
                formulaText.text = $"<color=#E67E22>Osuma:</color> {checkStr}   |   <color=#E74C3C>Vahinko:</color> {dmgStr}";
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(ability.Description)
                    ? "Tehokas sankariluokan erikoiskyky."
                    : ability.Description;
            }

            tooltipOverlayInstance.SetActive(true);

            // Position tooltip nicely above the button
            if (tooltipRect != null)
            {
                tooltipRect.position = screenWorldPos + new Vector3(0f, 130f, 0f);
            }
        }

        public static void HideTooltip()
        {
            if (tooltipOverlayInstance != null)
            {
                tooltipOverlayInstance.SetActive(false);
            }
        }

        private static void EnsureTooltipOverlayExists()
        {
            if (tooltipOverlayInstance != null) return;

            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject overlay = new GameObject("AbilityTooltipOverlay");
            overlay.transform.SetParent(canvas.transform, false);
            tooltipRect = overlay.AddComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(340f, 150f);
            tooltipRect.pivot = new Vector2(0.5f, 0f);

            Image bg = overlay.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);
            bg.raycastTarget = false;

            // Outline
            Outline outline = overlay.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Container for text lines
            VerticalLayoutGroup layout = overlay.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Title
            GameObject titleObj = new GameObject("Tooltip_Title");
            titleObj.transform.SetParent(overlay.transform, false);
            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.fontSize = 18f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.85f, 0.3f);
            titleText.alignment = TextAlignmentOptions.TopLeft;

            // Type & Range
            GameObject typeObj = new GameObject("Tooltip_TypeRange");
            typeObj.transform.SetParent(overlay.transform, false);
            typeAndRangeText = typeObj.AddComponent<TextMeshProUGUI>();
            typeAndRangeText.fontSize = 13f;
            typeAndRangeText.color = new Color(0.85f, 0.85f, 0.85f);

            // Formula
            GameObject formulaObj = new GameObject("Tooltip_Formula");
            formulaObj.transform.SetParent(overlay.transform, false);
            formulaText = formulaObj.AddComponent<TextMeshProUGUI>();
            formulaText.fontSize = 13f;
            formulaText.color = new Color(0.95f, 0.75f, 0.5f);

            // Description
            GameObject descObj = new GameObject("Tooltip_Desc");
            descObj.transform.SetParent(overlay.transform, false);
            descriptionText = descObj.AddComponent<TextMeshProUGUI>();
            descriptionText.fontSize = 12f;
            descriptionText.color = new Color(0.75f, 0.75f, 0.75f);
            descriptionText.textWrappingMode = TextWrappingModes.Normal;

            tooltipOverlayInstance = overlay;
            tooltipOverlayInstance.SetActive(false);
        }
    }
}
