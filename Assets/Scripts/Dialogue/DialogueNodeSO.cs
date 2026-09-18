using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// ScriptableObject representing a single narrative node in a conversation tree.
    /// Stores speaker information, dialogue prose, player choices, and terminal flags.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogueNode", menuName = "CastleOfDice/Dialogue/Node", order = 20)]
    public class DialogueNodeSO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Speaker Information")]
        [Tooltip("Display name of the speaking character (e.g., 'Baldur the Smith', 'Innkeeper Barnaby', 'Othelia', 'Mirabel').")]
        [SerializeField] private string speakerName = "Speaker";

        [Tooltip("Portrait sprite displayed in the dialogue window.")]
        [SerializeField] private Sprite speakerPortrait;

        [Header("Dialogue Content")]
        [Tooltip("Full text dialogue spoken by the character.")]
        [TextArea(3, 8)]
        [SerializeField] private string dialogueText = "Greetings, adventurer.";

        [Header("Player Choices")]
        [Tooltip("List of interactive choice options available to the player from this node.")]
        [SerializeField] private List<DialogueOption> options = new List<DialogueOption>();

        [Header("Flow Control")]
        [Tooltip("If true, choosing or reaching this node concludes the conversation.")]
        [SerializeField] private bool isExitNode = false;

        #endregion

        #region Public Properties

        /// <summary>Speaker display name.</summary>
        public string SpeakerName => speakerName;

        /// <summary>Speaker 2D portrait.</summary>
        public Sprite SpeakerPortrait => speakerPortrait;

        /// <summary>Spoken narrative content.</summary>
        public string DialogueText => dialogueText;

        /// <summary>Interactive choice options.</summary>
        public IReadOnlyList<DialogueOption> Options => options;

        /// <summary>Whether this node closes the dialogue interface.</summary>
        public bool IsExitNode => isExitNode;

        #endregion

        #region Editor Validation & Helpers

        /// <summary>
        /// Programmatic helper to add an option.
        /// </summary>
        public void AddOption(DialogueOption option)
        {
            if (option != null)
            {
                options.Add(option);
            }
        }

        #endregion
    }
}
