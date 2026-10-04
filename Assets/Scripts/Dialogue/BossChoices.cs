using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// Extra choices in the boss conversations (critical review C8, C9).
    /// Class options: the warrior challenges the Commander to an honour duel (his guard stays out), the mage
    /// debates the flaw in Malakor's binding (no mirror image, his true form shown), the rogue lifts the King's
    /// crown (-2 AC). Consequences: the Commander can be released from his oath and Malakor spared, both
    /// without a fight. A spared Malakor turns up in the King's fight to help or to betray. The choices are
    /// story flags, so they are saved and read by the bosses and the ending.
    /// </summary>
    public static class BossChoices
    {
        public const string CommanderBossId = "CursedCommander";
        public const string MalakorBossId = "ShadowMageMalakor";
        public const string KingBossId = "GargoyleKing";

        public const string HonorDuelFlag = "CommanderHonorDuel";
        public const string CommanderReleasedFlag = "CommanderReleased";
        public const string MalakorShakenFlag = "MalakorShaken";
        public const string MalakorSparedFlag = "MalakorSpared";
        public const string MalakorHelpedFlag = "MalakorHelped";
        public const string MalakorBetrayedFlag = "MalakorBetrayed";
        public const string CrownStolenFlag = "CrownStolen";

        /// <summary>The options this hero gets in the given boss's opening conversation.</summary>
        public static List<DialogueOption> BuildOptions(string bossId, PlayerUnit hero)
        {
            List<DialogueOption> options = new List<DialogueOption>();
            CharacterClassType? cls = hero != null && hero.CharacterClass != null ? hero.CharacterClass.ClassType : (CharacterClassType?)null;

            switch (bossId)
            {
                case CommanderBossId:
                    if (cls == CharacterClassType.Warrior)
                    {
                        options.Add(Check("[Warrior] Then face me alone, soldier to soldier. Send your guard away.",
                            13, "Honor / Persuasion Check", HonorDuelFlag,
                            Exit("Cursed Commander", "A duel... yes. As it was done at court. Stand back, guard! This one is mine alone."),
                            Exit("Cursed Commander", "Duel? You are no knight. Guard, with me!")));
                    }
                    options.Add(Check("[Soldier's Honor] Your King is gone. I release you from your oath, Sir Gareth.",
                        15, "Honor / Charisma Check", CommanderReleasedFlag,
                        Exit("Sir Gareth", "Released... I remember my name. Thank you, stranger. Tell them the gate held until the very end."),
                        Exit("Cursed Commander", "Only the King can release me! Draw your blade!")));
                    break;

                case MalakorBossId:
                    if (cls == CharacterClassType.Mage)
                    {
                        options.Add(Check("[Mage] Your binding holds only the loyal. It is already cracking; I can see it from here.",
                            14, "Arcana / Intelligence Check", MalakorShakenFlag,
                            Exit("Shadow Mage Malakor", "You... see the flaw? Then my mirrors are useless against you. Fight me as I am!"),
                            Exit("Shadow Mage Malakor", "A student lecturing the master! My mirrors will teach you humility.")));
                    }
                    options.Add(Check("[Mercy] You wrote the counter-rune. Live, and help undo what you did.",
                        15, "Persuasion / Charisma Check", MalakorSparedFlag,
                        Exit("Malakor", "Mercy... from you? Very well. I will be there when you face him. Whether I help you... we shall both find out."),
                        Exit("Shadow Mage Malakor", "Mercy is a word for the weak. Burn!")));
                    break;

                case KingBossId:
                    if (cls == CharacterClassType.Rogue)
                    {
                        options.Add(Check("[Rogue] (While he speaks, slip close and lift the crown off his stone head.)",
                            15, "Sleight of Hand / Dexterity Check", CrownStolenFlag,
                            Exit("The Gargoyle King", "My CROWN! Thief! Without it the stone listens to me less... no matter, you will die all the same!"),
                            Exit("The Gargoyle King", "Your fingers are clumsy, little thief. Kneel!")));
                    }
                    break;
            }
            return options;
        }

        /// <summary>
        /// True when the conversation that just ended resolved the boss without a fight (the Commander
        /// released, Malakor spared).
        /// </summary>
        public static bool ResolvedPeacefully(string bossId)
        {
            switch (bossId)
            {
                case CommanderBossId: return StoryFlags.Has(CommanderReleasedFlag);
                case MalakorBossId: return StoryFlags.Has(MalakorSparedFlag);
                default: return false;
            }
        }

        /// <summary>
        /// For a spared Malakor at the King's fight: decides once (and remembers) whether he helps or betrays.
        /// Returns true for help, false for betrayal; null when Malakor was not spared.
        /// </summary>
        public static bool? ResolveMalakorAtThrone()
        {
            if (!StoryFlags.Has(MalakorSparedFlag)) return null;
            if (StoryFlags.Has(MalakorHelpedFlag)) return true;
            if (StoryFlags.Has(MalakorBetrayedFlag)) return false;

            bool helps = DiceSystem.RollDice(2) == 2;
            StoryFlags.Set(helps ? MalakorHelpedFlag : MalakorBetrayedFlag);
            return helps;
        }

        /// <summary>Ending lines for the choices made on the way (empty when there were none).</summary>
        public static string BuildEpilogue()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (StoryFlags.Has(CommanderReleasedFlag))
                sb.Append("Sir Gareth walked out of the gate he had guarded for so long and laid his sword on the Courtyard steps. ");
            if (StoryFlags.Has(MalakorHelpedFlag))
                sb.Append("Malakor kept his word: his counter-rune is why the stone let go so quickly. He has asked to be judged by the village. ");
            else if (StoryFlags.Has(MalakorBetrayedFlag))
                sb.Append("Malakor betrayed you in the Throne Room and fled. Somewhere, the old mage is still running. ");
            else if (StoryFlags.Has(MalakorSparedFlag))
                sb.Append("Malakor, whom you spared, was never seen again. ");
            if (StoryFlags.Has(CrownStolenFlag))
                sb.Append("And the King's crown? It sits in your pack. I will not ask.");
            return sb.ToString().Trim();
        }

        private static DialogueOption Check(string text, int dc, string checkDescription, string flagOnSuccess, DialogueNodeSO success, DialogueNodeSO failure)
        {
            return new DialogueOption(text, success, true, dc, checkDescription, failure, flagOnSuccess);
        }

        private static DialogueNodeSO Exit(string speaker, string text)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.Initialize(speaker, text, null, false);
            node.SetOptions(new List<DialogueOption> { new DialogueOption("[Continue]", null) });
            return node;
        }
    }
}
