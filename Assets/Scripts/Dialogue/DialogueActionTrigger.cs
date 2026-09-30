using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
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
                    instance = FindAnyObjectByType<DialogueActionTrigger>(FindObjectsInactive.Include);
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

            // 1. Check for Shop / Blacksmith Opening
            if (ContainsAction(tag, text, TAG_OPEN_SHOP) 
                || ContainsAction(tag, text, "ACTION_OPEN_SHOP") 
                || ContainsAction(tag, text, "[ACTION_OPEN_BLACKSMITH]")
                || ContainsAction(tag, text, "ACTION_OPEN_BLACKSMITH")
                || text.IndexOf("[Blacksmith]", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("[Shop]", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Show me what you have for sale", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Show me your weapons", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 4. Check for Explicit Dialogue Close
            if (ContainsAction(tag, text, TAG_CLOSE_DIALOGUE) || ContainsAction(tag, text, "ACTION_CLOSE_DIALOGUE") || text.StartsWith("[Exit]", StringComparison.OrdinalIgnoreCase))
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

        public void ExecuteOpenShop()
        {
            Debug.Log("[DialogueActionTrigger] Intercepted Shop/Blacksmith Action: Closing dialogue and opening Blacksmith Baldur's shop.");

            // Conclude dialogue session cleanly
            DialogueController.Instance?.EndDialogue();

            // Open Shop UI
            ShopUIController shopUI = ShopUIController.Instance;
            if (shopUI != null)
            {
                shopUI.OpenShop();
            }
            else
            {
                ShopUIController fallback = FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
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

            QuestManager quests = QuestManager.Instance;
            if (quests == null)
            {
                Debug.LogWarning($"[DialogueActionTrigger] QuestManager.Instance is null. Quest '{questId}' could not be accepted.");
                return;
            }

            // A quest that is already running or done keeps the terms it was accepted with
            if (quests.GetQuestState(questId) == QuestState.NotStarted && hasBonus)
            {
                quests.SetNegotiatedBonus(questId, true);
                Debug.Log($"[DialogueActionTrigger] Bonus reward recorded for quest: '{questId}'.");
            }

            bool started = quests.StartQuest(questId);
            Debug.Log($"[DialogueActionTrigger] Accept quest '{questId}': started = {started} (Bonus Negotiated: {hasBonus}).");

            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        private void ExecuteCompleteQuest(string tag, string text)
        {
            string questId = ExtractQuestId(tag, text, fallback: "quest_cellar_pests");

            QuestManager quests = QuestManager.Instance;
            if (quests != null)
            {
                bool completed = quests.CompleteQuest(questId);
                Debug.Log($"[DialogueActionTrigger] Complete quest '{questId}': completed = {completed} (bonus = {quests.HasNegotiatedBonus(questId)}).");
            }

            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        /// <summary>
        /// Reads the quest ID from "[ACTION_ACCEPT_QUEST:quest_id]" / "[ACTION_COMPLETE_QUEST:quest_id(:bonus)]"
        /// in the option's tag (or, failing that, its text). Returns <paramref name="fallback"/> when none is given.
        /// </summary>
        public static string ExtractQuestId(string tag, string text, string fallback)
        {
            string id = FindQuestIdIn(tag);
            if (string.IsNullOrEmpty(id)) id = FindQuestIdIn(text);
            return string.IsNullOrEmpty(id) ? fallback : id;
        }

        private static string FindQuestIdIn(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;

            int keyIndex = source.IndexOf("ACTION_ACCEPT_QUEST:", StringComparison.OrdinalIgnoreCase);
            int keyLength = "ACTION_ACCEPT_QUEST:".Length;
            if (keyIndex < 0)
            {
                keyIndex = source.IndexOf("ACTION_COMPLETE_QUEST:", StringComparison.OrdinalIgnoreCase);
                keyLength = "ACTION_COMPLETE_QUEST:".Length;
            }
            if (keyIndex < 0) return null;

            int start = keyIndex + keyLength;
            int end = source.IndexOfAny(QuestIdTerminators, start);
            string id = (end >= 0 ? source.Substring(start, end - start) : source.Substring(start)).Trim();
            return id.Length > 0 ? id : null;
        }

        private static readonly char[] QuestIdTerminators = { ':', ']', ' ' };

        #endregion

        #region Bonus Tracking API

        /// <summary>
        /// Marks whether a quest has a negotiated bonus reward unlocked via dialogue checks (stored and saved by QuestManager).
        /// </summary>
        public static void SetQuestBonusNegotiated(string questId, bool bonus)
        {
            QuestManager.Instance?.SetNegotiatedBonus(questId, bonus);
        }

        /// <summary>
        /// Returns true if the player passed a negotiation check for this quest.
        /// </summary>
        public static bool HasQuestBonusNegotiated(string questId)
        {
            QuestManager quests = QuestManager.Instance;
            return quests != null && quests.HasNegotiatedBonus(questId);
        }

        #endregion
    }
}
