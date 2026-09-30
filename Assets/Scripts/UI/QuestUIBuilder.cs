using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Small helpers that build the quest journal and quest notification UI in code
    /// (own overlay canvas, dark fantasy panels, TMP labels).
    /// </summary>
    public static class QuestUIBuilder
    {
        /// <summary>
        /// Creates a screen-space overlay canvas (1920x1080 reference, match 0.5) under <paramref name="parent"/>.
        /// </summary>
        public static Canvas CreateOverlayCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject canvasObj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(parent, false);

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>
        /// Creates a dark panel (themed sprite when available) anchored at <paramref name="anchor"/>.
        /// </summary>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color fallbackColor)
        {
            GameObject panelObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObj.transform.SetParent(parent, false);

            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = panelObj.GetComponent<Image>();
            Sprite panelSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (panelSprite != null)
            {
                image.sprite = panelSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = fallbackColor;
            }

            return rect;
        }

        /// <summary>
        /// Creates a TMP label stretched inside <paramref name="parent"/> with the given padding (left, top, right, bottom).
        /// </summary>
        public static TMP_Text CreateLabel(Transform parent, string name, float fontSize, FontStyles style, Color color,
            TextAlignmentOptions alignment, Vector4 padding)
        {
            GameObject labelObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding.x, padding.w);
            rect.offsetMax = new Vector2(-padding.z, -padding.y);

            TMP_Text text = labelObj.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }
    }
}
