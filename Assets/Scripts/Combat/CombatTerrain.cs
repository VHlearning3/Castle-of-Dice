using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Ground effects on the combat grid (critical review B6): burning tiles from Fireball and barrel blasts,
    /// slippery ice from Frostbite, one explosive barrel per real fight, and cleanup when the fight ends.
    /// Cover itself is read from the grid's blocked tiles (<see cref="GridManager.GetCoverBetween"/>).
    /// </summary>
    public static class CombatTerrain
    {
        /// <summary>Rounds Fireball's flames keep burning.</summary>
        public const int FireballBurnRounds = 2;

        /// <summary>Rounds Frostbite's ice stays on the ground.</summary>
        public const int FrostIceRounds = 2;

        /// <summary>DEX check a unit makes at the start of a turn on ice; a failure means no moving that turn.</summary>
        public const int IceSaveDC = 10;

        /// <summary>Fire damage dice for starting a turn in flames (1d4).</summary>
        public const int BurnDamageSides = 4;

        private static readonly List<ExplosiveBarrel> barrels = new List<ExplosiveBarrel>();

        /// <summary>Barrels rolled onto the current battlefield.</summary>
        public static IReadOnlyList<ExplosiveBarrel> Barrels => barrels;

        /// <summary>Rolls one explosive barrel onto the grid for a real fight (Play Mode only).</summary>
        public static void PrepareBattlefield(GridManager grid, IReadOnlyList<CombatUnit> units)
        {
            Cleanup(grid);
            if (!Application.isPlaying || grid == null) return;

            ExplosiveBarrel barrel = ExplosiveBarrel.SpawnFor(grid, units);
            if (barrel != null) barrels.Add(barrel);
        }

        /// <summary>Removes barrels, flames, ice and warnings when the fight ends.</summary>
        public static void Cleanup(GridManager grid)
        {
            for (int i = 0; i < barrels.Count; i++)
            {
                ExplosiveBarrel barrel = barrels[i];
                if (barrel == null) continue;
                if (barrel.Tile != null)
                {
                    barrel.Tile.Barrel = null;
                    barrel.Tile.IsWalkable = true;
                    barrel.Tile.Cover = TileCover.None;
                }
                if (Application.isPlaying) Object.Destroy(barrel.gameObject);
                else Object.DestroyImmediate(barrel.gameObject);
            }
            barrels.Clear();
            grid?.ClearTerrainAndWarnings();
        }

        /// <summary>Sets a 3x3 square on fire and sets off barrels inside it (Fireball).</summary>
        public static void IgniteArea(GridManager grid, Vector2Int center, int rounds)
        {
            if (grid == null) return;
            List<ExplosiveBarrel> caught = new List<ExplosiveBarrel>();
            foreach (GridTile tile in grid.GetArea3x3(center))
            {
                tile.SetTerrain(TileTerrain.Burning, rounds);
                if (tile.Barrel != null) caught.Add(tile.Barrel);
            }
            for (int i = 0; i < caught.Count; i++) caught[i].Explode();
        }

        /// <summary>Freezes a tile and its four neighbours (Frostbite).</summary>
        public static void FreezePlus(GridManager grid, Vector2Int center, int rounds)
        {
            if (grid == null) return;
            Freeze(grid, center, rounds);
            Freeze(grid, center + Vector2Int.up, rounds);
            Freeze(grid, center + Vector2Int.down, rounds);
            Freeze(grid, center + Vector2Int.left, rounds);
            Freeze(grid, center + Vector2Int.right, rounds);
        }

        private static void Freeze(GridManager grid, Vector2Int pos, int rounds)
        {
            GridTile tile = grid.GetTileAt(pos);
            if (tile != null && tile.Terrain != TileTerrain.Burning) tile.SetTerrain(TileTerrain.Ice, rounds);
        }

        /// <summary>
        /// What the ground does to a unit starting its turn on it: flames burn for 1d4, ice makes it slip
        /// (DEX DC 10 or no moving this turn).
        /// </summary>
        public static void ApplyTurnStart(CombatUnit unit, GridManager grid)
        {
            if (unit == null || !unit.IsAlive || grid == null) return;
            GridTile tile = grid.GetTileAt(unit.GridPosition);
            if (tile == null) return;

            if (tile.Terrain == TileTerrain.Burning)
            {
                int burn = DiceSystem.RollDice(BurnDamageSides);
                Debug.Log($"[CombatTerrain] {unit.UnitName} stands in flames and burns for {burn}.");
                unit.TakeDamage(burn);
            }
            else if (tile.Terrain == TileTerrain.Ice)
            {
                int dexBonus = unit is PlayerUnit hero
                    ? HeroAttributes.GetModifier(hero, HeroAttribute.Dexterity)
                    : (unit is EnemyUnit enemy ? enemy.AttackBonus / 2 : 0);
                int roll = DiceSystem.RollDice(20);
                if (roll + dexBonus < IceSaveDC)
                {
                    Debug.Log($"[CombatTerrain] {unit.UnitName} slips on the ice ({roll}+{dexBonus} vs DC {IceSaveDC}) and can't move this turn.");
                    unit.StatusEffects?.ApplyEffectForThisTurn(StatusEffectType.Immobilized);
                }
            }
        }
    }
}
