using System.Collections.Generic;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// The story told through letters and diaries found in every zone (critical review C3).
    /// King Aldred feared death and asked his court mage Malakor for immortality; the ritual turned the
    /// castle to stone and bound its people to their last duties. The Commander, Sir Gareth, was the captain
    /// of the King's bodyguard and swore to guard the gates forever. Queen Isolde escaped to Oakhaven with
    /// her signet ring, the one thing the King could never refuse; Elder Othelia is her descendant.
    /// </summary>
    public static class LoreTexts
    {
        /// <summary>One note: its title and text.</summary>
        public readonly struct Note
        {
            public readonly string Title;
            public readonly string Body;

            public Note(string title, string body)
            {
                Title = title;
                Body = body;
            }
        }

        private static readonly Dictionary<string, Note> notes = new Dictionary<string, Note>
        {
            // Zone 1: Oakhaven
            ["village_notice"] = new Note("Notice Nailed to the Well",
                "By order of the Reeve: the castle road is CLOSED. No cart, child or fool is to pass the forest gate after dusk.\n\n" +
                "Since the night the castle went dark, the dead walk its walls. Whatever Sir Gareth's men guard up there, it is no longer the King."),
            ["othelia_letter"] = new Note("A Letter Kept in the Family",
                "Folded in Elder Othelia's family book, the ink brown with age:\n\n" +
                "My love,\n\nI kept the ring. You asked me to give it to Malakor for his ritual and I could not. " +
                "Forgive me, or do not, but if any part of you still remembers the girl you gave it to, let this ring remind you.\n\n" +
                "The village calls me Isolde the weaver now. They do not know whose crown I left behind.\n\n- I."),

            // Zone 2: Forest Path
            ["forest_patrol"] = new Note("Patrol Order, Third Watch",
                "Third watch to hold the forest road until relieved. No one passes to the castle without the Commander's seal.\n\n" +
                "Note in another hand: No one has relieved us in a very long time. Brother Tomas stopped breathing two winters ago. He still stands his post."),
            ["forest_hunter"] = new Note("Hunter's Last Entry",
                "Day 9. The old library gate west of the path is shut with a lock no key of mine fits, " +
                "but the stone around it is cracked and there is a rune cut above the latch. A strong arm could break it; a scholar could read it.\n\n" +
                "Day 10. Something with too many bones followed me back to camp. I am leaving at first light."),

            // Zone 3: Castle Courtyard
            ["courtyard_oath"] = new Note("The Captain's Oath",
                "I, Gareth of Westmarch, Captain of the King's Guard, swear before the throne that no enemy shall pass these gates " +
                "while I draw breath, nor after it, should my King require it.\n\n" +
                "Scrawled beneath, fresh and shaking: He required it. Malakor's ritual held us to every word we ever swore. " +
                "If a true soldier finds this, remind me what honour was. Release me."),
            ["courtyard_roster"] = new Note("Guard Duty Roster",
                "Gate: Gareth (captain), Hollis, Wren.\nWalls: Doran, Cobb, the twins.\n\n" +
                "Every name has been crossed out and written in again, over and over, in the same careful hand. " +
                "The ink of the last entries is grey, like stone dust."),

            // Zone 4: Library
            ["library_journal"] = new Note("Malakor's Research Journal",
                "His Majesty will not die. Those were his words, not mine. He wants a crown that outlasts the stars.\n\n" +
                "The binding works by turning flesh to living stone: no decay, no death, no change. " +
                "The cost is that everyone sworn to him is bound with him. I told him. He signed the decree anyway."),
            ["library_counterrune"] = new Note("An Unfinished Counter-Rune",
                "The binding has one flaw: it holds only what is loyal. A heart that remembers something it loves more than the crown will crack it.\n\n" +
                "The Queen's ring would do it. She took it with her. I could undo my work if I lived long enough to see it. " +
                "If you have come to kill me, perhaps ask first."),

            // Zone 5: Castle Hall
            ["hall_decree"] = new Note("Royal Decree",
                "Let it be known that King Aldred, by the grace of the old gods and the art of his court mage, " +
                "shall reign forever. Every subject sworn to the crown shall share in this eternity.\n\n" +
                "Sealed with the royal sigil. The queen's seal, which should sit beside it, is missing."),
            ["hall_diary"] = new Note("Kitchen Maid's Diary",
                "The stone came at supper. It started in the King's chair and crawled along the floor like frost. " +
                "Cook was halfway through a sentence.\n\n" +
                "I ran because I was not sworn, only paid. The guards could not run. They tried. Their legs would not let them leave their posts."),

            // Zone 6: Tower
            ["tower_inventory"] = new Note("Treasury Inventory",
                "Shelf IV: the Giant's draught, one vial, not to be opened.\n" +
                "Shelf V: the Queen's signet ring (missing since the ritual; a copy of the setting kept here).\n\n" +
                "Margin note: His Majesty could refuse his queen nothing while she wore it. That is why Malakor wanted it."),
            ["tower_warning"] = new Note("Warning Scratched in Stone",
                "The vault answers only to SUN, MOON and STAR, in that order, left to right as you face the vial.\n\n" +
                "Mind the floor stones. The keeper does not like thieves, and the chest in the corner is not a chest."),

            // Zone 7: Throne Room
            ["throne_queen"] = new Note("From the Queen",
                "Aldred, you are afraid, and fear has made you cruel. Malakor does not offer you life. He offers you a statue's patience.\n\n" +
                "If you go through with this I will leave, and I will take the ring. One day someone will bring it back to you. Look at it. Remember me.\n\n- Isolde"),
            ["throne_lastwords"] = new Note("The King's Last Entry",
                "It is done. I feel nothing. Not the cold, not the hunger, not the grief. I thought that would be peace.\n\n" +
                "Isolde is gone. The court stands still around me. I have forever, and nothing to fill it with."),
        };

        /// <summary>Looks a note up by id. Unknown ids return a blank note.</summary>
        public static Note Get(string id)
        {
            return id != null && notes.TryGetValue(id, out Note note) ? note : new Note("A Torn Page", "The ink has run; nothing can be read.");
        }

        /// <summary>Whether a note with this id exists.</summary>
        public static bool Exists(string id) => id != null && notes.ContainsKey(id);

        /// <summary>Every note id (tests and the journal).</summary>
        public static IEnumerable<string> AllIds => notes.Keys;

        /// <summary>Story flag set when the hero has read a note.</summary>
        public static string ReadFlag(string id) => "note_read_" + id;
    }
}
