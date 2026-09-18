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
            if (trackedPlayer != null)
            {
                trackedPlayer.OnHealthChanged += HandleHealthChanged;
            }
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

        private void Start()
        {
            RefreshAllHUD();
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

            // 2. Gold Counter
            if (InventoryManager.Instance != null)
            {
                UpdateGoldDisplay(InventoryManager.Instance.CurrentGold);
                UpdatePotionDisplay();
            }
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
            if (activeQuestSummaryText != null)
            {
                activeQuestSummaryText.text = $"{questID}: {current} / {required}";
            }
        }

        private void HandleQuestStateUpdated(string questID, QuestState state)
        {
            if (activeQuestSummaryText != null)
            {
                if (state == QuestState.Completed)
                {
                    activeQuestSummaryText.text = $"{questID}: Completed!";
                }
                else if (state == QuestState.InProgress)
                {
                    activeQuestSummaryText.text = $"{questID}: Active";
                }
            }
        }

        #endregion
    }
}
