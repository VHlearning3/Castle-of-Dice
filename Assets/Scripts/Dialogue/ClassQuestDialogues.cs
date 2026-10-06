using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// The village lines that hand out each hero's own quest (Vili, 2026-10-06): Baldur tells the knight about
    /// the King's crated blades, Othelia reads the Witch's catalogue to the mage, and Barnaby whispers to the
    /// rogue about the smugglers' strongbox. The choice shows only for that hero, only at that villager and
    /// only until the quest is taken or done.
    /// </summary>
    public static class ClassQuestDialogues
    {
        private struct Lines
        {
            public string Npc;
            public string Speaker;
            public string Ask;
            public string Answer;
            public string Accept;
            public string Farewell;
        }

        private static readonly Dictionary<CharacterClassType, Lines> lines = new Dictionary<CharacterClassType, Lines>
        {
            [CharacterClassType.Warrior] = new Lines
            {
                Npc = "Baldur",
                Speaker = "Baldur the Smith",
                Ask = "[Warrior] You know steel, smith. Where would a knight find a blade worth carrying?",
                Answer = "Ha! A real knight at my anvil. Listen: before the stone came, the King kept his finest blades in the Castle Hall, " +
                         "crated up in the north-east corner and nailed shut with oak a hand thick. No lock, no key. It takes a knight's shoulder " +
                         "to break those boards. Do it, and you'll carry an edge no forge of mine can match.",
                Accept = "[Accept] I'll break them open.",
                Farewell = "Mind the splinters. And bring that steel back past my forge so I can look at it!"
            },
            [CharacterClassType.Mage] = new Lines
            {
                Npc = "Othelia",
                Speaker = "Elder Othelia",
                Ask = "[Mage] Your family book has a list in the margin. Is that a library catalogue?",
                Answer = "Sharp eyes, Scholar. Queen Isolde copied it the night she fled: the order the Witch kept her five great tomes in. " +
                         "Sun, Moon, Star, the Mountain's Bone, and the Dark last of all. Isolde wrote that the Library's great shelves open " +
                         "for anyone who puts those tomes back in that order... but only a true scholar can read the ward that holds them.",
                Accept = "[Accept] I'll find those shelves.",
                Farewell = "Then may the old ink guide you, Scholar."
            },
            [CharacterClassType.Rogue] = new Lines
            {
                Npc = "Barnaby",
                Speaker = "Innkeeper Barnaby",
                Ask = "[Rogue] (Lean on the bar and lower your voice.) Heard any whispers about the forest gate?",
                Answer = "Hah, an elf who listens. Smugglers used to run a trail behind the locked gate on the Forest Path, right up to the " +
                         "Library's back wall. When the dead started walking they dropped a strongbox somewhere on that trail and never came " +
                         "back for it. Nobody's had fingers nimble enough for that gate lock since. Yours look nimble.",
                Accept = "[Accept] Then I'll see what they left behind.",
                Farewell = "Keep it quiet. And buy a round when you're rich."
            }
        };

        /// <summary>
        /// The quest choice <paramref name="hero"/> gets from the villager named <paramref name="npcName"/>, or null.
        /// Created nodes are added to <paramref name="createdNodes"/> so the caller can destroy them.
        /// </summary>
        public static DialogueOption BuildOption(string npcName, PlayerUnit hero, List<DialogueNodeSO> createdNodes)
        {
            ClassQuests.Definition def = ClassQuests.For(hero);
            if (def == null || string.IsNullOrEmpty(npcName)) return null;
            if (!lines.TryGetValue(def.Class, out Lines l)) return null;
            if (npcName.IndexOf(l.Npc, StringComparison.OrdinalIgnoreCase) < 0) return null;
            if (!IsOffered(def)) return null;

            DialogueNodeSO farewell = Node(l.Speaker, l.Farewell, createdNodes);
            farewell.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] Farewell.", null, false, 10, "", null, DialogueActionTrigger.TAG_CLOSE_DIALOGUE)
            });

            DialogueNodeSO answer = Node(l.Speaker, l.Answer, createdNodes);
            answer.SetOptions(new List<DialogueOption>
            {
                new DialogueOption(l.Accept, farewell, false, 10, "", null, $"[ACTION_ACCEPT_QUEST:{def.QuestId}]"),
                new DialogueOption("[Leave] Maybe later.", null, false, 10, "", null, DialogueActionTrigger.TAG_CLOSE_DIALOGUE)
            });

            return new DialogueOption(l.Ask, answer);
        }

        /// <summary>The villager who hands out a class's quest (tests and the journal).</summary>
        public static string GiverOf(CharacterClassType cls) => lines.TryGetValue(cls, out Lines l) ? l.Npc : null;

        /// <summary>
        /// Returns a copy of <paramref name="node"/> with <paramref name="extra"/> placed before its exit
        /// choices, so the quest choice shows on the opening line only.
        /// </summary>
        public static DialogueNodeSO WithOption(DialogueNodeSO node, DialogueOption extra, List<DialogueNodeSO> createdNodes)
        {
            if (node == null || extra == null) return node;
            DialogueNodeSO copy = ScriptableObject.CreateInstance<DialogueNodeSO>();
            copy.name = node.name + "_WithClassQuest";
            copy.hideFlags = HideFlags.DontSave;
            copy.Initialize(node.SpeakerName, node.DialogueText, node.SpeakerPortrait, node.IsExitNode);
            copy.SetOptions(DialogueController.ComposeOptions(node, new List<DialogueOption> { extra }));
            createdNodes?.Add(copy);
            return copy;
        }

        private static bool IsOffered(ClassQuests.Definition def)
        {
            if (ClassQuests.IsRewarded(def)) return false;
            QuestManager quests = QuestManager.Instance;
            return quests == null || quests.GetQuestState(def.QuestId) == QuestState.NotStarted;
        }

        private static DialogueNodeSO Node(string speaker, string text, List<DialogueNodeSO> createdNodes)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.name = "ClassQuest_" + speaker;
            node.hideFlags = HideFlags.DontSave;
            node.Initialize(speaker, text, null, false);
            createdNodes?.Add(node);
            return node;
        }
    }
}
