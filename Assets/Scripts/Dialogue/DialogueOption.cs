using System;
using UnityEngine;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// Represents an interactive player choice within a dialogue branch.
    /// Supports D20 skill check requirements, branching paths, and combat modifier tags.
    /// </summary>
    [Serializable]
    public class DialogueOption
    {
        #region Serialized Fields

        [Tooltip("The text displayed on the UI choice button.")]
        [SerializeField] private string optionText = "Continue...";

        [Tooltip("If true, selecting this option initiates a D20 skill check vs Target DC.")]
        [SerializeField] private bool requiresCheck = false;

        [Tooltip("Target Difficulty Class (DC) to meet or exceed (e.g., DC 10 Nature, DC 13 Persuasion/Honor, DC 16 Intimidation).")]
        [Range(1, 30)]
        [SerializeField] private int targetDC = 10;

        [Tooltip("Flavor or rule description shown in the UI (e.g. 'Persuade Barnaby for more gold [DC 13]').")]
        [SerializeField] private string skillCheckDescription = "Skill Check";

        [Tooltip("Next dialogue node loaded if no check was required or if the D20 check succeeds.")]
        [SerializeField] private DialogueNodeSO nextNodeSuccess;

        [Tooltip("Next dialogue node loaded if the D20 skill check fails.")]
        [SerializeField] private DialogueNodeSO nextNodeFailure;

        [Tooltip("Combat debuff tag applied to subsequent boss/enemy encounters upon success (e.g., 'CommanderArmorWeakened', '-2 AC').")]
        [SerializeField] private string combatDebuffTag = "";

        #endregion

        #region Public Properties

        /// <summary>Text displayed on UI choice button.</summary>
        public string OptionText => optionText;

        /// <summary>Whether a D20 roll is required to succeed.</summary>
        public bool RequiresCheck => requiresCheck;

        /// <summary>Target Difficulty Class (DC).</summary>
        public int TargetDC => targetDC;

        /// <summary>Skill check description displayed on hover or popup.</summary>
        public string SkillCheckDescription => skillCheckDescription;

        /// <summary>Target node upon success or normal continuation.</summary>
        public DialogueNodeSO NextNodeSuccess => nextNodeSuccess;

        /// <summary>Target node upon check failure.</summary>
        public DialogueNodeSO NextNodeFailure => nextNodeFailure;

        /// <summary>Identifier for combat debuffs triggered by this dialogue.</summary>
        public string CombatDebuffTag => combatDebuffTag;

        #endregion

        #region Constructors

        public DialogueOption()
        {
        }

        public DialogueOption(
            string text,
            DialogueNodeSO successNode,
            bool needsCheck = false,
            int dc = 10,
            string checkDesc = "",
            DialogueNodeSO failNode = null,
            string debuffTag = "")
        {
            optionText = text;
            nextNodeSuccess = successNode;
            requiresCheck = needsCheck;
            targetDC = dc;
            skillCheckDescription = checkDesc;
            nextNodeFailure = failNode;
            combatDebuffTag = debuffTag;
        }

        #endregion
    }
}
