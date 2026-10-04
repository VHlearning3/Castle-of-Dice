using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Controls the Title Screen, Class Selection (Warrior, Mage, Rogue),
    /// and Game Rules modal. Fulfills the "Main menu" requirement from the notebook.
    /// Resiliently ensures EventSystem (InputSystemUIInputModule), GraphicRaycaster,
    /// and cursor state for robust WebGL and desktop input.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        #region Singleton

        public static MainMenuController Instance { get; private set; }

        #endregion

        #region Constants

        public const string SCOTTISH_HARP_ATTRIBUTION_URL = "https://pixabay.com/music/scotland-harp-587446/";

        #endregion

        #region Serialized Fields

        [Header("Menu Panels")]
        [Tooltip("Root Main Menu panel containing title, background, and navigation buttons.")]
        [SerializeField] private GameObject mainMenuPanel;

        /// <summary>Whether the main menu (or its hero / rules screens) is on screen.</summary>
        public bool IsMenuOpen => (mainMenuPanel != null && mainMenuPanel.activeInHierarchy)
            || (classSelectionPanel != null && classSelectionPanel.activeInHierarchy);

        [Tooltip("Hero Class Selection modal popup.")]
        [SerializeField] private GameObject classSelectionPanel;

        [Tooltip("Rules & Tutorial dialog explaining the D20 system.")]
        [SerializeField] private GameObject rulesPanel;

        [Header("Character Class ScriptableObjects")]
        [SerializeField] private CharacterClassSO warriorClass;
        [SerializeField] private CharacterClassSO mageClass;
        [SerializeField] private CharacterClassSO rogueClass;

        #endregion

        #region Private State

        private bool hasGameStarted = false;

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

            LoadClassAssetsIfMissing();
            EnsureEventSystem();
            EnsureUIHierarchy();
        }

        private void Start()
        {
            // If the game was already running or player is actively exploring, don't block
            if (!hasGameStarted)
            {
                ShowMainMenu();
            }
        }

        #endregion

        #region Input System & EventSystem Setup

        /// <summary>
        /// Ensures an EventSystem exists in the scene and is configured with
        /// InputSystemUIInputModule (removing any legacy StandaloneInputModule)
        /// with valid actions hooked so hover and clicks work reliably in WebGL.
        /// </summary>
        public static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<EventSystem>();
            }

            // 1. Remove legacy StandaloneInputModule if present
            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(standalone);
                }
                else
                {
                    DestroyImmediate(standalone);
                }
            }

            // 2. Ensure InputSystemUIInputModule is present
            InputSystemUIInputModule uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            // 3. If actionsAsset is missing or unassigned, or actions are not hooked, assign default actions
            if (uiModule.actionsAsset == null || uiModule.point == null || uiModule.point.action == null ||
                uiModule.leftClick == null || uiModule.leftClick.action == null)
            {
                uiModule.AssignDefaultActions();
            }

            uiModule.enabled = true;
            eventSystem.enabled = true;
        }

        /// <summary>
        /// Ensures the target Canvas has a GraphicRaycaster component attached and enabled.
        /// </summary>
        public static void EnsureGraphicRaycaster(Canvas canvas)
        {
            if (canvas == null) return;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            raycaster.enabled = true;
        }

        #endregion

        #region Public Controls

        public void ShowMainMenu()
        {
            EnsureEventSystem();
            EnsureUIHierarchy();

            if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
                mainMenuPanel.transform.SetAsLastSibling();

                CanvasGroup cg = mainMenuPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }

            if (classSelectionPanel != null) classSelectionPanel.SetActive(false);
            if (rulesPanel != null) rulesPanel.SetActive(false);
            RefreshContinueButton();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayMode(GamePlayMode.Dialogue);
            }
        }

        /// <summary>Continue is only clickable when there is a saved adventure.</summary>
        private void RefreshContinueButton()
        {
            if (mainMenuPanel == null) return;
            Transform continueTr = mainMenuPanel.transform.Find("Menu_Buttons/Continue_Btn");
            Button continueBtn = continueTr != null ? continueTr.GetComponent<Button>() : null;
            if (continueBtn != null)
            {
                continueBtn.interactable = SaveSystem.HasSavedGame();
            }
        }

        public void HideMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (classSelectionPanel != null) classSelectionPanel.SetActive(false);
            if (rulesPanel != null) rulesPanel.SetActive(false);

            hasGameStarted = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetLocation(GameLocation.Village);
                GameManager.Instance.SetPlayMode(GamePlayMode.Exploration);
            }
        }

        /// <summary>
        /// "Continue": loads the save (progression, inventory, quests, health, story flags, difficulty),
        /// restores the hero class recorded in it and resumes play where the game was saved.
        /// Without a save the button is disabled.
        /// </summary>
        public void ContinueGame()
        {
            CharacterClassSO savedClass = null;
            PlayerSaveData save = SaveSystem.PeekSave();
            if (save == null)
            {
                Debug.Log("[MainMenuController] No saved adventure to continue.");
                return;
            }

            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.LoadProgression();
            }
            else
            {
                SaveSystem.LoadGame();
            }

            if (save != null && save.characterClass >= 0)
            {
                LoadClassAssetsIfMissing();
                switch ((CharacterClassType)save.characterClass)
                {
                    case CharacterClassType.Warrior: savedClass = warriorClass; break;
                    case CharacterClassType.Mage: savedClass = mageClass; break;
                    case CharacterClassType.Rogue: savedClass = rogueClass; break;
                }
            }

            if (savedClass != null)
            {
                PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
                if (player != null)
                {
                    player.SetCharacterClass(savedClass);
                }
                else
                {
                    PlayerDataSO.Session.SelectedClass = savedClass;
                }

                CombatUIController combatUI = FindAnyObjectByType<CombatUIController>();
                if (combatUI != null)
                {
                    combatUI.RefreshAbilityBar();
                }
            }

            HideMainMenu();

            // Resume in the zone where the game was saved (e.g. the Castle Hall rune shrine)
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (save != null && !string.IsNullOrEmpty(save.sceneName) && save.sceneName != currentScene &&
                SceneLoader.Instance != null && Application.CanStreamedLevelBeLoaded(save.sceneName))
            {
                SceneLoader.Instance.LoadScene(save.sceneName);
            }
            else
            {
                SaveSystem.ApplyPendingPosition(currentScene);
            }
        }

        public void OpenClassSelection()
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.ButtonClick);
            if (classSelectionPanel != null)
            {
                classSelectionPanel.SetActive(true);
                classSelectionPanel.transform.SetAsLastSibling();
                EnsureDifficultyRow();
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void CloseClassSelection()
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.ButtonClick);
            if (classSelectionPanel != null) classSelectionPanel.SetActive(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void OpenRules()
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.ButtonClick);
            if (rulesPanel != null)
            {
                rulesPanel.SetActive(true);
                rulesPanel.transform.SetAsLastSibling();
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void CloseRules()
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.ButtonClick);
            if (rulesPanel != null) rulesPanel.SetActive(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void OpenScottishHarpCredit()
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.ButtonClick);
            Application.OpenURL(SCOTTISH_HARP_ATTRIBUTION_URL);
        }

        #region Difficulty (critical review B9)

        private DifficultyLevel selectedDifficulty = DifficultyLevel.Normal;
        private readonly Button[] difficultyButtons = new Button[3];
        private TextMeshProUGUI difficultyDescription;

        /// <summary>Difficulty picked on the hero selection screen for the next New Adventure.</summary>
        public DifficultyLevel SelectedDifficulty => selectedDifficulty;

        /// <summary>Picks the difficulty for the next New Adventure.</summary>
        public void SelectDifficulty(DifficultyLevel level)
        {
            selectedDifficulty = level;
            RefreshDifficultyRow();
        }

        /// <summary>Adds the Easy / Normal / Hard row under the hero cards (built once, at runtime).</summary>
        private void EnsureDifficultyRow()
        {
            if (classSelectionPanel == null) return;
            if (classSelectionPanel.transform.Find("Difficulty_Row") == null)
            {
                GameObject row = new GameObject("Difficulty_Row", typeof(RectTransform));
                row.transform.SetParent(classSelectionPanel.transform, false);
                RectTransform rowRect = (RectTransform)row.transform;
                rowRect.anchoredPosition = new Vector2(0f, -262f);
                rowRect.sizeDelta = new Vector2(760f, 90f);

                GameObject labelObj = new GameObject("Difficulty_Label", typeof(RectTransform));
                labelObj.transform.SetParent(row.transform, false);
                RectTransform labelRect = (RectTransform)labelObj.transform;
                labelRect.anchoredPosition = new Vector2(-300f, 18f);
                labelRect.sizeDelta = new Vector2(160f, 40f);
                TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
                label.text = "Difficulty";
                label.fontSize = 22f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Right;
                label.color = new Color(1f, 0.85f, 0.3f);
                label.raycastTarget = false;

                for (int i = 0; i < 3; i++)
                {
                    DifficultyLevel level = (DifficultyLevel)i;
                    Button btn = UIFactory.CreateTextButton(row.transform, "Difficulty_" + DifficultySettings.GetLabel(level),
                        DifficultySettings.GetLabel(level), new Vector2(-110f + i * 165f, 18f), new Vector2(150f, 40f), new Color(0.25f, 0.3f, 0.4f));
                    btn.onClick.AddListener(() => SelectDifficulty(level));
                    difficultyButtons[i] = btn;
                }

                GameObject descObj = new GameObject("Difficulty_Description", typeof(RectTransform));
                descObj.transform.SetParent(row.transform, false);
                RectTransform descRect = (RectTransform)descObj.transform;
                descRect.anchoredPosition = new Vector2(0f, -26f);
                descRect.sizeDelta = new Vector2(700f, 30f);
                difficultyDescription = descObj.AddComponent<TextMeshProUGUI>();
                difficultyDescription.fontSize = 17f;
                difficultyDescription.alignment = TextAlignmentOptions.Center;
                difficultyDescription.color = UITheme.CreamText;
                difficultyDescription.raycastTarget = false;
            }

            RefreshDifficultyRow();
        }

        private void RefreshDifficultyRow()
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                Button btn = difficultyButtons[i];
                if (btn == null) continue;
                Image img = btn.GetComponent<Image>();
                bool selected = (int)selectedDifficulty == i;
                if (img != null) img.color = selected ? UITheme.ActionGreen : new Color(0.25f, 0.3f, 0.4f);
            }
            if (difficultyDescription != null)
            {
                difficultyDescription.text = $"{DifficultySettings.GetLabel(selectedDifficulty)}: {DifficultySettings.GetDescription(selectedDifficulty)}";
            }
        }

        #endregion

        public void SelectCharacterClass(CharacterClassSO chosenClass)
        {
            if (chosenClass == null)
            {
                Debug.LogWarning("[MainMenuController] SelectCharacterClass called with null class! Check that warrior/mage/rogue assets are assigned.");
                return;
            }

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(SFXClipType.CriticalSuccess);
            }

            // A new adventure starts from level 1 without the previous run's upgrades
            if (PlayerProgressionManager.Instance != null)
            {
                PlayerProgressionManager.Instance.ResetForNewGame();
            }
            else
            {
                PlayerDataSO.Session.ResetData();
                SaveSystem.ClearSave();
                StoryFlags.Clear();
            }
            DifficultySettings.Current = selectedDifficulty;

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.SetCharacterClass(chosenClass);
            }
            else
            {
                PlayerDataSO.Session.SelectedClass = chosenClass;
            }

            CombatUIController combatUI = FindAnyObjectByType<CombatUIController>();
            if (combatUI != null)
            {
                combatUI.RefreshAbilityBar();
            }

            HideMainMenu();

            // A few pages of story before the adventure starts (critical review D2)
            TutorialHints.ShowIntro();
        }

        #endregion

        #region Asset Discovery

        private void LoadClassAssetsIfMissing()
        {
            if (warriorClass == null)
            {
                warriorClass = Resources.Load<CharacterClassSO>("Data/Character_Warrior_SirRoland")
                    ?? LoadAssetFallback("Character_Warrior_SirRoland");
            }
            if (mageClass == null)
            {
                mageClass = Resources.Load<CharacterClassSO>("Data/Character_Mage_Elira")
                    ?? LoadAssetFallback("Character_Mage_Elira");
            }
            if (rogueClass == null)
            {
                rogueClass = Resources.Load<CharacterClassSO>("Data/Character_Rogue_Corvo")
                    ?? LoadAssetFallback("Character_Rogue_Corvo");
            }
        }

        private CharacterClassSO LoadAssetFallback(string assetName)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{assetName} t:CharacterClassSO");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>(path);
            }
