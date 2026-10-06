using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// The talk after an enemy surrenders (Vili, 2026-10-06). Only the Shadow Witch and the curse cultists yield.
    /// Once the fight is won (and any level-up window is closed) the hero decides their fate:
    /// - a cultist can be intimidated (gold and what they know), read with Insight (a tip about the Tower runes)
    ///   or let go (Heroic Inspiration);
    /// - the Witch can be spared (she will be at the throne; if she yielded to Queen Isolde's ring she keeps her
    ///   word for sure) or slain.
    /// </summary>
    public class SurrenderDialogues : MonoBehaviour
    {
        public const string CultistSpeaker = "Curse Cultist";
        public const string WitchSpeaker = "Shadow Witch";

        /// <summary>DC of the checks when questioning a cultist.</summary>
        public const int QuestionDC = 12;

        /// <summary>Gold a cultist hands over when intimidated / when the check fails.</summary>
        public const int IntimidatedGold = 20;
        public const int ScaredGold = 5;
        public const int InsightGold = 10;

        /// <summary>Story flag set when the Witch is slain after she yielded.</summary>
        public const string WitchSlainFlag = "MalakorSlainAfterYield";

        private static SurrenderDialogues instance;
        private static readonly List<EnemyUnit> pending = new List<EnemyUnit>(2);
        private static readonly Dictionary<DialogueNodeSO, Action> nodeEffects = new Dictionary<DialogueNodeSO, Action>();

        /// <summary>Enemies who surrendered in the running fight and still await the hero's decision.</summary>
        public static IReadOnlyList<EnemyUnit> Pending => pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            pending.Clear();
            nodeEffects.Clear();
            EnemyUnit.OnEnemyYielded -= HandleYielded;
            EnemyUnit.OnEnemyYielded += HandleYielded;
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            TurnManager.OnCombatEnded += HandleCombatEnded;
            DialogueController.OnDialogueStarted -= HandleNodeShown;
            DialogueController.OnDialogueStarted += HandleNodeShown;
            DialogueController.OnDialogueUpdated -= HandleNodeShown;
            DialogueController.OnDialogueUpdated += HandleNodeShown;
        }

        private static void HandleYielded(EnemyUnit enemy)
        {
            if (enemy != null && !pending.Contains(enemy)) pending.Add(enemy);
        }

        private static void HandleCombatEnded(bool victory)
        {
            if (!victory || pending.Count == 0)
            {
                pending.Clear();
                return;
            }
            if (!Application.isPlaying) return;

            if (instance == null)
            {
                GameObject host = new GameObject("SurrenderDialogues");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<SurrenderDialogues>();
            }
            instance.StartCoroutine(instance.TalkWhenReady());
        }

        private static void HandleNodeShown(DialogueNodeSO node)
        {
            if (node == null || !nodeEffects.TryGetValue(node, out Action effect)) return;
            nodeEffects.Remove(node);
            effect?.Invoke();
        }

        private IEnumerator TalkWhenReady()
        {
            yield return new WaitForSeconds(1.2f);

            while (pending.Count > 0)
            {
                // Wait for the victory moment: no dice, no level-up window, no other conversation
                float waited = 0f;
                while (waited < 30f && !IsQuiet())
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                EnemyUnit enemy = pending[0];
                pending.RemoveAt(0);
                DialogueNodeSO start = enemy is ShadowMageMalakorBoss ? BuildWitchTalk() : BuildCultistTalk(enemy != null ? enemy.UnitName : CultistSpeaker);

                DialogueController dialogue = DialogueController.Instance;
                if (dialogue == null) continue;
                dialogue.StartDialogue(start, FindAnyObjectByType<PlayerUnit>());
                while (dialogue.IsInDialogue) yield return null;
            }
        }

        private static bool IsQuiet()
        {
            if (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive) return false;
            if (DiceUIController.Instance != null && DiceUIController.Instance.IsDisplaying) return false;
            if (LevelUpUIController.Instance != null && LevelUpUIController.Instance.IsModalOpen) return false;
            if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue) return false;
            return true;
        }

        #region Cultist

        /// <summary>The questioning of a surrendered cultist.</summary>
        public static DialogueNodeSO BuildCultistTalk(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) speaker = CultistSpeaker;

            DialogueNodeSO talked = Line(speaker,
                "\"The King's stone guard pays us in old crowns to keep travellers off this road. The Witch? She tried to undo the curse once. " +
                "The guard dragged the Queen's ring back from Oakhaven and sealed it in the Tower so she never could.\" " +
                "He empties his purse into your hand and runs.");
            nodeEffects[talked] = () => GiveGold(IntimidatedGold);

            DialogueNodeSO scared = Line(speaker,
                "\"I know nothing, nothing!\" The cultist flings a thin purse at your feet and bolts into the trees.");
            nodeEffects[scared] = () => GiveGold(ScaredGold);

            DialogueNodeSO honest = Line(speaker,
                "\"...Hunger. The cult feeds us.\" Ashamed, he presses a few coins and a crumpled note into your hand: " +
                "\"The Tower answers to SUN, MOON and STAR.\" Then he is gone.");
            nodeEffects[honest] = () => GiveGold(InsightGold);

            DialogueNodeSO lied = Line(speaker,
                "\"For the glory of the curse!\" he lies, badly, and runs before you can ask again.");

            DialogueNodeSO freed = Line(speaker,
                "The cultist stares at you, then bows low. \"I will not forget this.\" He walks away down the road toward Oakhaven.");
            nodeEffects[freed] = () => HeroicInspiration.Grant("you let a beaten foe go free");

            DialogueNodeSO intro = ScriptableObject.CreateInstance<DialogueNodeSO>();
            intro.hideFlags = HideFlags.DontSave;
            intro.Initialize(speaker, "\"Mercy! Mercy, I yield! I only chant the words they taught me. Ask me anything, just let me live.\"");
            intro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Intimidation] Talk. Who sent you to this road?", talked, true, QuestionDC, "Intimidation / Charisma Check", scared),
                new DialogueOption("[Insight] You are no zealot. Why are you really here?", honest, true, QuestionDC, "Insight / Wisdom Check", lied),
                new DialogueOption("[Mercy] Go home. If I see you on this road again, I won't ask.", freed)
            });
            return intro;
        }

        #endregion

        #region Shadow Witch

        /// <summary>The Witch's surrender: spare her or slay her.</summary>
        public static DialogueNodeSO BuildWitchTalk()
        {
            bool ring = StoryFlags.Has(ShadowMageMalakorBoss.YieldedToRingFlag);

            DialogueNodeSO spared = Line(WitchSpeaker, ring
                ? "\"I swear it on her ring: when you face him, I will be there, and I will finish what I should have finished long ago.\" She folds into the shadows."
                : "\"Then I will be there when you face him. Whether I help you... we shall both find out.\" She folds into the shadows.");
            nodeEffects[spared] = () => StoryFlags.Set(BossChoices.MalakorSparedFlag);

            DialogueNodeSO slain = Line(WitchSpeaker,
                "The Witch does not raise her hands. Your blow falls, and she crumbles into shadow and dust.");
            nodeEffects[slain] = () => StoryFlags.Set(WitchSlainFlag);

            DialogueNodeSO intro = ScriptableObject.CreateInstance<DialogueNodeSO>();
            intro.hideFlags = HideFlags.DontSave;
            intro.Initialize(WitchSpeaker, ring
                ? "\"That ring... Isolde's ring. Put it away. I yield. She trusted me once, and I wrote the curse that took her king. Do what you came to do, or let me help undo it.\""
                : "\"Enough... I yield. Kill me if you must, but I wrote the binding, and I am the only one who can loosen it.\"");
            intro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Spare] Live. Be there when I face the King, and undo what you did.", spared),
                new DialogueOption("[Slay] Your curse ends with you.", slain)
            });
            return intro;
        }

        #endregion

        #region Helpers

        private static DialogueNodeSO Line(string speaker, string text)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.hideFlags = HideFlags.DontSave;
            node.Initialize(speaker, text);
            node.SetOptions(new List<DialogueOption> { new DialogueOption("[Continue]", null) });
            return node;
        }

        private static void GiveGold(int amount)
        {
            if (amount <= 0 || InventoryManager.Instance == null) return;
            InventoryManager.Instance.AddGold(amount);
            LockpickMinigameUI.ShowToast($"+{amount} gold");
        }

        #endregion
    }
}
