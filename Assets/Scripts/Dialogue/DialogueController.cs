using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;

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

        // Stores unlocked combat debuff tags (e.g., "CommanderArmorWeakened", "-2 AC")
        private readonly HashSet<string> registeredCombatDebuffs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Public Properties

        /// <summary>Currently active dialogue node.</summary>
        public DialogueNodeSO CurrentNode => currentNode;

        /// <summary>Whether a conversation is currently active.</summary>
        public bool IsInDialogue => isInDialogue;

        /// <summary>Read-only collection of active combat debuff tags gained from dialogues.</summary>
        public IReadOnlyCollection<string> RegisteredCombatDebuffs => registeredCombatDebuffs;

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
        public void StartDialogue(DialogueNodeSO startingNode, PlayerUnit player = null)
        {
            if (startingNode == null)
            {
                Debug.LogWarning("[DialogueController] Cannot start dialogue: Starting node is null.");
                return;
            }

            activePlayer = player != null ? player : FindAnyObjectByType<PlayerUnit>();
            currentNode = startingNode;
            isInDialogue = true;

            // Transition game mode to dialogue to halt exploration movement
            GameManager.Instance?.SetMode(GamePlayMode.Dialogue);

            Debug.Log($"[DialogueController] Started dialogue with {currentNode.SpeakerName}: \"{currentNode.DialogueText}\"");
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
            if (!isInDialogue || option == null) return;

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

                DiceResult rollResult = DiceSystem.RollD20(bonus, option.TargetDC, advantage);
                Debug.Log($"[DialogueController] Skill Check for '{option.SkillCheckDescription}' vs DC {option.TargetDC}: {rollResult}");

                OnSkillCheckRolled?.Invoke(rollResult, rollResult.isSuccess);

                if (rollResult.isSuccess)
                {
                    // Register combat debuff tag if present
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

        private void AdvanceToNode(DialogueNodeSO nextNode)
        {
            if (nextNode == null || nextNode.IsExitNode)
            {
                EndDialogue();
                return;
            }

            currentNode = nextNode;
            Debug.Log($"[DialogueController] Advanced to: {currentNode.SpeakerName} - \"{currentNode.DialogueText}\"");
            OnDialogueUpdated?.Invoke(currentNode);
        }

        /// <summary>
        /// Ends the current dialogue session and closes any active conversation UI.
        /// </summary>
        public void EndDialogue()
        {
            if (!isInDialogue) return;

            isInDialogue = false;
            currentNode = null;
            activePlayer = null;

            Debug.Log("[DialogueController] Dialogue session ended.");
            OnDialogueEnded?.Invoke();

            // Restore exploration mode if currently in dialogue mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GamePlayMode.Dialogue)
            {
                GameManager.Instance.SetMode(GamePlayMode.Exploration);
            }
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
