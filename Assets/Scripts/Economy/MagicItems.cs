using System;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// The three worn magic items (Vili, 2026-10-06). Each works while it is in the bag and worn; the bag (I)
    /// can take it off and put it back on. Taking one off is a saved story flag, so a new item is worn at once.
    /// - Oathkeeper Shield (Castle Courtyard, after the Cursed Commander): +1 AC and +1 to every saving throw.
    /// - Shadow Cloak (the mage's secret room in the Library): Advantage on Stealth, and no one notices the
    ///   wearer, so encounters never start, except the Gargoyle King's.
    /// - Boots of Striding (Treasure Tower): +1 movement.
    /// </summary>
    public static class MagicItems
    {
        public const string OathkeeperShieldId = "wondrous_oathkeeper_shield";
        public const string ShadowCloakId = "wondrous_shadow_cloak";
        public const string BootsOfStridingId = "wondrous_boots_striding";

        /// <summary>Every worn magic item, in the order the character sheet lists them.</summary>
        public static readonly string[] AllIds = { OathkeeperShieldId, ShadowCloakId, BootsOfStridingId };

        // Story flags set while an item is taken off (precomputed: read every frame by the HUD)
        private const string ShieldOffFlag = "MagicItemOff:" + OathkeeperShieldId;
        private const string CloakOffFlag = "MagicItemOff:" + ShadowCloakId;
        private const string BootsOffFlag = "MagicItemOff:" + BootsOfStridingId;

        /// <summary>Fired when an item is put on or taken off: (item id).</summary>
        public static event Action<string> OnWornChanged;

        /// <summary>Story flag that records an item as taken off.</summary>
        public static string TakenOffFlag(string itemId)
        {
            if (itemId == OathkeeperShieldId) return ShieldOffFlag;
            if (itemId == ShadowCloakId) return CloakOffFlag;
            if (itemId == BootsOfStridingId) return BootsOffFlag;
            return "MagicItemOff:" + itemId;
        }

        /// <summary>Whether <paramref name="itemId"/> is one of the worn magic items.</summary>
        public static bool IsMagicItem(string itemId)
        {
            return itemId == OathkeeperShieldId || itemId == ShadowCloakId || itemId == BootsOfStridingId;
        }

        /// <summary>Whether the bag holds the item.</summary>
        public static bool Owns(string itemId)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return false;
            ItemSO item = inventory.FindItemByID(itemId);
            return item != null && inventory.HasItem(item, 1);
        }

        /// <summary>Whether the item is in the bag and worn.</summary>
        public static bool IsWorn(string itemId)
        {
            return Owns(itemId) && !StoryFlags.Has(TakenOffFlag(itemId));
        }

        /// <summary>Puts the item on or takes it off.</summary>
        public static void SetWorn(string itemId, bool worn)
        {
            string flag = TakenOffFlag(itemId);
            bool changed = worn ? StoryFlags.Remove(flag) : !StoryFlags.Has(flag);
            if (!worn) StoryFlags.Set(flag);
            if (changed) OnWornChanged?.Invoke(itemId);
        }

        /// <summary>Toggles the item and returns whether it is now worn.</summary>
        public static bool ToggleWorn(string itemId)
        {
            bool wear = !IsWorn(itemId);
            SetWorn(itemId, wear);
            return wear;
        }

        /// <summary>AC from the Oathkeeper Shield.</summary>
        public static int ArmorBonus => IsWorn(OathkeeperShieldId) ? 1 : 0;

        /// <summary>Bonus to every saving throw from the Oathkeeper Shield.</summary>
        public static int SavingThrowBonus => IsWorn(OathkeeperShieldId) ? 1 : 0;

        /// <summary>Movement from the Boots of Striding.</summary>
        public static int MovementBonus => IsWorn(BootsOfStridingId) ? 1 : 0;

        /// <summary>The Shadow Cloak's Advantage on Stealth checks.</summary>
        public static bool HasStealthAdvantage => IsWorn(ShadowCloakId);

        /// <summary>While the Shadow Cloak is worn no one notices the hero except the Gargoyle King.</summary>
        public static bool IsUnseen => IsWorn(ShadowCloakId);

        /// <summary>What the item does, for the bag and the character sheet.</summary>
        public static string EffectText(string itemId)
        {
            switch (itemId)
            {
                case OathkeeperShieldId: return "+1 AC, +1 to saving throws";
                case ShadowCloakId: return "Advantage on Stealth; unseen by everyone but the Gargoyle King";
                case BootsOfStridingId: return "+1 movement";
                default: return string.Empty;
            }
        }
    }
}
