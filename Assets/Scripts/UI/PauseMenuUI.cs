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
            if (StoryPanelUI.IsOpen || LockpickMinigameUI.IsOpen || RerollableRoll.IsAwaitingDecision) return true;
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
            if (PlayerPrefs.HasKey(MusicVolumeKey)) SetMusicVolume(music);
            if (PlayerPrefs.HasKey(SfxVolumeKey)) SetSfxVolume(sfx);
        }

        #endregion

        #region UI

        public const string HelpText =
            "<b>Exploring</b>: click the ground or use WASD to walk. Click people and objects to talk or use them. " +
            "[M] map, [J] quest journal, [Q] drink a potion.\n\n" +
            "<b>Combat</b>: on your turn you may Move once and use one ability. Click an ability, then an enemy. " +
            "Every attack is d20 + bonus against the target's AC; a natural 20 doubles the damage, a natural 1 always misses.\n\n" +
            "<b>Tactics</b>: walking away from an enemy next to you gives it a free attack (Blink and Shadow Step do not). " +
            "Strong abilities need a few turns to recharge (the number on the icon). Crates give cover, pillars block arrows, " +
            "and the red barrel explodes.\n\n" +
            "<b>Saving</b>: pray at the rune shrine in the Castle Hall or save from this menu outside a fight.";

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
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            GameObject box = new GameObject("Pause_Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(panel.transform, false);
            ((RectTransform)box.transform).sizeDelta = new Vector2(560f, 640f);
            box.GetComponent<Image>().color = UITheme.PanelSlate;

            Text(box.transform, "Title", "PAUSED", new Vector2(0f, 270f), 40f, UITheme.GoldAccent);
            statusText = Text(box.transform, "Status", "", new Vector2(0f, 225f), 18f, UITheme.SoftText);

            Button resume = UIFactory.CreateTextButton(box.transform, "Resume_Btn", "Resume", new Vector2(0f, 165f), new Vector2(320f, 50f), UITheme.ActionGreen);
            resume.onClick.AddListener(Resume);
            saveButton = UIFactory.CreateTextButton(box.transform, "Save_Btn", "Save Game", new Vector2(0f, 105f), new Vector2(320f, 50f), new Color(0.25f, 0.4f, 0.6f));
            saveButton.onClick.AddListener(SaveGame);
            saveLabel = saveButton.GetComponentInChildren<TextMeshProUGUI>();

            musicSlider = Slider(box.transform, "Music", new Vector2(0f, 30f), SetMusicVolume);
            sfxSlider = Slider(box.transform, "Sound Effects", new Vector2(0f, -40f), SetSfxVolume);

            Button help = UIFactory.CreateTextButton(box.transform, "Help_Btn", "How to Play", new Vector2(0f, -120f), new Vector2(320f, 50f), new Color(0.45f, 0.35f, 0.25f));
            help.onClick.AddListener(ShowHelp);
            Button quit = UIFactory.CreateTextButton(box.transform, "Quit_Btn", "Quit to Main Menu", new Vector2(0f, -190f), new Vector2(320f, 50f), new Color(0.45f, 0.18f, 0.18f));
            quit.onClick.AddListener(QuitToMainMenu);

            Text(box.transform, "Hint", "Esc to resume", new Vector2(0f, -270f), 16f, UITheme.SoftText);
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, Vector2 pos, float size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(500f, 50f);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Slider Slider(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction<float> onChanged)
        {
            Text(parent, label + "_Label", label, pos + new Vector2(-140f, 0f), 18f, UITheme.CreamText).alignment = TextAlignmentOptions.Right;

            GameObject sliderObj = new GameObject(label + "_Slider", typeof(RectTransform));
            sliderObj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)sliderObj.transform;
            rect.anchoredPosition = pos + new Vector2(110f, 0f);
            rect.sizeDelta = new Vector2(240f, 20f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;
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
            handleAreaRect.sizeDelta = Vector2.zero;
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(20f, 30f);
            handle.GetComponent<Image>().color = UITheme.CreamText;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        #endregion
    }
}
