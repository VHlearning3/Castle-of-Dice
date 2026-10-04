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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
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
            panelRect.sizeDelta = new Vector2(860f, 600f);
            panelObj.GetComponent<Image>().color = new Color(0.17f, 0.13f, 0.09f, 0.97f);

            StoryPanelUI ui = canvasObj.AddComponent<StoryPanelUI>();
            ui.titleText = CreateText(panelObj.transform, "Title", new Vector2(0f, 240f), new Vector2(780f, 70f), 34f, FontStyles.Bold, UITheme.GoldAccent);
            ui.bodyText = CreateText(panelObj.transform, "Body", new Vector2(0f, 10f), new Vector2(760f, 380f), 23f, FontStyles.Normal, UITheme.ParchmentText);
            ui.bodyText.alignment = TextAlignmentOptions.TopLeft;
            ui.pageText = CreateText(panelObj.transform, "Page", new Vector2(-300f, -250f), new Vector2(160f, 40f), 18f, FontStyles.Italic, UITheme.SoftText);

            ui.skipButton = UIFactory.CreateTextButton(panelObj.transform, "Skip_Btn", "Skip", new Vector2(150f, -250f), new Vector2(150f, 48f), new Color(0.35f, 0.25f, 0.2f));
            ui.skipButton.onClick.AddListener(ui.Close);
            ui.nextButton = UIFactory.CreateTextButton(panelObj.transform, "Next_Btn", "Close", new Vector2(320f, -250f), new Vector2(170f, 48f), UITheme.ActionGreen);
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

        private void ShowPage()
        {
            int count = pageBodies.Length;
            titleText.text = pageIndex < pageTitles.Length ? pageTitles[pageIndex] : pageTitles[pageTitles.Length - 1];
            bodyText.text = pageBodies[pageIndex];
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
