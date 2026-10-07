using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// The pause menu (critical review D1): Esc opens it whenever no other window is using Esc.
    /// Resume, Save (not during a fight), separate music and sound-effect volume, How to Play, and
    /// Quit to Main Menu. The game is frozen (time scale 0) while it is open. Volumes are remembered in
    /// PlayerPrefs. Created once per session and kept across scene loads.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PauseMenuUI : MonoBehaviour
    {
        public const string MusicVolumeKey = "CastleOfDice_MusicVolume";
        public const string SfxVolumeKey = "CastleOfDice_SfxVolume";

        private static PauseMenuUI instance;

        private GameObject panel;
        private Slider musicSlider;
        private Slider sfxSlider;
        private TextMeshProUGUI musicValueText;
        private TextMeshProUGUI sfxValueText;
        private Button saveButton;
        private TextMeshProUGUI saveLabel;
        private TextMeshProUGUI statusText;
        private float savedTimeScale = 1f;

        /// <summary>Whether the pause menu is open.</summary>
        public static bool IsPaused => instance != null && instance.panel != null && instance.panel.activeSelf;

        /// <summary>The session's pause menu (created on first use).</summary>
        public static PauseMenuUI Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenuUI>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            BuildUi();
            ApplySavedVolumes();
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!GameInput.GetKeyDown(KeyCode.Escape)) return;

            if (IsPaused)
            {
                if (StoryPanelUI.IsOpen) return; // Esc on the help page is handled by closing it first
                Resume();
                return;
            }

            if (OtherWindowUsesEscape()) return;
            Open();
        }

        /// <summary>
        /// True when Esc belongs to something else this frame (shop, map, journal, lockpicking, dialogue,
        /// level-up, the story panel, the reroll choice) or the main menu is showing.
        /// </summary>
        public static bool OtherWindowUsesEscape()
        {
            if (StoryPanelUI.IsOpen || LockpickMinigameUI.IsOpen || TomeShelfMinigameUI.IsOpen || InventoryUI.UsesEscape || RerollableRoll.IsAwaitingDecision) return true;
            if (ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen) return true;
            if (DungeonMapUIController.Instance != null && DungeonMapUIController.Instance.IsMapOpen) return true;
            QuestJournalUI journal = FindAnyObjectByType<QuestJournalUI>();
            if (journal != null && journal.IsOpen) return true;
            if (LevelUpUIController.Instance != null && LevelUpUIController.Instance.IsModalOpen) return true;
            if (MainMenuController.Instance != null && MainMenuController.Instance.IsMenuOpen) return true;
            DialogueController dialogue = FindAnyObjectByType<DialogueController>();
            return dialogue != null && dialogue.IsInDialogue;
        }

        #region Actions

        /// <summary>Opens the pause menu and freezes the game.</summary>
        public void Open()
        {
            if (IsPaused) return;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            GameInput.SetExplorationInputEnabled(false);

            bool inCombat = GameManager.Instance != null && GameManager.Instance.CurrentMode == GamePlayMode.Combat;
            saveButton.interactable = !inCombat;
            saveLabel.text = inCombat ? "Save (not in a fight)" : "Save Game";
            saveLabel.color = inCombat ? DisabledTextColor : UITheme.CreamText;
            statusText.text = $"Difficulty: {DifficultySettings.GetLabel(DifficultySettings.Current)}";
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        /// <summary>Closes the menu and lets the game run again.</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            panel.SetActive(false);
            Time.timeScale = savedTimeScale;
            bool exploring = GameManager.Instance == null || GameManager.Instance.CurrentMode == GamePlayMode.Exploration;
            GameInput.SetExplorationInputEnabled(exploring);
        }

        private void SaveGame()
        {
            PlayerUnit hero = FindAnyObjectByType<PlayerUnit>();
            SaveSystem.SaveGame(PlayerDataSO.Session, hero);
            statusText.text = "Game saved.";
        }

        private void ShowHelp()
        {
            StoryPanelUI.Show("How to Play", HelpText);
        }

        private void QuitToMainMenu()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            GameInput.SetExplorationInputEnabled(true);
            if (GameManager.Instance != null) GameManager.Instance.SetMode(GamePlayMode.Exploration);

            string village = World.ThroneRoomEnding.MainMenuSceneName;
            if (SceneLoader.Instance != null && Application.CanStreamedLevelBeLoaded(village))
            {
                SceneLoader.Instance.LoadSceneAsync(village, ShowMainMenu, "StartSpawn");
            }
            else
            {
                ShowMainMenu();
            }
        }

        private static void ShowMainMenu()
        {
            MainMenuController menu = MainMenuController.Instance ?? FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu != null) menu.ShowMainMenu();
        }

        /// <summary>Sets and remembers the music volume (0..1).</summary>
        public static void SetMusicVolume(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, value);
            if (MusicManager.Instance != null) MusicManager.Instance.MusicVolume = value;
        }

        /// <summary>Sets and remembers the sound-effect volume (0..1).</summary>
        public static void SetSfxVolume(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
            if (SFXManager.Instance != null) SFXManager.Instance.SFXVolume = value;
        }

        private void ApplySavedVolumes()
        {
            float music = PlayerPrefs.GetFloat(MusicVolumeKey, MusicManager.Instance != null ? MusicManager.Instance.MusicVolume : 0.7f);
            float sfx = PlayerPrefs.GetFloat(SfxVolumeKey, SFXManager.Instance != null ? SFXManager.Instance.SFXVolume : 1f);
            musicSlider.SetValueWithoutNotify(music);
            sfxSlider.SetValueWithoutNotify(sfx);
            musicValueText.text = VolumeLabel(music);
            sfxValueText.text = VolumeLabel(sfx);
            if (PlayerPrefs.HasKey(MusicVolumeKey)) SetMusicVolume(music);
            if (PlayerPrefs.HasKey(SfxVolumeKey)) SetSfxVolume(sfx);
        }

        #endregion

        #region UI

        public const string HelpText =
            "<b>Exploring</b>: click the ground to walk. Click people and objects to talk or use them. " +
            "<b>Camera</b>: WASD pans, Q/E or a middle-mouse drag turns it, the mouse wheel zooms and [Home] recentres on your hero. " +
            "[M] map, [J] quest journal, [I] bag, [C] character sheet, [H] drink a potion, [V] sneak (a Stealth check can surprise the enemies).\n\n" +
            "<b>Combat</b>: each turn you have a move, an action and a bonus action, and a reaction each round. " +
            "Attacks are d20 + attribute + proficiency against AC; Fireball, War Cry and smoke make the enemies roll saving throws. " +
            "A natural 20 doubles the damage, a natural 1 always misses. At 0 HP you get one Death's Door save per fight.\n\n" +
            "<b>Tactics</b>: walking away from an enemy next to you gives it a free attack (Disengage, Blink and Shadow Step do not); " +
            "a fleeing enemy gives you one too. Dash doubles your move, Dodge makes you hard to hit. " +
            "Strong abilities need a few turns to recharge (the number on the icon). Crates give cover, pillars block arrows, " +
            "and the red barrel explodes.\n\n" +
            "<b>Saving</b>: pray at the rune shrine in the Castle Hall or save from this menu outside a fight.";

        // Gold-framed dark panel like the level-up window and the HUD cards (Vili, 2026-10-07)
        private const string PanelSprite = "Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png";
        private const string DividerSprite = "Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png";
        private const string ButtonSprite = "Assets/UI/Sprites/UI_Fantasy_Button_Normal.png";
        private const string ButtonHoverSprite = "Assets/UI/Sprites/UI_Fantasy_Button_Hover.png";
        private const string ButtonPressedSprite = "Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png";
        private const string TrackSprite = "Assets/UI/Sprites/UI_Fantasy_Bar_Track.png";
        private const string HandleSprite = "Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png";

        private static readonly Color ResumeTextColor = new Color(0.6f, 0.92f, 0.6f, 1f);
        private static readonly Color QuitTextColor = new Color(1f, 0.6f, 0.55f, 1f);
        private static readonly Color DisabledTextColor = new Color(0.55f, 0.55f, 0.6f, 1f);

        private void BuildUi()
        {
            GameObject canvasObj = new GameObject("PauseMenu_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 550;
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = new GameObject("Pause_Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObj.transform, false);
            RectTransform full = (RectTransform)panel.transform;
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.sizeDelta = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.75f);

            GameObject box = new GameObject("Pause_Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(panel.transform, false);
            ((RectTransform)box.transform).sizeDelta = new Vector2(540f, 700f);
            Framed(box.GetComponent<Image>(), PanelSprite, UITheme.PanelSlate);

            Text(box.transform, "Title", "PAUSED", new Vector2(0f, 292f), 44f, UITheme.GoldAccent);
            Divider(box.transform, "Title_Divider", new Vector2(0f, 255f), 380f);
            statusText = Text(box.transform, "Status", "", new Vector2(0f, 226f), 17f, UITheme.SoftText);
            statusText.fontStyle = FontStyles.Italic;

            Button resume = MenuButton(box.transform, "Resume_Btn", "Resume", new Vector2(0f, 166f), ResumeTextColor);
            resume.onClick.AddListener(Resume);
            saveButton = MenuButton(box.transform, "Save_Btn", "Save Game", new Vector2(0f, 106f), UITheme.CreamText);
            saveButton.onClick.AddListener(SaveGame);
            saveLabel = saveButton.GetComponentInChildren<TextMeshProUGUI>();

            Text(box.transform, "Audio_Header", "AUDIO", new Vector2(0f, 48f), 16f, UITheme.GoldAccent).characterSpacing = 8f;
            Divider(box.transform, "Audio_Divider", new Vector2(0f, 30f), 300f);
            musicSlider = Slider(box.transform, "Music", new Vector2(0f, 2f), SetMusicVolume, out musicValueText);
            sfxSlider = Slider(box.transform, "Sound Effects", new Vector2(0f, -66f), SetSfxVolume, out sfxValueText);

            Divider(box.transform, "Menu_Divider", new Vector2(0f, -132f), 300f);
            Button help = MenuButton(box.transform, "Help_Btn", "How to Play", new Vector2(0f, -170f), UITheme.CreamText);
            help.onClick.AddListener(ShowHelp);
            Button quit = MenuButton(box.transform, "Quit_Btn", "Quit to Main Menu", new Vector2(0f, -230f), QuitTextColor);
            quit.onClick.AddListener(QuitToMainMenu);

            Text(box.transform, "Hint", "Esc to resume", new Vector2(0f, -300f), 15f, UITheme.SoftText).fontStyle = FontStyles.Italic;
        }

        /// <summary>Shows a theme sprite as a 9-sliced frame, or a plain colour if the sprite is missing.</summary>
        private static void Framed(Image image, string spritePath, Color fallback)
        {
            Sprite sprite = UITheme.GetSprite(spritePath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = fallback;
            }
        }

        private static void Divider(Transform parent, string name, Vector2 pos, float width)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, 10f);
            Image img = obj.GetComponent<Image>();
            Framed(img, DividerSprite, new Color(UITheme.GoldAccent.r, UITheme.GoldAccent.g, UITheme.GoldAccent.b, 0.6f));
            img.raycastTarget = false;
        }

        /// <summary>A framed fantasy button: the gold-rimmed plate brightens on hover, the label carries the colour.</summary>
        private static Button MenuButton(Transform parent, string name, string label, Vector2 pos, Color textColor)
        {
            Button btn = UIFactory.CreateTextButton(parent, name, label, pos, new Vector2(340f, 52f), UITheme.PanelSlate);
            Image img = (Image)btn.targetGraphic;
            Sprite normal = UITheme.GetSprite(ButtonSprite);
            if (normal != null)
            {
                img.sprite = normal;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState states = new SpriteState
                {
                    highlightedSprite = UITheme.GetSprite(ButtonHoverSprite),
                    pressedSprite = UITheme.GetSprite(ButtonPressedSprite),
                    selectedSprite = normal,
                    disabledSprite = normal
                };
                btn.spriteState = states;
            }

            TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            tmp.color = textColor;
            tmp.fontSize = 19f;
            return btn;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, Vector2 pos, float size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(460f, 40f);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// A volume row: the name on the left and the percentage on the right, with the slider on its own line
        /// below (the old label sat behind the slider and only an "S" showed).
        /// </summary>
        private static Slider Slider(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction<float> onChanged, out TextMeshProUGUI valueText)
        {
            const float rowWidth = 400f;

            TextMeshProUGUI name = Text(parent, label + "_Label", label, pos + new Vector2(0f, 14f), 18f, UITheme.CreamText);
            ((RectTransform)name.transform).sizeDelta = new Vector2(rowWidth, 28f);
            name.alignment = TextAlignmentOptions.Left;

            TextMeshProUGUI value = Text(parent, label + "_Value", "100%", pos + new Vector2(0f, 14f), 17f, UITheme.GoldAccent);
            ((RectTransform)value.transform).sizeDelta = new Vector2(rowWidth, 28f);
            value.alignment = TextAlignmentOptions.Right;

            GameObject sliderObj = new GameObject(label + "_Slider", typeof(RectTransform));
            sliderObj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)sliderObj.transform;
            rect.anchoredPosition = pos + new Vector2(0f, -16f);
            rect.sizeDelta = new Vector2(rowWidth, 18f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Framed(bg.GetComponent<Image>(), TrackSprite, new Color(0.1f, 0.1f, 0.12f));

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(5f, 5f);
            fillAreaRect.offsetMax = new Vector2(-5f, -5f);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = UITheme.GoldAccent;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(22f, 14f);
            Image handleImg = handle.GetComponent<Image>();
            Framed(handleImg, HandleSprite, UITheme.CreamText);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            Navigation nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            ColorBlock cb = slider.colors;
            cb.highlightedColor = new Color(1f, 0.92f, 0.7f, 1f);
            cb.pressedColor = UITheme.GoldAccent;
            slider.colors = cb;

            slider.onValueChanged.AddListener(onChanged);
            slider.onValueChanged.AddListener(v => value.text = VolumeLabel(v));
            value.text = VolumeLabel(slider.value);
            valueText = value;
            return slider;
        }

        /// <summary>Volume shown as a whole percentage.</summary>
        public static string VolumeLabel(float volume) => $"{Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f)}%";

        #endregion
    }
}
