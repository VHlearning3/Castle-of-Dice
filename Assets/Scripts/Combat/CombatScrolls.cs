using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Spell scrolls sold by Pip the Peddler in the Castle Hall (critical review C4). The hero reads them from the
    /// combat item buttons when they choose (Vili, 2026-10-06), each costing the turn's action:
    /// Scroll of Warding raises a two-hit Mana Shield on the hero, Scroll of Embers burns every enemy for 2d4
    /// (a DEX save against the scroll's DC 13 halves it).
    /// </summary>
    public static class CombatScrolls
    {
        public const string WardingScrollId = "scroll_warding";
        public const string EmbersScrollId = "scroll_embers";

        /// <summary>Save DC of a scroll's spell.</summary>
        public const int ScrollSaveDC = 13;

        /// <summary>How many of the scroll the bag holds.</summary>
        public static int Count(string scrollId)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return 0;
            ItemSO scroll = inventory.FindItemByID(scrollId);
            return scroll != null ? inventory.GetItemCount(scroll) : 0;
        }

        /// <summary>Whether the hero can read <paramref name="scrollId"/> now: owned and the action unused.</summary>
        public static bool CanRead(PlayerUnit hero, string scrollId)
        {
            return hero != null && hero.IsAlive && !hero.HasActedThisTurn && Count(scrollId) > 0;
        }

        /// <summary>Reads the Scroll of Warding: a Mana Shield that stops the next two blows. Spends the action.</summary>
        public static bool ReadWarding(PlayerUnit hero)
        {
            if (!CanRead(hero, WardingScrollId) || !TryConsume(WardingScrollId)) return false;
            hero.HasActedThisTurn = true;
            if (hero.UnitAnimator != null) hero.UnitAnimator.SetTrigger("CastSpell");
            hero.StatusEffects?.ApplyManaShield(3, 2);
            Log($"{hero.UnitName} reads a Scroll of Warding: a shimmering shield will stop two blows.");
            return true;
        }

        /// <summary>
        /// Reads the Scroll of Embers: fire rains on every enemy in the fight for 2d4, halved by a DEX save
        /// (DC 13). Spends the action.
        /// </summary>
        public static bool ReadEmbers(PlayerUnit hero, IReadOnlyList<CombatUnit> units)
        {
            if (units == null || !CanRead(hero, EmbersScrollId) || !TryConsume(EmbersScrollId)) return false;
            hero.HasActedThisTurn = true;
            if (hero.UnitAnimator != null) hero.UnitAnimator.SetTrigger("CastSpell");

            int damage = DiceSystem.RollDamage(2, 4);
            Log($"{hero.UnitName} reads a Scroll of Embers: fire rains on every enemy for {damage}!");

            // Collect first: a burning enemy may die and leave the list while we go through it
            List<EnemyUnit> targets = new List<EnemyUnit>();
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is EnemyUnit enemy && enemy.IsAlive) targets.Add(enemy);
            }
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyUnit enemy = targets[i];
                if (!enemy.IsAlive) continue;
                DiceResult save = CombatRules.RollEnemySave(enemy, HeroAttribute.Dexterity, ScrollSaveDC, "the Scroll of Embers");
                if (Application.isPlaying) AbilityVfx.PlayExplosion(enemy.transform.position);
                enemy.TakeDamage(save.isSuccess ? damage / 2 : damage);
            }
            return true;
        }

        private static bool TryConsume(string scrollId)
        {
            InventoryManager inventory = InventoryManager.Instance;
            ItemSO scroll = inventory != null ? inventory.FindItemByID(scrollId) : null;
            return scroll != null && inventory.RemoveItem(scroll, 1);
        }

        private static void Log(string message)
        {
            Debug.Log("[CombatScrolls] " + message);
            UI.CombatUIController.Instance?.LogCombatMessage(message);
        }
    }
}
