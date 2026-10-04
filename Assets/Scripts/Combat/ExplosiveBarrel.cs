using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// An explosive barrel on the combat grid (critical review B6). It blocks its tile and gives half cover.
    /// Hitting it with an attack, catching it in a Fireball or letting flames reach it blows it up:
    /// 2d6 fire damage to everyone in the 3x3 around it (friend or foe) and burning ground for 2 rounds.
    /// </summary>
    public class ExplosiveBarrel : MonoBehaviour
    {
        /// <summary>Resources path of the barrel prefab made by the editor setup step.</summary>
        public const string PrefabResourcePath = "Combat/ExplosiveBarrel";

        public const int BlastDiceCount = 2;
        public const int BlastDiceSides = 6;
        public const int BurningRounds = 2;

        private GridTile tile;
        private bool hasExploded;

        /// <summary>The tile the barrel stands on.</summary>
        public GridTile Tile => tile;

        /// <summary>Whether the barrel already went off.</summary>
        public bool HasExploded => hasExploded;

        /// <summary>Puts the barrel on <paramref name="target"/>: the tile becomes blocked half cover.</summary>
        public void PlaceOn(GridTile target, GridManager grid)
        {
            if (target == null) return;
            tile = target;
            tile.Barrel = this;
            tile.IsWalkable = false;
            tile.Cover = TileCover.Half;
            if (grid != null)
            {
                transform.position = grid.GetWorldPosition(target.GridPosition);
            }
        }

        /// <summary>
        /// Blows the barrel up. Returns the units it hurt. Barrels next to it go off too.
        /// </summary>
        public List<CombatUnit> Explode()
        {
            List<CombatUnit> hurt = new List<CombatUnit>();
            if (hasExploded) return hurt;
            hasExploded = true;

            GridManager grid = GridManager.Instance;
            Vector2Int center = tile != null ? tile.GridPosition : Vector2Int.zero;

            if (tile != null)
            {
                tile.Barrel = null;
                tile.IsWalkable = true;
                tile.Cover = TileCover.None;
            }

            int damage = DiceSystem.RollDamage(BlastDiceCount, BlastDiceSides);
            Debug.Log($"[ExplosiveBarrel] KABOOM! The barrel at {center} explodes for {damage} fire damage.");
            if (grid != null)
            {
                AbilityVfx.PlayExplosion(grid.GetWorldPosition(center));
            }

            List<ExplosiveBarrel> chained = new List<ExplosiveBarrel>();
            if (grid != null)
            {
                foreach (GridTile t in grid.GetArea3x3(center))
                {
                    t.SetTerrain(TileTerrain.Burning, BurningRounds);
                    if (t.Barrel != null && t.Barrel != this && !t.Barrel.HasExploded) chained.Add(t.Barrel);
                }
            }

            // Everyone in the blast, read from unit positions so a unit mid-walk is not missed
            if (TurnManager.Instance != null)
            {
                IReadOnlyList<CombatUnit> units = TurnManager.Instance.ActiveUnits;
                for (int i = 0; i < units.Count; i++)
                {
                    CombatUnit unit = units[i];
                    if (unit == null || !unit.IsAlive || hurt.Contains(unit)) continue;
                    if (grid != null && grid.GetDistance(center, unit.GridPosition) <= 1) hurt.Add(unit);
                }
            }
            for (int i = 0; i < hurt.Count; i++)
            {
                hurt[i].TakeDamage(damage);
            }

            for (int i = 0; i < chained.Count; i++)
            {
                chained[i].Explode();
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return hurt;
        }

        /// <summary>
        /// Rolls a barrel onto a free tile at least 2 tiles from every combatant, near the enemies when it can.
        /// Returns null when the grid has no room for one.
        /// </summary>
        public static ExplosiveBarrel SpawnFor(GridManager grid, IReadOnlyList<CombatUnit> units)
        {
            if (grid == null || units == null) return null;

            GridTile best = null;
            int bestScore = int.MaxValue;
            foreach (var kvp in grid.Tiles)
            {
                GridTile t = kvp.Value;
                if (t == null || !t.IsWalkable || t.IsOccupied || t.Barrel != null) continue;

                int nearestUnit = int.MaxValue;
                int nearestEnemy = int.MaxValue;
                for (int i = 0; i < units.Count; i++)
                {
                    CombatUnit u = units[i];
                    if (u == null || !u.IsAlive) continue;
                    int d = grid.GetDistance(t.GridPosition, u.GridPosition);
                    if (d < nearestUnit) nearestUnit = d;
                    if (u is EnemyUnit && d < nearestEnemy) nearestEnemy = d;
                }
                if (nearestUnit < 2) continue;

                // Blocked or edge tiles around it would trap units; keep it in the open
                int openNeighbours = 0;
                foreach (GridTile n in grid.GetArea3x3(t.GridPosition))
                {
                    if (n != t && n.IsWalkable) openNeighbours++;
                }
                if (openNeighbours < 7) continue;

                int score = Mathf.Abs(nearestEnemy - 2);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            if (best == null) return null;

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            GameObject obj;
            if (prefab != null)
            {
                obj = Instantiate(prefab);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.65f, 0.12f, 0.08f);
            }
            obj.name = "Explosive_Barrel";

            ExplosiveBarrel barrel = obj.GetComponent<ExplosiveBarrel>();
            if (barrel == null) barrel = obj.AddComponent<ExplosiveBarrel>();
            barrel.PlaceOn(best, grid);
            return barrel;
        }
    }
}
