using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Interactive village non-player character (e.g. Barnaby the Innkeeper, Elder Othelia, Mirabel the Herbalist).
    /// Inherits from Interactable and initiates conversation with DialogueController upon player interaction.
    /// </summary>
    public class VillageNPC : Interactable
    {
        #region Serialized Fields

        [Header("NPC Identity")]
        [Tooltip("Display name of the village NPC (e.g., 'Barnaby', 'Elder Othelia', 'Mirabel').")]
        [SerializeField] private string npcName = "Village NPC";

        [Header("Dialogue")]
        [Tooltip("Starting dialogue node triggered when the player interacts with this NPC.")]
        [SerializeField] private DialogueNodeSO startingDialogueNode;

        #endregion

        #region Public Properties

        /// <summary>Display name of this NPC.</summary>
        public string NpcName => npcName;

        /// <summary>Starting conversation node.</summary>
        public DialogueNodeSO StartingDialogueNode
        {
            get => startingDialogueNode;
            set => startingDialogueNode = value;
        }

        #endregion

        #region Unity Lifecycle & Validation

        private void Reset()
        {
            interactionRadius = 3.0f;
            promptMessage = $"Talk to {npcName}";
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(promptMessage) || promptMessage == "Interact")
            {
                promptMessage = $"Talk to {npcName}";
            }
        }

        #endregion

        #region Interaction

        /// <summary>
        /// Initiates dialogue with this NPC via DialogueController.
        /// </summary>
        /// <param name="player">The player initiating the conversation.</param>
        public override void Interact(PlayerUnit player)
        {
            if (DialogueController.Instance == null)
            {
                Debug.LogError($"[VillageNPC] Cannot start dialogue with '{npcName}': DialogueController.Instance is null in the scene.");
                return;
            }

            if (startingDialogueNode == null)
            {
                Debug.LogWarning($"[VillageNPC] No starting DialogueNodeSO assigned to '{npcName}'.");
                return;
            }

            DialogueController.Instance.StartDialogue(startingDialogueNode);
        }

        #endregion
    }
}
