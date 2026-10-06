using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// The mage's book-ordering minigame (Vili, 2026-10-06): a 2D window with the Witch's shelf (five places)
    /// and her five tomes on the table below. Click a tome, then a place on the shelf; click a shelved tome to
    /// take it back. When the shelf is full the ward checks the order: right opens the great bookshelves,
    /// wrong pushes the tomes back to the table. ESC steps away. Builds its own overlay canvas on first use.
    /// </summary>
    public class TomeShelfMinigameUI : MonoBehaviour
    {
        public static TomeShelfMinigameUI Instance { get; private set; }

        /// <summary>Whether the minigame is on screen.</summary>
        public static bool IsOpen => Instance != null && Instance.modalRoot != null && Instance.modalRoot.activeSelf;

        // Order the tomes lie on the table (never the solved order)
        private static readonly int[] TableOrder = { 3, 0, 4, 2, 1 };

        private static readonly Color[] SpineColors =
        {
            new Color(0.86f, 0.62f, 0.16f, 1f),  // Sun: gold
            new Color(0.55f, 0.66f, 0.82f, 1f),  // Moon: silver blue
            new Color(0.48f, 0.30f, 0.70f, 1f),  // Star: violet
            new Color(0.50f, 0.45f, 0.40f, 1f),  // Stone: grey brown
            new Color(0.20f, 0.10f, 0.14f, 1f)   // Dark: near black
        };

        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

        private const float SpineWidth = 150f;
        private const float SpineHeight = 220f;
        private const float SpineGap = 26f;
        private const float WrongHoldSeconds = 0.9f;
        private const float RightHoldSeconds = 0.8f;

        private GameObject modalRoot;
        private TMP_Text statusText;
        private readonly Button[] slotButtons = new Button[5];
        private readonly Image[] slotImages = new Image[5];
        private readonly TMP_Text[] slotLabels = new TMP_Text[5];
        private readonly Button[] tomeButtons = new Button[5];
        private readonly Image[] tomeOutlines = new Image[5];

        private TomeOrderPuzzle puzzle;
        private Action<bool> onFinished;
        private int selectedTome = -1;
        private float holdTimer;
        private bool holdIsWin;
        private bool isBuilt;
        private int openedFrame;

        /// <summary>Opens the minigame; <paramref name="finished"/> gets true when the order is right, false when the hero steps away.</summary>
        public static void Open(Action<bool> finished)
        {
            EnsureInstance().Begin(finished);
        }

        /// <summary>The puzzle being shown (tests).</summary>
        public static TomeOrderPuzzle CurrentPuzzle => Instance != null ? Instance.puzzle : null;

        /// <summary>Clicks a tome on the table (tests and buttons).</summary>
        public void ClickTome(int tome)
        {
            if (holdTimer > 0f || puzzle == null) return;
            if (puzzle.SlotOf(tome) >= 0) return;
            selectedTome = selectedTome == tome ? -1 : tome;
            Play(SFXClipType.ButtonClick);
            Refresh();
        }

        /// <summary>Clicks a place on the shelf (tests and buttons).</summary>
        public void ClickSlot(int slot)
        {
            if (holdTimer > 0f || puzzle == null) return;
            if (selectedTome >= 0)
            {
                puzzle.Place(selectedTome, slot);
                selectedTome = -1;
                Play(SFXClipType.ButtonClick);
            }
            else if (puzzle.TomeIn(slot) >= 0)
            {
                puzzle.Remove(slot);
                Play(SFXClipType.ButtonClick);
            }
            Refresh();
            CheckFull();
        }

        /// <summary>Steps away without solving.</summary>
        public void Close()
        {
            Finish(false);
        }

        private static TomeShelfMinigameUI EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject root = new GameObject("TomeShelfMinigameUI");
                Instance = root.AddComponent<TomeShelfMinigameUI>();
            }
            Instance.BuildUI();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (IsOpen) GameInput.SetExplorationInputEnabled(true);
            Instance = null;
        }

        private void Begin(Action<bool> finished)
        {
            if (IsOpen) return;
            puzzle = new TomeOrderPuzzle();
            onFinished = finished;
            selectedTome = -1;
            holdTimer = 0f;
            openedFrame = Time.frameCount;
            statusText.text = "Pick a tome, then its place on the shelf.";
            modalRoot.SetActive(true);
            GameInput.SetExplorationInputEnabled(false);
            Refresh();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (holdTimer > 0f)
            {
                holdTimer -= Time.unscaledDeltaTime;
                if (holdTimer > 0f) return;
                if (holdIsWin)
                {
                    Finish(true);
                    return;
                }
                puzzle.Clear();
                statusText.text = "Pick a tome, then its place on the shelf.";
                Refresh();
                return;
            }

            if (Time.frameCount != openedFrame && GameInput.GetKeyDown(KeyCode.Escape))
            {
                Finish(false);
            }
        }

        private void CheckFull()
        {
            if (!puzzle.IsFull) return;
            if (puzzle.IsSolved)
            {
                statusText.text = "<color=#F6D578>The ward fades. Something behind the shelves gives way...</color>";
                Play(SFXClipType.QuestComplete);
                holdIsWin = true;
                holdTimer = RightHoldSeconds;
            }
            else
            {
                statusText.text = "<color=#E07060>The ward flares and pushes the tomes back to the table.</color>";
                Play(SFXClipType.CriticalFailure);
                holdIsWin = false;
                holdTimer = WrongHoldSeconds;
            }
        }

        private void Finish(bool solved)
        {
            if (!IsOpen) return;
            Action<bool> callback = onFinished;
            onFinished = null;
            modalRoot.SetActive(false);
            GameInput.SetExplorationInputEnabled(true);
            callback?.Invoke(solved);
        }

        private void Refresh()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                int tome = puzzle.TomeIn(slot);
                slotImages[slot].color = tome >= 0 ? SpineColors[tome] : new Color(0.10f, 0.08f, 0.07f, 0.9f);
                slotLabels[slot].text = tome >= 0 ? TomeOrderPuzzle.Titles[tome] : Numerals[slot];
                slotLabels[slot].color = tome < 0 ? new Color(0.55f, 0.48f, 0.38f, 1f) : (tome <= 1 ? new Color(0.16f, 0.10f, 0.05f, 1f) : UITheme.CreamText);
                slotLabels[slot].fontStyle = tome >= 0 ? FontStyles.Bold : FontStyles.Normal;
                slotLabels[slot].fontSize = tome >= 0 ? 20f : 40f;
            }
            for (int i = 0; i < 5; i++)
            {
                int tome = TableOrder[i];
                bool shelved = puzzle.SlotOf(tome) >= 0;
                tomeButtons[i].gameObject.SetActive(!shelved);
                tomeOutlines[i].enabled = tome == selectedTome;
            }
        }

        private static void Play(SFXClipType clip)
        {
            SFXManager.Instance?.PlaySFX(clip, Vector3.zero);
        }

        #region UI Construction

        private void BuildUI()
        {
            if (isBuilt) return;
            isBuilt = true;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dimmer; its "Modal" name keeps the world click raycaster from reaching through
            modalRoot = new GameObject("TomeShelfModal", typeof(RectTransform));
            modalRoot.transform.SetParent(transform, false);
            RectTransform dimRect = (RectTransform)modalRoot.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            Image dim = modalRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);

            RectTransform panel = Rect("TomeShelfModal_Panel", modalRoot.transform, new Vector2(1180f, 860f), Vector2.zero);
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

            TMP_Text title = Text("Title", panel, "The Witch's Catalogue", 38f, UITheme.GoldAccent, new Vector2(0f, 375f), new Vector2(1000f, 50f));
            title.fontStyle = FontStyles.Bold;
            TMP_Text riddle = Text("Riddle", panel, TomeOrderPuzzle.Riddle, 22f, UITheme.ParchmentText, new Vector2(0f, 280f), new Vector2(1000f, 130f));
            riddle.fontStyle = FontStyles.Italic;

            // The shelf: a dark wooden board with five places
            RectTransform shelf = Rect("Shelf", panel, new Vector2(5 * SpineWidth + 6 * SpineGap, SpineHeight + 40f), new Vector2(0f, 75f));
            shelf.gameObject.AddComponent<Image>().color = new Color(0.30f, 0.19f, 0.11f, 1f);
            float startX = -2f * (SpineWidth + SpineGap);
            for (int slot = 0; slot < 5; slot++)
            {
                int s = slot;
                RectTransform r = Rect("Slot" + (slot + 1), shelf, new Vector2(SpineWidth, SpineHeight), new Vector2(startX + slot * (SpineWidth + SpineGap), 0f));
                slotImages[slot] = r.gameObject.AddComponent<Image>();
                slotButtons[slot] = r.gameObject.AddComponent<Button>();
                slotButtons[slot].targetGraphic = slotImages[slot];
                slotButtons[slot].onClick.AddListener(() => ClickSlot(s));
                slotLabels[slot] = Text("Label", r, Numerals[slot], 40f, UITheme.SoftText, Vector2.zero, new Vector2(SpineWidth - 16f, SpineHeight - 16f));
                slotLabels[slot].enableWordWrapping = true;
            }

            Text("TableLabel", panel, "On the reading table:", 20f, UITheme.SoftText, new Vector2(0f, -78f), new Vector2(600f, 30f));

            // The tomes on the table
            for (int i = 0; i < 5; i++)
            {
                int tome = TableOrder[i];
                RectTransform r = Rect("Tome_" + tome, panel, new Vector2(SpineWidth, SpineHeight), new Vector2(startX + i * (SpineWidth + SpineGap), -215f));
                Image outline = r.gameObject.AddComponent<Image>();
                outline.color = UITheme.CoinGold;
                tomeOutlines[i] = outline;
                RectTransform spine = Rect("Spine", r, new Vector2(SpineWidth - 10f, SpineHeight - 10f), Vector2.zero);
                Image spineImage = spine.gameObject.AddComponent<Image>();
                spineImage.color = SpineColors[tome];
                Button button = r.gameObject.AddComponent<Button>();
                button.targetGraphic = spineImage;
                button.onClick.AddListener(() => ClickTome(tome));
                tomeButtons[i] = button;
                TMP_Text label = Text("Title", spine, TomeOrderPuzzle.Titles[tome], 20f, tome <= 1 ? new Color(0.16f, 0.10f, 0.05f, 1f) : UITheme.CreamText, Vector2.zero, new Vector2(SpineWidth - 26f, SpineHeight - 26f));
                label.enableWordWrapping = true;
                label.fontStyle = FontStyles.Bold;
            }

            statusText = Text("Status", panel, string.Empty, 22f, UITheme.CreamText, new Vector2(0f, -355f), new Vector2(1000f, 34f));
            Text("Hint", panel, "Click a tome, then a place on the shelf. Click a shelved tome to take it back.     ESC: step away", 17f, UITheme.ParchmentText, new Vector2(0f, -392f), new Vector2(1100f, 28f));

            modalRoot.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            RectTransform rect = Rect(name, parent, box, position);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        #endregion
    }
}
