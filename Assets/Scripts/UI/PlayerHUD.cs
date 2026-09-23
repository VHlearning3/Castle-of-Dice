using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Data;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Persistent Heads-Up Display (HUD) pinned to the screen during exploration and combat.
    /// Tracks hero current/max HP, gold purse count, a quick-slot button to consume health potions,
    /// and active quest status summaries.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        #region Singleton

        private static PlayerHUD instance;

        public static PlayerHUD Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include);
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Serialized Fields

        [Header("Health Bar")]
        [Tooltip("Slider displaying the hero's current hit point percentage.")]
        [SerializeField] private Slider healthSlider;

        [Tooltip("Text display formatted as 'HP: 25 / 30'.")]
        [SerializeField] private TMP_Text healthText;

        [Header("Gold Counter")]
        [Tooltip("Text display indicating available gold coins in the player's pouch.")]
        [SerializeField] private TMP_Text goldCounterText;

        [Header("Quick Potion Hotbar")]
        [Tooltip("Button allowing immediate consumption of a Health Potion.")]
        [SerializeField] private Button quickPotionButton;

        [Tooltip("Label displaying the number of remaining potions (e.g., 'x2').")]
        [SerializeField] private TMP_Text potionCountText;

        [Tooltip("Potion item definition used for quick consumption.")]
        [SerializeField] private ItemSO healthPotionItem;

        [Header("Active Quest Tracker")]
        [Tooltip("Text component displaying active quest title and objective count (e.g. 'Cellar Rats: 2/3').")]
        [SerializeField] private TMP_Text activeQuestSummaryText;

        #endregion

        #region Private State

        private PlayerUnit trackedPlayer;

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

            AutoLocateComponents();

            if (quickPotionButton != null)
            {
                quickPotionButton.onClick.AddListener(OnQuickPotionClicked);
            }
        }

        private void OnEnable()
        {
            InventoryManager.OnGoldChanged += HandleGoldChanged;
            InventoryManager.OnInventoryChanged += HandleInventoryChanged;
            QuestManager.OnQuestProgressUpdated += HandleQuestProgressUpdated;
            QuestManager.OnQuestStateUpdated += HandleQuestStateUpdated;

            LocatePlayer();
        }

        private void OnDisable()
        {
            InventoryManager.OnGoldChanged -= HandleGoldChanged;
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
            QuestManager.OnQuestProgressUpdated -= HandleQuestProgressUpdated;
            QuestManager.OnQuestStateUpdated -= HandleQuestStateUpdated;

            if (trackedPlayer != null)
            {
                trackedPlayer.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (quickPotionButton != null)
            {
                quickPotionButton.onClick.RemoveListener(OnQuickPotionClicked);
            }
        }

        private void Start()
        {
            RefreshAllHUD();
        }

        #endregion

        #region Auto-Locate Setup

        private void AutoLocateComponents()
        {
            // Auto-locate slider
            if (healthSlider == null)
            {
                healthSlider = GetComponentInChildren<Slider>(true);
            }

            // Auto-locate texts
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in texts)
            {
                string lower = txt.name.ToLowerInvariant();
                if (healthText == null && (lower.Contains("hp") || lower.Contains("health")))
                {
                    healthText = txt;
                }
                else if (goldCounterText == null && (lower.Contains("gold") || lower.Contains("money") || lower.Contains("coin")))
                {
                    goldCounterText = txt;
                }
                else if (potionCountText == null && (lower.Contains("potioncount") || lower.Contains("count") || lower.Contains("qty")))
                {
                    potionCountText = txt;
                }
                else if (activeQuestSummaryText == null && (lower.Contains("quest") || lower.Contains("objective") || lower.Contains("tracker") || lower.Contains("summary")))
                {
                    activeQuestSummaryText = txt;
                }
            }

            // Auto-locate button
            if (quickPotionButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    string lower = btn.name.ToLowerInvariant();
                    if (lower.Contains("potion") || lower.Contains("heal") || lower.Contains("hotbar"))
                    {
                        quickPotionButton = btn;
                        break;
                    }
                }
            }

            // Sanitize text properties for clean layout without overflow
            if (healthText != null)
            {
                healthText.margin = Vector4.zero;
                healthText.enableAutoSizing = true;
                healthText.fontSizeMin = 14f;
                healthText.fontSizeMax = 36f;
                healthText.raycastTarget = false;
            }
            if (goldCounterText != null)
            {
                goldCounterText.margin = Vector4.zero;
                goldCounterText.enableAutoSizing = true;
                goldCounterText.fontSizeMin = 14f;
                goldCounterText.fontSizeMax = 36f;
                goldCounterText.raycastTarget = false;
            }
            if (potionCountText != null)
            {
                potionCountText.margin = Vector4.zero;
                potionCountText.enableAutoSizing = true;
                potionCountText.fontSizeMin = 12f;
                potionCountText.fontSizeMax = 24f;
                potionCountText.raycastTarget = false;
            }
            if (activeQuestSummaryText != null)
            {
                activeQuestSummaryText.margin = Vector4.zero;
                activeQuestSummaryText.enableAutoSizing = true;
                activeQuestSummaryText.fontSizeMin = 14f;
                activeQuestSummaryText.fontSizeMax = 32f;
                activeQuestSummaryText.textWrappingMode = TextWrappingModes.Normal;
                activeQuestSummaryText.raycastTarget = false;
            }
            if (quickPotionButton != null)
            {
                TMP_Text pText = quickPotionButton.GetComponentInChildren<TMP_Text>(true);
                if (pText != null)
                {
                    pText.margin = Vector4.zero;
                    pText.enableAutoSizing = true;
                    pText.fontSizeMin = 12f;
                    pText.fontSizeMax = 24f;
                    pText.raycastTarget = false;
                }
            }
        }

        #endregion

        #region Player Tracking

        private void LocatePlayer()
        {
            if (trackedPlayer == null)
            {
                trackedPlayer = FindAnyObjectByType<PlayerUnit>();
                if (trackedPlayer != null)
                {
                    trackedPlayer.OnHealthChanged -= HandleHealthChanged;
                    trackedPlayer.OnHealthChanged += HandleHealthChanged;
                }
            }
        }

        #endregion

        #region Refresh HUD

        /// <summary>
        /// Refreshes all elements of the persistent HUD.
        /// </summary>
        public void RefreshAllHUD()
        {
            LocatePlayer();

            // 1. Health Bar
            if (trackedPlayer != null)
            {
                UpdateHealthDisplay(trackedPlayer.CurrentHP, trackedPlayer.MaxHP);
            }

            // 2. Gold Counter & Potions
            if (InventoryManager.Instance != null)
            {
                UpdateGoldDisplay(InventoryManager.Instance.CurrentGold);
                UpdatePotionDisplay();
            }

            // 3. Quest Summary
            UpdateQuestSummaryText();
        }

        private void UpdateHealthDisplay(int currentHP, int maxHP)
        {
            if (healthSlider != null && maxHP > 0)
            {
                healthSlider.maxValue = maxHP;
                healthSlider.value = currentHP;
            }

            if (healthText != null)
            {
                healthText.text = $"HP: {currentHP} / {maxHP}";
            }
        }

        private void UpdateGoldDisplay(int goldAmount)
        {
            if (goldCounterText != null)
            {
                goldCounterText.text = $"{goldAmount} Gold";
            }
        }

        private void UpdatePotionDisplay()
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return;

            int count = (healthPotionItem != null) ? inventory.GetItemCount(healthPotionItem) : 0;

            if (potionCountText != null)
            {
                potionCountText.text = $"x{count}";
            }

            if (quickPotionButton != null)
            {
                quickPotionButton.interactable = count > 0;
            }
        }

        /// <summary>
        /// Synchronizes the active quest summary text tracker on the HUD.
        /// Can be called directly or supplied with a custom status message.
        /// </summary>
        public void UpdateQuestSummaryText(string customText = null)
        {
            if (activeQuestSummaryText == null) return;

            if (!string.IsNullOrEmpty(customText))
            {
                activeQuestSummaryText.text = customText;
                return;
            }

            // Query QuestManager for active quest
            QuestManager qm = QuestManager.Instance;
            if (qm != null)
            {
                QuestSO activeQuest = qm.GetActiveQuest();
                if (activeQuest != null)
                {
                    int current = qm.GetQuestProgress(activeQuest.QuestID);
                    activeQuestSummaryText.text = $"{activeQuest.QuestTitle}: {current} / {activeQuest.RequiredAmount}";
                    return;
                }
            }

            activeQuestSummaryText.text = "No Active Quests";
        }

        #endregion

        #region Hotbar Actions

        private void OnQuickPotionClicked()
        {
            LocatePlayer();
            if (trackedPlayer == null || healthPotionItem == null) return;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null && inventory.HasItem(healthPotionItem, 1))
            {
                inventory.UseItem(healthPotionItem, trackedPlayer);
                UpdatePotionDisplay();
            }
        }

        #endregion

        #region Event Listeners

        private void HandleHealthChanged(int current, int max)
        {
            UpdateHealthDisplay(current, max);
        }

        private void HandleGoldChanged(int newGold)
        {
            UpdateGoldDisplay(newGold);
        }

        private void HandleInventoryChanged()
        {
            UpdatePotionDisplay();
        }

        private void HandleQuestProgressUpdated(string questID, int current, int required)
        {
            QuestManager qm = QuestManager.Instance;
            QuestSO quest = qm != null ? qm.GetQuest(questID) : null;
            string title = quest != null ? quest.QuestTitle : questID;

            UpdateQuestSummaryText($"{title}: {current} / {required}");
        }

        private void HandleQuestStateUpdated(string questID, QuestState state)
        {
            QuestManager qm = QuestManager.Instance;
            QuestSO quest = qm != null ? qm.GetQuest(questID) : null;
            string title = quest != null ? quest.QuestTitle : questID;

            if (state == QuestState.Completed)
            {
                UpdateQuestSummaryText($"{title}: Completed!");
            }
            else if (state == QuestState.InProgress)
            {
                int current = qm != null ? qm.GetQuestProgress(questID) : 0;
                int req = quest != null ? quest.RequiredAmount : 1;
                UpdateQuestSummaryText($"{title}: {current} / {req}");
            }
            else
            {
                UpdateQuestSummaryText();
            }
        }

        #endregion
    }
}
