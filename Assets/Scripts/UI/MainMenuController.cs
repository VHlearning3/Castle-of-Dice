using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Controls the Title Screen, Class Selection (Warrior, Mage, Rogue),
    /// and Game Rules modal. Fulfills the "Main menu" requirement from the notebook.
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

        #region Public Controls

        public void ShowMainMenu()
        {
            EnsureUIHierarchy();
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (classSelectionPanel != null) classSelectionPanel.SetActive(false);
            if (rulesPanel != null) rulesPanel.SetActive(false);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayMode(GamePlayMode.Dialogue);
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

        public void OpenClassSelection()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            if (classSelectionPanel != null) classSelectionPanel.SetActive(true);
        }

        public void CloseClassSelection()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            if (classSelectionPanel != null) classSelectionPanel.SetActive(false);
        }

        public void OpenRules()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            if (rulesPanel != null) rulesPanel.SetActive(true);
        }

        public void CloseRules()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            if (rulesPanel != null) rulesPanel.SetActive(false);
        }

        public void OpenScottishHarpCredit()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            Application.OpenURL(SCOTTISH_HARP_ATTRIBUTION_URL);
        }

        public void SelectCharacterClass(CharacterClassSO chosenClass)
        {
            if (chosenClass == null) return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundType.CriticalSuccess);
            }

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.SetCharacterClass(chosenClass);
                player.InitializeUnit();
            }

            CombatUIController combatUI = FindAnyObjectByType<CombatUIController>();
            if (combatUI != null)
            {
                combatUI.RefreshAbilityBar();
            }

            HideMainMenu();
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
            if (mainMenuPanel != null) return;

            Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("MainMenuPanel");
            if (existing != null)
            {
                mainMenuPanel = existing.gameObject;
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

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.96f);

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

            // Subtitle
            GameObject subObj = new GameObject("Subtitle_Text");
            subObj.transform.SetParent(panel.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchoredPosition = new Vector2(0f, 105f);
            subRect.sizeDelta = new Vector2(600f, 40f);
            TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
            subTMP.text = "Castle of the D20 — Taktinen 3D-Linnaseikkailu";
            subTMP.fontSize = 20f;
            subTMP.alignment = TextAlignmentOptions.Center;
            subTMP.color = new Color(0.8f, 0.85f, 0.9f);

            // Buttons Container
            GameObject btnContainer = new GameObject("Menu_Buttons");
            btnContainer.transform.SetParent(panel.transform, false);
            RectTransform bcRect = btnContainer.AddComponent<RectTransform>();
            bcRect.anchoredPosition = new Vector2(0f, -40f);
            bcRect.sizeDelta = new Vector2(280f, 220f);

            Button newGameBtn = CreateMenuButton(btnContainer.transform, "NewGame_Btn", "Uusi seikkailu", new Vector2(0f, 70f), new Color(0.2f, 0.55f, 0.3f));
            newGameBtn.onClick.AddListener(OpenClassSelection);

            Button continueBtn = CreateMenuButton(btnContainer.transform, "Continue_Btn", "Jatka peliä", new Vector2(0f, 10f), new Color(0.25f, 0.4f, 0.6f));
            continueBtn.onClick.AddListener(HideMainMenu);

            Button rulesBtn = CreateMenuButton(btnContainer.transform, "Rules_Btn", "Säännöt & D20-opas", new Vector2(0f, -50f), new Color(0.45f, 0.35f, 0.25f));
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
            creditTMP.text = "<size=13><color=#8899AA>Musiikki: <u><color=#AACCFF>Scottish Harp (Pixabay)</color></u></color></size>";
            creditTMP.alignment = TextAlignmentOptions.Center;
            creditTMP.raycastTarget = true;
            Button creditBtn = creditObj.AddComponent<Button>();
            creditBtn.onClick.AddListener(OpenScottishHarpCredit);

            // 2. Class Selection Modal
            BuildClassSelectionModal(panel.transform);

            // 3. Rules Modal
            BuildRulesModal(panel.transform);
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

            Image bg = csPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);

            // Title
            GameObject titleObj = new GameObject("CS_Title");
            titleObj.transform.SetParent(csPanel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 190f);
            titleRect.sizeDelta = new Vector2(600f, 60f);
            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "VALITSE SANKARISI";
            titleTMP.fontSize = 36f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = new Color(1f, 0.85f, 0.3f);

            // 3 Class Cards
            CreateClassCard(csPanel.transform, "Warrior_Card", "Sir Roland Rautakoura", "SOTURI", "Korkea HP & AC. Etulinjan miekkataituri.", new Vector2(-280f, 10f), () => SelectCharacterClass(warriorClass));
            CreateClassCard(csPanel.transform, "Mage_Card", "Oppinut Elira", "VELHO", "Massiivinen Tulipallo-aluevahinko (3x3). Teleportti.", new Vector2(0f, 10f), () => SelectCharacterClass(mageClass));
            CreateClassCard(csPanel.transform, "Rogue_Card", "Varjo-Corvo", "VARAS", "Kriittinen pistevahinko, Savupommi ja tiirikointi.", new Vector2(280f, 10f), () => SelectCharacterClass(rogueClass));

            // Back button
            Button backBtn = CreateMenuButton(csPanel.transform, "Back_Btn", "Takaisin", new Vector2(0f, -190f), new Color(0.4f, 0.2f, 0.2f));
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
            img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

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

            // Choose button
            Button chooseBtn = CreateMenuButton(card.transform, "SelectBtn", "Valitse", new Vector2(0f, -100f), new Color(0.2f, 0.55f, 0.3f));
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

            Image bg = rPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);

            GameObject box = new GameObject("Rules_Box");
            box.transform.SetParent(rPanel.transform, false);
            RectTransform bRect = box.AddComponent<RectTransform>();
            bRect.sizeDelta = new Vector2(640f, 450f);

            Image bImg = box.AddComponent<Image>();
            bImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

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
            tTMP.text = "<b><size=22><color=#F1C40F>D20-SÄÄNTÖJÄRJESTELMÄ</color></size></b>\n\n" +
                "• <b>Toiminnot:</b> Jokainen toiminto ja hyökkäys ratkaistaan 20-tahoisella nopalla:\n" +
                "   <i>Tulos = d20 + Taitobonus ≥ DC / AC</i>\n\n" +
                "• <b>Luonnollinen 20 (Nat 20):</b> Kriittinen osuma! Tuplavahinko taistelussa tai täydellinen onnistuminen dialogissa.\n\n" +
                "• <b>Luonnollinen 1 (Nat 1):</b> Kriittinen epäonnistuminen. Vuoro päättyy välittömästi hutiin.\n\n" +
                "• <b>Kivenkolon kylä:</b> Osta terveysjuomia ja päivityksiä (+1 vahinko / +1 AC) sepältä ennen linnaan astumista!\n\n" +
                "• <b>Musiikki / Credits:</b> Scottish Harp (Pixabay): https://pixabay.com/music/scotland-harp-587446/";

            Button closeBtn = CreateMenuButton(box.transform, "CloseRules_Btn", "Sulje", new Vector2(0f, -180f), new Color(0.5f, 0.3f, 0.2f));
            closeBtn.onClick.AddListener(CloseRules);

            rPanel.SetActive(false);
        }

        private Button CreateMenuButton(Transform parent, string name, string label, Vector2 pos, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 44f);
            rect.anchoredPosition = pos;

            Image img = btnObj.AddComponent<Image>();
            img.color = color;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = color * 1.3f;
            cb.pressedColor = color * 0.8f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = rect.sizeDelta;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 17f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        #endregion
    }
}
