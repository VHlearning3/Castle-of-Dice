using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Castle Hall characters whose conversations are written in code (critical review C4):
    /// Pip, a peddler trapped in the castle since the curse, sells spell scrolls and rare potions (the second
    /// shop), and the ghost of Sir Aldric guards the treasury vault behind a riddle.
    /// </summary>
    public class ScriptedNpc : Interactable
    {
        public enum Kind
        {
            TrappedMerchant,
            GhostGuard
        }

        [SerializeField] private Kind kind = Kind.TrappedMerchant;

        /// <summary>Which character this is.</summary>
        public Kind NpcKind
        {
            get => kind;
            set => kind = value;
        }

        protected override void Awake()
        {
            base.Awake();
            promptMessage = kind == Kind.TrappedMerchant ? "Talk to Pip the Peddler" : "Speak with the ghost";
            interactionRadius = Mathf.Max(interactionRadius, 3.5f);
        }

        public override void Interact(PlayerUnit player)
        {
            DialogueNodeSO start = kind == Kind.TrappedMerchant ? HubDialogues.BuildMerchant() : HubDialogues.BuildGhostGuard();
            DialogueController.Instance?.StartDialogue(start, player);
        }
    }

    /// <summary>The Castle Hall conversations (critical review C4).</summary>
    public static class HubDialogues
    {
        /// <summary>Speaker name of the trapped merchant.</summary>
        public const string MerchantName = "Pip the Peddler";

        /// <summary>Speaker name of the ghost guard.</summary>
        public const string GhostName = "Ghost of Sir Aldric";

        /// <summary>Story flag set once the ghost's vault is open.</summary>
        public const string VaultOpenedFlag = "HallVaultOpened";

        /// <summary>What Pip sells: item id, shown name, price.</summary>
        public static readonly (string id, string name, int price)[] MerchantStock =
        {
            (CombatScrolls.WardingScrollId, "Scroll of Warding", 60),
            (CombatScrolls.EmbersScrollId, "Scroll of Embers", 50),
            (Economy.ShopManager.REROLL_RUNE_ID, "Rune of Fate", 60),
            (Economy.ShopManager.GREATER_POTION_ID, "Greater Health Potion", 30),
        };

        public static DialogueNodeSO BuildMerchant()
        {
            DialogueNodeSO start = Node(MerchantName,
                "A customer! Do you know how long I have been stuck in this hall? Since the stone came, that is how long. " +
                "The guards will not let me out and the dead do not buy anything. Scrolls, potions, runes: everything must go.");
            DialogueNodeSO bought = Node(MerchantName, "A pleasure! Read the scroll before a fight and it does the rest. Anything else?");
            DialogueNodeSO poor = Node(MerchantName, "Ah. Gold first, friend. Even trapped peddlers have principles.");
            DialogueNodeSO about = Node(MerchantName,
                "Scroll of Warding: the next fight starts with a mana shield that stops two blows.\n" +
                "Scroll of Embers: the next fight starts with fire raining on every enemy.\n" +
                "Both burn themselves up when the fighting begins. The rune lets you roll again when fate fails you.");

            List<DialogueOption> options = new List<DialogueOption>();
            for (int i = 0; i < MerchantStock.Length; i++)
            {
                var item = MerchantStock[i];
                options.Add(new DialogueOption($"[Buy] {item.name} - {item.price} gold", bought, false, 10, "", poor,
                    $"[ACTION_BUY:{item.id}:{item.price}]"));
            }
            options.Add(new DialogueOption("[Ask] What do the scrolls do?", about));
            options.Add(new DialogueOption("[Leave] Good luck getting out, Pip.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]"));
            start.SetOptions(options);
            bought.SetOptions(options);
            poor.SetOptions(options);
            about.SetOptions(options);
            return start;
        }

        public static DialogueNodeSO BuildGhostGuard()
        {
            if (StoryFlags.Has(VaultOpenedFlag))
            {
                DialogueNodeSO done = Node(GhostName, "The vault is yours, traveller. Spend its gold breaking the curse, and let me rest.");
                done.SetOptions(new List<DialogueOption>
                {
                    new DialogueOption("[Leave] Rest well, Sir Aldric.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
                });
                return done;
            }

            DialogueNodeSO start = Node(GhostName,
                "Halt. I kept the Queen's treasury in life and I keep it still. Answer my riddle and the vault opens.\n\n" +
                "\"The more of me you take, the more of me you leave behind. What am I?\"");
            DialogueNodeSO right = Node(GhostName,
                "Footsteps. As the Queen's were, the night she fled. Take what is inside; she would want it used against him.");
            DialogueNodeSO wrong = Node(GhostName, "No. Think, and come back. I have nothing but time.");
            right.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] Thank you, Sir Aldric.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            wrong.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] I will think on it.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });

            start.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("\"Time.\"", wrong),
                new DialogueOption("\"Footsteps.\"", right, false, 10, "", null, "[ACTION_OPEN_VAULT]"),
                new DialogueOption("\"Coins.\"", wrong),
                new DialogueOption("[Leave] Another time.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            return start;
        }

        private static DialogueNodeSO Node(string speaker, string text)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.Initialize(speaker, text, null, false);
            return node;
        }
    }
}
