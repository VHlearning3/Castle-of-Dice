using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Small findable lore for the castle road (Vili, 2026-10-06): one place per zone where a skill check turns up
    /// a secret, placed at runtime next to that zone's letters so no scene is edited.
    /// </summary>
    public static class InvestigationSpawner
    {
        /// <summary>One thing to investigate.</summary>
        public sealed class Spot
        {
            public string Id;
            public string Scene;
            public string Anchor;
            public Vector3 Offset;
            public string Prompt;
            public HeroSkill Skill;
            public int DC;
            public int Gold;
            public string Title;
            public string Body;
            public string FailText;
        }

        /// <summary>Every investigation spot, one per zone from the Forest Path to the Throne Room.</summary>
        public static readonly Spot[] All =
        {
            new Spot
            {
                Id = "forest_cache", Scene = "Zone_2_ForestPath", Anchor = "LoreNote_forest_hunter", Offset = new Vector3(3f, 0f, 2f),
                Prompt = "Search the old oak's roots", Skill = HeroSkill.Perception, DC = 12, Gold = 12,
                Title = "A Hunter's Cache",
                Body = "Wrapped in oilcloth under a mossy root: a handful of coins and a wooden token carved with a crowned rose, the Queen's crest.\n\n" +
                       "A note in a careful hand: \"For whoever brings Isolde's ring home. The forest remembers her kindness.\"",
                FailText = "Only moss, mud and beetles."
            },
            new Spot
            {
                Id = "courtyard_tally", Scene = "Zone_3_CastleCourtyard", Anchor = "LoreNote_courtyard_roster", Offset = new Vector3(3f, 0f, 0f),
                Prompt = "Study the scratched stones", Skill = HeroSkill.Investigation, DC = 13, Gold = 0,
                Title = "Tally Marks in the Stone",
                Body = "Someone counted days here, five strokes at a time. The marks stop at 4,380: twelve years.\n\n" +
                       "Then they begin again in a different, shakier hand: \"Wren is stone now. I will count for both of us.\"",
                FailText = "Just scratches. Probably."
            },
            new Spot
            {
                Id = "library_margin", Scene = "Zone_4_Library", Anchor = "LoreNote_library_journal", Offset = new Vector3(3f, 0f, 0f),
                Prompt = "Decipher the faded margins", Skill = HeroSkill.Arcana, DC = 13, Gold = 0,
                Title = "Notes in the Margin",
                Body = "In the Witch's cramped hand, beside a diagram of the binding:\n\n" +
                       "\"It answers to the crown, not to the King. Whoever wears it commands the stone and never dies. " +
                       "That is the true danger. Break the crown, or it will find a new head.\"",
                FailText = "The runes swim in front of your eyes."
            },
            new Spot
            {
                Id = "hall_flagstone", Scene = "Zone_5_CastleHall", Anchor = "LoreNote_hall_diary", Offset = new Vector3(3f, 0f, 0f),
                Prompt = "Check the loose flagstone", Skill = HeroSkill.Perception, DC = 12, Gold = 15,
                Title = "Under the Flagstone",
                Body = "Under a loose flagstone: a little purse of savings and a pressed flower.\n\n" +
                       "\"For my mother in Oakhaven, if I never get out.\" You pocket it, and promise yourself to find her mother.",
                FailText = "The floor seems solid enough."
            },
            new Spot
            {
                Id = "tower_starchart", Scene = "Zone_6_Tower", Anchor = "LoreNote_tower_inventory", Offset = new Vector3(3f, 0f, 0f),
                Prompt = "Read the old star chart", Skill = HeroSkill.History, DC = 14, Gold = 0,
                Title = "The Night of the Ritual",
                Body = "A star chart of the night the castle went still. The Witch marked three lights: the Sun's last glow, the rising Moon " +
                       "and the evening Star.\n\nBeneath, in the King's own hand: \"In that order, she said. Always in that order.\"",
                FailText = "Just dots and lines to you."
            },
            new Spot
            {
                Id = "throne_portrait", Scene = "Zone_7_ThroneRoom", Anchor = "LoreNote_throne_queen", Offset = new Vector3(3f, 0f, 0f),
                Prompt = "Look closer at the slashed portrait", Skill = HeroSkill.Insight, DC = 12, Gold = 0,
                Title = "The Slashed Portrait",
                Body = "The King and Queen, painted on their wedding day. Someone slashed the King's face away.\n\n" +
                       "But the cut came from behind the canvas, slow and careful: done by someone who loved him and could not bear " +
                       "to look at what he had become.",
                FailText = "Vandals. Nothing more."
            },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SpawnFor(scene.name);
        }

        /// <summary>The spot with <paramref name="id"/>, or null.</summary>
        public static Spot Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        /// <summary>Places the scene's investigation spots next to their anchors. Returns how many.</summary>
        public static int SpawnFor(string sceneName)
        {
            if (!Application.isPlaying) return 0;
            int placed = 0;
            for (int i = 0; i < All.Length; i++)
            {
                Spot spot = All[i];
                if (spot.Scene != sceneName) continue;
                if (GameObject.Find("Investigate_" + spot.Id) != null) continue;

                GameObject anchor = GameObject.Find(spot.Anchor);
                if (anchor == null) continue;

                Vector3 preferred = anchor.transform.position + spot.Offset;
                InvestigateSpot.Create(spot, MagicItemSpawner.FindFreeSpot(preferred, 0.6f));
                placed++;
            }
            return placed;
        }
    }
}
