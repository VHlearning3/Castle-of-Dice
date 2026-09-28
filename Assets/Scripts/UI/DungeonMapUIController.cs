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
    /// Master controller for the Expedition Dungeon Map UI Modal (MasterSpec §3, §6).
    /// Displays the non-linear layout of the castle wings, tracks current player location,
    /// marks cleared wings/bosses, highlights the Rogue secret nature route,
    /// suspends exploration inputs while active, and binds to [M] hotkey and HUD button.
    /// </summary>
    public class DungeonMapUIController : MonoBehaviour
    {
        #region Singleton

        public static DungeonMapUIController Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("UI Modal Root")]
        [Tooltip("Root GameObject for the map modal dialog.")]
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
            public string Description;
            public Vector2 Position;
            public RectTransform ContainerRect;
            public Image BackgroundImage;
            public TMP_Text TitleLabel;
            public TMP_Text StatusBadge;
            public TMP_Text DescLabel;
            public GameObject LocationPin;
        }

        private readonly List<ZoneNodeUI> zoneNodes = new List<ZoneNodeUI>();

        #endregion

        #region Public Properties

        /// <summary>Whether the expedition map modal is currently open on screen.</summary>
        public bool IsMapOpen => mapModalPanel != null && mapModalPanel.activeSelf;

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

        #region Dynamic Refresh & Node State

        /// <summary>
        /// Refreshes all zone badges, current location indicators, and boss clearance markers.
        /// </summary>
        public void RefreshMapNodes()
        {
            GameManager gm = GameManager.Instance;
            GameLocation curLoc = gm != null ? gm.CurrentLocation : GameLocation.Village;

            bool isCourtyardCleared = gm != null && (gm.IsCommanderDefeated || gm.IsWingCleared(GameLocation.Courtyard));
            bool isLibraryCleared = gm != null && (gm.IsMalakorDefeated || gm.IsWingCleared(GameLocation.Library));
            bool isCrownHallCleared = gm != null && (gm.IsGargoyleKingDefeated || gm.IsWingCleared(GameLocation.CrownHall));

            foreach (var node in zoneNodes)
            {
                if (node == null || node.ContainerRect == null) continue;

                bool isCurrent = false;
                bool isCleared = false;
                string statusText = "UNEXPLORED";
                string statusColor = "#9ca3af";

                switch (node.ZoneKey)
                {
                    case "Village":
                        isCurrent = (curLoc == GameLocation.Village);
                        isCleared = true; // Village is permanent safe haven
                        statusText = "SANCTUARY";
                        statusColor = "#60a5fa";
                        break;

                    case "Forest":
                        isCurrent = (curLoc == GameLocation.Forest);
                        isCleared = true;
                        statusText = "PATROLLED";
                        statusColor = "#34d399";
                        break;

                    case "Courtyard":
                        isCurrent = (curLoc == GameLocation.Courtyard);
                        isCleared = isCourtyardCleared;
                        statusText = isCleared ? "CLEARED" : "BOSS: COMMANDER";
                        statusColor = isCleared ? "#4ade80" : "#f87171";
                        break;

                    case "Library":
                        isCurrent = (curLoc == GameLocation.Library);
                        isCleared = isLibraryCleared;
                        statusText = isCleared ? "CLEARED" : "BOSS: MALAKOR";
                        statusColor = isCleared ? "#4ade80" : "#f87171";
                        break;

                    case "Hall":
                        isCurrent = false;
                        isCleared = isCourtyardCleared || isLibraryCleared;
                        statusText = isCleared ? "ACCESSIBLE" : "LOCKED (NEEDS WING CLEAR)";
                        statusColor = isCleared ? "#38bdf8" : "#6b7280";
                        break;

                    case "CrownHall":
                        isCurrent = (curLoc == GameLocation.CrownHall);
                        isCleared = isCrownHallCleared;
                        statusText = isCleared ? "VANQUISHED" : "FINAL BOSS: GARGOYLE KING";
                        statusColor = isCleared ? "#4ade80" : "#ef4444";
                        break;
                }

                // Update Location Pin
                if (node.LocationPin != null)
                {
                    node.LocationPin.SetActive(isCurrent);
                }

                // Update Status Badge
                if (node.StatusBadge != null)
                {
                    node.StatusBadge.text = isCurrent
                        ? $"<color=#facc15><b>[ YOU ARE HERE ]</b></color> <color={statusColor}>{statusText}</color>"
                        : $"<color={statusColor}><b>[{statusText}]</b></color>";
                }

                // Border styling
                if (node.BackgroundImage != null)
                {
                    node.BackgroundImage.color = isCurrent
                        ? new Color(1f, 0.9f, 0.4f, 1f)
                        : (isCleared ? new Color(0.85f, 0.95f, 0.85f, 0.95f) : new Color(0.7f, 0.7f, 0.75f, 0.85f));
                }
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
        /// Self-healing builder ensuring all modal hierarchy elements, headers, room nodes,
        /// and legend items are properly created and styled without manual scene editing.
        /// </summary>
        public void EnsureUIHierarchy()
        {
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

            // 1. Root Modal Panel (Fullscreen Dark Dimmer)
            if (mapModalPanel == null)
            {
                Transform existing = parentCanvas.transform.Find("Dungeon_Map_Modal");
                if (existing != null)
                {
                    mapModalPanel = existing.gameObject;
                }
                else
                {
                    mapModalPanel = new GameObject("Dungeon_Map_Modal", typeof(RectTransform), typeof(Image));
                    mapModalPanel.transform.SetParent(parentCanvas.transform, false);

                    RectTransform rt = mapModalPanel.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    Image bgDim = mapModalPanel.GetComponent<Image>();
                    bgDim.color = new Color(0.04f, 0.05f, 0.08f, 0.85f);
                }
            }

            // 2. Main Parchment Frame
            Transform frameTr = mapModalPanel.transform.Find("Map_Frame_Panel");
            GameObject frameObj;
            if (frameTr == null)
            {
                frameObj = new GameObject("Map_Frame_Panel", typeof(RectTransform), typeof(Image));
                frameObj.transform.SetParent(mapModalPanel.transform, false);

                RectTransform frRt = frameObj.GetComponent<RectTransform>();
                frRt.anchorMin = new Vector2(0.5f, 0.5f);
                frRt.anchorMax = new Vector2(0.5f, 0.5f);
                frRt.sizeDelta = new Vector2(960f, 620f);
                frRt.anchoredPosition = Vector2.zero;

                Image frImg = frameObj.GetComponent<Image>();
                frImg.sprite = panelDarkSprite;
                frImg.type = Image.Type.Sliced;
                frImg.color = Color.white;
            }
            else
            {
                frameObj = frameTr.gameObject;
            }

            // 3. Header Title & Gold Divider
            Transform headerTr = frameObj.transform.Find("Header_Title");
            if (headerTr == null)
            {
                GameObject titleObj = new GameObject("Header_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(frameObj.transform, false);

                RectTransform tRt = titleObj.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0f, 1f);
                tRt.anchorMax = new Vector2(1f, 1f);
                tRt.sizeDelta = new Vector2(0f, 48f);
                tRt.anchoredPosition = new Vector2(0f, -32f);

                titleText = titleObj.GetComponent<TextMeshProUGUI>();
                titleText.text = "EXPEDITION MAP - CASTLE OF DICE";
                titleText.fontSize = 24f;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.color = new Color(0.98f, 0.85f, 0.45f);
            }
            else
            {
                titleText = headerTr.GetComponent<TMP_Text>();
            }

            Transform subTr = frameObj.transform.Find("Header_Subtitle");
            if (subTr == null)
            {
                GameObject subObj = new GameObject("Header_Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                subObj.transform.SetParent(frameObj.transform, false);

                RectTransform sRt = subObj.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 1f);
                sRt.anchorMax = new Vector2(1f, 1f);
                sRt.sizeDelta = new Vector2(0f, 26f);
                sRt.anchoredPosition = new Vector2(0f, -60f);

                subtitleText = subObj.GetComponent<TextMeshProUGUI>();
                subtitleText.text = "Non-linear tactical progression across castle wings, chambers, and secret routes";
                subtitleText.fontSize = 13f;
                subtitleText.alignment = TextAlignmentOptions.Center;
                subtitleText.color = new Color(0.8f, 0.82f, 0.88f);
            }

            // Gold divider line
            Transform divTr = frameObj.transform.Find("Divider_Gold");
            if (divTr == null)
            {
                GameObject divObj = new GameObject("Divider_Gold", typeof(RectTransform), typeof(Image));
                divObj.transform.SetParent(frameObj.transform, false);

                RectTransform dRt = divObj.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0.1f, 1f);
                dRt.anchorMax = new Vector2(0.9f, 1f);
                dRt.sizeDelta = new Vector2(0f, 3f);
                dRt.anchoredPosition = new Vector2(0f, -80f);

                Image dImg = divObj.GetComponent<Image>();
                dImg.sprite = dividerGoldSprite;
                dImg.type = Image.Type.Sliced;
                dImg.color = new Color(1f, 0.85f, 0.35f, 0.9f);
            }

            // Close Button
            Transform closeTr = frameObj.transform.Find("Button_Close");
            if (closeTr == null)
            {
                GameObject btnObj = new GameObject("Button_Close", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(frameObj.transform, false);

                RectTransform bRt = btnObj.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(1f, 1f);
                bRt.anchorMax = new Vector2(1f, 1f);
                bRt.sizeDelta = new Vector2(140f, 38f);
                bRt.anchoredPosition = new Vector2(-85f, -38f);

                Image bImg = btnObj.GetComponent<Image>();
                bImg.sprite = buttonNormalSprite ?? slotFrameSprite;
                bImg.type = Image.Type.Sliced;

                closeButton = btnObj.GetComponent<Button>();

                GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtObj.transform.SetParent(btnObj.transform, false);
                RectTransform tRt = txtObj.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.sizeDelta = Vector2.zero;

                TMP_Text lbl = txtObj.GetComponent<TextMeshProUGUI>();
                lbl.text = "CLOSE (M / ESC)";
                lbl.fontSize = 12f;
                lbl.fontStyle = FontStyles.Bold;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color = Color.white;
            }
            else
            {
                closeButton = closeTr.GetComponent<Button>();
            }

            // 4. Construct Zone Node Cards
            Transform nodesRoot = frameObj.transform.Find("Zone_Nodes_Container");
            if (nodesRoot == null)
            {
                GameObject nrObj = new GameObject("Zone_Nodes_Container", typeof(RectTransform));
                nrObj.transform.SetParent(frameObj.transform, false);
                RectTransform nrRt = nrObj.GetComponent<RectTransform>();
                nrRt.anchorMin = Vector2.zero;
                nrRt.anchorMax = Vector2.one;
                nrRt.sizeDelta = Vector2.zero;
                nodesRoot = nrObj.transform;
            }

            BuildZoneNodesIfEmpty(nodesRoot);

            // 5. Construct Legend Bar at Bottom
            Transform legendTr = frameObj.transform.Find("Legend_Bar");
            if (legendTr == null)
            {
                GameObject legObj = new GameObject("Legend_Bar", typeof(RectTransform), typeof(TextMeshProUGUI));
                legObj.transform.SetParent(frameObj.transform, false);

                RectTransform lRt = legObj.GetComponent<RectTransform>();
                lRt.anchorMin = new Vector2(0f, 0f);
                lRt.anchorMax = new Vector2(1f, 0f);
                lRt.sizeDelta = new Vector2(0f, 34f);
                lRt.anchoredPosition = new Vector2(0f, 20f);

                TMP_Text legText = legObj.GetComponent<TextMeshProUGUI>();
                legText.text = "<color=#facc15>[*] Current Location</color>    " +
                               "<color=#4ade80>[*] Cleared</color>    " +
                               "<color=#f87171>[*] Boss / Combat</color>    " +
                               "<color=#38bdf8>[*] Secret Route (Rogue Lockpick)</color>";
                legText.fontSize = 12f;
                legText.alignment = TextAlignmentOptions.Center;
            }

            WireListeners();
        }

        private void BuildZoneNodesIfEmpty(Transform container)
        {
            if (zoneNodes.Count > 0 && container.childCount > 0)
            {
                return;
            }

            zoneNodes.Clear();

            // Node definitions matching MasterSpec §3 Progression Map:
            // 1. Village & Cellar (Bottom-Left: -260, -145)
            CreateNode(container, "Village", "OAKHAVEN VILLAGE & CELLAR",
                "Sanctuary with shop, blacksmith Baldur, and tutorial wine cellar.",
                new Vector2(-250f, -145f), new Vector2(320f, 85f));

            // 2. Whispering Woods / Forest (Middle-Left: -250, -35)
            CreateNode(container, "Forest", "WHISPERING WOODS (FOREST)",
                "Non-combat nature buffer. Contains marsh herb harvesting nodes.",
                new Vector2(-250f, -35f), new Vector2(320f, 85f));

            // 3. Wing 1: Castle Courtyard (Middle-Right: +190, -35)
            CreateNode(container, "Courtyard", "WING 1: CASTLE COURTYARD",
                "Boss 1: Cursed Commander (50 HP). Unlocks Signet Ring reward.",
                new Vector2(190f, -35f), new Vector2(320f, 85f));

            // 4. Wing 2: Grand Archives / Library (Upper-Left: -250, +75)
            CreateNode(container, "Library", "WING 2: GRAND ARCHIVES (LIBRARY)",
                "Boss 2: Shadow Mage Malakor (40 HP). Arcane teleportation & Greater Potion.",
                new Vector2(-250f, +75f), new Vector2(320f, 85f));

            // 5. The Great Hall (Upper-Right: +190, +75)
            CreateNode(container, "Hall", "THE GREAT HALL (SANCTUARY)",
                "Central hub connecting wings. SavePoint altar and path to Crown Hall.",
                new Vector2(190f, +75f), new Vector2(320f, 85f));

            // 6. Wing 3: Crown Hall (Top Center: -30, +185)
            CreateNode(container, "CrownHall", "WING 3: CROWN HALL (THRONE ROOM)",
                "Final Boss: The Gargoyle King (60 HP). Vanquish to complete the campaign!",
                new Vector2(-30f, +185f), new Vector2(400f, 85f));

            // Visual Secret Passage Connector Badge (between Forest and Library)
            GameObject secretBadge = new GameObject("Secret_Route_Indicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            secretBadge.transform.SetParent(container, false);
            RectTransform sRt = secretBadge.GetComponent<RectTransform>();
            sRt.anchoredPosition = new Vector2(-420f, +20f);
            sRt.sizeDelta = new Vector2(140f, 32f);

            TMP_Text sText = secretBadge.GetComponent<TextMeshProUGUI>();
            sText.text = "<color=#38bdf8>[SECRET NATURE ROUTE]\n(Rogue Bypass)</color>";
            sText.fontSize = 9.5f;
            sText.alignment = TextAlignmentOptions.Center;
        }

        private void CreateNode(Transform parent, string key, string name, string desc, Vector2 pos, Vector2 size)
        {
            GameObject nodeObj = new GameObject($"Node_{key}", typeof(RectTransform), typeof(Image));
            nodeObj.transform.SetParent(parent, false);

            RectTransform nRt = nodeObj.GetComponent<RectTransform>();
            nRt.anchoredPosition = pos;
            nRt.sizeDelta = size;

            Image bg = nodeObj.GetComponent<Image>();
            bg.sprite = slotFrameSprite ?? panelDarkSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.85f, 0.85f, 0.9f, 0.95f);

            // Title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(nodeObj.transform, false);
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.04f, 0.62f);
            tRt.anchorMax = new Vector2(0.96f, 0.96f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            TMP_Text title = titleObj.GetComponent<TextMeshProUGUI>();
            title.text = name;
            title.fontSize = 11.5f;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(1f, 0.95f, 0.7f);

            // Status Badge
            GameObject statusObj = new GameObject("StatusBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(nodeObj.transform, false);
            RectTransform stRt = statusObj.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0.04f, 0.35f);
            stRt.anchorMax = new Vector2(0.96f, 0.65f);
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;

            TMP_Text status = statusObj.GetComponent<TextMeshProUGUI>();
            status.text = "[UNEXPLORED]";
            status.fontSize = 10f;
            status.fontStyle = FontStyles.Bold;

            // Description
            GameObject descObj = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(nodeObj.transform, false);
            RectTransform dRt = descObj.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.04f, 0.05f);
            dRt.anchorMax = new Vector2(0.96f, 0.38f);
            dRt.offsetMin = Vector2.zero;
            dRt.offsetMax = Vector2.zero;

            TMP_Text dText = descObj.GetComponent<TextMeshProUGUI>();
            dText.text = desc;
            dText.fontSize = 9.5f;
            dText.color = new Color(0.75f, 0.78f, 0.84f);

            // Location Pin Icon
            GameObject pinObj = new GameObject("Pin", typeof(RectTransform), typeof(TextMeshProUGUI));
            pinObj.transform.SetParent(nodeObj.transform, false);
            RectTransform pRt = pinObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(1f, 0.5f);
            pRt.anchorMax = new Vector2(1f, 0.5f);
            pRt.sizeDelta = new Vector2(48f, 24f);
            pRt.anchoredPosition = new Vector2(-28f, 0f);

            TMP_Text pText = pinObj.GetComponent<TextMeshProUGUI>();
            pText.text = "<color=#facc15><b>[YOU]</b></color>";
            pText.fontSize = 11f;
            pText.alignment = TextAlignmentOptions.Center;
            pinObj.SetActive(false);

            zoneNodes.Add(new ZoneNodeUI
            {
                ZoneKey = key,
                DisplayName = name,
                Description = desc,
                Position = pos,
                ContainerRect = nRt,
                BackgroundImage = bg,
                TitleLabel = title,
                StatusBadge = status,
                DescLabel = dText,
                LocationPin = pinObj
            });
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
