using System;
using System.Collections.Generic;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Story and dialogue flags of the adventure (critical review A6, C9): dialogue combat bonuses such as
    /// Baldur's "CommanderArmorWeakened", and choices such as sparing Malakor. Static, so a flag set in the
    /// village is still there in the Courtyard, and written into the save.
    /// </summary>
    public static class StoryFlags
    {
        private static readonly HashSet<string> flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fired when a flag is set: (flag).</summary>
        public static event Action<string> OnFlagSet;

        /// <summary>All flags currently set.</summary>
        public static IReadOnlyCollection<string> All => flags;

        /// <summary>Sets a flag. Blank names are ignored.</summary>
        public static void Set(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return;
            if (flags.Add(flag))
            {
                OnFlagSet?.Invoke(flag);
            }
        }

        /// <summary>Whether the flag is set.</summary>
        public static bool Has(string flag)
        {
            return !string.IsNullOrEmpty(flag) && flags.Contains(flag);
        }

        /// <summary>Clears one flag. Returns true when it was set.</summary>
        public static bool Remove(string flag)
        {
            return !string.IsNullOrEmpty(flag) && flags.Remove(flag);
        }

        /// <summary>Clears every flag (new adventure).</summary>
        public static void Clear()
        {
            flags.Clear();
        }

        /// <summary>Copies the flags into <paramref name="buffer"/> for the save file.</summary>
        public static void CaptureTo(List<string> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            buffer.AddRange(flags);
        }

        /// <summary>Replaces the flags with the saved ones.</summary>
        public static void Restore(IEnumerable<string> saved)
        {
            flags.Clear();
            if (saved == null) return;
            foreach (string flag in saved)
            {
                if (!string.IsNullOrWhiteSpace(flag)) flags.Add(flag);
            }
        }
    }
}
