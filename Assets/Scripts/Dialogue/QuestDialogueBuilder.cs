using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// Builds the conversation a quest giver opens with once their quest has been accepted:
    /// a reminder while it is unfinished, a hand-in choice when it is done, and a thank-you afterwards.
    /// Before the quest is accepted the giver keeps their own intro dialogue.
    /// </summary>
    public static class QuestDialogueBuilder
    {
        /// <summary>Prefix of the player's hand-in choice.</summary>
        public const string TurnInPrefix = "[Turn in] ";

        /// <summary>
        /// Returns the opening node for a giver whose quest is in <paramref name="state"/>, or null when the
        /// giver's own intro dialogue should play (quest not accepted yet).
        /// </summary>
        /// <param name="quest">The giver's quest.</param>
        /// <param name="state">Current quest state.</param>
        /// <param name="progress">Current objective counter.</param>
        /// <param name="speaker">Name shown in the dialogue window.</param>
        /// <param name="sideOptions">Choices the giver always offers besides the quest (e.g. Baldur's shop), placed before [Leave].</param>
        /// <param name="createdNodes">Receives every node built, so the caller can destroy them later.</param>
        public static DialogueNodeSO BuildOpeningNode(
            QuestSO quest,
            QuestState state,
            int progress,
            string speaker,
            IReadOnlyList<DialogueOption> sideOptions,
            List<DialogueNodeSO> createdNodes)
        {
            if (quest == null || state == QuestState.NotStarted) return null;

            string name = string.IsNullOrWhiteSpace(speaker) ? quest.QuestGiverName : speaker;

            if (state == QuestState.Completed)
            {
                return CreateNode(name, Fallback(quest.CompletedText, "Thank you again for your help, friend."),
                    sideOptions, LeaveOption("[Leave] Take care."), createdNodes);
            }

            int required = Mathf.Max(1, quest.RequiredAmount);
            if (progress < required)
            {
                string reminder = Fallback(quest.InProgressText, $"How goes it? {quest.ObjectiveSummary}.");
                reminder += $"\n<color=#F1C40F>{quest.ObjectiveSummary}: {Mathf.Max(0, progress)}/{required}</color>";
                if (!string.IsNullOrEmpty(quest.ObjectiveLocation))
                {
                    reminder += $"\n<color=#A0AEC0>{quest.ObjectiveLocation}</color>";
                }

                return CreateNode(name, reminder, sideOptions, LeaveOption("[Leave] I'm still working on it."), createdNodes);
            }

            // Objective done: hand-in choice leads to the thank-you node
            DialogueNodeSO thanks = CreateNode(name, Fallback(quest.ThanksText, "You have my deepest thanks. Here is your reward."),
                sideOptions, LeaveOption("[Leave] Glad to help."), createdNodes);

            DialogueOption turnIn = new DialogueOption(
                TurnInPrefix + Fallback(quest.TurnInOptionText, "It's done."),
                thanks, false, 10, "", null,
                $"{DialogueActionTrigger.TAG_COMPLETE_QUEST.TrimEnd(']')}:{quest.QuestID}]");

            DialogueNodeSO ready = CreateNode(name, Fallback(quest.ReadyText, "You're back! Did you manage it?"),
                null, LeaveOption("[Leave] Not yet, I'll be back."), createdNodes);
            List<DialogueOption> readyOptions = new List<DialogueOption> { turnIn };
            AppendSideOptions(readyOptions, sideOptions);
            readyOptions.Add(LeaveOption("[Leave] Not yet, I'll be back."));
            ready.SetOptions(readyOptions);
            return ready;
        }

        private static DialogueNodeSO CreateNode(string speaker, string text, IReadOnlyList<DialogueOption> sideOptions,
            DialogueOption leave, List<DialogueNodeSO> createdNodes)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.name = $"QuestDialogue_{speaker}";
            node.hideFlags = HideFlags.DontSave;
            node.Initialize(speaker, text, null, false);

            List<DialogueOption> options = new List<DialogueOption>();
            AppendSideOptions(options, sideOptions);
            options.Add(leave);
            node.SetOptions(options);

            createdNodes?.Add(node);
            return node;
        }

        private static void AppendSideOptions(List<DialogueOption> options, IReadOnlyList<DialogueOption> sideOptions)
        {
            if (sideOptions == null) return;
            for (int i = 0; i < sideOptions.Count; i++)
            {
                if (sideOptions[i] != null) options.Add(sideOptions[i]);
            }
        }

        private static DialogueOption LeaveOption(string text)
        {
            return new DialogueOption(text, null, false, 10, "", null, DialogueActionTrigger.TAG_CLOSE_DIALOGUE);
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
