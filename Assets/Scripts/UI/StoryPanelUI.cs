using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// A parchment panel over the game for text the player reads: letters and diaries found in the zones (C3),
    /// the story intro after New Adventure and the combat tips (D2), and the help page of the pause menu (D1).
    /// Built at runtime on its own canvas; exploration input pauses while it is open.
    /// </summary>
    public class StoryPanelUI : MonoBehaviour
    {
        private static StoryPanelUI instance;

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI pageText;
        private Button nextButton;
        private Button skipButton;
        private TextMeshProUGUI nextLabel;
        private RectTransform panelRect;

        private const float PanelWidth = 860f;
        private const float MinPanelHeight = 600f;
        private const float ScreenMargin = 60f;
        private const float TitleTop = 60f;
        private const float BodyTop = 100f;
        private const float BodyBottom = 95f;
        private const float BodySideMargin = 50f;
        private const float ButtonBottom = 50f;
        private const float BodyFontSize = 23f;
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private string[] pageTitles;
        private string[] pageBodies;
        private int pageIndex;
        private Action onClosed;
        private bool inputWasEnabled = true;

        /// <summary>True while a panel is on screen.</summary>
        public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

        /// <summary>Shows one page (a letter, a tip, the help text).</summary>
        public static void Show(string title, string body, Action closed = null)
        {
            ShowPages(new[] { title }, new[] { body }, closed);
        }

        /// <summary>Shows several pages with Next / Skip (the intro).</summary>
        public static void ShowPages(string[] titles, string[] bodies, Action closed = null)
        {
            if (titles == null || bodies == null || titles.Length == 0) return;
            StoryPanelUI panel = GetOrCreate();
            if (!panel.gameObject.activeSelf)
            {
                panel.inputWasEnabled = GameInput.IsExplorationInputEnabled;
            }
            panel.pageTitles = titles;
            panel.pageBodies = bodies;
            panel.pageIndex = 0;
            panel.onClosed = closed;
            panel.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            GameInput.SetExplorationInputEnabled(false);
            panel.ShowPage();
        }

        /// <summary>Closes the panel (Esc, tests).</summary>
        public static void CloseIfOpen()
        {
            if (IsOpen) instance.Close();
        }

        private static StoryPanelUI GetOrCreate()
        {
            if (instance != null) return instance;

            GameObject canvasObj = new GameObject("StoryPanel_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            if (Application.isPlaying) MainMenuController.EnsureEventSystem();

            // Dim the game behind the parchment
            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvasObj.transform, false);
            RectTransform dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            GameObject panelObj = new GameObject("StoryPanel", typeof(RectTransform), typeof(Image));
            panelObj.transform.SetParent(canvasObj.transform, false);
            RectTransform panelRect = (RectTransform)panelObj.transform;
            panelRect.sizeDelta = new Vector2(PanelWidth, MinPanelHeight);
            panelObj.GetComponent<Image>().color = new Color(0.17f, 0.13f, 0.09f, 0.97f);

            StoryPanelUI ui = canvasObj.AddComponent<StoryPanelUI>();
            ui.panelRect = panelRect;

            // Title pinned to the top edge, buttons to the bottom edge and the body fills the space between,
            // so a long page (the help text) can grow the panel without running under the Close button
            ui.titleText = CreateText(panelObj.transform, "Title", new Vector2(0f, -TitleTop), new Vector2(780f, 70f), 34f, FontStyles.Bold, UITheme.GoldAccent);
            Pin((RectTransform)ui.titleText.transform, 1f);
            ui.bodyText = CreateText(panelObj.transform, "Body", Vector2.zero, Vector2.zero, BodyFontSize, FontStyles.Normal, UITheme.ParchmentText);
            ui.bodyText.alignment = TextAlignmentOptions.TopLeft;
            RectTransform bodyRect = (RectTransform)ui.bodyText.transform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(BodySideMargin, BodyBottom);
            bodyRect.offsetMax = new Vector2(-BodySideMargin, -BodyTop);
            ui.bodyText.fontSizeMax = BodyFontSize;
            ui.bodyText.fontSizeMin = 16f;
            ui.bodyText.enableAutoSizing = true;
            ui.pageText = CreateText(panelObj.transform, "Page", new Vector2(-300f, ButtonBottom), new Vector2(160f, 40f), 18f, FontStyles.Italic, UITheme.SoftText);
            Pin((RectTransform)ui.pageText.transform, 0f);

            ui.skipButton = UIFactory.CreateTextButton(panelObj.transform, "Skip_Btn", "Skip", new Vector2(150f, ButtonBottom), new Vector2(150f, 48f), new Color(0.35f, 0.25f, 0.2f));
            Pin((RectTransform)ui.skipButton.transform, 0f);
            ui.skipButton.onClick.AddListener(ui.Close);
            ui.nextButton = UIFactory.CreateTextButton(panelObj.transform, "Next_Btn", "Close", new Vector2(320f, ButtonBottom), new Vector2(170f, 48f), UITheme.ActionGreen);
            Pin((RectTransform)ui.nextButton.transform, 0f);
            ui.nextButton.onClick.AddListener(ui.Next);
            ui.nextLabel = ui.nextButton.GetComponentInChildren<TextMeshProUGUI>();

            instance = ui;
            return ui;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, FontStyles style, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Keeps a child at its offset from the panel's top (1) or bottom (0) edge as the panel grows.</summary>
        private static void Pin(RectTransform rect, float edge)
        {
            Vector2 pos = rect.anchoredPosition;
            rect.anchorMin = new Vector2(0.5f, edge);
            rect.anchorMax = new Vector2(0.5f, edge);
            rect.anchoredPosition = pos;
        }

        /// <summary>
        /// Panel height for a body text that needs <paramref name="bodyHeight"/> units: never below the
        /// standard 600, never taller than the canvas allows at the current screen size.
        /// </summary>
        public static float PanelHeightFor(float bodyHeight, float canvasHeight)
        {
            float wanted = bodyHeight + BodyTop + BodyBottom + 10f;
            float max = Mathf.Max(MinPanelHeight, canvasHeight - ScreenMargin * 2f);
            return Mathf.Clamp(wanted, MinPanelHeight, max);
        }

        /// <summary>Height of the 1920x1080, match 0.5 canvas on the current screen.</summary>
        private static float CanvasHeight()
        {
            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            float logScale = Mathf.Lerp(Mathf.Log(w / ReferenceResolution.x, 2f), Mathf.Log(h / ReferenceResolution.y, 2f), 0.5f);
            return h / Mathf.Pow(2f, logScale);
        }

        /// <summary>The panel's current height (tests).</summary>
        public static float CurrentPanelHeight => instance != null && instance.panelRect != null ? instance.panelRect.sizeDelta.y : 0f;

        /// <summary>The body text component (tests).</summary>
        public static TextMeshProUGUI CurrentBody => instance != null ? instance.bodyText : null;

        private void FitPanelToBody()
        {
            if (panelRect == null) return;
            float bodyWidth = PanelWidth - BodySideMargin * 2f;
            bodyText.enableAutoSizing = false;
            bodyText.fontSize = BodyFontSize;
            float needed = bodyText.GetPreferredValues(bodyText.text, bodyWidth, 0f).y;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeightFor(needed, CanvasHeight()));

            // Auto-size shrinks the font only if even the tallest panel the screen allows is too short
            bodyText.enableAutoSizing = true;
            bodyText.ForceMeshUpdate();
        }

        private void ShowPage()
        {
            int count = pageBodies.Length;
            titleText.text = pageIndex < pageTitles.Length ? pageTitles[pageIndex] : pageTitles[pageTitles.Length - 1];
            bodyText.text = pageBodies[pageIndex];
            FitPanelToBody();
            pageText.text = count > 1 ? $"{pageIndex + 1} / {count}" : string.Empty;
            bool last = pageIndex >= count - 1;
            nextLabel.text = last ? "Close" : "Next";
            skipButton.gameObject.SetActive(!last);
        }

        private void Next()
        {
            if (pageIndex < pageBodies.Length - 1)
            {
                pageIndex++;
                ShowPage();
                return;
            }
            Close();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            GameInput.SetExplorationInputEnabled(inputWasEnabled);
            Action closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
