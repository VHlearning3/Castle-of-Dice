using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// Singleton manager orchestrating narrative dialogues and D20 skill checks.
    /// Handles branch selection, dice check resolution, dialogue progression events,
    /// and registers combat debuffs triggered through conversation (e.g. Cursed Commander armor weakness).
    /// </summary>
    public class DialogueController : MonoBehaviour
    {
        #region Singleton

        private static DialogueController instance;

        public static DialogueController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DialogueController>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("DialogueController");
                        instance = go.AddComponent<DialogueController>();
                        Debug.Log("[DialogueController] Auto-created DialogueController GameObject in scene.");
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Private State

        private DialogueNodeSO currentNode;
        private PlayerUnit activePlayer;
        private bool isInDialogue = false;
        private bool isResolvingCheck = false;

        // Stores unlocked combat debuff tags (e.g., "CommanderArmorWeakened", "-2 AC")
        private readonly HashSet<string> registeredCombatDebuffs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Choices the speaker adds to every node of this conversation (e.g. a villager's [Fight] option)
        private readonly List<DialogueOption> sessionOptions = new List<DialogueOption>();

        #endregion

        #region Public Properties

        /// <summary>Currently active dialogue node.</summary>
        public DialogueNodeSO CurrentNode => currentNode;

        /// <summary>Whether a conversation is currently active.</summary>
        public bool IsInDialogue => isInDialogue;

        /// <summary>Read-only collection of active combat debuff tags gained from dialogues.</summary>
        public IReadOnlyCollection<string> RegisteredCombatDebuffs => registeredCombatDebuffs;

        /// <summary>Choices added to every node of the current conversation by the speaker.</summary>
        public IReadOnlyList<DialogueOption> SessionOptions => sessionOptions;

        #endregion

        #region Events

        /// <summary>Fired when a dialogue sequence begins with a starting node.</summary>
        public static event Action<DialogueNodeSO> OnDialogueStarted;

        /// <summary>Fired when the conversation advances to a new node.</summary>
        public static event Action<DialogueNodeSO> OnDialogueUpdated;

        /// <summary>Fired when conversation concludes.</summary>
        public static event Action OnDialogueEnded;

        /// <summary>Fired when a D20 skill check is rolled during dialogue: (result, isSuccess).</summary>
        public static event Action<DiceResult, bool> OnSkillCheckRolled;

        /// <summary>Fired when an interactive dialogue choice is selected by the player.</summary>
        public static event Action<DialogueOption> OnOptionSelected;

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

            // Pre-warm DialogueActionTrigger listener for dialogue action tags
            _ = DialogueActionTrigger.Instance;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Dialogue Flow

        /// <summary>
        /// Begins a dialogue interaction with the specified starting node.
        /// </summary>
        /// <param name="startingNode">The root node of the conversation.</param>
        /// <param name="player">Optional player reference used for skill check bonus calculations.</param>
        /// <param name="extraOptions">Optional choices shown on every node of this conversation, before its exit choices.</param>
        public void StartDialogue(DialogueNodeSO startingNode, PlayerUnit player = null, IReadOnlyList<DialogueOption> extraOptions = null)
        {
            if (startingNode == null)
            {
                Debug.LogWarning("[DialogueController] Cannot start dialogue: Starting node is null.");
                return;
            }

            sessionOptions.Clear();
            if (extraOptions != null)
            {
                for (int i = 0; i < extraOptions.Count; i++)
                {
                    if (extraOptions[i] != null) sessionOptions.Add(extraOptions[i]);
                }
            }

            activePlayer = player != null ? player : FindAnyObjectByType<PlayerUnit>();
            currentNode = startingNode;
            isInDialogue = true;

            // Transition game mode to dialogue to halt exploration movement
            GameManager.Instance?.SetMode(GamePlayMode.Dialogue);

            Debug.Log($"[DialogueController] Started dialogue with {currentNode.SpeakerName}: \"{currentNode.DialogueText}\"");

            // Guarantee Dialogue UI is active and displaying even if panel GameObject starts inactive in scene
            DialogueUIController ui = DialogueUIController.Instance ?? FindAnyObjectByType<DialogueUIController>(FindObjectsInactive.Include);
            if (ui != null)
            {
                if (!ui.gameObject.activeSelf)
                {
                    ui.gameObject.SetActive(true);
                }
                ui.DisplayDialogueNode(currentNode);
            }

            OnDialogueStarted?.Invoke(currentNode);

            if (currentNode.IsExitNode)
            {
                EndDialogue();
            }
        }

        /// <summary>
        /// Selects an interactive choice option from the current dialogue node.
        /// Resolves D20 skill checks if required and branches to the appropriate next node.
        /// </summary>
        public void SelectOption(DialogueOption option)
        {
            if (!isInDialogue || option == null || isResolvingCheck) return;

            OnOptionSelected?.Invoke(option);

            if (option.RequiresCheck)
            {
                // Calculate bonus from active player's attribute modifier
                int bonus = activePlayer != null ? activePlayer.PrimaryAttributeBonus : 2;
                AdvantageType advantage = AdvantageType.None;

                // Rogue lockpicking or persuasion advantages if applicable
                if (activePlayer != null && activePlayer.CharacterClass?.ClassType == CharacterClassType.Rogue
                    && option.SkillCheckDescription.IndexOf("Lockpick", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    advantage = AdvantageType.Advantage;
                }

                StartCoroutine(ResolveSkillCheckRoutine(option, bonus, advantage));
            }
            else
            {
                // Apply debuff tag if defined on guaranteed choices
                if (!string.IsNullOrEmpty(option.CombatDebuffTag))
                {
                    RegisterCombatDebuff(option.CombatDebuffTag);
                }

                AdvanceToNode(option.NextNodeSuccess);
            }
        }

        private System.Collections.IEnumerator ResolveSkillCheckRoutine(DialogueOption option, int bonus, AdvantageType advantage)
        {
            isResolvingCheck = true;

            // 1. Hide choice buttons immediately and temporarily fade dialogue window so D20 modal is completely unobstructed
            DialogueUIController ui = DialogueUIController.Instance;
            if (ui != null)
            {
                ui.HideChoiceButtons();
                ui.SetDialogueVisible(false);
            }

            // 2. Roll D20 (a failure can be rerolled with the Rune of Reroll; the final result decides the branch)
            bool resolved = false;
            DiceResult rollResult = default;
            DiceResult firstRoll = RerollableRoll.Roll(bonus, option.TargetDC, advantage, final =>
            {
                rollResult = final;
                resolved = true;
            }, option.SkillCheckDescription);
            Debug.Log($"[DialogueController] Skill Check for '{option.SkillCheckDescription}' vs DC {option.TargetDC}: {firstRoll}");

            // 3. Wait for the reroll choice (if any) and until the dice roll modal is dismissed
            while (!resolved)
            {
                yield return null;
            }

            OnSkillCheckRolled?.Invoke(rollResult, rollResult.isSuccess);

            if (DiceUIController.Instance != null)
            {
                while (DiceUIController.Instance != null && DiceUIController.Instance.IsDisplaying)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            isResolvingCheck = false;

            // 4. Branch to success or failure node and reveal dialogue UI with the NPC's response
            if (rollResult.isSuccess)
            {
                if (!string.IsNullOrEmpty(option.CombatDebuffTag))
                {
                    RegisterCombatDebuff(option.CombatDebuffTag);
                }

                AdvanceToNode(option.NextNodeSuccess);
            }
            else
            {
                AdvanceToNode(option.NextNodeFailure);
            }
        }

        private void AdvanceToNode(DialogueNodeSO nextNode)
        {
            if (nextNode == null || nextNode.IsExitNode)
            {
                EndDialogue();
                return;
            }

            currentNode = nextNode;
            Debug.Log($"[DialogueController] Advanced to: {currentNode.SpeakerName} - \"{currentNode.DialogueText}\"");

            DialogueUIController ui = DialogueUIController.Instance ?? FindAnyObjectByType<DialogueUIController>(FindObjectsInactive.Include);
            if (ui != null)
            {
                if (!ui.gameObject.activeSelf)
                {
                    ui.gameObject.SetActive(true);
                }
                ui.DisplayDialogueNode(currentNode);
            }

            OnDialogueUpdated?.Invoke(currentNode);
        }

        /// <summary>
        /// Ends the current dialogue session and closes any active conversation UI.
        /// </summary>
        public void EndDialogue()
        {
            if (!isInDialogue) return;

            isInDialogue = false;
            isResolvingCheck = false;
            currentNode = null;
            activePlayer = null;
            sessionOptions.Clear();

            Debug.Log("[DialogueController] Dialogue session ended.");

            DialogueUIController ui = DialogueUIController.Instance ?? FindAnyObjectByType<DialogueUIController>(FindObjectsInactive.Include);
            if (ui != null)
            {
                ui.HideDialogue();
            }

            OnDialogueEnded?.Invoke();

            // Restore exploration mode if currently in dialogue mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GamePlayMode.Dialogue)
            {
                GameManager.Instance.SetMode(GamePlayMode.Exploration);
            }
        }

        /// <summary>
        /// Returns the choices to show for <paramref name="node"/>: its own options with the conversation's
        /// extra options inserted before the trailing exit choices. Nodes without options stay empty so the
        /// UI keeps its Continue button.
        /// </summary>
        public static List<DialogueOption> ComposeOptions(DialogueNodeSO node, IReadOnlyList<DialogueOption> extraOptions)
        {
            List<DialogueOption> result = new List<DialogueOption>();
            if (node == null || node.Options == null) return result;

            IReadOnlyList<DialogueOption> own = node.Options;
            for (int i = 0; i < own.Count; i++)
            {
                if (own[i] != null) result.Add(own[i]);
            }

            if (result.Count == 0 || extraOptions == null) return result;

            int insertAt = result.Count;
            while (insertAt > 0 && IsExitOption(result[insertAt - 1]))
            {
                insertAt--;
            }

            for (int i = 0; i < extraOptions.Count; i++)
            {
                DialogueOption extra = extraOptions[i];
                if (extra == null || result.Contains(extra)) continue;
                result.Insert(insertAt, extra);
                insertAt++;
            }

            return result;
        }

        /// <summary>
        /// Whether a choice leaves the conversation ([Exit] / [Leave] / [Poistu] or the close-dialogue action).
        /// </summary>
        public static bool IsExitOption(DialogueOption option)
        {
            if (option == null) return false;

            string text = option.OptionText ?? string.Empty;
            string tag = option.CombatDebuffTag ?? string.Empty;
            return text.StartsWith("[Exit]", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("[Leave]", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("[Poistu", StringComparison.OrdinalIgnoreCase)
                || tag.IndexOf("ACTION_CLOSE_DIALOGUE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Combat Debuff Integration

        /// <summary>
        /// Registers a narrative combat modifier/debuff (e.g. "CommanderArmorWeakened").
        /// </summary>
        public void RegisterCombatDebuff(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            if (tag.StartsWith("[ACTION_", StringComparison.OrdinalIgnoreCase)) return;

            registeredCombatDebuffs.Add(tag);
            Debug.Log($"[DialogueController] Combat debuff unlocked: '{tag}'.");
        }

        /// <summary>
        /// Checks whether a specific combat debuff was unlocked via previous dialogue checks.
        /// </summary>
        public bool HasCombatDebuff(string tag)
        {
            return !string.IsNullOrEmpty(tag) && registeredCombatDebuffs.Contains(tag);
        }

        /// <summary>
        /// Consumes a debuff tag when applying its effect to an encounter or boss.
        /// </summary>
        public bool ConsumeCombatDebuff(string tag)
        {
            if (HasCombatDebuff(tag))
            {
                registeredCombatDebuffs.Remove(tag);
                Debug.Log($"[DialogueController] Combat debuff consumed: '{tag}'.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all stored dialogue debuff tags.
        /// </summary>
        public void ClearAllCombatDebuffs()
        {
            registeredCombatDebuffs.Clear();
        }

        #endregion
    }
}
