using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Interactive village non-player character (e.g. Blacksmith Baldur, Innkeeper Barnaby, Elder Othelia).
    /// Inherits from Interactable and initiates conversation with DialogueController upon player interaction.
    /// Includes intelligent procedural fallback trees for Baldur and Barnaby if ScriptableObjects are unassigned.
    /// </summary>
    public class VillageNPC : Interactable
    {
        #region Serialized Fields

        [Header("NPC Identity")]
        [Tooltip("Display name of the village NPC (e.g., 'Baldur the Smith', 'Barnaby', 'Elder Othelia').")]
        [SerializeField] private string npcName = "Village NPC";

        [Header("Blacksmith Role")]
        [Tooltip("If checked, this NPC functions as the village Blacksmith (Baldur). Enables blacksmith dialogue and shop interaction.")]
        [SerializeField] private bool isBlacksmith = false;

        [Tooltip("If checked, clicking this NPC directly opens the Blacksmith shop instead of starting conversation.")]
        [SerializeField] private bool openShopDirectlyOnInteract = false;

        [Header("Dialogue")]
        [Tooltip("Starting dialogue node triggered when the player interacts with this NPC.")]
        [SerializeField] private DialogueNodeSO startingDialogueNode;

        [Header("Brawl")]
        [Tooltip("If checked, this NPC's dialogue offers a [Fight] choice that starts combat on the spot. Only offered in the starting village.")]
        [SerializeField] private bool offerFightOption = true;

        [Tooltip("Hit points the NPC fights with.")]
        [SerializeField] private int brawlMaxHP = 16;

        [Tooltip("Armor Class the NPC fights with.")]
        [SerializeField] private int brawlArmorClass = 11;

        [Tooltip("Bonus added to the NPC's D20 attack rolls.")]
        [SerializeField] private int brawlAttackBonus = 2;

        [Tooltip("Damage dealt by the NPC's punches on a hit.")]
        [SerializeField] private int brawlDamage = 3;

        #endregion

        #region Constants

        /// <summary>Scene of the starting village, the only place villagers can be fought.</summary>
        public const string StartingVillageSceneName = "Zone_1_VillageAndCellar";

        /// <summary>Action tag carried by the [Fight] dialogue choice.</summary>
        public const string FightActionTag = "[ACTION_START_FIGHT]";

        #endregion

        #region Private State

        private DialogueOption fightOption;
        private PlayerUnit conversationPlayer;
        private bool isListeningForFight;

        #endregion

        #region Public Properties

        /// <summary>Display name of this NPC.</summary>
        public string NpcName => npcName;

        /// <summary>Whether this NPC functions as the village blacksmith.</summary>
        public bool IsBlacksmith
        {
            get => isBlacksmith;
            set => isBlacksmith = value;
        }

        /// <summary>Whether interaction directly opens the shop UI.</summary>
        public bool OpenShopDirectlyOnInteract
        {
            get => openShopDirectlyOnInteract;
            set => openShopDirectlyOnInteract = value;
        }

        /// <summary>Starting conversation node.</summary>
        public DialogueNodeSO StartingDialogueNode
        {
            get => startingDialogueNode;
            set => startingDialogueNode = value;
        }

        /// <summary>Whether this NPC's dialogue offers the [Fight] choice.</summary>
        public bool OfferFightOption
        {
            get => offerFightOption;
            set => offerFightOption = value;
        }

        /// <summary>Whether the [Fight] choice is shown right now (enabled and standing in the starting village).</summary>
        public bool OffersFight => CanOfferFight(offerFightOption, gameObject.scene.name);

        /// <summary>Hit points the NPC fights with.</summary>
        public int BrawlMaxHP => brawlMaxHP;

        /// <summary>Armor Class the NPC fights with.</summary>
        public int BrawlArmorClass => brawlArmorClass;

        /// <summary>Attack roll bonus the NPC fights with.</summary>
        public int BrawlAttackBonus => brawlAttackBonus;

        /// <summary>Damage per hit the NPC fights with.</summary>
        public int BrawlDamage => brawlDamage;

        #endregion

        #region Unity Lifecycle & Validation

        private void Reset()
        {
            interactionRadius = 3.0f;
            CheckAutoBlacksmith();
            UpdatePromptMessage();
        }

        private void OnValidate()
        {
            CheckAutoBlacksmith();
            UpdatePromptMessage();
        }

        private void CheckAutoBlacksmith()
        {
            if (!isBlacksmith)
            {
                if (name.IndexOf("Baldur", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Smith", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    npcName.IndexOf("Baldur", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    npcName.IndexOf("Smith", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isBlacksmith = true;
                }
            }
        }

        private void UpdatePromptMessage()
        {
            if (string.IsNullOrEmpty(promptMessage) || promptMessage == "Interact" || promptMessage.StartsWith("Talk to"))
            {
                if (openShopDirectlyOnInteract)
                {
                    promptMessage = isBlacksmith ? "Trade with Blacksmith" : $"Shop with {npcName}";
                }
                else
                {
                    promptMessage = isBlacksmith ? $"Talk to Blacksmith {npcName}" : $"Talk to {npcName}";
                }
            }
        }

        #endregion

        #region Interaction

        /// <summary>
        /// Initiates dialogue or direct shop interaction with this NPC.
        /// </summary>
        /// <param name="player">The player initiating the conversation.</param>
        public override void Interact(PlayerUnit player)
        {
            if (openShopDirectlyOnInteract)
            {
                ShopUIController shopUI = ShopUIController.Instance;
                if (shopUI != null)
                {
                    shopUI.OpenShop();
                    return;
                }
            }

            DialogueController controller = DialogueController.Instance;
            if (controller == null)
            {
                Debug.LogError($"[VillageNPC] Cannot start dialogue with '{npcName}': DialogueController could not be located or instantiated.");
                return;
            }

            if (startingDialogueNode == null)
            {
                startingDialogueNode = ResolveFallbackDialogueTree();
            }

            List<DialogueOption> extraOptions = null;
            if (OffersFight)
            {
                if (fightOption == null)
                {
                    fightOption = CreateFightOption();
                }
                extraOptions = new List<DialogueOption> { fightOption };
                conversationPlayer = player;
                StartListeningForFight();
            }

            controller.StartDialogue(startingDialogueNode, player, extraOptions);
        }

        private void OnDisable()
        {
            StopListeningForFight();
        }

        [ContextMenu("Open Shop Directly")]
        public void OpenShopFromContextMenu()
        {
            ShopUIController.Instance?.OpenShop();
        }

        private DialogueNodeSO ResolveFallbackDialogueTree()
        {
            // 1. Blacksmith Baldur Tree
            if (isBlacksmith ||
                npcName.IndexOf("Baldur", StringComparison.OrdinalIgnoreCase) >= 0 ||
                npcName.IndexOf("Smith", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Baldur", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BuildBaldurDialogueTree();
            }

            // 2. Generic Villager Fallback
            Debug.LogWarning($"[VillageNPC] No starting DialogueNodeSO assigned to '{npcName}'. Creating generic greeting dialogue.");
            DialogueNodeSO genericNode = ScriptableObject.CreateInstance<DialogueNodeSO>();
            genericNode.Initialize(
                speaker: string.IsNullOrWhiteSpace(npcName) ? "Villager" : npcName,
                text: "Greetings, traveler! Welcome to the village of Oakhaven. Speak to Innkeeper Barnaby for lodging or Blacksmith Baldur for weapons and armor.",
                portrait: null,
                isExit: false
            );
            genericNode.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] Farewell.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });

            return genericNode;
        }

        /// <summary>
        /// Generates the complete 4-option narrative tree for Blacksmith Baldur in memory
        /// if no pre-baked asset is assigned in the Inspector.
        /// </summary>
        public static DialogueNodeSO BuildBaldurDialogueTree()
        {
            DialogueNodeSO startNode = ScriptableObject.CreateInstance<DialogueNodeSO>();
            DialogueNodeSO loreNode = ScriptableObject.CreateInstance<DialogueNodeSO>();
            DialogueNodeSO questsNode = ScriptableObject.CreateInstance<DialogueNodeSO>();

            // 1. Baldur Start Node
            startNode.Initialize(
                speaker: "Baldur the Smith",
                text: "Greetings, traveler. You'd be a fool to face the castle's terrors with dull iron. Bring me salvage scrap from the ruins, and I'll temper steel that cuts bone. What do you need?",
                portrait: null,
                isExit: false
            );

            // 2. Baldur Lore Node (Reveals Commander armor weakness and sets CommanderArmorWeakened debuff)
            loreNode.Initialize(
                speaker: "Baldur the Smith",
                text: "The Cursed Commander wears ancient plate and wields a heavy shield. But centuries in the damp courtyard have rusted the armor joints at his knees. Aim for the greaves and he won't be able to deflect your blows! (Enemy AC reduced by 2 for first 2 rounds)",
                portrait: null,
                isExit: false
            );
            loreNode.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Blacksmith] Good to know. Let me see what you have for sale.", null, false, 10, "", null, "[ACTION_OPEN_SHOP]"),
                new DialogueOption("[Back] Let me ask about something else.", startNode, false, 10, "", null, ""),
                new DialogueOption("[Exit] Thank you for the advice. Farewell.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });

            // 3. Baldur Quests Node
            questsNode.Initialize(
                speaker: "Baldur the Smith",
                text: "The forge fires are starving for quality ore. The old watchtowers and courtyard are full of scrap metal from fallen sentries. Gather 5 pieces of Scrap Metal and bring them to me, and I'll pay you in gold and tempered steel!",
                portrait: null,
                isExit: false
            );
            questsNode.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Accept] I'll gather scrap metal from the castle ruins.", null, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_scrap_metal]"),
                new DialogueOption("[Blacksmith] I already have scrap metal to sell.", null, false, 10, "", null, "[ACTION_OPEN_SHOP]"),
                new DialogueOption("[Back] Let's speak of other matters.", startNode, false, 10, "", null, ""),
                new DialogueOption("[Exit] I have business elsewhere.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });

            // Configure Start Node Options
            startNode.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Blacksmith] Show me your weapons, armor, and forge (Open Shop).", null, false, 10, "", null, "[ACTION_OPEN_SHOP]"),
                new DialogueOption("[Lore] What do you know about the castle defences?", loreNode, false, 10, "", null, "CommanderArmorWeakened"),
                new DialogueOption("[Quests] Do you have any extra work for me?", questsNode, false, 10, "", null, ""),
                new DialogueOption("[Exit] I'll keep my own weapons for now.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });

            return startNode;
        }

        #endregion

        #region Fight Option

        /// <summary>
        /// Villagers can be fought only in the starting village; boss challengers elsewhere reuse this component.
        /// </summary>
        public static bool CanOfferFight(bool offerFightOption, string sceneName)
        {
            return offerFightOption && string.Equals(sceneName, StartingVillageSceneName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the [Fight] dialogue choice. It needs no skill check and ends the conversation.
        /// </summary>
        public static DialogueOption CreateFightOption()
        {
            return new DialogueOption("[Fight] Enough talk. Put up your fists!", null, false, 10, "", null, FightActionTag);
        }

        private void StartListeningForFight()
        {
            if (isListeningForFight) return;
            DialogueController.OnOptionSelected += HandleOptionSelected;
            DialogueController.OnDialogueEnded += StopListeningForFight;
            isListeningForFight = true;
        }

        private void StopListeningForFight()
        {
            if (!isListeningForFight) return;
            DialogueController.OnOptionSelected -= HandleOptionSelected;
            DialogueController.OnDialogueEnded -= StopListeningForFight;
            isListeningForFight = false;
        }

        private void HandleOptionSelected(DialogueOption option)
        {
            if (option == null || option != fightOption) return;

            PlayerUnit player = conversationPlayer != null ? conversationPlayer : FindAnyObjectByType<PlayerUnit>();
            StopListeningForFight();
            conversationPlayer = null;

            DialogueController.Instance?.EndDialogue();
            StartFight(player);
        }

        /// <summary>
        /// Starts turn-based combat against this NPC where they stand.
        /// </summary>
        public NpcBrawlerUnit StartFight(PlayerUnit player)
        {
            return NpcBrawlerUnit.Begin(this, player);
        }

        #endregion
    }
}
