using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Shared builders for procedurally created UI so every panel produces identical, consistently styled controls.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>Default label size used by modal and menu buttons.</summary>
        public const float ButtonFontSize = 17f;

        /// <summary>
        /// Creates a solid-colour button with a centred bold label. Hover/press tints derive from <paramref name="color"/>.
        /// </summary>
        public static Button CreateTextButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image img = btnObj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = true;
            Navigation nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = color * 1.3f;
            cb.pressedColor = color * 0.8f;
            cb.selectedColor = color * 1.2f;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = ButtonFontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btn;
        }
    }
}
