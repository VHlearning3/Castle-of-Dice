using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.UI;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// Decoupled bridge component that connects DialogueOption selection events to external game systems.
    /// Intercepts action tags in CombatDebuffTag or OptionText (such as '[ACTION_OPEN_SHOP]' or '[ACTION_ACCEPT_QUEST]')
    /// and invokes the corresponding UI controllers, quest activations, or economy managers.
    /// </summary>
    public class DialogueActionTrigger : MonoBehaviour
    {
        #region Constants & Action Tags

        public const string TAG_OPEN_SHOP = "[ACTION_OPEN_SHOP]";
        public const string TAG_ACCEPT_QUEST = "[ACTION_ACCEPT_QUEST]";
        public const string TAG_COMPLETE_QUEST = "[ACTION_COMPLETE_QUEST]";
        public const string TAG_CLOSE_DIALOGUE = "[ACTION_CLOSE_DIALOGUE]";

        #endregion

        #region Singleton / Static State

        private static DialogueActionTrigger instance;

        public static DialogueActionTrigger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DialogueActionTrigger>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("DialogueActionTrigger");
                        instance = go.AddComponent<DialogueActionTrigger>();
                        Debug.Log("[DialogueActionTrigger] Auto-created DialogueActionTrigger GameObject in scene.");
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        // Tracks quests where the player negotiated a bonus reward
        private static readonly HashSet<string> questsWithNegotiatedBonus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        }

        private void OnEnable()
        {
            DialogueController.OnOptionSelected += HandleOptionSelected;
        }

        private void OnDisable()
        {
            DialogueController.OnOptionSelected -= HandleOptionSelected;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Option Handling

        private void HandleOptionSelected(DialogueOption option)
        {
            if (option == null) return;

            string tag = option.CombatDebuffTag ?? string.Empty;
            string text = option.OptionText ?? string.Empty;

            // 1. Check for Shop Opening
            if (ContainsAction(tag, text, TAG_OPEN_SHOP) || ContainsAction(tag, text, "ACTION_OPEN_SHOP") || text.StartsWith("[Shop]", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteOpenShop();
                return;
            }

            // 2. Check for Quest Acceptance
            if (ContainsAction(tag, text, TAG_ACCEPT_QUEST) || ContainsAction(tag, text, "ACTION_ACCEPT_QUEST"))
            {
                ExecuteAcceptQuest(tag, text);
            }

            // 3. Check for Quest Completion
            if (ContainsAction(tag, text, TAG_COMPLETE_QUEST) || ContainsAction(tag, text, "ACTION_COMPLETE_QUEST"))
            {
                ExecuteCompleteQuest(tag, text);
            }

            // 4. Check for Explicit Close
            if (ContainsAction(tag, text, TAG_CLOSE_DIALOGUE) || ContainsAction(tag, text, "ACTION_CLOSE_DIALOGUE"))
            {
                DialogueController.Instance?.EndDialogue();
            }
        }

        private static bool ContainsAction(string tag, string text, string actionKey)
        {
            return tag.IndexOf(actionKey, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(actionKey, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Action Implementations

        private void ExecuteOpenShop()
        {
            Debug.Log("[DialogueActionTrigger] Intercepted Shop Action: Closing dialogue and opening Blacksmith Baldur's shop.");

            // Conclude dialogue session
            DialogueController.Instance?.EndDialogue();

            // Open Shop UI
            if (ShopUIController.Instance != null)
            {
                ShopUIController.Instance.OpenShop();
            }
            else
            {
                ShopUIController fallback = FindAnyObjectByType<ShopUIController>();
                if (fallback != null)
                {
                    fallback.OpenShop();
                }
                else
                {
                    Debug.LogError("[DialogueActionTrigger] Cannot open shop: ShopUIController instance not found in scene!");
                }
            }
        }

        private void ExecuteAcceptQuest(string tag, string text)
        {
            string questId = ExtractQuestId(tag, text, fallback: "quest_cellar_pests");
            bool hasBonus = tag.IndexOf(":bonus", StringComparison.OrdinalIgnoreCase) >= 0
                         || text.IndexOf(":bonus", StringComparison.OrdinalIgnoreCase) >= 0
                         || tag.IndexOf("BarnabyNegotiationBonus", StringComparison.OrdinalIgnoreCase) >= 0;

            if (hasBonus)
            {
                SetQuestBonusNegotiated(questId, true);
                Debug.Log($"[DialogueActionTrigger] Bonus reward recorded for quest: '{questId}'.");
            }

            if (QuestManager.Instance != null)
            {
                bool started = QuestManager.Instance.StartQuest(questId);
                if (!started)
                {
                    // If not registered under 'quest_cellar_pests', try 'CellarRats'
                    QuestManager.Instance.StartQuest("CellarRats");
                }
                Debug.Log($"[DialogueActionTrigger] Started Quest: '{questId}' (Bonus Negotiated: {hasBonus}).");
            }
            else
            {
                Debug.LogWarning($"[DialogueActionTrigger] QuestManager.Instance is null. Quest '{questId}' could not be accepted.");
            }
        }

        private void ExecuteCompleteQuest(string tag, string text)
        {
            string questId = ExtractQuestId(tag, text, fallback: "quest_cellar_pests");
            bool bonus = HasQuestBonusNegotiated(questId);

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.CompleteQuest(questId, grantedBonus: bonus);
                Debug.Log($"[DialogueActionTrigger] Completed Quest: '{questId}' with bonus = {bonus}.");
            }
        }

        private string ExtractQuestId(string tag, string text, string fallback)
        {
            // Expected format: "[ACTION_ACCEPT_QUEST:quest_id]" or "[ACTION_ACCEPT_QUEST:quest_id:bonus]"
            string source = tag.IndexOf(TAG_ACCEPT_QUEST, StringComparison.OrdinalIgnoreCase) >= 0 ? tag : text;
            int startIndex = source.IndexOf(TAG_ACCEPT_QUEST, StringComparison.OrdinalIgnoreCase);

            if (startIndex >= 0)
            {
                int colonIndex = source.IndexOf(':', startIndex);
                if (colonIndex >= 0)
                {
                    int endIndex = source.IndexOfAny(new[] { ':', ']', ' ' }, colonIndex + 1);
                    if (endIndex > colonIndex)
                    {
                        return source.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
                    }
                    else
                    {
                        return source.Substring(colonIndex + 1).Trim();
                    }
                }
            }

            return fallback;
        }

        #endregion

        #region Bonus Tracking API

        /// <summary>
        /// Marks whether a quest has a negotiated bonus reward unlocked via dialogue checks.
        /// </summary>
        public static void SetQuestBonusNegotiated(string questId, bool bonus)
        {
            if (string.IsNullOrEmpty(questId)) return;

            if (bonus)
            {
                questsWithNegotiatedBonus.Add(questId);
            }
            else
            {
                questsWithNegotiatedBonus.Remove(questId);
            }
        }

        /// <summary>
        /// Returns true if the player passed a negotiation check for this quest.
        /// </summary>
        public static bool HasQuestBonusNegotiated(string questId)
        {
            return !string.IsNullOrEmpty(questId) && questsWithNegotiatedBonus.Contains(questId);
        }

        #endregion
    }
}