#endif
            return null;
        }

        #endregion

        #region Procedural UI Hierarchy Builder

        private void EnsureUIHierarchy()
        {
            EnsureEventSystem();

            Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            EnsureGraphicRaycaster(canvas);

            // Deactivate LevelUp modal if active at boot
            Transform levelUpPanel = canvas.transform.Find("LevelUp_Modal_Panel");
            if (levelUpPanel != null && levelUpPanel.gameObject.activeSelf)
            {
                levelUpPanel.gameObject.SetActive(false);
            }

            if (mainMenuPanel == null)
            {
                Transform existing = canvas.transform.Find("MainMenuPanel");
                if (existing != null)
                {
                    mainMenuPanel = existing.gameObject;
                }
            }

            if (mainMenuPanel != null)
            {
                WireExistingHierarchy(mainMenuPanel);
                return;
            }

            // 1. Root Main Menu Panel
            GameObject panel = new GameObject("MainMenuPanel");
            panel.transform.SetParent(canvas.transform, false);
            mainMenuPanel = panel;

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            CanvasGroup cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.96f);
            bg.raycastTarget = true;

            // Title
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 160f);
            titleRect.sizeDelta = new Vector2(700f, 80f);
            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "CASTLE OF DICE";
            titleTMP.fontSize = 52f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(1f, 0.82f, 0.28f);
            titleTMP.raycastTarget = false;

            // Subtitle
            GameObject subObj = new GameObject("Subtitle_Text");
            subObj.transform.SetParent(panel.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchoredPosition = new Vector2(0f, 105f);
            subRect.sizeDelta = new Vector2(600f, 40f);
            TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
            subTMP.text = "Castle of the D20 — Tactical 3D Castle Adventure";
            subTMP.fontSize = 20f;
            subTMP.alignment = TextAlignmentOptions.Center;
            subTMP.color = new Color(0.8f, 0.85f, 0.9f);
            subTMP.raycastTarget = false;

            // Buttons Container
            GameObject btnContainer = new GameObject("Menu_Buttons");
            btnContainer.transform.SetParent(panel.transform, false);
            RectTransform bcRect = btnContainer.AddComponent<RectTransform>();
            bcRect.anchoredPosition = new Vector2(0f, -40f);
            bcRect.sizeDelta = new Vector2(280f, 220f);

            Button newGameBtn = CreateMenuButton(btnContainer.transform, "NewGame_Btn", "New Adventure", new Vector2(0f, 70f), UITheme.ActionGreen);
            newGameBtn.onClick.AddListener(OpenClassSelection);

            Button continueBtn = CreateMenuButton(btnContainer.transform, "Continue_Btn", "Continue", new Vector2(0f, 10f), new Color(0.25f, 0.4f, 0.6f));
            continueBtn.onClick.AddListener(ContinueGame);

            Button rulesBtn = CreateMenuButton(btnContainer.transform, "Rules_Btn", "Rules & D20 Guide", new Vector2(0f, -50f), new Color(0.45f, 0.35f, 0.25f));
            rulesBtn.onClick.AddListener(OpenRules);

            // Music Attribution Link
            GameObject creditObj = new GameObject("MusicCredit_Btn");
            creditObj.transform.SetParent(panel.transform, false);
            RectTransform creditRect = creditObj.AddComponent<RectTransform>();
            creditRect.anchorMin = new Vector2(0.5f, 0f);
            creditRect.anchorMax = new Vector2(0.5f, 0f);
            creditRect.pivot = new Vector2(0.5f, 0f);
            creditRect.anchoredPosition = new Vector2(0f, 15f);
            creditRect.sizeDelta = new Vector2(500f, 30f);
            TextMeshProUGUI creditTMP = creditObj.AddComponent<TextMeshProUGUI>();
            creditTMP.text = "<size=13><color=#8899AA>Music: <u><color=#AACCFF>Scottish Harp (Pixabay)</color></u></color></size>";
            creditTMP.alignment = TextAlignmentOptions.Center;
            creditTMP.raycastTarget = true;
            Button creditBtn = creditObj.AddComponent<Button>();
            creditBtn.onClick.AddListener(OpenScottishHarpCredit);

            // 2. Class Selection Modal
            BuildClassSelectionModal(panel.transform);

            // 3. Rules Modal
            BuildRulesModal(panel.transform);
        }

        private void WireExistingHierarchy(GameObject panel)
        {
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = panel.AddComponent<CanvasGroup>();
            }
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // Wire Menu_Buttons
            Transform btnContainer = panel.transform.Find("Menu_Buttons");
            if (btnContainer != null)
            {
                Transform newGameTr = btnContainer.Find("NewGame_Btn");
                if (newGameTr != null)
                {
                    Button btn = newGameTr.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                        btn.onClick.RemoveListener(OpenClassSelection);
                        btn.onClick.AddListener(OpenClassSelection);
                    }
                }

                Transform continueTr = btnContainer.Find("Continue_Btn");
                if (continueTr != null)
                {
                    Button btn = continueTr.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                        btn.onClick.RemoveListener(HideMainMenu);
                        btn.onClick.RemoveListener(ContinueGame);
                        btn.onClick.AddListener(ContinueGame);
                    }
                }

                Transform rulesTr = btnContainer.Find("Rules_Btn");
                if (rulesTr != null)
                {
                    Button btn = rulesTr.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                        btn.onClick.RemoveListener(OpenRules);
                        btn.onClick.AddListener(OpenRules);
                    }
                }
            }

            Transform creditTr = panel.transform.Find("MusicCredit_Btn");
            if (creditTr != null)
            {
                Button btn = creditTr.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveListener(OpenScottishHarpCredit);
                    btn.onClick.AddListener(OpenScottishHarpCredit);
                }
            }

            Transform csTr = panel.transform.Find("ClassSelectionModal");
            if (csTr != null)
            {
                classSelectionPanel = csTr.gameObject;
                Transform backTr = csTr.Find("Back_Btn");
                if (backTr != null)
                {
                    Button btn = backTr.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.RemoveListener(CloseClassSelection);
                        btn.onClick.AddListener(CloseClassSelection);
                    }
                }

                WireClassCard(csTr.Find("Warrior_Card"), () => SelectCharacterClass(warriorClass));
                WireClassCard(csTr.Find("Mage_Card"), () => SelectCharacterClass(mageClass));
                WireClassCard(csTr.Find("Rogue_Card"), () => SelectCharacterClass(rogueClass));
            }
            else
            {
                BuildClassSelectionModal(panel.transform);
            }

            Transform rulesTrModal = panel.transform.Find("RulesModal");
            if (rulesTrModal != null)
            {
                rulesPanel = rulesTrModal.gameObject;
                Transform boxTr = rulesTrModal.Find("Rules_Box");
                if (boxTr != null)
                {
                    Transform closeTr = boxTr.Find("CloseRules_Btn");
                    if (closeTr != null)
                    {
                        Button btn = closeTr.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveListener(CloseRules);
                            btn.onClick.AddListener(CloseRules);
                        }
                    }
                }
            }
            else
            {
                BuildRulesModal(panel.transform);
            }
        }

        private void WireClassCard(Transform cardTr, UnityEngine.Events.UnityAction onSelect)
        {
            if (cardTr == null) return;
            Button cardBtn = cardTr.GetComponent<Button>();
            if (cardBtn != null)
            {
                cardBtn.interactable = true;
                cardBtn.onClick.RemoveAllListeners();
                cardBtn.onClick.AddListener(onSelect);
            }
            Transform selectBtnTr = cardTr.Find("SelectBtn");
            if (selectBtnTr != null)
            {
                Button sBtn = selectBtnTr.GetComponent<Button>();
                if (sBtn != null)
                {
                    sBtn.interactable = true;
                    sBtn.onClick.RemoveAllListeners();
                    sBtn.onClick.AddListener(onSelect);
                }
            }
        }

        private void BuildClassSelectionModal(Transform parent)
        {
            GameObject csPanel = new GameObject("ClassSelectionModal");
            csPanel.transform.SetParent(parent, false);
            classSelectionPanel = csPanel;

            RectTransform rect = csPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            CanvasGroup cg = csPanel.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            Image bg = csPanel.AddComponent<Image>();
            bg.color = UITheme.PanelAbyss;
            bg.raycastTarget = true;

            // Title
            GameObject titleObj = new GameObject("CS_Title");
            titleObj.transform.SetParent(csPanel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 190f);
            titleRect.sizeDelta = new Vector2(600f, 60f);
            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "CHOOSE YOUR HERO";
            titleTMP.fontSize = 36f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(1f, 0.85f, 0.3f);
            titleTMP.raycastTarget = false;

            // 3 Class Cards
            CreateClassCard(csPanel.transform, "Warrior_Card", "Sir Roland", "WARRIOR", "High HP & AC. Frontline swordmaster.", new Vector2(-280f, 10f), () => SelectCharacterClass(warriorClass));
            CreateClassCard(csPanel.transform, "Mage_Card", "Scholar Elira", "MAGE", "Devastating Fireball area damage (3x3). Teleportation.", new Vector2(0f, 10f), () => SelectCharacterClass(mageClass));
            CreateClassCard(csPanel.transform, "Rogue_Card", "Shadow-Corvo", "ROGUE", "Critical puncture damage, Smoke Bomb and lockpicking.", new Vector2(280f, 10f), () => SelectCharacterClass(rogueClass));

            // Back button
            Button backBtn = CreateMenuButton(csPanel.transform, "Back_Btn", "Back", new Vector2(0f, -190f), new Color(0.4f, 0.2f, 0.2f));
            backBtn.onClick.AddListener(CloseClassSelection);

            csPanel.SetActive(false);
        }

        private void CreateClassCard(Transform parent, string name, string heroName, string role, string desc, Vector2 pos, UnityEngine.Events.UnityAction onSelect)
        {
            GameObject card = new GameObject(name);
            card.transform.SetParent(parent, false);
            RectTransform rect = card.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(250f, 300f);

            Image img = card.AddComponent<Image>();
            img.color = UITheme.PanelSlate;
            img.raycastTarget = true;

            // Make the entire card clickable
            Button cardBtn = card.AddComponent<Button>();
            cardBtn.targetGraphic = img;
            cardBtn.interactable = true;
            Navigation nav = cardBtn.navigation;
            nav.mode = Navigation.Mode.None;
            cardBtn.navigation = nav;

            ColorBlock ccb = cardBtn.colors;
            ccb.highlightedColor = new Color(0.18f, 0.22f, 0.28f, 1f);
            ccb.pressedColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            cardBtn.colors = ccb;
            cardBtn.onClick.AddListener(onSelect);

            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.7f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Name
            GameObject nameObj = new GameObject("HeroName");
            nameObj.transform.SetParent(card.transform, false);
            RectTransform nr = nameObj.AddComponent<RectTransform>();
            nr.anchoredPosition = new Vector2(0f, 110f);
            nr.sizeDelta = new Vector2(230f, 40f);
            TextMeshProUGUI nt = nameObj.AddComponent<TextMeshProUGUI>();
            nt.text = heroName;
            nt.fontSize = 17f;
            nt.fontStyle = FontStyles.Bold;
            nt.alignment = TextAlignmentOptions.Center;
            nt.color = Color.white;
            nt.raycastTarget = false;

            // Role
            GameObject roleObj = new GameObject("Role");
            roleObj.transform.SetParent(card.transform, false);
            RectTransform rr = roleObj.AddComponent<RectTransform>();
            rr.anchoredPosition = new Vector2(0f, 75f);
            rr.sizeDelta = new Vector2(230f, 30f);
            TextMeshProUGUI rt = roleObj.AddComponent<TextMeshProUGUI>();
            rt.text = $"<color=#F1C40F>[ {role} ]</color>";
            rt.fontSize = 15f;
            rt.alignment = TextAlignmentOptions.Center;
            rt.raycastTarget = false;

            // Desc
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform, false);
            RectTransform dr = descObj.AddComponent<RectTransform>();
            dr.anchoredPosition = new Vector2(0f, 0f);
            dr.sizeDelta = new Vector2(220f, 100f);
            TextMeshProUGUI dt = descObj.AddComponent<TextMeshProUGUI>();
            dt.text = desc;
            dt.fontSize = 13f;
            dt.alignment = TextAlignmentOptions.Center;
            dt.color = new Color(0.85f, 0.85f, 0.85f);
            dt.textWrappingMode = TextWrappingModes.Normal;
            dt.raycastTarget = false;

            // Choose button (secondary, still present for visual affordance)
            Button chooseBtn = CreateMenuButton(card.transform, "SelectBtn", "Select", new Vector2(0f, -100f), UITheme.ActionGreen);
            chooseBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 40f);
            chooseBtn.onClick.AddListener(onSelect);
        }

        private void BuildRulesModal(Transform parent)
        {
            GameObject rPanel = new GameObject("RulesModal");
            rPanel.transform.SetParent(parent, false);
            rulesPanel = rPanel;

            RectTransform rect = rPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            CanvasGroup cg = rPanel.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            Image bg = rPanel.AddComponent<Image>();
            bg.color = UITheme.PanelAbyss;
            bg.raycastTarget = true;

            GameObject box = new GameObject("Rules_Box");
            box.transform.SetParent(rPanel.transform, false);
            RectTransform bRect = box.AddComponent<RectTransform>();
            bRect.sizeDelta = new Vector2(640f, 450f);

            Image bImg = box.AddComponent<Image>();
            bImg.color = UITheme.PanelSlate;
            bImg.raycastTarget = true;

            Outline o = box.AddComponent<Outline>();
            o.effectColor = new Color(0.85f, 0.7f, 0.2f, 0.8f);

            GameObject textObj = new GameObject("Rules_Text");
            textObj.transform.SetParent(box.transform, false);
            RectTransform tRect = textObj.AddComponent<RectTransform>();
            tRect.sizeDelta = new Vector2(600f, 360f);
            tRect.anchoredPosition = new Vector2(0f, 25f);
            TextMeshProUGUI tTMP = textObj.AddComponent<TextMeshProUGUI>();
            tTMP.fontSize = 14f;
            tTMP.color = Color.white;
            tTMP.textWrappingMode = TextWrappingModes.Normal;
            tTMP.text = "<b><size=22><color=#F1C40F>D20 RULE SYSTEM</color></size></b>\n\n" +
                "• <b>Actions:</b> Every action and attack is resolved with a 20-sided die:\n" +
                "   <i>Result = d20 + Skill Bonus >= DC / AC</i>\n\n" +
                "• <b>Natural 20 (Nat 20):</b> Critical Success! Double damage in combat or automatic triumph in dialogue.\n\n" +
                "• <b>Natural 1 (Nat 1):</b> Critical Failure! Action ends in an immediate fumble.\n\n" +
                "• <b>Oakhaven Village:</b> Purchase health potions and forge upgrades (+1 Damage / +1 AC) from the blacksmith before entering the castle!\n\n" +
                "• <b>Music / Credits:</b> Scottish Harp (Pixabay): https://pixabay.com/music/scotland-harp-587446/";
            tTMP.raycastTarget = false;

            Button closeBtn = CreateMenuButton(box.transform, "CloseRules_Btn", "Close", new Vector2(0f, -180f), new Color(0.5f, 0.3f, 0.2f));
            closeBtn.onClick.AddListener(CloseRules);

            rPanel.SetActive(false);
        }

        private Button CreateMenuButton(Transform parent, string name, string label, Vector2 pos, Color color)
        {
            return UIFactory.CreateTextButton(parent, name, label, pos, new Vector2(240f, 44f), color);
        }

        #endregion
    }
}
