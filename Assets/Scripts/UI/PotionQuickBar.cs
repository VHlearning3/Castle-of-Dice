using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>Why a health potion can or cannot be drunk right now.</summary>
    public enum PotionUseCheck
    {
        Ready,
        NoPotion,
        NoHero,
        FullHealth,
        NotYourTurn,
        ActionUsed
    }

    /// <summary>
    /// Two flask slots in the bottom-left corner (small and large health potion), each with the potion icon,
    /// how many the hero carries and how much it heals. Clicking a slot drinks one. Outside combat that is
    /// free; in combat it is only allowed on the hero's turn and uses the turn's action (movement stays).
    /// A potion is never wasted at full health. [Q] drinks a small potion first, then a large one.
    /// Visible while exploring and fighting, hidden during dialogue and the shop.
    /// Values are compared as integers each frame and text is only rewritten when something changed.
    /// </summary>
    public class PotionQuickBar : MonoBehaviour
    {
        #region Layout Constants

        public const string BarObjectName = "Flask_Quick_Bar";
        public const float SlotSize = 84f;
        public const float SlotGap = 10f;

        private static readonly Color CreamText = new Color(1.0f, 0.96f, 0.88f, 1f);
        private static readonly Color GoldValue = new Color(0.96f, 0.85f, 0.50f, 1f);
        private static readonly Color HealGreen = new Color(0.45f, 0.95f, 0.50f, 1f);
        private static readonly Color DimIcon = new Color(0.6f, 0.6f, 0.6f, 0.45f);
        private static readonly Color RefusedGrey = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color SlotFallback = new Color(0.12f, 0.10f, 0.08f, 0.92f);

        #endregion

        #region Nested Types

        private sealed class Slot
        {
            public string ItemID;
            public Button Button;
            public Image Icon;
            public TMP_Text Amount;
            public TMP_Text HealLabel;
            public long Signature;
        }

        #endregion

        #region State

        private static PotionQuickBar instance;

        private readonly Slot smallSlot = new Slot { ItemID = ShopManager.SMALL_POTION_ID };
        private readonly Slot largeSlot = new Slot { ItemID = ShopManager.GREATER_POTION_ID };
        private CanvasGroup canvasGroup;
        private PlayerUnit trackedPlayer;
        private float nextPlayerLookup;
        private bool built;

        #endregion

        #region Public API

        /// <summary>The bar in the running scene, if one was built.</summary>
        public static PotionQuickBar Instance => instance;

        /// <summary>The small-potion slot button (null until built).</summary>
        public Button SmallSlotButton => smallSlot.Button;

        /// <summary>The large-potion slot button (null until built).</summary>
        public Button LargeSlotButton => largeSlot.Button;

        /// <summary>
        /// Finds or creates the bar under <paramref name="canvasRoot"/>, anchored to the bottom-left corner.
        /// Safe to call repeatedly.
        /// </summary>
        public static PotionQuickBar EnsureBar(RectTransform canvasRoot, Sprite slotFrame, Sprite badge, Sprite potionIcon)
        {
            if (canvasRoot == null) return null;

            Transform existing = canvasRoot.Find(BarObjectName);
            GameObject barObj = existing != null ? existing.gameObject : new GameObject(BarObjectName, typeof(RectTransform), typeof(CanvasGroup));
            barObj.transform.SetParent(canvasRoot, false);

            RectTransform rect = barObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 20f);
            rect.sizeDelta = new Vector2(SlotSize * 2f + SlotGap, SlotSize);
            rect.localScale = Vector3.one;

            PotionQuickBar bar = barObj.GetComponent<PotionQuickBar>();
            if (bar == null) bar = barObj.AddComponent<PotionQuickBar>();
            bar.Build(slotFrame, badge, potionIcon);
            return bar;
        }

        /// <summary>
        /// The potion rules in one place: you need a potion, a living hero who is hurt, and in combat it must be
        /// the hero's turn with the action still unused.
        /// </summary>
        public static PotionUseCheck CheckUse(int potionCount, bool heroAlive, int currentHP, int maxHP, bool inCombat, bool heroTurn, bool actionUsed)
        {
            if (potionCount <= 0) return PotionUseCheck.NoPotion;
            if (!heroAlive) return PotionUseCheck.NoHero;
            if (currentHP >= maxHP) return PotionUseCheck.FullHealth;
            if (inCombat && !heroTurn) return PotionUseCheck.NotYourTurn;
            if (inCombat && actionUsed) return PotionUseCheck.ActionUsed;
            return PotionUseCheck.Ready;
        }

        /// <summary>Checks the rules for <paramref name="potion"/> against the live hero, inventory and battle.</summary>
        public static PotionUseCheck CheckUse(ItemSO potion, PlayerUnit player)
        {
            InventoryManager inventory = InventoryManager.Instance;
            int count = inventory != null && potion != null ? inventory.GetItemCount(potion) : 0;
            if (player == null) return count > 0 ? PotionUseCheck.NoHero : PotionUseCheck.NoPotion;

            TurnManager tm = TurnManager.Instance;
            bool inCombat = tm != null && tm.IsCombatActive;
            bool heroTurn = inCombat && tm.CurrentState == TurnState.PlayerTurn
                && (tm.CurrentActiveUnit == null || tm.CurrentActiveUnit == player);

            return CheckUse(count, player.IsAlive, player.CurrentHP, player.MaxHP, inCombat, heroTurn, player.HasActedThisTurn);
        }

        /// <summary>
        /// Drinks one <paramref name="potion"/> if the rules allow it: heals, removes it from the pack and, in combat,
        /// spends the hero's action. Shows a short message over the hero either way.
        /// </summary>
        public static PotionUseCheck TryDrink(ItemSO potion, PlayerUnit player)
        {
            PotionUseCheck check = CheckUse(potion, player);
            if (check != PotionUseCheck.Ready)
            {
                ShowRefusal(check, player);
                return check;
            }

            TurnManager tm = TurnManager.Instance;
            bool inCombat = tm != null && tm.IsCombatActive;
            int before = player.CurrentHP;

            if (!InventoryManager.Instance.UseItem(potion, player)) return PotionUseCheck.NoPotion;

            int healed = player.CurrentHP - before;
            if (Application.isPlaying)
            {
                SFXManager.Instance?.PlaySFX(SFXClipType.PotionDrink, player.transform.position);
                AbilityVfx.PlayPotionHeal(player);
                FloatingCombatText.Instance?.ShowText(player.transform.position + Vector3.up * 2.2f, "+" + healed + " HP", HealGreen);
            }

            if (inCombat)
            {
                player.HasActedThisTurn = true;
                CombatUIController combatUI = CombatUIController.Instance;
                if (combatUI != null)
                {
                    combatUI.LogCombatMessage(player.UnitName + " drinks a " + potion.ItemName + " and recovers " + healed + " HP.");
                    combatUI.OnHeroActionSpentOutsideBar();
                }
            }

            if (instance != null) instance.ForceRefresh();
            return PotionUseCheck.Ready;
        }

        /// <summary>[Q]: a small potion first, a large one when no small potions are left.</summary>
        public static PotionUseCheck TryDrinkQuick(PlayerUnit player)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return PotionUseCheck.NoPotion;

            ItemSO small = inventory.FindItemByID(ShopManager.SMALL_POTION_ID);
            if (small != null && inventory.HasItem(small, 1)) return TryDrink(small, player);

            ItemSO large = inventory.FindItemByID(ShopManager.GREATER_POTION_ID);
            if (large != null && inventory.HasItem(large, 1)) return TryDrink(large, player);

            ShowRefusal(PotionUseCheck.NoPotion, player);
            return PotionUseCheck.NoPotion;
        }

        /// <summary>Short player-facing reason a potion was not drunk.</summary>
        public static string RefusalText(PotionUseCheck check)
        {
            switch (check)
            {
                case PotionUseCheck.NoPotion: return "No potions left";
                case PotionUseCheck.FullHealth: return "Already at full health";
                case PotionUseCheck.NotYourTurn: return "Wait for your turn";
                case PotionUseCheck.ActionUsed: return "Action already used this turn";
                case PotionUseCheck.NoHero: return "No hero to drink it";
                default: return "";
            }
        }

        /// <summary>Re-reads counts and rules on the next frame.</summary>
        public void ForceRefresh()
        {
            smallSlot.Signature = 0;
            largeSlot.Signature = 0;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance == null || instance == this) instance = this;
        }

        private void OnEnable()
        {
            if (instance == null) instance = this;
            InventoryManager.OnInventoryChanged += ForceRefresh;
            ForceRefresh();
        }

        private void OnDisable()
        {
            InventoryManager.OnInventoryChanged -= ForceRefresh;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!built) return;

            if (trackedPlayer == null && Time.unscaledTime >= nextPlayerLookup)
            {
                nextPlayerLookup = Time.unscaledTime + 1f;
                trackedPlayer = FindAnyObjectByType<PlayerUnit>();
            }

            UpdateVisibility();
            RefreshSlot(smallSlot);
            RefreshSlot(largeSlot);
        }

        #endregion

        #region Refresh

        private void UpdateVisibility()
        {
            if (canvasGroup == null) return;

            GameManager gm = GameManager.Instance;
            bool show = gm == null || gm.CurrentMode == GamePlayMode.Exploration || gm.CurrentMode == GamePlayMode.Combat;
            float alpha = show ? 1f : 0f;
            if (canvasGroup.alpha != alpha)
            {
                canvasGroup.alpha = alpha;
                canvasGroup.interactable = show;
                canvasGroup.blocksRaycasts = show;
            }
        }

        private void RefreshSlot(Slot slot)
        {
            if (slot.Button == null) return;

            InventoryManager inventory = InventoryManager.Instance;
            ItemSO item = inventory != null ? inventory.FindItemByID(slot.ItemID) : null;
            int count = item != null ? inventory.GetItemCount(item) : 0;
            PotionUseCheck check = item != null ? CheckUse(item, trackedPlayer) : PotionUseCheck.NoPotion;
            int heal = item != null ? item.StatBonusValue : 0;

            long signature;
            unchecked
            {
                signature = 17;
                signature = signature * 31 + count;
                signature = signature * 31 + (int)check;
                signature = signature * 31 + heal;
                if (signature == 0) signature = 1;
            }
            if (signature == slot.Signature) return;
            slot.Signature = signature;

            slot.Amount.text = "x" + count;
            slot.Amount.color = count > 0 ? GoldValue : RefusedGrey;
            if (heal > 0) slot.HealLabel.text = "+" + heal + " HP";

            // Clickable whenever there is one to drink, so a refused click can say why
            slot.Button.interactable = count > 0;
            slot.Icon.color = check == PotionUseCheck.Ready ? Color.white : DimIcon;
        }

        #endregion

        #region Actions

        private void OnSlotClicked(Slot slot)
        {
            if (trackedPlayer == null) trackedPlayer = FindAnyObjectByType<PlayerUnit>();

            InventoryManager inventory = InventoryManager.Instance;
            ItemSO item = inventory != null ? inventory.FindItemByID(slot.ItemID) : null;
            if (item == null)
            {
                ShowRefusal(PotionUseCheck.NoPotion, trackedPlayer);
                return;
            }
            TryDrink(item, trackedPlayer);
        }

        private static void ShowRefusal(PotionUseCheck check, PlayerUnit player)
        {
            string text = RefusalText(check);
            if (string.IsNullOrEmpty(text)) return;

            if (Application.isPlaying && player != null && FloatingCombatText.Instance != null)
            {
                FloatingCombatText.Instance.ShowText(player.transform.position + Vector3.up * 2.2f, text, RefusedGrey);
            }

            TurnManager tm = TurnManager.Instance;
            if (tm != null && tm.IsCombatActive && CombatUIController.Instance != null)
            {
                CombatUIController.Instance.LogCombatMessage(text + ".");
            }
        }

        #endregion

        #region Hierarchy Construction

        private void Build(Sprite slotFrame, Sprite badge, Sprite potionIcon)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            BuildSlot(smallSlot, "Flask_Slot_Small", 0, 44f, "Q", slotFrame, badge, potionIcon);
            BuildSlot(largeSlot, "Flask_Slot_Large", 1, 58f, null, slotFrame, badge, potionIcon);
            built = true;
            ForceRefresh();
        }

        private void BuildSlot(Slot slot, string name, int index, float iconSize, string hotkey, Sprite slotFrame, Sprite badge, Sprite potionIcon)
        {
            GameObject slotObj = EnsureChild(transform, name, typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = slotObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(index * (SlotSize + SlotGap), 0f);
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);

            Image frame = slotObj.GetComponent<Image>();
            ApplySprite(frame, slotFrame, SlotFallback);

            slot.Button = slotObj.GetComponent<Button>();
            slot.Button.targetGraphic = frame;
            ColorBlock colors = slot.Button.colors;
            colors.highlightedColor = new Color(1f, 0.92f, 0.7f, 1f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.5f, 1f);
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.8f);
            slot.Button.colors = colors;
            slot.Button.onClick.RemoveAllListeners();
            Slot captured = slot;
            slot.Button.onClick.AddListener(() => OnSlotClicked(captured));

            // Flask icon (the large potion is drawn bigger)
            GameObject iconObj = EnsureChild(slotObj.transform, "Flask_Icon", typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -6f - (58f - iconSize) * 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            slot.Icon = iconObj.GetComponent<Image>();
            if (potionIcon != null) slot.Icon.sprite = potionIcon;
            slot.Icon.preserveAspect = true;
            slot.Icon.raycastTarget = false;

            // Heal amount along the bottom edge
            slot.HealLabel = EnsureText(slotObj.transform, "Flask_Heal_Label", 11f, FontStyles.Bold, CreamText, TextAlignmentOptions.Center);
            RectTransform healRect = slot.HealLabel.rectTransform;
            healRect.anchorMin = new Vector2(0f, 0f);
            healRect.anchorMax = new Vector2(1f, 0f);
            healRect.pivot = new Vector2(0.5f, 0f);
            healRect.anchoredPosition = new Vector2(0f, 5f);
            healRect.sizeDelta = new Vector2(-8f, 16f);
            slot.HealLabel.text = index == 0 ? "Small" : "Large";

            // Count badge, top-right corner
            GameObject badgeObj = EnsureChild(slotObj.transform, "Flask_Amount_Badge", typeof(CanvasRenderer), typeof(Image));
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(4f, 4f);
            badgeRect.sizeDelta = new Vector2(34f, 20f);
            Image badgeImg = badgeObj.GetComponent<Image>();
            ApplySprite(badgeImg, badge, new Color(0.2f, 0.16f, 0.1f, 1f));
            badgeImg.raycastTarget = false;

            slot.Amount = EnsureText(badgeObj.transform, "Flask_Amount", 11.5f, FontStyles.Bold, GoldValue, TextAlignmentOptions.Center);
            Stretch(slot.Amount.rectTransform);
            slot.Amount.text = "x0";

            // Hotkey tag, top-left corner
            Transform tagTr = slotObj.transform.Find("Flask_Key_Tag");
            if (!string.IsNullOrEmpty(hotkey))
            {
                TMP_Text tag = EnsureText(slotObj.transform, "Flask_Key_Tag", 11f, FontStyles.Bold, GoldValue, TextAlignmentOptions.Center);
                RectTransform tagRect = tag.rectTransform;
                tagRect.anchorMin = new Vector2(0f, 1f);
                tagRect.anchorMax = new Vector2(0f, 1f);
                tagRect.pivot = new Vector2(0f, 1f);
                tagRect.anchoredPosition = new Vector2(5f, -3f);
                tagRect.sizeDelta = new Vector2(18f, 16f);
                tag.text = hotkey;
            }
            else if (tagTr != null)
            {
                tagTr.gameObject.SetActive(false);
            }
        }

        #endregion

        #region UI Helpers

        private static GameObject EnsureChild(Transform parent, string name, params System.Type[] components)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject go = new GameObject(name, typeof(RectTransform));
            for (int i = 0; i < components.Length; i++)
            {
                go.AddComponent(components[i]);
            }
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text EnsureText(Transform parent, string name, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = EnsureChild(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void ApplySprite(Image image, Sprite sprite, Color fallback)
        {
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
