using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Master controller for the Expedition Map UI Modal (MasterSpec §3, §6).
    /// Shows the seven world zones as connected cards in the dark/gold HUD style, highlights the zone the
    /// player is in (pulsing gold frame, "YOU ARE HERE" tag and a header line), marks cleared bosses and
    /// locked areas, draws the Rogue secret route, suspends exploration input while open, and binds to the
    /// [M] hotkey and HUD button.
    /// </summary>
    public class DungeonMapUIController : MonoBehaviour
    {
        #region Singleton

        public static DungeonMapUIController Instance { get; private set; }

        #endregion

        #region Layout Constants

        public const string ModalName = "Dungeon_Map_Modal";

        public const string KeyVillage = "Village";
        public const string KeyForest = "Forest";
        public const string KeyCourtyard = "Courtyard";
        public const string KeyLibrary = "Library";
        public const string KeyHall = "Hall";
        public const string KeyTower = "Tower";
        public const string KeyCrownHall = "CrownHall";

        private static readonly Vector2 FrameSize = new Vector2(1560f, 920f);
        private static readonly Vector2 CardSize = new Vector2(380f, 128f);
        private static readonly Vector2 PinOffset = new Vector2(0f, 21f);

        private static readonly Color GoldText = new Color(0.98f, 0.85f, 0.45f, 1f);
        private static readonly Color HereGold = new Color(1f, 0.80f, 0.25f, 1f);
        private static readonly Color CardFillNormal = new Color(0.07f, 0.08f, 0.12f, 0.95f);
        private static readonly Color CardFillCurrent = new Color(0.24f, 0.17f, 0.05f, 0.97f);
        private static readonly Color CardFillLocked = new Color(0.05f, 0.05f, 0.07f, 0.95f);
        private static readonly Color PathOpen = new Color(0.78f, 0.60f, 0.28f, 0.95f);
        private static readonly Color PathLocked = new Color(0.32f, 0.32f, 0.36f, 0.8f);
        private static readonly Color PathSecret = new Color(0.22f, 0.74f, 0.97f, 0.95f);

        #endregion

        #region Serialized Fields

        [Header("UI Modal Root")]
        [Tooltip("Root GameObject for the map modal dialog. Rebuilt procedurally at runtime.")]
        [SerializeField] private GameObject mapModalPanel;

        [Header("Theme Sprites")]
        [SerializeField] private Sprite panelDarkSprite;
        [SerializeField] private Sprite dividerGoldSprite;
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite buttonNormalSprite;
        [SerializeField] private Sprite pillBadgeSprite;

        [Header("Header Elements")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Button closeButton;

        #endregion

        #region Zone Node Representation

        private class ZoneNodeUI
        {
            public string ZoneKey;
            public string DisplayName;
            public RectTransform ContainerRect;
            public Image BackgroundImage;
            public Image FillImage;
            public Image GlowImage;
            public TMP_Text TitleLabel;
            public TMP_Text StatusBadge;
            public TMP_Text DescLabel;
            public GameObject LocationPin;
            public TMP_Text LocationPinLabel;
            public bool IsCurrent;
            public bool IsUnlocked;
        }

        private class PathUI
        {
            public string FromKey;
            public string ToKey;
            public bool IsSecret;
            public readonly List<Image> Segments = new List<Image>();
        }

        private readonly List<ZoneNodeUI> zoneNodes = new List<ZoneNodeUI>();
        private readonly List<PathUI> paths = new List<PathUI>();
        private ZoneNodeUI currentNode;

        #endregion

        #region Public Properties

        /// <summary>Whether the expedition map modal is currently open on screen.</summary>
        public bool IsMapOpen => mapModalPanel != null && mapModalPanel.activeSelf;

        /// <summary>Number of zone cards on the map (one per world zone).</summary>
        public int ZoneNodeCount => zoneNodes.Count;

        /// <summary>Node key of the zone marked "YOU ARE HERE" after the last refresh (null if none).</summary>
        public string CurrentNodeKey => currentNode != null ? currentNode.ZoneKey : null;

        /// <summary>Header text telling the player which zone they are in.</summary>
        public string CurrentLocationHeader => subtitleText != null ? subtitleText.text : string.Empty;

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

            LoadThemeSpritesIfMissing();
            EnsureUIHierarchy();

            if (mapModalPanel != null)
            {
                mapModalPanel.SetActive(false);
            }
        }

        private void Start()
        {
            if (mapModalPanel != null)
            {
                mapModalPanel.SetActive(false);
            }

            WireListeners();
            SubscribeToGameEvents();
        }

        private void Update()
        {
            // Toggle map via [M] hotkey
            if (GameInput.IsMapHotkeyPressed())
            {
                ToggleMap();
            }
            // Close via [ESC] if open
            else if (IsMapOpen && GameInput.GetKeyDown(KeyCode.Escape))
            {
                CloseMap();
            }

            if (IsMapOpen)
            {
                AnimateCurrentZoneMarker();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromGameEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToGameEvents()
        {
            GameManager.OnLocationChanged += HandleLocationChanged;
            GameManager.OnWingCleared += HandleWingCleared;
            GameManager.OnBossDefeated += HandleBossDefeated;
        }

        private void UnsubscribeFromGameEvents()
        {
            GameManager.OnLocationChanged -= HandleLocationChanged;
            GameManager.OnWingCleared -= HandleWingCleared;
            GameManager.OnBossDefeated -= HandleBossDefeated;
        }

        private void HandleLocationChanged(GameLocation loc)
        {
            if (IsMapOpen)
            {
                RefreshMapNodes();
            }
        }

        private void HandleWingCleared(GameLocation loc)
        {
            if (IsMapOpen)
            {
                RefreshMapNodes();
            }
        }

        private void HandleBossDefeated(string bossID)
        {
            if (IsMapOpen)
            {
                RefreshMapNodes();
            }
        }

        #endregion

        #region Public Modal API

        /// <summary>
        /// Toggles the map modal on or off.
        /// </summary>
        public void ToggleMap()
        {
            if (IsMapOpen)
            {
                CloseMap();
            }
            else
            {
                ShowMap();
            }
        }

        /// <summary>
        /// Displays the Expedition Map modal, refreshes room statuses, and suspends exploration input.
        /// </summary>
        public void ShowMap()
        {
            EnsureUIHierarchy();

            if (mapModalPanel != null)
            {
                mapModalPanel.transform.SetAsLastSibling();
                mapModalPanel.SetActive(true);
            }

            // Suspend world exploration movement
            GameInput.SetExplorationInputEnabled(false);

            RefreshMapNodes();

            // Play open audio
            PlaySound(SFXClipType.ButtonClick);

            Debug.Log("[DungeonMapUIController] Expedition Map opened.");
        }

        /// <summary>
        /// Closes the Expedition Map modal and restores world exploration input.
        /// </summary>
        public void CloseMap()
        {
            if (mapModalPanel != null)
            {
                mapModalPanel.SetActive(false);
            }

            // Restore world exploration movement
            GameInput.SetExplorationInputEnabled(true);

            // Play close audio
            PlaySound(SFXClipType.ButtonClick);

            Debug.Log("[DungeonMapUIController] Expedition Map closed.");
        }

        #endregion

        #region Zone Mapping

        /// <summary>
        /// Map card that represents a game location. The wine cellar sits under the village card and the
        /// Throne Room is the Crown Hall card.
        /// </summary>
        public static string GetNodeKeyForLocation(GameLocation location)
        {
            switch (location)
            {
                case GameLocation.Village:
                case GameLocation.Cellar:
                    return KeyVillage;
                case GameLocation.Forest:
                    return KeyForest;
                case GameLocation.Courtyard:
                    return KeyCourtyard;
                case GameLocation.Library:
                    return KeyLibrary;
                case GameLocation.CastleHall:
                    return KeyHall;
                case GameLocation.Tower:
                    return KeyTower;
                case GameLocation.CrownHall:
                    return KeyCrownHall;
                default:
                    return KeyVillage;
            }
        }

        /// <summary>
        /// Zone name shown in the map header. Matches the names on the top-center zone banner.
        /// </summary>
        public static string GetZoneDisplayName(GameLocation location)
        {
            switch (location)
            {
                case GameLocation.Village: return "Oakhaven Village";
                case GameLocation.Cellar: return "Village Wine Cellar";
                case GameLocation.Forest: return "Whispering Woods";
                case GameLocation.Courtyard: return "Castle Courtyard";
                case GameLocation.Library: return "Grand Archives";
                case GameLocation.CastleHall: return "The Great Hall";
                case GameLocation.Tower: return "Treasure Tower";
                case GameLocation.CrownHall: return "The Throne Room";
                default: return "Castle of Dice";
            }
        }

        #endregion

        #region Dynamic Refresh & Node State

        /// <summary>
        /// Refreshes all zone badges, the current location highlight, locked areas and path colours.
        /// </summary>
        public void RefreshMapNodes()
        {
            GameManager gm = GameManager.Instance;
            GameLocation curLoc = gm != null ? gm.CurrentLocation : GameLocation.Village;
            string curKey = GetNodeKeyForLocation(curLoc);

            bool isCourtyardCleared = gm != null && (gm.IsCommanderDefeated || gm.IsWingCleared(GameLocation.Courtyard));
            bool isLibraryCleared = gm != null && (gm.IsMalakorDefeated || gm.IsWingCleared(GameLocation.Library));
            bool isCrownHallCleared = gm != null && (gm.IsGargoyleKingDefeated || gm.IsWingCleared(GameLocation.CrownHall));

            // The Great Hall, Tower and Throne Room are only reached through the hall.
            bool isInnerCastleOpen = isCourtyardCleared || isLibraryCleared
                || curKey == KeyHall || curKey == KeyTower || curKey == KeyCrownHall;

            if (subtitleText != null)
            {
                subtitleText.text = $"You are in:  <color=#ffd34d><b>{GetZoneDisplayName(curLoc)}</b></color>";
            }

            currentNode = null;

            for (int i = 0; i < zoneNodes.Count; i++)
            {
                ZoneNodeUI node = zoneNodes[i];
                if (node == null || node.ContainerRect == null) continue;

                bool isCurrent = node.ZoneKey == curKey;
                bool isUnlocked = true;
                string statusText;
                string statusColor;

                switch (node.ZoneKey)
                {
                    case KeyVillage:
                        statusText = "SAFE HAVEN";
                        statusColor = "#7cb8ff";
                        break;

                    case KeyForest:
                        statusText = "WILDERNESS";
                        statusColor = "#5fd49a";
                        break;

                    case KeyCourtyard:
                        statusText = isCourtyardCleared ? "CLEARED" : "BOSS: CURSED COMMANDER";
                        statusColor = isCourtyardCleared ? "#6ee78f" : "#ff7a7a";
                        break;

                    case KeyLibrary:
                        statusText = isLibraryCleared ? "CLEARED" : "BOSS: SHADOW MAGE MALAKOR";
                        statusColor = isLibraryCleared ? "#6ee78f" : "#ff7a7a";
                        break;

                    case KeyHall:
                        isUnlocked = isInnerCastleOpen;
                        statusText = isUnlocked ? "SAFE HAVEN - SAVE ALTAR" : "LOCKED - DEFEAT A WING BOSS";
                        statusColor = isUnlocked ? "#7cb8ff" : "#8b8f99";
                        break;

                    case KeyTower:
                        isUnlocked = isInnerCastleOpen;
                        statusText = isUnlocked ? "TREASURE" : "LOCKED";
                        statusColor = isUnlocked ? "#ffc44d" : "#8b8f99";
                        break;

                    case KeyCrownHall:
                        isUnlocked = isInnerCastleOpen;
                        statusText = isCrownHallCleared ? "VANQUISHED" : (isUnlocked ? "FINAL BOSS: GARGOYLE KING" : "LOCKED - FINAL BOSS");
                        statusColor = isCrownHallCleared ? "#6ee78f" : (isUnlocked ? "#ff5c5c" : "#8b8f99");
                        break;

                    default:
                        statusText = "UNEXPLORED";
                        statusColor = "#9ca3af";
                        break;
                }

                node.IsCurrent = isCurrent;
                node.IsUnlocked = isUnlocked;

                if (node.StatusBadge != null)
                {
                    node.StatusBadge.text = $"<color={statusColor}>{statusText}</color>";
                }

                if (node.LocationPin != null)
                {
                    node.LocationPin.SetActive(isCurrent);
                }

                if (isCurrent && node.LocationPinLabel != null)
                {
                    node.LocationPinLabel.text = curLoc == GameLocation.Cellar ? "YOU ARE HERE: CELLAR" : "YOU ARE HERE";
                }

                if (node.GlowImage != null)
                {
                    node.GlowImage.gameObject.SetActive(isCurrent);
                }

                if (node.FillImage != null)
                {
                    node.FillImage.color = isCurrent ? CardFillCurrent : (isUnlocked ? CardFillNormal : CardFillLocked);
                }

                if (node.BackgroundImage != null)
                {
                    node.BackgroundImage.color = isCurrent ? HereGold : (isUnlocked ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.9f));
                }

                if (node.TitleLabel != null)
                {
                    node.TitleLabel.color = isCurrent ? new Color(1f, 0.93f, 0.62f) : (isUnlocked ? GoldText : new Color(0.55f, 0.55f, 0.6f));
                }

                if (node.DescLabel != null)
                {
                    node.DescLabel.color = isUnlocked ? new Color(0.86f, 0.87f, 0.9f) : new Color(0.5f, 0.5f, 0.55f);
                }

                if (isCurrent)
                {
                    currentNode = node;
                }
                else if (node.LocationPin != null)
                {
                    ((RectTransform)node.LocationPin.transform).anchoredPosition = PinOffset;
                }
            }

            for (int i = 0; i < paths.Count; i++)
            {
                PathUI path = paths[i];
                bool open = IsNodeUnlocked(path.FromKey) && IsNodeUnlocked(path.ToKey);
                Color c = path.IsSecret ? PathSecret : (open ? PathOpen : PathLocked);
                for (int s = 0; s < path.Segments.Count; s++)
                {
                    if (path.Segments[s] != null) path.Segments[s].color = c;
                }
            }
        }

        private bool IsNodeUnlocked(string key)
        {
            for (int i = 0; i < zoneNodes.Count; i++)
            {
                if (zoneNodes[i].ZoneKey == key) return zoneNodes[i].IsUnlocked;
            }
            return true;
        }

        /// <summary>Pulses the gold frame and bobs the "YOU ARE HERE" tag on the current zone card.</summary>
        private void AnimateCurrentZoneMarker()
        {
            if (currentNode == null) return;

            float t = Time.unscaledTime;

            if (currentNode.GlowImage != null)
            {
                Color g = HereGold;
                g.a = 0.55f + 0.45f * Mathf.Sin(t * 3.5f);
                currentNode.GlowImage.color = g;
            }

            if (currentNode.LocationPin != null)
            {
                RectTransform pinRt = (RectTransform)currentNode.LocationPin.transform;
                pinRt.anchoredPosition = PinOffset + new Vector2(0f, 4f * Mathf.Sin(t * 4f));
            }
        }

        #endregion

        #region Setup & Procedural Hierarchy Builder

        private void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            if (pillBadgeSprite == null)
                pillBadgeSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
        }

        private void WireListeners()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseMap);
                closeButton.onClick.AddListener(CloseMap);
            }
        }

        /// <summary>
        /// Builds the whole map modal once per controller instance. Any modal left in the scene from an
        /// earlier layout (older scenes carry a baked copy with a white frame and duplicated cards) is
        /// thrown away and replaced, so the map always matches this code.
        /// </summary>
        public void EnsureUIHierarchy()
        {
            if (mapModalPanel != null && zoneNodes.Count > 0)
            {
                return;
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindAnyObjectByType<Canvas>();
            }

            if (parentCanvas == null)
            {
                Debug.LogWarning("[DungeonMapUIController] No Canvas found in scene to mount Expedition Map modal.");
                return;
            }

            LoadThemeSpritesIfMissing();
            RemoveStaleModals(parentCanvas.transform);

            zoneNodes.Clear();
            paths.Clear();
            currentNode = null;

            // 1. Root Modal Panel (fullscreen dark dimmer that also blocks clicks to the world)
            mapModalPanel = new GameObject(ModalName, typeof(RectTransform), typeof(Image));
            mapModalPanel.transform.SetParent(parentCanvas.transform, false);
            RectTransform rootRt = mapModalPanel.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            mapModalPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.82f);

            // 2. Dark/gold frame matching the HUD cards
            GameObject frameObj = new GameObject("Map_Frame_Panel", typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(mapModalPanel.transform, false);
            RectTransform frRt = frameObj.GetComponent<RectTransform>();
            frRt.anchorMin = new Vector2(0.5f, 0.5f);
            frRt.anchorMax = new Vector2(0.5f, 0.5f);
            frRt.sizeDelta = FrameSize;
            frRt.anchoredPosition = Vector2.zero;
            ApplyPanelLook(frameObj.GetComponent<Image>(), panelDarkSprite, new Color(0.06f, 0.07f, 0.1f, 0.98f), 3f);

            BuildHeader(frameObj.transform);

            // 3. Map area: paths first so they sit behind the cards
            GameObject nodesObj = new GameObject("Zone_Nodes_Container", typeof(RectTransform));
            nodesObj.transform.SetParent(frameObj.transform, false);
            RectTransform nodesRt = nodesObj.GetComponent<RectTransform>();
            nodesRt.anchorMin = new Vector2(0.5f, 0.5f);
            nodesRt.anchorMax = new Vector2(0.5f, 0.5f);
            nodesRt.sizeDelta = Vector2.zero;
            nodesRt.anchoredPosition = Vector2.zero;

            GameObject pathsObj = new GameObject("Zone_Paths", typeof(RectTransform));
            pathsObj.transform.SetParent(nodesObj.transform, false);
            RectTransform pathsRt = pathsObj.GetComponent<RectTransform>();
            pathsRt.sizeDelta = Vector2.zero;

            BuildZoneMap(nodesObj.transform, pathsObj.transform);

            // 4. Legend along the bottom edge
            BuildLegend(frameObj.transform);

            mapModalPanel.SetActive(false);
            WireListeners();
        }

        private void RemoveStaleModals(Transform canvasTr)
        {
            var stale = new List<GameObject>();
            for (int i = 0; i < canvasTr.childCount; i++)
            {
                Transform child = canvasTr.GetChild(i);
                if (child.name == ModalName)
                {
                    stale.Add(child.gameObject);
                }
            }

            if (mapModalPanel != null && !stale.Contains(mapModalPanel))
            {
                stale.Add(mapModalPanel);
            }

            for (int i = 0; i < stale.Count; i++)
            {
                GameObject go = stale[i];
                if (go == null) continue;

                go.SetActive(false);
                go.name = ModalName + "_Stale";
                if (Application.isPlaying)
                {
                    Destroy(go);
                }
                else
                {
                    DestroyImmediate(go);
                }
            }

            mapModalPanel = null;
        }

        private void BuildHeader(Transform frame)
        {
            titleText = CreateText(frame, "Header_Title", "EXPEDITION MAP", 46f, GoldText, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform tRt = titleText.rectTransform;
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.sizeDelta = new Vector2(-440f, 60f);
            tRt.anchoredPosition = new Vector2(0f, -50f);
            titleText.characterSpacing = 6f;

            subtitleText = CreateText(frame, "Header_Subtitle", "You are in:", 30f, new Color(0.93f, 0.90f, 0.85f), FontStyles.Normal, TextAlignmentOptions.Center);
            RectTransform sRt = subtitleText.rectTransform;
            sRt.anchorMin = new Vector2(0f, 1f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.sizeDelta = new Vector2(-440f, 40f);
            sRt.anchoredPosition = new Vector2(0f, -100f);

            GameObject divObj = new GameObject("Divider_Gold", typeof(RectTransform), typeof(Image));
            divObj.transform.SetParent(frame, false);
            RectTransform dRt = divObj.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.08f, 1f);
            dRt.anchorMax = new Vector2(0.92f, 1f);
            dRt.sizeDelta = new Vector2(0f, 4f);
            dRt.anchoredPosition = new Vector2(0f, -134f);
            Image dImg = divObj.GetComponent<Image>();
            dImg.sprite = dividerGoldSprite;
            dImg.type = dividerGoldSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            dImg.color = new Color(1f, 0.82f, 0.35f, 0.9f);
            dImg.raycastTarget = false;

            GameObject btnObj = new GameObject("Button_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(frame, false);
            RectTransform bRt = btnObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 1f);
            bRt.anchorMax = new Vector2(1f, 1f);
            bRt.sizeDelta = new Vector2(200f, 54f);
            bRt.anchoredPosition = new Vector2(-130f, -54f);
            ApplyPanelLook(btnObj.GetComponent<Image>(), buttonNormalSprite ?? slotFrameSprite, new Color(0.25f, 0.18f, 0.08f, 1f), 2f);
            closeButton = btnObj.GetComponent<Button>();

            TMP_Text lbl = CreateText(btnObj.transform, "Label", "CLOSE  [M]", 22f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(lbl.rectTransform);
        }

        private void BuildZoneMap(Transform nodesRoot, Transform pathsRoot)
        {
            // Card positions follow the real door links between the zone scenes:
            // Village - Forest; Forest - Courtyard; Forest - Library (secret Rogue gate);
            // Courtyard/Library - Great Hall; Great Hall - Tower; Great Hall - Throne Room.
            Vector2 throne = new Vector2(0f, 215f);
            Vector2 hall = new Vector2(0f, 45f);
            Vector2 tower = new Vector2(480f, 45f);
            Vector2 library = new Vector2(-400f, -125f);
            Vector2 courtyard = new Vector2(400f, -125f);
            Vector2 forest = new Vector2(0f, -295f);
            Vector2 village = new Vector2(-480f, -295f);

            CreatePath(pathsRoot, KeyVillage, village, KeyForest, forest, false);
            CreatePath(pathsRoot, KeyForest, forest, KeyCourtyard, courtyard, false);
            CreatePath(pathsRoot, KeyForest, forest, KeyLibrary, library, true);
            CreatePath(pathsRoot, KeyCourtyard, courtyard, KeyHall, hall, false);
            CreatePath(pathsRoot, KeyLibrary, library, KeyHall, hall, false);
            CreatePath(pathsRoot, KeyHall, hall, KeyTower, tower, false);
            CreatePath(pathsRoot, KeyHall, hall, KeyCrownHall, throne, false);

            // Secret route tag in the gap between the Forest and Library cards
            GameObject secretObj = new GameObject("Secret_Route_Indicator", typeof(RectTransform), typeof(Image));
            secretObj.transform.SetParent(pathsRoot, false);
            RectTransform secRt = secretObj.GetComponent<RectTransform>();
            secRt.sizeDelta = new Vector2(170f, 32f);
            secRt.anchoredPosition = new Vector2(-255f, -210f);
            Image secImg = secretObj.GetComponent<Image>();
            ApplyPanelLook(secImg, pillBadgeSprite, new Color(0.04f, 0.12f, 0.2f, 0.95f), 0f);
            secImg.color = new Color(0.1f, 0.35f, 0.55f, 1f);
            secImg.raycastTarget = false;
            TMP_Text secText = CreateText(secretObj.transform, "Label", "SECRET ROUTE", 16f, new Color(0.75f, 0.93f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(secText.rectTransform);

            CreateNode(nodesRoot, KeyVillage, "OAKHAVEN VILLAGE",
                "Baldur's forge, the tavern and the wine cellar.", village);
            CreateNode(nodesRoot, KeyForest, "WHISPERING WOODS",
                "Forest path to the castle. Hidden gate for Rogues.", forest);
            CreateNode(nodesRoot, KeyCourtyard, "CASTLE COURTYARD",
                "Wing 1. The Cursed Commander guards the gate.", courtyard);
            CreateNode(nodesRoot, KeyLibrary, "GRAND ARCHIVES",
                "Wing 2. Library of the Shadow Mage Malakor.", library);
            CreateNode(nodesRoot, KeyHall, "THE GREAT HALL",
                "Central hub with the Runestone save altar.", hall);
            CreateNode(nodesRoot, KeyTower, "TREASURE TOWER",
                "Othelia's ring and the +30 Max HP elixir.", tower);
            CreateNode(nodesRoot, KeyCrownHall, "THE THRONE ROOM",
                "Wing 3. The Gargoyle King awaits.", throne);
        }

        private void CreatePath(Transform parent, string fromKey, Vector2 from, string toKey, Vector2 to, bool dashed)
        {
            var path = new PathUI { FromKey = fromKey, ToKey = toKey, IsSecret = dashed };

            Vector2 delta = to - from;
            float length = delta.magnitude;
            Vector2 dir = delta / length;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float thickness = dashed ? 6f : 8f;

            if (!dashed)
            {
                path.Segments.Add(CreateLineSegment(parent, $"Path_{fromKey}_{toKey}", (from + to) * 0.5f, length, thickness, angle));
            }
            else
            {
                const float dash = 22f;
                const float gap = 14f;
                int index = 0;
                for (float d = 0f; d < length; d += dash + gap)
                {
                    float segLen = Mathf.Min(dash, length - d);
                    Vector2 center = from + dir * (d + segLen * 0.5f);
                    path.Segments.Add(CreateLineSegment(parent, $"Path_{fromKey}_{toKey}_{index++}", center, segLen, thickness, angle));
                }
            }

            paths.Add(path);
        }

        private Image CreateLineSegment(Transform parent, string name, Vector2 center, float length, float thickness, float angle)
        {
            GameObject seg = new GameObject(name, typeof(RectTransform), typeof(Image));
            seg.transform.SetParent(parent, false);
            RectTransform rt = seg.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(length, thickness);
            rt.anchoredPosition = center;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image img = seg.GetComponent<Image>();
            img.color = PathOpen;
            img.raycastTarget = false;
            return img;
        }

        private void CreateNode(Transform parent, string key, string name, string desc, Vector2 pos)
        {
            // Pulsing gold glow behind the current zone's card
            GameObject glowObj = new GameObject($"Glow_{key}", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(parent, false);
            RectTransform gRt = glowObj.GetComponent<RectTransform>();
            gRt.anchoredPosition = pos;
            gRt.sizeDelta = CardSize + new Vector2(26f, 26f);
            Image glow = glowObj.GetComponent<Image>();
            glow.color = HereGold;
            glow.raycastTarget = false;
            glowObj.SetActive(false);

            GameObject nodeObj = new GameObject($"Node_{key}", typeof(RectTransform), typeof(Image));
            nodeObj.transform.SetParent(parent, false);
            RectTransform nRt = nodeObj.GetComponent<RectTransform>();
            nRt.anchoredPosition = pos;
            nRt.sizeDelta = CardSize;
            Image bg = nodeObj.GetComponent<Image>();
            ApplyPanelLook(bg, slotFrameSprite ?? panelDarkSprite, new Color(0.55f, 0.43f, 0.2f, 1f), 0f);
            bg.raycastTarget = false;

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(nodeObj.transform, false);
            RectTransform fRt = fillObj.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = new Vector2(5f, 5f);
            fRt.offsetMax = new Vector2(-5f, -5f);
            Image fill = fillObj.GetComponent<Image>();
            fill.color = CardFillNormal;
            fill.raycastTarget = false;

            TMP_Text title = CreateText(nodeObj.transform, "Title", name, 25f, GoldText, FontStyles.Bold, TextAlignmentOptions.Left);
            SetBand(title.rectTransform, 0.64f, 0.95f);
            title.characterSpacing = 2f;

            TMP_Text status = CreateText(nodeObj.transform, "StatusBadge", "UNEXPLORED", 19f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            SetBand(status.rectTransform, 0.38f, 0.64f);

            TMP_Text dText = CreateText(nodeObj.transform, "Description", desc, 17f, new Color(0.86f, 0.87f, 0.9f), FontStyles.Normal, TextAlignmentOptions.Left);
            SetBand(dText.rectTransform, 0.06f, 0.38f);
            dText.enableAutoSizing = true;
            dText.fontSizeMin = 13f;
            dText.fontSizeMax = 17f;

            // "YOU ARE HERE" tag just above the card
            GameObject pinObj = new GameObject("Pin", typeof(RectTransform), typeof(Image));
            pinObj.transform.SetParent(nodeObj.transform, false);
            RectTransform pRt = pinObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 1f);
            pRt.anchorMax = new Vector2(0.5f, 1f);
            pRt.sizeDelta = new Vector2(250f, 36f);
            pRt.anchoredPosition = PinOffset;
            Image pinImg = pinObj.GetComponent<Image>();
            ApplyPanelLook(pinImg, pillBadgeSprite, HereGold, 0f);
            pinImg.color = HereGold;
            pinImg.raycastTarget = false;

            TMP_Text pinText = CreateText(pinObj.transform, "Label", "YOU ARE HERE", 20f, new Color(0.14f, 0.08f, 0.01f), FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(pinText.rectTransform);
            pinObj.SetActive(false);

            zoneNodes.Add(new ZoneNodeUI
            {
                ZoneKey = key,
                DisplayName = name,
                ContainerRect = nRt,
                BackgroundImage = bg,
                FillImage = fill,
                GlowImage = glow,
                TitleLabel = title,
                StatusBadge = status,
                DescLabel = dText,
                LocationPin = pinObj,
                LocationPinLabel = pinText,
                IsUnlocked = true
            });
        }

        private void BuildLegend(Transform frame)
        {
            GameObject legObj = new GameObject("Legend_Bar", typeof(RectTransform));
            legObj.transform.SetParent(frame, false);
            RectTransform lRt = legObj.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0.5f, 0f);
            lRt.anchorMax = new Vector2(0.5f, 0f);
            lRt.sizeDelta = new Vector2(1400f, 40f);
            lRt.anchoredPosition = new Vector2(0f, 46f);

            CreateLegendItem(legObj.transform, -525f, HereGold, "You are here");
            CreateLegendItem(legObj.transform, -175f, new Color(0.43f, 0.9f, 0.56f), "Cleared");
            CreateLegendItem(legObj.transform, 175f, new Color(1f, 0.45f, 0.45f), "Boss fight");
            CreateLegendItem(legObj.transform, 525f, PathSecret, "Secret route (Rogue)");
        }

        private void CreateLegendItem(Transform parent, float x, Color color, string label)
        {
            GameObject swatch = new GameObject($"Legend_Swatch_{label}", typeof(RectTransform), typeof(Image));
            swatch.transform.SetParent(parent, false);
            RectTransform sRt = swatch.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(24f, 24f);
            sRt.anchoredPosition = new Vector2(x - 120f, 0f);
            Image sImg = swatch.GetComponent<Image>();
            sImg.color = color;
            sImg.raycastTarget = false;

            TMP_Text text = CreateText(parent, $"Legend_{label}", label, 21f, new Color(0.9f, 0.88f, 0.83f), FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform tRt = text.rectTransform;
            tRt.sizeDelta = new Vector2(230f, 36f);
            tRt.anchoredPosition = new Vector2(x + 13f, 0f);
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetBand(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(18f, 0f);
            rt.offsetMax = new Vector2(-18f, 0f);
        }

        /// <summary>
        /// Uses the themed sliced sprite when available; otherwise a solid dark fill with a gold outline so
        /// the panel never renders as a plain white box.
        /// </summary>
        private static void ApplyPanelLook(Image img, Sprite sprite, Color fallbackColor, float fallbackOutline)
        {
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallbackColor;
                if (fallbackOutline > 0f)
                {
                    Outline outline = img.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.85f, 0.66f, 0.3f, 1f);
                    outline.effectDistance = new Vector2(fallbackOutline, -fallbackOutline);
                }
            }
        }

        private void PlaySound(SFXClipType clipType)
        {
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(clipType);
            }
        }

        #endregion
    }
}
