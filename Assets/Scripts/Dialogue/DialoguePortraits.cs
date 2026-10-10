using System.Collections.Generic;
using UnityEngine;

namespace CastleOfTheD20.Dialogue
{
    /// <summary>
    /// Face icons for the dialogue box's portrait slot, rendered from each speaker's own model by
    /// DialoguePortraitRenderer into Resources/Portraits. Speakers are matched by a word in their shown name,
    /// so "Barnaby", "Innkeeper Barnaby" and "Lady Othelia" / "Elder Othelia" all find their face.
    /// A speaker with no face here keeps the slot's placeholder.
    /// </summary>
    public static class DialoguePortraits
    {
        public const string ResourceFolder = "Portraits/";

        // Name fragment (lower case) -> portrait key. Checked in order, so longer names come first.
        public static readonly string[,] Keys =
        {
            { "mirror twin", "MirrorTwin" },
            { "gargoyle king", "GargoyleKing" },
            { "the king", "GargoyleKing" },
            { "commander", "CursedCommander" },
            { "witch", "ShadowWitch" },
            { "cultist", "Cultist" },
            { "aldric", "GhostAldric" },
            { "pip", "Pip" },
            { "blonde elf", "BlondeElf" },
            { "baldur", "Baldur" },
            { "othelia", "Othelia" },
            { "barnaby", "Barnaby" },
            { "mirabel", "Mirabel" },
        };

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The portrait key for a shown speaker name, or null.</summary>
        public static string KeyFor(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName)) return null;
            string lower = speakerName.ToLowerInvariant();
            for (int i = 0; i < Keys.GetLength(0); i++)
            {
                if (lower.Contains(Keys[i, 0])) return Keys[i, 1];
            }
            return null;
        }

        /// <summary>The speaker's face icon, or null when there is none.</summary>
        public static Sprite For(string speakerName)
        {
            string key = KeyFor(speakerName);
            if (key == null) return null;
            if (!cache.TryGetValue(key, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(ResourceFolder + key);
                cache[key] = sprite;
            }
            return sprite;
        }
    }
}
