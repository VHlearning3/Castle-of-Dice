using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>How a lockpick minigame session ended.</summary>
    public enum LockpickOutcome
    {
        Unlocked,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Modal window for the Rogue lockpick minigame, plus the short "Can't lockpick" popup for other classes.
    /// Builds its own overlay canvas on first use, so no scene needs to contain it.
    /// SPACE or left click sets the pin while the marker is in the gold zone; ESC steps away from the lock.
    /// </summary>
    public class LockpickMinigameUI : MonoBehaviour
    {
        #region Singleton

        public static LockpickMinigameUI Instance { get; private set; }

        /// <summary>Whether a lockpick minigame is currently on screen.</summary>
        public static bool IsOpen => Instance != null && Instance.game != null;

        #endregion

        #region Layout Constants

        private const float BarWidth = 600f;
        private const float BarHeight = 36f;
        private const float PinSize = 34f;
        private const float PinSpacing = 52f;
        private const float ResultHoldSeconds = 0.7f;
        private const float SlipFlashSeconds = 0.25f;
        private const float ToastSeconds = 1.6f;

        private static readonly Color BarColor = new Color(0.07f, 0.08f, 0.11f, 1f);
        private static readonly Color SlipColor = new Color(0.75f, 0.18f, 0.16f, 1f);
        private static readonly Color PinDownColor = new Color(0.30f, 0.32f, 0.38f, 1f);

        #endregion

        #region Private State

        private GameObject modalRoot;
        private TMP_Text titleText;
        private TMP_Text slipsText;
        private RectTransform barRect;
        private Image barImage;
        private RectTransform sweetZoneRect;
        private RectTransform markerRect;
        private RectTransform pinRow;
        private Image[] pinImages = new Image[0];

        private GameObject toastRoot;
        private TMP_Text toastText;
        private float toastTimer;

        private bool isBuilt;
        private LockpickMinigame game;
        private Action<LockpickOutcome> onFinished;
        private int openedFrame;
        private bool resolving;
        private LockpickOutcome pendingOutcome;
        private float resolveTimer;
        private float slipFlashTimer;

        #endregion

        #region Public API

        /// <summary>Opens the minigame for a lock; <paramref name="finished"/> is called once when it ends.</summary>
        public static void Open(LockpickMinigame minigame, string title, Action<LockpickOutcome> finished)
        {
            if (minigame == null) return;
            EnsureInstance().Begin(minigame, title, finished);
        }

        /// <summary>Shows a short centred popup message (e.g. "Can't lockpick").</summary>
        public static void ShowToast(string message)
        {
            EnsureInstance().ShowToastInternal(message);
        }

        private static LockpickMinigameUI EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject root = new GameObject("LockpickMinigameUI");
                Instance = root.AddComponent<LockpickMinigameUI>();
            }
            Instance.BuildUI();
            return Instance;
        }

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
            BuildUI();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            // Never leave the hero frozen if the scene unloads mid-minigame
            if (game != null)
            {
                GameInput.SetExplorationInputEnabled(true);
            }
            Instance = null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                if (toastTimer <= 0f) toastRoot.SetActive(false);
            }

            if (game == null) return;

            if (slipFlashTimer > 0f)
            {
                slipFlashTimer -= dt;
                if (slipFlashTimer <= 0f) barImage.color = BarColor;
            }

            // Hold the final pin / slip on screen for a moment before closing
            if (resolving)
            {
                resolveTimer -= dt;
                if (resolveTimer <= 0f) Finish(pendingOutcome);
                return;
            }

            game.Tick(dt);
            PositionMarker();

            // The click that opened the lock must not also set the first pin
            if (Time.frameCount == openedFrame) return;

            if (GameInput.GetKeyDown(KeyCode.Escape))
            {
                Finish(LockpickOutcome.Cancelled);
                return;
            }

            if (GameInput.GetKeyDown(KeyCode.Space) || GameInput.GetLeftMouseButtonDown())
            {
                HandlePress(game.Press());
            }
        }

        #endregion

        #region Minigame Flow

        private void Begin(LockpickMinigame minigame, string title, Action<LockpickOutcome> finished)
        {
            if (game != null) return;

            game = minigame;
            onFinished = finished;
            openedFrame = Time.frameCount;
            resolving = false;
            slipFlashTimer = 0f;

            titleText.text = string.IsNullOrEmpty(title) ? "Pick the Lock" : title;
            barImage.color = BarColor;
            BuildPins(game.PinCount);
            RefreshPins();
            RefreshSlips();
            PositionSweetZone();
            PositionMarker();

            modalRoot.SetActive(true);
            GameInput.SetExplorationInputEnabled(false);
        }

        private void HandlePress(LockpickPressResult result)
        {
            switch (result)
            {
                case LockpickPressResult.PinSet:
                    PlaySfx(SFXClipType.ButtonClick);
                    RefreshPins();
                    PositionSweetZone();
                    break;

                case LockpickPressResult.Slipped:
                    PlaySfx(SFXClipType.CriticalFailure);
                    FlashSlip();
                    RefreshSlips();
                    break;

                case LockpickPressResult.Unlocked:
                    PlaySfx(SFXClipType.DoorOpen);
                    RefreshPins();
                    BeginResolve(LockpickOutcome.Unlocked);
                    break;

                case LockpickPressResult.Broken:
                    PlaySfx(SFXClipType.CriticalFailure);
                    FlashSlip();
                    RefreshSlips();
                    BeginResolve(LockpickOutcome.Failed);
                    break;
            }
        }

        private void BeginResolve(LockpickOutcome outcome)
        {
            resolving = true;
            pendingOutcome = outcome;
            resolveTimer = ResultHoldSeconds;
        }

        private void Finish(LockpickOutcome outcome)
        {
            Action<LockpickOutcome> callback = onFinished;
            game = null;
            onFinished = null;
            resolving = false;

            modalRoot.SetActive(false);
            GameInput.SetExplorationInputEnabled(true);

            callback?.Invoke(outcome);
        }

        #endregion

        #region Visual Refresh

        private void PositionMarker()
        {
            markerRect.anchoredPosition = new Vector2((game.MarkerPosition - 0.5f) * BarWidth, 0f);
        }

        private void PositionSweetZone()
        {
            sweetZoneRect.sizeDelta = new Vector2(game.SweetZoneWidth * BarWidth, BarHeight);
            sweetZoneRect.anchoredPosition = new Vector2((game.SweetZoneCenter - 0.5f) * BarWidth, 0f);
        }

        private void RefreshPins()
        {
            for (int i = 0; i < pinImages.Length; i++)
            {
                pinImages[i].color = i < game.PinsSet ? UITheme.CoinGold : PinDownColor;
            }
        }

        private void RefreshSlips()
        {
            slipsText.text = "Slips " + game.Slips + " / " + game.MaxSlips;
        }

        private void FlashSlip()
        {
            barImage.color = SlipColor;
            slipFlashTimer = SlipFlashSeconds;
        }

        private void PlaySfx(SFXClipType clip)
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(clip);
        }

        #endregion

        #region Toast

        private void ShowToastInternal(string message)
        {
            toastText.text = message;
            toastRoot.SetActive(true);
            toastTimer = ToastSeconds;
        }

        /// <summary>Text of the popup currently on screen, or null when none is showing.</summary>
        public string VisibleToastMessage => toastRoot != null && toastRoot.activeSelf ? toastText.text : null;

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            if (isBuilt) return;
            isBuilt = true;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dimmer; its "Modal" name keeps the world click raycaster from reaching through
            modalRoot = new GameObject("LockpickModal", typeof(RectTransform));
            modalRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)modalRoot.transform);
            Image dim = modalRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = true;

            RectTransform panel = CreateRect("LockpickModal_Panel", modalRoot.transform, new Vector2(720f, 330f), Vector2.zero);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            Sprite panelSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = Color.white;
            }
            else
            {
                panelImage.color = UITheme.PanelSlate;
            }

            titleText = CreateText("Title", panel, "Pick the Lock", 34f, UITheme.GoldAccent, new Vector2(0f, 120f), new Vector2(660f, 48f));
            titleText.fontStyle = FontStyles.Bold;

            pinRow = CreateRect("Pins", panel, new Vector2(BarWidth, PinSize), new Vector2(0f, 60f));

            barRect = CreateRect("Bar", panel, new Vector2(BarWidth, BarHeight), new Vector2(0f, 0f));
            barImage = barRect.gameObject.AddComponent<Image>();
            barImage.color = BarColor;

            sweetZoneRect = CreateRect("GoldZone", barRect, new Vector2(100f, BarHeight), Vector2.zero);
            Image zone = sweetZoneRect.gameObject.AddComponent<Image>();
            zone.color = UITheme.CoinGold;
            zone.raycastTarget = false;

            markerRect = CreateRect("Pick", barRect, new Vector2(10f, BarHeight + 18f), Vector2.zero);
            Image marker = markerRect.gameObject.AddComponent<Image>();
            marker.color = UITheme.CreamText;
            marker.raycastTarget = false;

            slipsText = CreateText("Slips", panel, "Slips 0 / 2", 22f, UITheme.SoftText, new Vector2(0f, -55f), new Vector2(660f, 32f));
            CreateText("Hint", panel, "SPACE or click when the pick is in the gold zone     ESC: step away", 18f, UITheme.ParchmentText, new Vector2(0f, -110f), new Vector2(680f, 30f));

            modalRoot.SetActive(false);

            // Popup text for classes that cannot pick locks
            toastRoot = new GameObject("LockpickPopup", typeof(RectTransform));
            toastRoot.transform.SetParent(transform, false);
            RectTransform toastRect = (RectTransform)toastRoot.transform;
            toastRect.sizeDelta = new Vector2(520f, 70f);
            toastRect.anchoredPosition = new Vector2(0f, 180f);
            Image toastBg = toastRoot.AddComponent<Image>();
            toastBg.color = UITheme.PanelAbyss;
            toastBg.raycastTarget = false;
            toastText = CreateText("Text", toastRect, string.Empty, 30f, UITheme.CreamText, Vector2.zero, new Vector2(500f, 60f));
            toastText.fontStyle = FontStyles.Bold;
            toastRoot.SetActive(false);
        }

        private void BuildPins(int count)
        {
            if (pinImages.Length == count) return;

            for (int i = pinRow.childCount - 1; i >= 0; i--)
            {
                Destroy(pinRow.GetChild(i).gameObject);
            }

            pinImages = new Image[count];
            float startX = -(count - 1) * PinSpacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                RectTransform pin = CreateRect("Pin" + (i + 1), pinRow, new Vector2(PinSize, PinSize), new Vector2(startX + i * PinSpacing, 0f));
                Image img = pin.gameObject.AddComponent<Image>();
                img.color = PinDownColor;
                img.raycastTarget = false;
                pinImages[i] = img;
            }
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            RectTransform rect = CreateRect(name, parent, box, position);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
