using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// User Interface Controller for tactical turn-based combat.
    /// Manages the 4-slot ability action bar, End Turn button, turn phase banners,
    /// and a scrolling combat activity log.
    /// </summary>
    public class CombatUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Root & Containers")]
        [Tooltip("Root GameObject containing the combat action bar and turn controls (e.g. CombatActionBar).")]
        [SerializeField] private GameObject combatActionBar;

        [Header("Ability Action Bar (4 Slots)")]
        [Tooltip("Button components corresponding to the 4 class abilities.")]
        [SerializeField] private List<Button> abilityButtons = new List<Button>(4);

        [Tooltip("Icon images for the 4 ability buttons.")]
        [SerializeField] private List<Image> abilityIcons = new List<Image>(4);

        [Tooltip("Labels displaying ability names on the 4 buttons.")]
        [SerializeField] private List<TMP_Text> abilityNames = new List<TMP_Text>(4);

        [Tooltip("Labels displaying ability range on the 4 buttons.")]
        [SerializeField] private List<TMP_Text> abilityRanges = new List<TMP_Text>(4);

        [Header("Turn Controls & Banner")]
        [Tooltip("Button to manually conclude the hero's turn.")]
        [SerializeField] private Button endTurnButton;

        [Tooltip("Button the hero presses before clicking a tile to move. Built at runtime if left empty.")]
        [SerializeField] private Button moveButton;

        [Tooltip("Banner text indicating turn phase (PLAYER TURN / ENEMY TURN / VICTORY / DEFEAT).")]
        [SerializeField] private TMP_Text turnBannerText;

        [Tooltip("Round counter display text (e.g. 'Round 1').")]
        [SerializeField] private TMP_Text roundCounterText;

        [Header("Combat Log")]
        [Tooltip("Text component displaying scrolling combat activity messages.")]
        [SerializeField] private TMP_Text combatLogText;

        [Tooltip("Maximum lines displayed in the combat log before pruning.")]
        [SerializeField] private int maxLogLines = 20;

        [Header("Fantasy Theme Sprites & Slots")]
        [SerializeField] private Sprite panelDarkSprite;
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite dividerGoldSprite;
        [SerializeField] private Sprite buttonNormalSprite;
        [SerializeField] private Sprite buttonHoverSprite;
        [SerializeField] private Sprite buttonPressedSprite;
        [SerializeField] private Sprite hourglassIconSprite;
        [SerializeField] private Sprite defaultAbilityIconSprite;

        public GameObject CombatActionBar => combatActionBar;
        public Button EndTurnButton => endTurnButton;
        public Button MoveButton => moveButton;

        /// <summary>True after the hero pressed Move; only then does clicking a tile move them.</summary>
        public bool IsMoveModeActive => moveModeActive;
        public IReadOnlyList<Button> AbilityButtons => abilityButtons;
        public IReadOnlyList<Image> AbilityIcons => abilityIcons;
        public IReadOnlyList<TMP_Text> AbilityNames => abilityNames;
        public IReadOnlyList<TMP_Text> AbilityRanges => abilityRanges;

        #endregion

        #region Singleton & Access

        private static CombatUIController instance;

        /// <summary>
        /// Singleton instance accessor. Lazily discovers the component in the active scene
        /// (including inactive objects) or under any Canvas hierarchy.
        /// </summary>
        public static CombatUIController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<CombatUIController>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var canvas in canvases)
                        {
                            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.name.IndexOf("CombatActionBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    child.name.IndexOf("CombatPanel", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    instance = child.GetComponent<CombatUIController>() ?? child.gameObject.AddComponent<CombatUIController>();
                                    break;
                                }
                            }
                            if (instance != null) break;
                        }
                    }
                }
                if (instance != null && !instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(true);
                }

                return instance;
            }
            private set => instance = value;
        }

        /// <summary>
        /// Guarantees that CombatUIController, its host GameObject, and the action bar container
        /// are active, subscribed to events, and ready for combat interactions.
        /// </summary>
        public void EnsureActiveAndReady(bool showCombatBar = true)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (combatActionBar != null && !combatActionBar.activeSelf)
            {
                combatActionBar.SetActive(true);
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            EnsureStyledHierarchy();
            LocatePlayer();

            if (showCombatBar)
            {
                SetCombatBarVisible(true);
                RefreshAbilityBar();
            }
        }

        #endregion

        #region Private State

        private CanvasGroup canvasGroup;
        private PlayerUnit activePlayer;
        private readonly List<string> logHistory = new List<string>();
        private int selectedAbilitySlot = -1;
        private bool moveModeActive;

        #endregion

        #region Theme Sprites & Auto-Discovery

        public void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            if (buttonHoverSprite == null)
                buttonHoverSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            if (buttonPressedSprite == null)
                buttonPressedSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");
            if (hourglassIconSprite == null)
                hourglassIconSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_Hourglass.png");
            if (defaultAbilityIconSprite == null)
                defaultAbilityIconSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Icon_Sword.png");
        }

        public void AutoLocateComponents()
        {
            if (combatActionBar == null)
            {
                Transform barTransform = transform.Find("CombatActionBar")
                    ?? transform.Find("ActionBar")
                    ?? transform.Find("CombatPanel");

                if (barTransform != null)
                {
                    combatActionBar = barTransform.gameObject;
                }
                else if (abilityButtons.Count > 0 && abilityButtons[0] != null)
                {
                    combatActionBar = abilityButtons[0].transform.parent?.gameObject;
                }
                else
                {
                    combatActionBar = gameObject;
                }
            }

            Transform root = combatActionBar != null ? combatActionBar.transform : transform;

            // Locate End Turn button
            if (endTurnButton == null)
            {
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    string lower = b.gameObject.name.ToLowerInvariant();
                    if (lower.Contains("end") || lower.Contains("turn"))
                    {
                        endTurnButton = b;
                        break;
                    }
                }
            }

            // Locate Ability buttons
            Transform container = root.Find("AblilitiesContainer")
                ?? root.Find("AbilitiesContainer")
                ?? root.Find("AbilityContainer");

            if (container != null)
            {
                Button[] btns = container.GetComponentsInChildren<Button>(true);
                if (abilityButtons.Count < 4)
                {
                    abilityButtons.Clear();
                    for (int i = 0; i < Mathf.Min(4, btns.Length); i++)
                    {
                        abilityButtons.Add(btns[i]);
                    }
                }
            }

            while (abilityIcons.Count < abilityButtons.Count) abilityIcons.Add(null);
            while (abilityNames.Count < abilityButtons.Count) abilityNames.Add(null);
            while (abilityRanges.Count < abilityButtons.Count) abilityRanges.Add(null);

            for (int i = 0; i < abilityButtons.Count; i++)
            {
                if (abilityButtons[i] == null) continue;
                Transform btnTr = abilityButtons[i].transform;

                if (abilityIcons[i] == null)
                {
                    Transform iconTr = btnTr.Find("Ability_Slot_Frame/Ability_Icon")
                        ?? btnTr.Find("Slot_Frame/Ability_Icon")
                        ?? btnTr.Find("Ability_Icon")
                        ?? btnTr.Find("Icon");

                    if (iconTr != null)
                    {
                        abilityIcons[i] = iconTr.GetComponent<Image>();
                    }
                }

                if (abilityNames[i] == null)
                {
                    Transform nameTr = btnTr.Find("Ability_Text_Area/Ability_Name_Text")
                        ?? btnTr.Find("Ability_Name_Text")
                        ?? btnTr.Find("Name")
                        ?? btnTr.Find("Text (TMP)");

                    if (nameTr != null)
                    {
                        abilityNames[i] = nameTr.GetComponent<TMP_Text>();
                    }
                }

                if (abilityRanges[i] == null)
                {
                    Transform rangeTr = btnTr.Find("Ability_Text_Area/Ability_Range_Text")
                        ?? btnTr.Find("Ability_Range_Text")
                        ?? btnTr.Find("Range")
                        ?? btnTr.Find("RangeText");

                    if (rangeTr != null)
                    {
                        abilityRanges[i] = rangeTr.GetComponent<TMP_Text>();
                    }
                }
            }
        }

        /// <summary>
        /// Restructures and styles the Combat HUD:
        /// 1. Centered bottom combat tray.
        /// 2. 9-sliced fantasy End Turn button with hourglass icon and gold typography (replaces neon yellow).
        /// 3. 4 tabletop ability cards with 9-sliced buttons, framed ability icon slots, bold titles, and range labels.
        /// </summary>
        private const string ClickShieldName = "ActionBar_ClickShield";

        /// <summary>
        /// Invisible raycast target covering the whole action bar strip (including the gaps between buttons
        /// and the margin down to the screen edge), so a click that misses a button never moves the hero.
        /// </summary>
        private void EnsureClickShield()
        {
            if (combatActionBar == null) return;

            Transform existing = combatActionBar.transform.Find(ClickShieldName);
            GameObject shield = existing != null
                ? existing.gameObject
                : new GameObject(ClickShieldName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shield.transform.SetParent(combatActionBar.transform, false);
            shield.transform.SetAsFirstSibling(); // behind the buttons so they still receive clicks

            RectTransform rt = shield.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-90f, -24f); // past the bar edges and down to the bottom of the screen
            rt.offsetMax = new Vector2(90f, 45f);

            // Visible action tray: the same riveted dark slate + gold frame plate as the HUD cards.
            // It is the first sibling of the bar, so every ability / End Turn button renders on top of it.
            Image img = shield.GetComponent<Image>();
            if (panelDarkSprite != null)
            {
                img.sprite = panelDarkSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0.93f);
            }
            else
            {
                img.sprite = null;
                img.color = UITheme.PanelSlate;
            }
            img.raycastTarget = true; // this is what swallows clicks that miss a button

            // Gold filigree along the top edge, above the buttons (tray top sits 45px over the bar)
            Image trim = EnsureTrayDecoration(shield.transform, "Tray_TopTrim", dividerGoldSprite, UITheme.GoldAccent);
            if (trim != null)
            {
                RectTransform trimRect = trim.rectTransform;
                trimRect.anchorMin = new Vector2(0f, 1f);
                trimRect.anchorMax = new Vector2(1f, 1f);
                trimRect.pivot = new Vector2(0.5f, 0.5f);
                trimRect.offsetMin = new Vector2(60f, -9f);
                trimRect.offsetMax = new Vector2(-60f, 9f);
            }

            // Soft inner shade so the buttons read as sitting in a recessed tray
            Image shade = EnsureTrayDecoration(shield.transform, "Tray_InnerShade", null, new Color(0f, 0f, 0f, 0.28f));
            if (shade != null)
            {
                RectTransform shadeRect = shade.rectTransform;
                shadeRect.anchorMin = Vector2.zero;
                shadeRect.anchorMax = Vector2.one;
                shadeRect.pivot = new Vector2(0.5f, 0.5f);
                shadeRect.offsetMin = new Vector2(70f, 14f);
                shadeRect.offsetMax = new Vector2(-80f, -34f); // ability row ends 10px short of the bar edge; keep equal margins
                shade.transform.SetAsFirstSibling();
            }

            // Gold rivet caps at both ends of the tray
            PlaceTrayCap(shield.transform, "Tray_Cap_Left", new Vector2(0f, 0.5f), new Vector2(34f, 8f));
            PlaceTrayCap(shield.transform, "Tray_Cap_Right", new Vector2(1f, 0.5f), new Vector2(-34f, 8f));
        }

        private Image EnsureTrayDecoration(Transform tray, string name, Sprite sprite, Color color)
        {
            Transform existing = tray.Find(name);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(tray, false);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false; // the tray itself handles click blocking
            return image;
        }

        private void PlaceTrayCap(Transform tray, string name, Vector2 anchor, Vector2 offset)
        {
            Sprite capSprite = UITheme.Active != null && UITheme.Active.pillBadge != null ? UITheme.Active.pillBadge : slotFrameSprite;
            Image cap = EnsureTrayDecoration(tray, name, capSprite, UITheme.GoldAccent);
            RectTransform capRect = cap.rectTransform;
            capRect.anchorMin = anchor;
            capRect.anchorMax = anchor;
            capRect.pivot = new Vector2(0.5f, 0.5f);
            capRect.anchoredPosition = offset;
            // The pill sprite is horizontal; rotate it to stand as a vertical gold end cap
            capRect.sizeDelta = new Vector2(46f, 16f);
            capRect.localEulerAngles = new Vector3(0f, 0f, 90f);
        }

        private const string MoveButtonName = "Move_Button";
        private const float MoveButtonX = 175f;       // End Turn is 165px wide at x = 0
        private const float MoveButtonWidth = 145f;
        private const float AbilityRowStartX = 330f;  // Move button ends at 320

        /// <summary>
        /// Builds (or re-styles) the Move button between End Turn and the ability cards.
        /// Tiles only move the hero after this button is pressed, so a stray click can't waste the move.
        /// </summary>
        private void EnsureMoveButton()
        {
            if (combatActionBar == null) return;

            if (moveButton == null)
            {
                Transform existing = combatActionBar.transform.Find(MoveButtonName);
                GameObject go = existing != null
                    ? existing.gameObject
                    : new GameObject(MoveButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                moveButton = go.GetComponent<Button>();
                if (moveButton == null) moveButton = go.AddComponent<Button>();
            }

            moveButton.transform.SetParent(combatActionBar.transform, false);
            RectTransform rect = moveButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(MoveButtonX, 0f);
            rect.sizeDelta = new Vector2(MoveButtonWidth, 66f);
            rect.localScale = Vector3.one;

            Image img = moveButton.GetComponent<Image>();
            if (img != null)
            {
                if (buttonNormalSprite != null)
                {
                    img.sprite = buttonNormalSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                else
                {
                    img.color = UITheme.PanelSlate;
                }
                img.raycastTarget = true;
                moveButton.targetGraphic = img;
            }

            if (buttonHoverSprite != null && buttonPressedSprite != null)
            {
                moveButton.transition = Selectable.Transition.SpriteSwap;
                moveButton.spriteState = new SpriteState
                {
                    highlightedSprite = buttonHoverSprite,
                    pressedSprite = buttonPressedSprite,
                    selectedSprite = buttonHoverSprite,
                    disabledSprite = buttonNormalSprite
                };
            }

            TMP_Text label = moveButton.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                GameObject textObj = new GameObject("Move_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(moveButton.transform, false);
                label = textObj.GetComponent<TMP_Text>();
            }
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            labelRect.localScale = Vector3.one;

            label.text = "Move";
            label.fontSize = 15.5f;
            label.fontStyle = FontStyles.Bold;
            label.color = UITheme.GoldAccent;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }

        public void EnsureStyledHierarchy()
        {
            LoadThemeSpritesIfMissing();
            AutoLocateComponents();

            if (combatActionBar == null) combatActionBar = gameObject;

            // 1. Root Combat Action Bar
            RectTransform barRect = combatActionBar.GetComponent<RectTransform>();
            if (barRect != null)
            {
                barRect.anchorMin = new Vector2(0.5f, 0f);
                barRect.anchorMax = new Vector2(0.5f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(0f, 20f);
                barRect.sizeDelta = new Vector2(1210f, 76f); // End Turn + Move + 4 ability cards
                barRect.localScale = Vector3.one;
            }

            EnsureClickShield();

            // 2. Style End Turn Button
            if (endTurnButton != null)
            {
                endTurnButton.transform.SetParent(combatActionBar.transform, false);
                RectTransform endRect = endTurnButton.GetComponent<RectTransform>();
                endRect.anchorMin = new Vector2(0f, 0.5f);
                endRect.anchorMax = new Vector2(0f, 0.5f);
                endRect.pivot = new Vector2(0f, 0.5f);
                endRect.anchoredPosition = new Vector2(0f, 0f);
                endRect.sizeDelta = new Vector2(165f, 66f);
                endRect.localScale = Vector3.one; // Explicitly remove the 2x2x2 distortion!

                Image endImg = endTurnButton.GetComponent<Image>();
                if (endImg != null && buttonNormalSprite != null)
                {
                    endImg.sprite = buttonNormalSprite;
                    endImg.type = Image.Type.Sliced;
                    endImg.color = Color.white; // Explicitly remove lime-yellow color!
                }

                if (buttonHoverSprite != null && buttonPressedSprite != null)
                {
                    endTurnButton.transition = Selectable.Transition.SpriteSwap;
                    SpriteState ss = new SpriteState
                    {
                        highlightedSprite = buttonHoverSprite,
                        pressedSprite = buttonPressedSprite,
                        selectedSprite = buttonHoverSprite,
                        disabledSprite = buttonNormalSprite
                    };
                    endTurnButton.spriteState = ss;
                }

                // Add Hourglass Icon
                Transform hIconTr = endTurnButton.transform.Find("Hourglass_Icon");
                GameObject hIconObj = hIconTr != null ? hIconTr.gameObject : null;
                if (hIconObj == null)
                {
                    hIconObj = new GameObject("Hourglass_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    hIconObj.transform.SetParent(endTurnButton.transform, false);
                }
                RectTransform hiRect = hIconObj.GetComponent<RectTransform>();
                hiRect.anchorMin = new Vector2(0f, 0.5f);
                hiRect.anchorMax = new Vector2(0f, 0.5f);
                hiRect.pivot = new Vector2(0f, 0.5f);
                hiRect.anchoredPosition = new Vector2(14f, 0f);
                hiRect.sizeDelta = new Vector2(28f, 28f);
                hiRect.localScale = Vector3.one;

                Image hiImg = hIconObj.GetComponent<Image>();
                if (hiImg != null && hourglassIconSprite != null)
                {
                    hiImg.sprite = hourglassIconSprite;
                    hiImg.preserveAspect = true;
                    hiImg.color = Color.white;
                    hiImg.raycastTarget = false;
                }

                // Style End Turn text
                TMP_Text endText = endTurnButton.GetComponentInChildren<TMP_Text>(true);
                if (endText != null)
                {
                    RectTransform etRect = endText.GetComponent<RectTransform>();
                    etRect.anchorMin = Vector2.zero;
                    etRect.anchorMax = Vector2.one;
                    etRect.pivot = new Vector2(0.5f, 0.5f);
                    etRect.offsetMin = new Vector2(44f, 0f);
                    etRect.offsetMax = new Vector2(-8f, 0f);
                    etRect.localScale = Vector3.one;

                    endText.text = "End Turn";
                    endText.fontSize = 15.5f;
                    endText.fontStyle = FontStyles.Bold;
                    endText.color = UITheme.GoldAccent; // #F6D578
                    endText.alignment = TextAlignmentOptions.Center;
                    endText.enableAutoSizing = false;
                    endText.raycastTarget = false;
                }
            }

            // 3. Move button, right of End Turn
            EnsureMoveButton();

            // 4. Style Abilities Container
            Transform containerTr = combatActionBar.transform.Find("AblilitiesContainer")
                ?? combatActionBar.transform.Find("AbilitiesContainer")
                ?? combatActionBar.transform.Find("AbilityContainer");

            if (containerTr != null)
            {
                RectTransform contRect = containerTr.GetComponent<RectTransform>();
                contRect.anchorMin = new Vector2(0f, 0.5f);
                contRect.anchorMax = new Vector2(1f, 0.5f);
                contRect.pivot = new Vector2(0f, 0.5f);
                contRect.anchoredPosition = new Vector2(AbilityRowStartX, 0f);
                contRect.sizeDelta = new Vector2(-AbilityRowStartX, 76f);
                contRect.localScale = Vector3.one;

                HorizontalLayoutGroup hlg = containerTr.GetComponent<HorizontalLayoutGroup>();
                if (hlg == null) hlg = containerTr.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }

            // 5. Style each of the 4 Ability Buttons
            for (int i = 0; i < abilityButtons.Count; i++)
            {
                if (abilityButtons[i] == null) continue;
                Button btn = abilityButtons[i];
                Transform btnTr = btn.transform;

                RectTransform bRect = btn.GetComponent<RectTransform>();
                bRect.sizeDelta = new Vector2(210f, 66f);
                bRect.localScale = Vector3.one;

                // Button Sprite
                Image btnImg = btn.GetComponent<Image>();
                if (btnImg != null && buttonNormalSprite != null)
                {
                    btnImg.sprite = buttonNormalSprite;
                    btnImg.type = Image.Type.Sliced;
                    btnImg.color = Color.white;
                }

                if (buttonHoverSprite != null && buttonPressedSprite != null)
                {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    SpriteState ss = new SpriteState
                    {
                        highlightedSprite = buttonHoverSprite,
                        pressedSprite = buttonPressedSprite,
                        selectedSprite = buttonHoverSprite,
                        disabledSprite = buttonNormalSprite
                    };
                    btn.spriteState = ss;
                }

                // Framed Ability Icon Slot
                Transform frameTr = btnTr.Find("Ability_Slot_Frame");
                GameObject frameObj = frameTr != null ? frameTr.gameObject : null;
                if (frameObj == null)
                {
                    frameObj = new GameObject("Ability_Slot_Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    frameObj.transform.SetParent(btnTr, false);
                }
                RectTransform fRect = frameObj.GetComponent<RectTransform>();
                fRect.anchorMin = new Vector2(0f, 0.5f);
                fRect.anchorMax = new Vector2(0f, 0.5f);
                fRect.pivot = new Vector2(0f, 0.5f);
                fRect.anchoredPosition = new Vector2(8f, 0f);
                fRect.sizeDelta = new Vector2(48f, 48f);
                fRect.localScale = Vector3.one;

                Image frameImg = frameObj.GetComponent<Image>();
                if (frameImg != null && slotFrameSprite != null)
                {
                    frameImg.sprite = slotFrameSprite;
                    frameImg.type = Image.Type.Sliced;
                    frameImg.color = Color.white;
                    frameImg.raycastTarget = false;
                }

                // Child Icon Image inside Frame
                Transform iconTr = frameObj.transform.Find("Ability_Icon");
                GameObject iconObj = iconTr != null ? iconTr.gameObject : null;
                if (iconObj == null)
                {
                    iconObj = new GameObject("Ability_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconObj.transform.SetParent(frameObj.transform, false);
                }
                RectTransform iRect = iconObj.GetComponent<RectTransform>();
                iRect.anchorMin = Vector2.zero;
                iRect.anchorMax = Vector2.one;
                iRect.pivot = new Vector2(0.5f, 0.5f);
                iRect.anchoredPosition = Vector2.zero;
                iRect.sizeDelta = new Vector2(-10f, -10f);
                iRect.localScale = Vector3.one;

                Image iconImg = iconObj.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.preserveAspect = true;
                    iconImg.color = Color.white;
                    iconImg.raycastTarget = false;
                    if (abilityIcons.Count > i)
                    {
                        abilityIcons[i] = iconImg;
                    }
                    else
                    {
                        abilityIcons.Add(iconImg);
                    }
                }

                // Text Container Area (Right side)
                Transform textAreaTr = btnTr.Find("Ability_Text_Area");
                GameObject textAreaObj = textAreaTr != null ? textAreaTr.gameObject : null;
                if (textAreaObj == null)
                {
                    textAreaObj = new GameObject("Ability_Text_Area", typeof(RectTransform));
                    textAreaObj.transform.SetParent(btnTr, false);
                }
                RectTransform taRect = textAreaObj.GetComponent<RectTransform>();
                taRect.anchorMin = new Vector2(0f, 0f);
                taRect.anchorMax = new Vector2(1f, 1f);
                taRect.pivot = new Vector2(0f, 0.5f);
                taRect.offsetMin = new Vector2(62f, 4f);
                taRect.offsetMax = new Vector2(-6f, -4f);
                taRect.localScale = Vector3.one;

                // Move original text child if it was directly under button
                Transform oldText = btnTr.Find("Text (TMP)");
                if (oldText != null && oldText.parent != textAreaObj.transform)
                {
                    oldText.SetParent(textAreaObj.transform, false);
                    oldText.name = "Ability_Name_Text";
                }

                // Ability Name Text
                Transform nameTr = textAreaObj.transform.Find("Ability_Name_Text");
                GameObject nameObj = nameTr != null ? nameTr.gameObject : null;
                if (nameObj == null)
                {
                    nameObj = new GameObject("Ability_Name_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    nameObj.transform.SetParent(textAreaObj.transform, false);
                }
                RectTransform nameRect = nameObj.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0f, 0.5f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.pivot = new Vector2(0f, 0.5f);
                nameRect.offsetMin = Vector2.zero;
                nameRect.offsetMax = Vector2.zero;
                nameRect.localScale = Vector3.one;

                TMP_Text nameTxt = nameObj.GetComponent<TMP_Text>();
                if (nameTxt != null)
                {
                    nameTxt.fontSize = 14f;
                    nameTxt.fontStyle = FontStyles.Bold;
                    nameTxt.color = UITheme.GoldAccent; // #F6D578
                    nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    nameTxt.enableAutoSizing = false;
                    nameTxt.raycastTarget = false;
                    if (abilityNames.Count > i)
                    {
                        abilityNames[i] = nameTxt;
                    }
                    else
                    {
                        abilityNames.Add(nameTxt);
                    }
                }

                // Ability Range / Type Text
                Transform rangeTr = textAreaObj.transform.Find("Ability_Range_Text");
                GameObject rangeObj = rangeTr != null ? rangeTr.gameObject : null;
                if (rangeObj == null)
                {
                    rangeObj = new GameObject("Ability_Range_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    rangeObj.transform.SetParent(textAreaObj.transform, false);
                }
                RectTransform rangeRect = rangeObj.GetComponent<RectTransform>();
                rangeRect.anchorMin = new Vector2(0f, 0f);
                rangeRect.anchorMax = new Vector2(1f, 0.5f);
                rangeRect.pivot = new Vector2(0f, 0.5f);
                rangeRect.offsetMin = Vector2.zero;
                rangeRect.offsetMax = Vector2.zero;
                rangeRect.localScale = Vector3.one;

                TMP_Text rangeTxt = rangeObj.GetComponent<TMP_Text>();
                if (rangeTxt != null)
                {
                    rangeTxt.fontSize = 11.5f;
                    rangeTxt.color = new Color(0.65f, 0.72f, 0.82f, 1f); // #A4B7D1
                    rangeTxt.alignment = TextAlignmentOptions.MidlineLeft;
                    rangeTxt.enableAutoSizing = false;
                    rangeTxt.raycastTarget = false;
                    if (abilityRanges.Count > i)
                    {
                        abilityRanges[i] = rangeTxt;
                    }
                    else
                    {
                        abilityRanges.Add(rangeTxt);
                    }
                }

                // Destroy legacy ghost text children (1ButtonText, 2ButtonText, Text, etc.)
                for (int c = btnTr.childCount - 1; c >= 0; c--)
                {
                    Transform child = btnTr.GetChild(c);
                    string childName = child.name;
                    if (childName != "Ability_Slot_Frame" && childName != "Ability_Text_Area" && childName != "Hourglass_Icon")
                    {
                        if (childName.Contains("ButtonText") || childName.Contains("Text (") || childName == "Text" || child.GetComponent<TMP_Text>() != null || child.GetComponent<UnityEngine.UI.Text>() != null)
                        {
                            if (Application.isPlaying)
                                Destroy(child.gameObject);
                            else
                                DestroyImmediate(child.gameObject);
                        }
                    }
                }
            }

            AbilityTooltipUI.SetThemeSprites(panelDarkSprite, slotFrameSprite, dividerGoldSprite, defaultAbilityIconSprite);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            LoadThemeSpritesIfMissing();
            AutoLocateComponents();
            EnsureStyledHierarchy();
            AbilityTooltipUI.SetThemeSprites(panelDarkSprite, slotFrameSprite, dividerGoldSprite, defaultAbilityIconSprite);

            // Locate or initialize CanvasGroup for flicker-free show/hide without disabling GameObject
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Hide action bar by default until combat is active
            SetCombatBarVisible(false);

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            if (moveButton != null)
            {
                moveButton.onClick.RemoveAllListeners();
                moveButton.onClick.AddListener(ToggleMoveMode);
            }

            // Hook up ability slot clicks
            for (int i = 0; i < abilityButtons.Count; i++)
            {
                int slotIndex = i;
                if (abilityButtons[i] != null)
                {
                    abilityButtons[i].onClick.RemoveAllListeners();
                    abilityButtons[i].onClick.AddListener(() => OnAbilitySlotClicked(slotIndex));
                }
            }

            // Sanitize combat text components
            if (turnBannerText != null)
            {
                turnBannerText.margin = Vector4.zero;
                turnBannerText.enableAutoSizing = true;
                turnBannerText.fontSizeMin = 16f;
                turnBannerText.fontSizeMax = 36f;
                turnBannerText.textWrappingMode = TextWrappingModes.Normal;
                turnBannerText.raycastTarget = false;
            }

            if (roundCounterText != null)
            {
                roundCounterText.margin = Vector4.zero;
                roundCounterText.enableAutoSizing = true;
                roundCounterText.fontSizeMin = 12f;
                roundCounterText.fontSizeMax = 24f;
                roundCounterText.raycastTarget = false;
            }

            if (endTurnButton != null)
            {
                TMP_Text endText = endTurnButton.GetComponentInChildren<TMP_Text>(true);
                if (endText != null)
                {
                    endText.margin = Vector4.zero;
                    endText.enableAutoSizing = true;
                    endText.fontSizeMin = 12f;
                    endText.fontSizeMax = 24f;
                    endText.raycastTarget = false;
                }
            }

            if (combatLogText != null)
            {
                combatLogText.margin = Vector4.zero;
                combatLogText.enableAutoSizing = true;
                combatLogText.fontSizeMin = 12f;
                combatLogText.fontSizeMax = 20f;
                combatLogText.textWrappingMode = TextWrappingModes.Normal;
                combatLogText.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            }

            if (moveButton != null)
            {
                moveButton.onClick.RemoveListener(ToggleMoveMode);
            }
        }

        private void OnEnable()
        {
            GameManager.OnPlayModeChanged += HandlePlayModeChanged;
            TurnManager.OnTurnStateChanged += HandleTurnStateChanged;
            TurnManager.OnUnitTurnStarted += HandleUnitTurnStarted;
            TurnManager.OnCombatEnded += HandleCombatEnded;
            TurnManager.OnCombatVictoryScrapAwarded += HandleCombatVictoryScrapAwarded;
            CombatUnit.OnAnyUnitDamaged += HandleUnitDamaged;
            GridTile.OnTileClicked += HandleTileMouseDown;
        }

        private void OnDisable()
        {
            GameManager.OnPlayModeChanged -= HandlePlayModeChanged;
            TurnManager.OnTurnStateChanged -= HandleTurnStateChanged;
            TurnManager.OnUnitTurnStarted -= HandleUnitTurnStarted;
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            TurnManager.OnCombatVictoryScrapAwarded -= HandleCombatVictoryScrapAwarded;
            CombatUnit.OnAnyUnitDamaged -= HandleUnitDamaged;
            GridTile.OnTileClicked -= HandleTileMouseDown;
        }

        private void Start()
        {
            LocatePlayer();
            RefreshAbilityBar();

            // Ensure action bar reflects initial game mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Combat)
            {
                SetCombatBarVisible(false);
            }
        }

        private GridTile lastHoveredTile;
        private Camera combatCamera;

        // Zero-GC per-frame hover raycast buffers (see HandleCombatTileRaycast / IsPointerOverUI)
        private readonly RaycastHit[] tileRaycastHits = new RaycastHit[32];
        private UnityEngine.EventSystems.PointerEventData cachedPointerData;
        private UnityEngine.EventSystems.EventSystem cachedPointerEventSystem;
        private readonly List<UnityEngine.EventSystems.RaycastResult> uiRaycastList = new List<UnityEngine.EventSystems.RaycastResult>();
        private int lastClickFrame = -1;
        private GridTile lastClickTile = null;

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentMode != GamePlayMode.Combat)
            {
                ClearHoveredTile();
                return;
            }

            if (TurnManager.Instance == null || TurnManager.Instance.CurrentState != TurnState.PlayerTurn)
            {
                ClearHoveredTile();
                return;
            }

            HandleCombatTileRaycast();
        }

        private bool IsEnemyOnTile(GridTile tile)
        {
            if (tile == null) return false;
            if (tile.IsOccupied && tile.OccupyingUnit != null && tile.OccupyingUnit is EnemyUnit) return true;

            if (TurnManager.Instance != null)
            {
                foreach (var unit in TurnManager.Instance.ActiveUnits)
                {
                    if (unit != null && unit.IsAlive && unit is EnemyUnit && unit.GridPosition == tile.GridPosition)
                    {
                        unit.EnsureTilePosition(); // unit re-registers its own tile
                        return true;
                    }
                }
            }
            return false;
        }

        private void ClearHoveredTile()
        {
            if (lastHoveredTile != null)
            {
                lastHoveredTile.TriggerUnhover();
                lastHoveredTile = null;
            }
        }

        private void HandleCombatTileRaycast()
        {
            // Do not raycast through interactive UI elements (buttons, dialogs, action bar)
            if (IsPointerOverUI())
            {
                ClearHoveredTile();
                return;
            }

            if (combatCamera == null)
            {
                combatCamera = Camera.main ?? FindAnyObjectByType<Camera>();
                if (combatCamera == null) return;
            }

            Ray ray = combatCamera.ScreenPointToRay(GameInput.GetMousePosition());
            GridTile targetTile = null;

            // Non-allocating multi-hit raycast (ignores trigger colliders such as Cellar_Encounter_Trigger)
            RaycastHit[] hits = tileRaycastHits;
            int hitCount = Physics.RaycastNonAlloc(ray, hits, 250f, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                // Insertion sort by distance (tiny N, no comparer allocation) so the nearest hit wins
                for (int i = 1; i < hitCount; i++)
                {
                    RaycastHit key = hits[i];
                    int j = i - 1;
                    while (j >= 0 && hits[j].distance > key.distance)
                    {
                        hits[j + 1] = hits[j];
                        j--;
                    }
                    hits[j + 1] = key;
                }

                // Priority 1: Direct GridTile hit
                for (int i = 0; i < hitCount; i++)
                {
                    GridTile directTile = hits[i].collider.GetComponentInParent<GridTile>();
                    if (directTile != null)
                    {
                        targetTile = directTile;
                        break;
                    }
                }

                // Priority 2: Direct CombatUnit hit -> resolve unit's tile
                if (targetTile == null)
                {
                    for (int i = 0; i < hitCount; i++)
                    {
                        CombatUnit hitUnit = hits[i].collider.GetComponentInParent<CombatUnit>();
                        if (hitUnit != null)
                        {
                            hitUnit.EnsureTilePosition();
                            targetTile = hitUnit.CurrentTile ?? (GridManager.Instance != null ? GridManager.Instance.GetTileAt(hitUnit.GridPosition) : null);
                            if (targetTile != null)
                            {
                                break;
                            }
                        }
                    }
                }

                // Priority 3: Hit floor or ground surface -> project point onto grid coordinates
                if (targetTile == null && GridManager.Instance != null)
                {
                    for (int i = 0; i < hitCount; i++)
                    {
                        RaycastHit h = hits[i];
                        if (h.collider != null && !h.collider.isTrigger)
                        {
                            Vector3 origin = GridManager.Instance.OriginWorldPosition;
                            if (Mathf.Abs(h.point.y - origin.y) < 3.0f)
                            {
                                Vector2Int gridPos = GridManager.Instance.GetGridPosition(h.point);
                                GridTile floorTile = GridManager.Instance.GetTileAt(gridPos);
                                if (floorTile != null)
                                {
                                    targetTile = floorTile;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Update hover state
            if (targetTile != lastHoveredTile)
            {
                if (lastHoveredTile != null)
                {
                    lastHoveredTile.TriggerUnhover();
                }

                if (targetTile != null)
                {
                    if (IsEnemyOnTile(targetTile))
                    {
                        targetTile.ApplyHighlight(TileHighlightType.EnemyTarget);
                    }
                    else
                    {
                        targetTile.TriggerHover();
                    }
                }

                lastHoveredTile = targetTile;
            }

            // Detect left click on tile
            if (targetTile != null && GameInput.GetLeftMouseButtonDown())
            {
                HandleTileClicked(targetTile);
            }
        }

        private bool IsPointerOverUI()
        {
            // No IsPointerOverGameObject() shortcut: with InputSystemUIInputModule it can lag a frame
            // behind the click, which let clicks on the action bar tray fall through to the tiles.
            if (UnityEngine.EventSystems.EventSystem.current == null) return false;

            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (cachedPointerData == null || cachedPointerEventSystem != eventSystem)
            {
                cachedPointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem);
                cachedPointerEventSystem = eventSystem;
            }
            UnityEngine.EventSystems.PointerEventData pointerData = cachedPointerData;
            pointerData.Reset();
            pointerData.position = GameInput.GetMousePosition();

            uiRaycastList.Clear();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, uiRaycastList);

            for (int i = 0; i < uiRaycastList.Count; i++)
            {
                GameObject obj = uiRaycastList[i].gameObject;
                if (obj == null) continue;

                // Anything on the action bar strip (buttons, gaps, click shield) blocks tile clicks
                if (combatActionBar != null && obj.transform.IsChildOf(combatActionBar.transform))
                {
                    return true;
                }

                if (obj.GetComponentInParent<Selectable>() != null ||
                    obj.GetComponentInParent<Button>() != null ||
                    obj.GetComponentInParent<TMP_InputField>() != null)
                {
                    return true;
                }

                string n = obj.name; // case-insensitive search without allocating a lowered copy
                if (n.IndexOf("dialogue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("modal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("popup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Player & Ability Bar Setup

        private void LocatePlayer()
        {
            if (activePlayer == null)
            {
                activePlayer = FindAnyObjectByType<PlayerUnit>();
            }

            if (activePlayer != null)
            {
                if (activePlayer.ActiveAbilities == null || activePlayer.ActiveAbilities.Count == 0)
                {
                    activePlayer.InitializeUnit();
                }
                activePlayer.EnsureTilePosition();
            }
        }

        /// <summary>
        /// Populates the 4 action buttons with abilities from the hero's CharacterClassSO loadout.
        /// </summary>
        public void RefreshAbilityBar()
        {
            LocatePlayer();

            if (activePlayer == null) return;

            IReadOnlyList<AbilitySO> abilities = activePlayer.ActiveAbilities;

            for (int i = 0; i < abilityButtons.Count; i++)
            {
                if (i < abilities.Count && abilities[i] != null)
                {
                    AbilitySO ability = abilities[i];

                    // Rogue ability rework: Lockpick is an exploration passive, excluded from combat action bar
                    if (ability.AbilityID.IndexOf("lockpick", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        abilityButtons[i].gameObject.SetActive(false);
                        continue;
                    }

                    abilityButtons[i].gameObject.SetActive(true);

                    if (abilityNames.Count > i && abilityNames[i] != null)
                    {
                        abilityNames[i].text = ability.AbilityName;
                    }

                    if (abilityRanges.Count > i && abilityRanges[i] != null)
                    {
                        abilityRanges[i].text = ability.TargetType == AbilityTargetType.Self ? "Self" : $"Rng: {ability.Range}";
                    }

                    if (abilityIcons.Count > i && abilityIcons[i] != null)
                    {
                        if (ability.AbilityIcon != null)
                        {
                            abilityIcons[i].sprite = ability.AbilityIcon;
                            abilityIcons[i].enabled = true;
                        }
                        else if (defaultAbilityIconSprite != null)
                        {
                            abilityIcons[i].sprite = defaultAbilityIconSprite;
                            abilityIcons[i].enabled = true;
                        }
                    }

                    // Enable/disable based on whether action has already been used
                    abilityButtons[i].interactable = !activePlayer.HasActedThisTurn && TurnManager.Instance?.CurrentState == TurnState.PlayerTurn;

                    // Highlight selected ability button state
                    Image btnImg = abilityButtons[i].GetComponent<Image>();
                    if (btnImg != null && buttonNormalSprite != null)
                    {
                        btnImg.sprite = (selectedAbilitySlot == i && buttonHoverSprite != null) ? buttonHoverSprite : buttonNormalSprite;
                    }

                    // Attach tooltip listener for rich ability card on hover
                    var tooltip = abilityButtons[i].GetComponent<AbilityTooltipUI>() ?? abilityButtons[i].gameObject.AddComponent<AbilityTooltipUI>();
                    tooltip.SlotIndex = i;
                }
                else
                {
                    abilityButtons[i].gameObject.SetActive(false);
                }
            }

            if (endTurnButton != null)
            {
                bool isPlayerTurn = TurnManager.Instance != null && TurnManager.Instance.CurrentState == TurnState.PlayerTurn;
                endTurnButton.interactable = isPlayerTurn;
            }

            RefreshMoveButton();
        }

        private bool CanMoveNow()
        {
            return activePlayer != null
                && !activePlayer.HasMovedThisTurn
                && activePlayer.MovementRange > 0
                && TurnManager.Instance != null
                && TurnManager.Instance.CurrentState == TurnState.PlayerTurn;
        }

        private void RefreshMoveButton()
        {
            if (moveButton == null) return;

            bool canMove = CanMoveNow();
            if (!canMove) moveModeActive = false;
            moveButton.interactable = canMove;

            TMP_Text label = moveButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                if (moveModeActive) label.text = "Cancel Move";
                else if (activePlayer != null && activePlayer.HasMovedThisTurn) label.text = "Moved";
                else label.text = activePlayer != null ? $"Move ({activePlayer.MovementRange})" : "Move";
            }

            // Pressed-in look while move mode is on, same as a selected ability card
            Image img = moveButton.GetComponent<Image>();
            if (img != null && buttonNormalSprite != null)
            {
                img.sprite = (moveModeActive && buttonHoverSprite != null) ? buttonHoverSprite : buttonNormalSprite;
            }
        }

        #endregion

        #region UI Event Callbacks

        /// <summary>
        /// Move button: turns move mode on (tiles in reach light up and a click moves the hero there)
        /// or off again. Picking an ability, moving, or ending the turn also turns it off.
        /// </summary>
        public void ToggleMoveMode()
        {
            LocatePlayer();
            if (!moveModeActive && !CanMoveNow()) return;

            moveModeActive = !moveModeActive;
            if (moveModeActive)
            {
                selectedAbilitySlot = -1;
                LogCombatMessage("Move: click a highlighted tile.");
            }

            RefreshAbilityBar();
            UpdateMovementHighlights();
        }

        private void OnAbilitySlotClicked(int slotIndex)
        {
            LocatePlayer();
            if (activePlayer == null || activePlayer.HasActedThisTurn || activePlayer.IsWalking) return;
            activePlayer.EnsureTilePosition();
            moveModeActive = false;

            // Clicking the same slot toggles it off
            if (selectedAbilitySlot == slotIndex)
            {
                selectedAbilitySlot = -1;
                UpdateMovementHighlights();
                RefreshAbilityBar();
                return;
            }

            AbilitySO ability = activePlayer.GetAbility(slotIndex);
            if (ability == null) return;

            // If it's a Self ability, execute immediately in-place without needing tile click
            if (ability.TargetType == AbilityTargetType.Self)
            {
                activePlayer.UseAbility(slotIndex, activePlayer.GridPosition, AbilityExecutor.Instance);
                selectedAbilitySlot = -1;
                RefreshAbilityBar();
                UpdateMovementHighlights();
                return;
            }

            selectedAbilitySlot = slotIndex;
            LogCombatMessage($"Selected: {ability.AbilityName}. Target an enemy or grid cell.");

            // Highlight targetable area
            HighlightAbilityTargets(ability);
        }

        private void HighlightAbilityTargets(AbilitySO ability)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || activePlayer == null) return;

            activePlayer.EnsureTilePosition();
            grid.ClearAllHighlights();

            // 1. Direct attack range
            List<GridTile> targetableTiles = grid.GetTilesInRadius(activePlayer.GridPosition, ability.Range);
            grid.HighlightTiles(targetableTiles, TileHighlightType.TargetArea);

            // Highlight enemies directly in attack range in Red
            foreach (var tile in targetableTiles)
            {
                if (IsEnemyOnTile(tile))
                {
                    tile.ApplyHighlight(TileHighlightType.EnemyTarget);
                }
            }

            // 2. If movement is available, also highlight enemies reachable with move + ability
            if (!activePlayer.HasMovedThisTurn && activePlayer.MovementRange > 0)
            {
                int maxReach = activePlayer.MovementRange + ability.Range;
                List<GridTile> reachTiles = grid.GetTilesInRadius(activePlayer.GridPosition, maxReach);
                foreach (var tile in reachTiles)
                {
                    if (IsEnemyOnTile(tile) && !targetableTiles.Contains(tile))
                    {
                        tile.ApplyHighlight(TileHighlightType.EnemyTarget);
                    }
                }
            }
        }

        /// <summary>
        /// GridTile.OnMouseDown fires from physics raycasts, which UI does not block, so a click on the
        /// action bar tray used to land on the tile underneath it. Drop those clicks here.
        /// </summary>
        private void HandleTileMouseDown(GridTile tile)
        {
            if (IsPointerOverUI()) return;
            HandleTileClicked(tile);
        }

        private void HandleTileClicked(GridTile tile)
        {
            if (tile == null) return;
            if (TurnManager.Instance == null || TurnManager.Instance.CurrentState != TurnState.PlayerTurn) return;
            if (Time.frameCount == lastClickFrame && lastClickTile == tile) return;
            lastClickFrame = Time.frameCount;
            lastClickTile = tile;

            LocatePlayer();
            if (activePlayer == null) return;

            // Ignore clicks until the hero has finished walking to their tile
            if (activePlayer.IsWalking) return;
            activePlayer.EnsureTilePosition();

            // Find if there is an occupying unit, with fallback across active combatants
            CombatUnit occupyingUnit = tile.OccupyingUnit;
            if (occupyingUnit == null && TurnManager.Instance != null)
            {
                foreach (var unit in TurnManager.Instance.ActiveUnits)
                {
                    if (unit != null && unit.IsAlive && unit is EnemyUnit && unit.GridPosition == tile.GridPosition)
                    {
                        occupyingUnit = unit;
                        unit.EnsureTilePosition(); // unit re-registers its own tile
                        break;
                    }
                }
            }
            bool isEnemy = occupyingUnit != null && occupyingUnit is EnemyUnit;

            // 1. If an ability is actively selected, execute it or move into range to execute
            if (selectedAbilitySlot >= 0)
            {
                ExecuteAbilityOrMoveIntoRange(selectedAbilitySlot, tile);
                return;
            }

            // 2. If no ability is selected, but the player clicked directly on an enemy:
            // Automatically attack with Primary Ability (Slot 0, e.g. Sword Slash / basic attack)
            if (isEnemy)
            {
                if (!activePlayer.HasActedThisTurn && activePlayer.CanUseAbility(0))
                {
                    ExecuteAbilityOrMoveIntoRange(0, tile);
                }
                else if (activePlayer.HasActedThisTurn)
                {
                    LogCombatMessage($"{activePlayer.UnitName} has already used their action this turn.");
                }
                return;
            }

            // 3. Empty tile: the hero only moves while move mode is on (Move button), never by a stray click
            if (!moveModeActive) return;

            if (!activePlayer.HasMovedThisTurn && tile.IsWalkable && !tile.IsOccupied && GridManager.Instance != null)
            {
                var reachable = GridManager.Instance.GetReachableTiles(activePlayer.GridPosition, activePlayer.MovementRange);
                if (reachable.Contains(tile))
                {
                    activePlayer.WalkToTile(tile);
                    activePlayer.HasMovedThisTurn = true;
                    moveModeActive = false;
                    RefreshAbilityBar();
                    UpdateMovementHighlights();
                    LogCombatMessage($"{activePlayer.UnitName} moved to tile ({tile.GridPosition.x}, {tile.GridPosition.y}).");
                }
                else
                {
                    LogCombatMessage("Destination is out of movement range.");
                }
            }
        }

        private void ExecuteAbilityOrMoveIntoRange(int slotIndex, GridTile targetTile)
        {
            if (activePlayer == null || targetTile == null) return;

            if (activePlayer.HasActedThisTurn)
            {
                LogCombatMessage($"{activePlayer.UnitName} has already used their action this turn.");
                return;
            }

            AbilitySO ability = activePlayer.GetAbility(slotIndex);
            if (ability == null) return;

            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            activePlayer.EnsureTilePosition();
            Vector2Int casterPos = activePlayer.GridPosition;
            Vector2Int targetPos = targetTile.GridPosition;

            // Self-targeted abilities execute immediately in place without moving
            if (ability.TargetType == AbilityTargetType.Self)
            {
                bool selfSuccess = activePlayer.UseAbility(slotIndex, casterPos, AbilityExecutor.Instance);
                if (selfSuccess)
                {
                    selectedAbilitySlot = -1;
                    RefreshAbilityBar();
                    UpdateMovementHighlights();
                }
                return;
            }

            // Check if there is an occupying unit on this tile (fallback across active units)
            CombatUnit targetOccupant = targetTile.OccupyingUnit;
            if (targetOccupant == null && TurnManager.Instance != null)
            {
                foreach (var unit in TurnManager.Instance.ActiveUnits)
                {
                    if (unit != null && unit.IsAlive && unit.GridPosition == targetPos)
                    {
                        targetOccupant = unit;
                        unit.EnsureTilePosition(); // unit re-registers its own tile
                        break;
                    }
                }
            }

            // A SingleTarget ability clicked on an empty tile does nothing: moving is the Move button's job
            bool isOccupiedUnit = targetOccupant != null;
            if (ability.TargetType == AbilityTargetType.SingleTarget && !isOccupiedUnit)
            {
                LogCombatMessage($"{ability.AbilityName} requires a target enemy unit.");
                return;
            }

            int currentDist = grid.GetDistance(casterPos, targetPos);

            // Case A: Target is ALREADY within ability range! Attack immediately without moving!
            if (currentDist <= ability.Range)
            {
                bool success = activePlayer.UseAbility(slotIndex, targetPos, AbilityExecutor.Instance);
                if (success)
                {
                    selectedAbilitySlot = -1;
                    RefreshAbilityBar();
                    UpdateMovementHighlights();
                }
                return;
            }

            // Case B: Target is OUTSIDE ability range. Automatically move into range if movement is available!
            if (!activePlayer.HasMovedThisTurn && activePlayer.MovementRange > 0)
            {
                GridTile bestTile = grid.FindBestReachableTileToTarget(casterPos, activePlayer.MovementRange, targetPos, ability.Range);
                if (bestTile != null && bestTile != activePlayer.CurrentTile)
                {
                    PlayerUnit walker = activePlayer;
                    walker.WalkToTile(bestTile);
                    walker.HasMovedThisTurn = true;
                    LogCombatMessage($"{walker.UnitName} moved to ({bestTile.GridPosition.x}, {bestTile.GridPosition.y}) to use {ability.AbilityName}.");
                    selectedAbilitySlot = -1;
                    RefreshAbilityBar();
                    UpdateMovementHighlights();

                    // Use the ability from the new position once the hero gets there
                    walker.WhenWalkFinished(() =>
                    {
                        if (walker == null || !walker.IsAlive) return;
                        if (TurnManager.Instance != null && TurnManager.Instance.CurrentState != TurnState.PlayerTurn) return;

                        walker.UseAbility(slotIndex, targetPos, AbilityExecutor.Instance);
                        RefreshAbilityBar();
                        UpdateMovementHighlights();
                    });
                    return;
                }
                else
                {
                    // Out of reach even after a full move: don't spend the move without attacking.
                    // Walking closer is done with the Move button.
                    LogCombatMessage($"Target is out of reach! (Distance: {currentDist}, Movement: {activePlayer.MovementRange}, Range: {ability.Range})");
                }
            }
            else
            {
                LogCombatMessage($"Target is out of range! (Distance: {currentDist}, Range: {ability.Range})");
            }
        }

        private void UpdateMovementHighlights()
        {
            if (activePlayer == null || GridManager.Instance == null) return;

            GridManager.Instance.ClearAllHighlights();

            // No ability selected and it's the player's turn:
            if (selectedAbilitySlot < 0 && TurnManager.Instance?.CurrentState == TurnState.PlayerTurn)
            {
                // Blue move tiles only while move mode is on
                if (moveModeActive && !activePlayer.HasMovedThisTurn)
                {
                    var reachable = GridManager.Instance.GetReachableTiles(activePlayer.GridPosition, activePlayer.MovementRange);
                    GridManager.Instance.HighlightTiles(reachable, TileHighlightType.Reachable);
                }

                // Highlight any enemies currently within direct primary attack range with EnemyTarget (Red)
                AbilitySO primaryAbility = activePlayer.GetAbility(0);
                int attackRange = primaryAbility != null ? primaryAbility.Range : 1;
                List<GridTile> inRangeTiles = GridManager.Instance.GetTilesInRadius(activePlayer.GridPosition, attackRange);
                foreach (var tile in inRangeTiles)
                {
                    if (IsEnemyOnTile(tile))
                    {
                        tile.ApplyHighlight(TileHighlightType.EnemyTarget);
                    }
                }
            }
        }

        private void OnEndTurnClicked()
        {
            selectedAbilitySlot = -1;
            moveModeActive = false;
            GridManager.Instance?.ClearAllHighlights();
            TurnManager.Instance?.EndPlayerTurn();
        }

        #endregion

        #region Turn & State Listeners

        private void HandlePlayModeChanged(GamePlayMode mode)
        {
            if (mode == GamePlayMode.Combat)
            {
                bool isPlayerTurn = TurnManager.Instance != null && TurnManager.Instance.IsCombatActive && TurnManager.Instance.CurrentState == TurnState.PlayerTurn;
                SetCombatBarVisible(isPlayerTurn);
            }
            else
            {
                SetCombatBarVisible(false);
            }
        }

        private void HandleTurnStateChanged(TurnState newState)
        {
            bool showBar = (newState == TurnState.PlayerTurn) && (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive);
            SetCombatBarVisible(showBar);

            if (turnBannerText != null)
            {
                turnBannerText.text = newState switch
                {
                    TurnState.PlayerTurn => "PLAYER TURN",
                    TurnState.EnemyTurn => "ENEMY TURN",
                    TurnState.ResolveAbilities => "RESOLVING ACTIONS...",
                    TurnState.Victory => "VICTORY!",
                    TurnState.Defeat => "DEFEAT",
                    _ => ""
                };
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = (newState == TurnState.PlayerTurn);
            }

            RefreshAbilityBar();
            if (newState == TurnState.PlayerTurn)
            {
                LocatePlayer();
                UpdateMovementHighlights();
            }
        }

        private void HandleUnitTurnStarted(CombatUnit unit)
        {
            if (roundCounterText != null && TurnManager.Instance != null)
            {
                roundCounterText.text = $"Round {TurnManager.Instance.TurnCounter}";
            }

            if (unit != null)
            {
                LogCombatMessage($"--- Turn: {unit.UnitName} ---");
            }

            bool isPlayer = unit is PlayerUnit;
            moveModeActive = false; // every turn starts with move mode off
            SetCombatBarVisible(isPlayer);
            RefreshAbilityBar();
            if (isPlayer)
            {
                LocatePlayer();
                UpdateMovementHighlights();
            }
        }

        private void HandleUnitDamaged(CombatUnit unit, int damage, bool isCritical)
        {
            string critLabel = isCritical ? " [CRITICAL HIT!]" : "";
            LogCombatMessage($"{unit.UnitName} took {damage} damage{critLabel}. (HP: {unit.CurrentHP}/{unit.MaxHP})");
        }

        private void HandleCombatEnded(bool isVictory)
        {
            SetCombatBarVisible(false);
            string outcome = isVictory ? "VICTORY! All foes vanquished." : "DEFEAT! Party defeated.";
            LogCombatMessage(outcome);
            RefreshAbilityBar();
        }

        private void HandleCombatVictoryScrapAwarded(int scrapAmount)
        {
            LogCombatMessage($"<color=#E0A938>[LOOT] Gained +{scrapAmount} Scrap Metal from defeated enemies!</color>");
        }

        /// <summary>
        /// Controls visibility of the combat action bar and associated turn controls.
        /// Utilizes CanvasGroup to fade in/out cleanly without deactivating the host script GameObject.
        /// </summary>
        public void SetCombatBarVisible(bool visible)
        {
            if (visible)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                if (combatActionBar != null && !combatActionBar.activeSelf)
                {
                    combatActionBar.SetActive(true);
                }

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }

                transform.SetAsLastSibling();
                if (combatActionBar != null && combatActionBar != gameObject)
                {
                    combatActionBar.transform.SetAsLastSibling();
                }
            }
            else
            {
                if (canvasGroup == null)
                {
                    canvasGroup = GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasGroup = gameObject.AddComponent<CanvasGroup>();
                    }
                }

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }

                if (combatActionBar != null && combatActionBar != gameObject)
                {
                    combatActionBar.SetActive(false);
                }

                selectedAbilitySlot = -1;
                moveModeActive = false;
                GridManager.Instance?.ClearAllHighlights();
            }
        }

        #endregion

        #region Combat Activity Logging

        /// <summary>
        /// Appends a new line of text to the combat log display.
        /// </summary>
        public void LogCombatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            logHistory.Add(message);
            if (logHistory.Count > maxLogLines)
            {
                logHistory.RemoveAt(0);
            }

            if (combatLogText != null)
            {
                combatLogText.text = string.Join("\n", logHistory);
            }
        }

        /// <summary>
        /// Clears all entries from the combat log.
        /// </summary>
        public void ClearLog()
        {
            logHistory.Clear();
            if (combatLogText != null)
            {
                combatLogText.text = "";
            }
        }

        #endregion
    }
}
