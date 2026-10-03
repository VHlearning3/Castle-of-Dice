using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Which enemy the player is pointing at, so its stat card and its model can be lit up together.
    /// Hovering a card wins over hovering a model on the grid, which wins over the last clicked enemy.
    /// CombatUIController writes the grid hover and clicks, the enemy cards write their own hover and clicks,
    /// and CombatStatsHUD reads <see cref="Highlighted"/> each frame.
    /// </summary>
    public static class EnemyFocus
    {
        /// <summary>Enemy last clicked (on its model or its card); cleared by clicking empty ground.</summary>
        public static EnemyUnit Selected { get; set; }

        /// <summary>Enemy whose model or tile is under the mouse.</summary>
        public static EnemyUnit TileHovered { get; set; }

        /// <summary>Enemy whose stat card is under the mouse.</summary>
        public static EnemyUnit CardHovered { get; set; }

        /// <summary>The enemy to highlight right now, or null. Dead enemies are never highlighted.</summary>
        public static EnemyUnit Highlighted
        {
            get
            {
                if (IsLive(CardHovered)) return CardHovered;
                if (IsLive(TileHovered)) return TileHovered;
                if (IsLive(Selected)) return Selected;
                return null;
            }
        }

        /// <summary>Forgets everything (battle over, scene change).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Selected = null;
            TileHovered = null;
            CardHovered = null;
        }

        private static bool IsLive(EnemyUnit enemy)
        {
            return enemy != null && enemy.IsAlive;
        }
    }
}
