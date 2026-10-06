using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Combat log history on the right edge of the screen, under the quest card. Shows the lines
    /// <see cref="CombatUIController.LogCombatMessage"/> writes (turns, attack rolls, damage, loot), newest
    /// at the bottom; older lines scroll off the top. Visible during combat and for a few seconds after,
    /// including enemy turns when the action bar is hidden.
    /// </summary>
    public class CombatLogPanel : MonoBehaviour
    {
        public const string PanelName = "Combat_Log_Panel";

        /// <summary>Seconds the log stays up after a fight ends, so the last lines can be read.</summary>
        public const float LingerSeconds = 6f;

        // Right edge, 20 %..65 % of the screen height from the bottom (35 %..80 % from the top)
        public static readonly Vector2 AnchorMin = new Vector2(1f, 0.20f);
        public static readonly Vector2 AnchorMax = new Vector2(1f, 0.65f);
        public const float PanelWidth = 300f;
        public const float RightMargin = 24f;

        private TMP_Text logText;
        private CanvasGroup group;
        private float hideAt = -1f;
        private bool wasInCombat;

        public TMP_Text LogText => logText;
        public bool IsShown => group != null && group.alpha > 0.01f;

        /// <summary>Finds the panel under <paramref name="canvas"/>, building it the first time.</summary>
        public static CombatLogPanel Ensure(RectTransform canvas, Sprite panelSprite, Sprite dividerSprite)
        {
            if (canvas == null) return null;

            Transform existing = canvas.Find(PanelName);
            GameObject panelObj = existing != null
                ? existing.gameObject
                : new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            panelObj.transform.SetParent(canvas, false);

            CombatLogPanel panel = panelObj.GetComponent<CombatLogPanel>();
            if (panel == null) panel = panelObj.AddComponent<CombatLogPanel>();
            panel.Build(panelSprite, dividerSprite);
            return panel;
        }

        private void Build(Sprite panelSprite, Sprite dividerSprite)
        {
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-RightMargin, 0f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);
            rect.localScale = Vector3.one;

            Image bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            if (panelSprite != null)
            {
                bg.sprite = panelSprite;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(1f, 1f, 1f, 0.92f);
            }
            else
            {
                bg.color = UITheme.PanelSlate;
            }
            bg.raycastTarget = false;

            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // Header
            TMP_Text header = FindOrCreateText(transform, "Combat_Log_Header");
            header.text = "COMBAT LOG";
            header.fontSize = 14f;
            header.fontStyle = FontStyles.Bold;
            header.characterSpacing = 1.5f;
            header.color = new Color(0.92f, 0.78f, 0.38f, 1f);
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.raycastTarget = false;
            RectTransform hRect = header.rectTransform;
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0f, 1f);
            hRect.anchoredPosition = new Vector2(16f, -8f);
            hRect.sizeDelta = new Vector2(-32f, 24f);

            // Divider
            Transform divTr = transform.Find("Combat_Log_Divider");
            GameObject divObj = divTr != null ? divTr.gameObject : new GameObject("Combat_Log_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divObj.transform.SetParent(transform, false);
            RectTransform dRect = (RectTransform)divObj.transform;
            dRect.anchorMin = new Vector2(0f, 1f);
            dRect.anchorMax = new Vector2(1f, 1f);
            dRect.pivot = new Vector2(0.5f, 1f);
            dRect.anchoredPosition = new Vector2(0f, -34f);
            dRect.sizeDelta = new Vector2(-32f, 3f);
            Image dImg = divObj.GetComponent<Image>();
            dImg.raycastTarget = false;
            if (dividerSprite != null)
            {
                dImg.sprite = dividerSprite;
                dImg.type = Image.Type.Sliced;
            }
            else
            {
                dImg.color = new Color(0.92f, 0.78f, 0.38f, 0.6f);
            }

            // Clipped viewport: lines grow upwards from the bottom and the oldest ones slide out of view
            Transform viewTr = transform.Find("Combat_Log_Viewport");
            GameObject viewObj = viewTr != null ? viewTr.gameObject : new GameObject("Combat_Log_Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewObj.transform.SetParent(transform, false);
            RectTransform vRect = (RectTransform)viewObj.transform;
            vRect.anchorMin = Vector2.zero;
            vRect.anchorMax = Vector2.one;
            vRect.pivot = new Vector2(0.5f, 0.5f);
            vRect.offsetMin = new Vector2(16f, 12f);
            vRect.offsetMax = new Vector2(-14f, -42f);

            logText = FindOrCreateText(viewObj.transform, "Combat_Log_Text");
            logText.fontSize = 13f;
            logText.enableAutoSizing = false;
            logText.alignment = TextAlignmentOptions.BottomLeft;
            logText.textWrappingMode = TextWrappingModes.Normal;
            logText.overflowMode = TextOverflowModes.Overflow;
            logText.richText = true;
            logText.color = UITheme.ParchmentText;
            logText.paragraphSpacing = 4f;
            logText.margin = Vector4.zero;
            logText.raycastTarget = false;
            RectTransform tRect = logText.rectTransform;
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.pivot = new Vector2(0.5f, 0f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            SetShown(IsCombatMode());
        }

        private static TMP_Text FindOrCreateText(Transform parent, string name)
        {
            Transform tr = parent.Find(name);
            if (tr == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                obj.transform.SetParent(parent, false);
                tr = obj.transform;
            }
            return tr.GetComponent<TMP_Text>();
        }

        private static bool IsCombatMode()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.CurrentMode == GamePlayMode.Combat;
        }

        private void Update()
        {
            bool inCombat = IsCombatMode();
            if (wasInCombat && !inCombat) hideAt = Time.unscaledTime + LingerSeconds;
            wasInCombat = inCombat;

            bool show = inCombat || Time.unscaledTime < hideAt;
            if (show != IsShown) SetShown(show);
        }

        private void SetShown(bool shown)
        {
            if (group == null) return;
            group.alpha = shown ? 1f : 0f;
        }
    }
}
