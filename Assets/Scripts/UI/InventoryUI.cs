using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// The hero's bag (Vili, 2026-10-06): everything carried, with how many and what it does. Opens with [I] or the
    /// BAG button on the HUD (left of the MAP button), outside battle. Shows gold, scrap and the permanent weapon
    /// and armour upgrades on top, then one row per item: potions, scrolls, poison, the Rune of Fate and quest
    /// items. [I], [Esc] or the close button shuts it. Builds its own overlay canvas on first use.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        /// <summary>Whether the bag is on screen.</summary>
        public static bool IsOpen => Instance != null && Instance.modalRoot != null && Instance.modalRoot.activeSelf;

        /// <summary>Name of the HUD's bag button.</summary>
        public const string BagButtonName = "Button_Bag_Toggle";

        /// <summary>Shown when the bag is asked for in battle or in a conversation.</summary>
        public const string NotNowMessage = "You can open your bag outside battle.";

        private const float PanelWidth = 900f;
        private const float PanelHeight = 760f;
        private const float RowHeight = 74f;

        private static readonly Color RowColor = new Color(0.10f, 0.11f, 0.15f, 0.92f);
        private static readonly Color IconBackColor = new Color(0.20f, 0.17f, 0.12f, 1f);

        private GameObject modalRoot;
        private RectTransform listContent;
        private TMP_Text goldText;
        private TMP_Text scrapText;
        private TMP_Text weaponText;
        private TMP_Text armorText;
        private TMP_Text emptyText;
        private readonly List<GameObject> rows = new List<GameObject>();
        private bool isBuilt;
        private int openedFrame;
        private int closedFrame = -1;

        /// <summary>Whether Esc belongs to the bag this frame (open, or just closed by this very key press).</summary>
        public static bool UsesEscape => IsOpen || (Instance != null && Instance.closedFrame == Time.frameCount);

        /// <summary>One line of the bag: what, how many, what it does.</summary>
        public struct Entry
        {
            public string Name;
            public int Count;
            public string Description;
            public Sprite Icon;
            public ItemType Type;
        }

        #region Public API

        /// <summary>Opens or closes the bag (the [I] key and the HUD button).</summary>
        public static void Toggle()
        {
            InventoryUI ui = EnsureInstance();
            if (IsOpen) ui.Close();
            else ui.Open();
        }

        /// <summary>Creates the bag (closed) so the [I] key works; safe to call every scene.</summary>
        public static InventoryUI EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject root = new GameObject("InventoryUI");
                Instance = root.AddComponent<InventoryUI>();
            }
            Instance.BuildUI();
            return Instance;
        }

        /// <summary>Opens the bag when nothing else is going on (exploration, no conversation or other window).</summary>
        public bool Open()
        {
            BuildUI();
            if (IsOpen) return true;
            if (!CanOpenNow())
            {
                LockpickMinigameUI.ShowToast(NotNowMessage);
                return false;
            }

            Refresh();
            modalRoot.SetActive(true);
            openedFrame = Time.frameCount;
            GameInput.SetExplorationInputEnabled(false);
            SFXManager.Instance?.PlaySFX(SFXClipType.ButtonClick, Vector3.zero);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            closedFrame = Time.frameCount;
            modalRoot.SetActive(false);
            GameInput.SetExplorationInputEnabled(true);
        }

        /// <summary>
        /// What the hero carries, in bag order: potions and other consumables, the Rune of Fate, upgrades, then
        /// quest items. Scrap is left out (it is shown on top).
        /// </summary>
        public static List<Entry> CollectEntries(InventoryManager inventory)
        {
            List<Entry> entries = new List<Entry>();
            if (inventory == null) return entries;

            foreach (KeyValuePair<ItemSO, int> pair in inventory.Items)
            {
                ItemSO item = pair.Key;
                if (item == null || pair.Value <= 0 || item.ItemType == ItemType.ScrapMetal) continue;
                if (item.ItemID == ShopManager.REROLL_RUNE_ID) continue; // counted below
                entries.Add(new Entry { Name = item.ItemName, Count = pair.Value, Description = item.Description, Icon = item.ItemIcon, Type = item.ItemType });
            }

            if (inventory.RerollScrollCount > 0)
            {
                ItemSO rune = inventory.FindItemByID(ShopManager.REROLL_RUNE_ID);
                entries.Add(new Entry
                {
                    Name = rune != null ? rune.ItemName : "Rune of Fate",
                    Count = inventory.RerollScrollCount,
                    Description = rune != null ? rune.Description : "When one of your d20 rolls fails, break the rune to roll again.",
                    Icon = rune != null ? rune.ItemIcon : null,
                    Type = ItemType.Consumable
                });
            }

            entries.Sort((a, b) =>
            {
                int order = Order(a.Type).CompareTo(Order(b.Type));
                return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
            });
            return entries;
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
        }

        private void OnEnable()
        {
            InventoryManager.OnInventoryChanged += HandleInventoryChanged;
        }

        private void OnDisable()
        {
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (IsOpen) GameInput.SetExplorationInputEnabled(true);
            Instance = null;
        }

        private void Update()
        {
            if (IsOpen)
            {
                if (Time.frameCount == openedFrame) return;
                if (GameInput.GetKeyDown(KeyCode.I) || GameInput.GetKeyDown(KeyCode.Escape)) Close();
                return;
            }

            if (GameInput.GetKeyDown(KeyCode.I)) Open();
        }

        #endregion

        #region Helpers

        private static bool CanOpenNow()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CurrentMode != GamePlayMode.Exploration) return false;
            if (Dialogue.DialogueController.Instance != null && Dialogue.DialogueController.Instance.IsInDialogue) return false;
            if (StoryPanelUI.IsOpen || LockpickMinigameUI.IsOpen || TomeShelfMinigameUI.IsOpen) return false;
            if (ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen) return false;
            if (DungeonMapUIController.Instance != null && DungeonMapUIController.Instance.IsMapOpen) return false;
            if (LevelUpUIController.Instance != null && LevelUpUIController.Instance.IsModalOpen) return false;
            return true;
        }

        private static int Order(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable: return 0;
                case ItemType.WeaponUpgrade: return 1;
                case ItemType.ArmorUpgrade: return 2;
                case ItemType.QuestItem: return 3;
                default: return 4;
            }
        }

        private void HandleInventoryChanged()
        {
            if (IsOpen) Refresh();
        }

        private void Refresh()
        {
            InventoryManager inventory = InventoryManager.Instance;
            PlayerUnit hero = FindAnyObjectByType<PlayerUnit>();

            goldText.text = $"<color=#FFD34D>Gold</color>  {(inventory != null ? inventory.CurrentGold : 0)}";
            scrapText.text = $"<color=#C9CED6>Scrap</color>  {(inventory != null ? inventory.ScrapMetalCount : 0)}";
            weaponText.text = $"<color=#E8A07A>Weapon</color>  +{(hero != null ? hero.WeaponDamageBonus : 0)} damage";
            armorText.text = $"<color=#9CC3E6>Armour</color>  +{(hero != null ? hero.ArmorClassBonus : 0)} AC";

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null) Destroy(rows[i]);
            }
            rows.Clear();

            List<Entry> entries = CollectEntries(inventory);
            emptyText.gameObject.SetActive(entries.Count == 0);
            for (int i = 0; i < entries.Count; i++)
            {
                rows.Add(BuildRow(entries[i]));
            }
        }

        private GameObject BuildRow(Entry entry)
        {
            RectTransform row = Rect("Row_" + entry.Name, listContent, new Vector2(PanelWidth - 80f, RowHeight), Vector2.zero);
            row.gameObject.AddComponent<Image>().color = RowColor;
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = RowHeight;
            layout.minHeight = RowHeight;

            RectTransform iconBack = Rect("Icon", row, new Vector2(56f, 56f), Vector2.zero);
            iconBack.anchorMin = iconBack.anchorMax = new Vector2(0f, 0.5f);
            iconBack.pivot = new Vector2(0f, 0.5f);
            iconBack.anchoredPosition = new Vector2(10f, 0f);
            Image back = iconBack.gameObject.AddComponent<Image>();
            back.color = IconBackColor;
            if (entry.Icon != null)
            {
                RectTransform iconRect = Rect("Sprite", iconBack, new Vector2(48f, 48f), Vector2.zero);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = entry.Icon;
                icon.preserveAspect = true;
            }
            else
            {
                TMP_Text letter = Text("Letter", iconBack, entry.Name.Length > 0 ? entry.Name.Substring(0, 1) : "?", 30f, UITheme.GoldAccent, Vector2.zero, new Vector2(56f, 56f));
                letter.fontStyle = FontStyles.Bold;
            }

            TMP_Text name = Text("Name", row, entry.Count > 1 ? $"{entry.Name}  <color=#FFD34D>x{entry.Count}</color>" : entry.Name,
                22f, UITheme.CreamText, Vector2.zero, new Vector2(PanelWidth - 190f, 30f));
            name.alignment = TextAlignmentOptions.Left;
            name.fontStyle = FontStyles.Bold;
            RectTransform nameRect = name.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(80f, -6f);

            TMP_Text desc = Text("Description", row, entry.Description ?? string.Empty, 17f, UITheme.SoftText, Vector2.zero, new Vector2(PanelWidth - 190f, 38f));
            desc.alignment = TextAlignmentOptions.TopLeft;
            desc.textWrappingMode = TextWrappingModes.Normal;
            RectTransform descRect = desc.rectTransform;
            descRect.anchorMin = descRect.anchorMax = new Vector2(0f, 1f);
            descRect.pivot = new Vector2(0f, 1f);
            descRect.anchoredPosition = new Vector2(80f, -34f);
            return row.gameObject;
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            if (isBuilt) return;
            isBuilt = true;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 91;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dimmer; its "Modal" name keeps the world click raycaster from reaching through
            modalRoot = new GameObject("InventoryModal", typeof(RectTransform));
            modalRoot.transform.SetParent(transform, false);
            RectTransform dimRect = (RectTransform)modalRoot.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            modalRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            RectTransform panel = Rect("InventoryModal_Panel", modalRoot.transform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
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

            TMP_Text title = Text("Title", panel, "Bag", 38f, UITheme.GoldAccent, new Vector2(0f, PanelHeight * 0.5f - 46f), new Vector2(600f, 50f));
            title.fontStyle = FontStyles.Bold;

            // Close button, top-right
            RectTransform close = Rect("Close", panel, new Vector2(44f, 44f), new Vector2(PanelWidth * 0.5f - 40f, PanelHeight * 0.5f - 40f));
            Image closeImage = close.gameObject.AddComponent<Image>();
            closeImage.color = new Color(0.35f, 0.12f, 0.10f, 1f);
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(Close);
            Text("X", close, "X", 24f, UITheme.CreamText, Vector2.zero, new Vector2(44f, 44f)).fontStyle = FontStyles.Bold;

            // Purse and upgrades
            float statsY = PanelHeight * 0.5f - 104f;
            goldText = Text("Gold", panel, string.Empty, 21f, UITheme.CreamText, new Vector2(-300f, statsY), new Vector2(200f, 32f));
            scrapText = Text("Scrap", panel, string.Empty, 21f, UITheme.CreamText, new Vector2(-100f, statsY), new Vector2(200f, 32f));
            weaponText = Text("Weapon", panel, string.Empty, 21f, UITheme.CreamText, new Vector2(100f, statsY), new Vector2(220f, 32f));
            armorText = Text("Armour", panel, string.Empty, 21f, UITheme.CreamText, new Vector2(310f, statsY), new Vector2(200f, 32f));

            // Item list (scrolls when long)
            float listHeight = PanelHeight - 210f;
            RectTransform viewport = Rect("List", panel, new Vector2(PanelWidth - 60f, listHeight), new Vector2(0f, -60f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);

            listContent = Rect("Content", viewport, new Vector2(PanelWidth - 60f, 0f), Vector2.zero);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 4, 4);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = listContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            emptyText = Text("Empty", viewport, "Your bag is empty. Baldur and Pip sell potions, scrolls and runes.", 20f, UITheme.SoftText, Vector2.zero, new Vector2(700f, 40f));

            Text("Hint", panel, "[I] or [Esc]: close", 16f, UITheme.ParchmentText, new Vector2(0f, -PanelHeight * 0.5f + 28f), new Vector2(600f, 26f));

            modalRoot.SetActive(false);
        }

        /// <summary>
        /// Puts the BAG (I) button on the HUD, left of the MAP (M) button and styled like it. Safe to call again.
        /// </summary>
        public static Button EnsureBagButton(Transform hud)
        {
            if (hud == null) return null;
            if (Application.isPlaying) EnsureInstance(); // the HUD builder tool calls this at edit time too

            Transform existing = hud.Find(BagButtonName);
            Button button;
            Transform map = hud.Find("Button_Map_Toggle");
            RectTransform mapRect = map != null ? map.GetComponent<RectTransform>() : null;

            if (existing != null)
            {
                button = existing.GetComponent<Button>();
            }
            else if (map != null)
            {
                GameObject clone = Instantiate(map.gameObject, hud, false);
                clone.name = BagButtonName;
                button = clone.GetComponent<Button>();
                if (button != null) button.onClick = new Button.ButtonClickedEvent(); // drop the map's own listener
                TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "BAG (I)";
            }
            else
            {
                RectTransform rect = Rect(BagButtonName, hud, new Vector2(110f, 32f), Vector2.zero);
                Image image = rect.gameObject.AddComponent<Image>();
                image.color = new Color(0.30f, 0.24f, 0.16f, 0.95f);
                button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                TMP_Text label = Text("Label", rect, "BAG (I)", 11.5f, Color.white, Vector2.zero, new Vector2(110f, 32f));
                label.fontStyle = FontStyles.Bold;
            }

            RectTransform bagRect = button != null ? button.GetComponent<RectTransform>() : null;
            if (bagRect != null)
            {
                float mapWidth = mapRect != null ? mapRect.sizeDelta.x : 110f;
                float mapRight = mapRect != null ? mapRect.anchoredPosition.x : PlayerHUD.MapButtonRightEdge;
                float mapY = mapRect != null ? mapRect.anchoredPosition.y : -18f;
                bagRect.anchorMin = bagRect.anchorMax = new Vector2(1f, 1f);
                bagRect.pivot = new Vector2(1f, 1f);
                bagRect.sizeDelta = new Vector2(mapWidth, mapRect != null ? mapRect.sizeDelta.y : 32f);
                bagRect.anchoredPosition = new Vector2(mapRight - mapWidth - 10f, mapY);
            }

            if (button != null)
            {
                button.onClick.RemoveListener(Toggle);
                button.onClick.AddListener(Toggle);
            }
            return button;
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
            tmp.richText = true;
            return tmp;
        }

        #endregion
    }
}
