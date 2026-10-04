using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Spell scrolls sold by Pip the Peddler in the Castle Hall (critical review C4). Like the Poison Vial they
    /// are read automatically when a real fight starts, one of each kind per fight:
    /// Scroll of Warding raises a two-hit Mana Shield on the hero, Scroll of Embers burns every enemy for 2d4.
    /// </summary>
    public static class CombatScrolls
    {
        public const string WardingScrollId = "scroll_warding";
        public const string EmbersScrollId = "scroll_embers";

        /// <summary>Reads the hero's scrolls at the start of a fight. Returns how many were used.</summary>
        public static int UseAtFightStart(PlayerUnit hero, IReadOnlyList<CombatUnit> units)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (hero == null || inventory == null) return 0;
            int used = 0;

            ItemSO warding = inventory.FindItemByID(WardingScrollId);
            if (warding != null && inventory.HasItem(warding, 1))
            {
                inventory.RemoveItem(warding, 1);
                hero.StatusEffects?.ApplyManaShield(3, 2);
                Log($"{hero.UnitName} reads a Scroll of Warding: a shimmering shield will stop two blows.");
                used++;
            }

            ItemSO embers = inventory.FindItemByID(EmbersScrollId);
            if (embers != null && inventory.HasItem(embers, 1) && units != null)
            {
                inventory.RemoveItem(embers, 1);
                int damage = DiceSystem.RollDamage(2, 4);
                Log($"{hero.UnitName} reads a Scroll of Embers: fire rains on every enemy for {damage}!");
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] is EnemyUnit enemy && enemy.IsAlive)
                    {
                        if (Application.isPlaying) AbilityVfx.PlayExplosion(enemy.transform.position);
                        enemy.TakeDamage(damage);
                    }
                }
                used++;
            }
            return used;
        }

        private static void Log(string message)
        {
            Debug.Log("[CombatScrolls] " + message);
            UI.CombatUIController.Instance?.LogCombatMessage(message);
        }
    }
}
