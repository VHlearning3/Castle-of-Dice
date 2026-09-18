using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Singleton manager responsible for the tactical battlefield grid.
    /// Handles coordinate mappings, tile lookup, pathfinding, distance metrics, and area-of-effect calculations.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        #region Singleton

        public static GridManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Grid Dimensions")]
        [SerializeField] private int width = 10;
        [SerializeField] private int height = 10;
        [SerializeField] private float tileSize = 2.0f;
        [SerializeField] private Vector3 originWorldPosition = Vector3.zero;

        [Header("Procedural Generation (Optional)")]
        [Tooltip("Prefab instantiated when GenerateGrid() is called programmatically.")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private Transform tilesParent;

        #endregion

        #region Private State

        private readonly Dictionary<Vector2Int, GridTile> tiles = new Dictionary<Vector2Int, GridTile>();

        #endregion

        #region Public Properties

        /// <summary>Grid column count.</summary>
        public int Width => width;

        /// <summary>Grid row count.</summary>
        public int Height => height;

        /// <summary>Size of each square tile in Unity world units.</summary>
        public float TileSize => tileSize;

        /// <summary>Read-only dictionary of all registered tiles.</summary>
        public IReadOnlyDictionary<Vector2Int, GridTile> Tiles => tiles;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Automatically register any existing tiles placed in scene
            RegisterSceneTiles();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Grid Generation & Registration

        /// <summary>
        /// Automatically discovers and registers existing GridTile components in the scene.
        /// </summary>
        public void RegisterSceneTiles()
        {
            GridTile[] sceneTiles = FindObjectsByType<GridTile>(FindObjectsSortMode.None);
            foreach (GridTile tile in sceneTiles)
            {
                RegisterTile(tile);
            }
        }

        /// <summary>
        /// Registers an individual tile into the grid coordinate lookup.
        /// </summary>
        public void RegisterTile(GridTile tile)
        {
            if (tile == null) return;
            tiles[tile.GridPosition] = tile;
        }

        /// <summary>
        /// Programmatically creates a grid of tiles using the specified dimensions and prefab.
        /// </summary>
        public void GenerateGrid(int gridWidth, int gridHeight, GameObject customPrefab = null)
        {
            ClearGrid();

            width = gridWidth;
            height = gridHeight;
            GameObject prefabToUse = customPrefab != null ? customPrefab : tilePrefab;

            Transform parent = tilesParent != null ? tilesParent : transform;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 worldPos = GetWorldPosition(pos);

                    GameObject tileObj;
                    if (prefabToUse != null)
                    {
                        tileObj = Instantiate(prefabToUse, worldPos, Quaternion.identity, parent);
                    }
                    else
                    {
                        tileObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        tileObj.transform.position = worldPos;
                        tileObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                        tileObj.transform.localScale = new Vector3(tileSize * 0.95f, tileSize * 0.95f, 1f);
                        tileObj.transform.SetParent(parent);
                    }

                    GridTile gridTile = tileObj.GetComponent<GridTile>();
                    if (gridTile == null)
                    {
                        gridTile = tileObj.AddComponent<GridTile>();
                    }

                    gridTile.Initialize(pos, walkable: true);
                    RegisterTile(gridTile);
                }
            }
        }

        /// <summary>
        /// Clears all existing registered tiles and destroys their GameObjects.
        /// </summary>
        public void ClearGrid()
        {
            foreach (var kvp in tiles)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            tiles.Clear();
        }

        #endregion

        #region Coordinate & Lookup Methods

        /// <summary>
        /// Retrieves the GridTile at the given coordinate, or null if outside bounds or empty.
        /// </summary>
        public GridTile GetTileAt(Vector2Int pos)
        {
            tiles.TryGetValue(pos, out GridTile tile);
            return tile;
        }

        /// <summary>
        /// Checks whether a given grid coordinate falls within standard rectangular bounds.
        /// </summary>
        public bool IsWithinBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
        }

        /// <summary>
        /// Converts a grid coordinate into 3D world space coordinates.
        /// </summary>
        public Vector3 GetWorldPosition(Vector2Int gridPos)
        {
            return originWorldPosition + new Vector3(gridPos.x * tileSize, 0f, gridPos.y * tileSize);
        }

        /// <summary>
        /// Converts a 3D world space coordinate into the nearest grid coordinate.
        /// </summary>
        public Vector2Int GetGridPosition(Vector3 worldPos)
        {
            Vector3 local = worldPos - originWorldPosition;
            int x = Mathf.RoundToInt(local.x / tileSize);
            int y = Mathf.RoundToInt(local.z / tileSize);
            return new Vector2Int(x, y);
        }

        #endregion

        #region Distance & Area Calculations

        /// <summary>
        /// Calculates Manhattan distance (|dx| + |dy|) between two grid coordinates.
        /// </summary>
        public int GetDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// Calculates Chebyshev (diagonal-allowed) distance (max(|dx|, |dy|)) between two grid coordinates.
        /// </summary>
        public int GetChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        /// <summary>
        /// Returns all tiles within a specified Manhattan radius from a center tile.
        /// </summary>
        public List<GridTile> GetTilesInRadius(Vector2Int center, int radius)
        {
            List<GridTile> result = new List<GridTile>();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector2Int pos = center + new Vector2Int(x, y);
                    if (GetDistance(center, pos) <= radius)
                    {
                        GridTile tile = GetTileAt(pos);
                        if (tile != null)
                        {
                            result.Add(tile);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all valid tiles forming a 3x3 square centered around the specified coordinate.
        /// Used for AOE spells like Fireball.
        /// </summary>
        public List<GridTile> GetArea3x3(Vector2Int center)
        {
            List<GridTile> result = new List<GridTile>(9);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int pos = center + new Vector2Int(dx, dy);
                    GridTile tile = GetTileAt(pos);
                    if (tile != null)
                    {
                        result.Add(tile);
                    }
                }
            }

            return result;
        }

        #endregion

        #region Pathfinding & Reachability

        /// <summary>
        /// Finds all reachable, walkable, and unoccupied tiles from a starting coordinate within movement range.
        /// Uses Breadth-First Search (BFS) to properly account for obstacles and blocked tiles.
        /// </summary>
        public List<GridTile> GetReachableTiles(Vector2Int startPos, int movementRange)
        {
            List<GridTile> reachable = new List<GridTile>();
            if (movementRange <= 0) return reachable;

            Queue<(Vector2Int pos, int dist)> queue = new Queue<(Vector2Int, int)>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            queue.Enqueue((startPos, 0));
            visited.Add(startPos);

            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            while (queue.Count > 0)
            {
                var (currentPos, currentDist) = queue.Dequeue();

                if (currentDist >= movementRange) continue;

                foreach (Vector2Int dir in directions)
                {
                    Vector2Int nextPos = currentPos + dir;

                    if (visited.Contains(nextPos)) continue;

                    GridTile tile = GetTileAt(nextPos);
                    if (tile == null || !tile.IsWalkable) continue;

                    visited.Add(nextPos);

                    // If tile is not occupied, it's a valid stopping tile
                    if (!tile.IsOccupied)
                    {
                        reachable.Add(tile);
                        queue.Enqueue((nextPos, currentDist + 1));
                    }
                }
            }

            return reachable;
        }

        /// <summary>
        /// Computes the shortest path between two points using BFS pathfinding.
        /// Returns an ordered list of tiles from start to destination (excluding start, including target).
        /// Returns null if no valid path exists.
        /// </summary>
        public List<GridTile> FindPath(Vector2Int startPos, Vector2Int targetPos)
        {
            if (startPos == targetPos) return new List<GridTile>();

            GridTile targetTile = GetTileAt(targetPos);
            if (targetTile == null || !targetTile.IsWalkable) return null;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(startPos);
            cameFrom[startPos] = startPos;

            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            bool found = false;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current == targetPos)
                {
                    found = true;
                    break;
                }

                foreach (Vector2Int dir in directions)
                {
                    Vector2Int next = current + dir;
                    if (cameFrom.ContainsKey(next)) continue;

                    GridTile tile = GetTileAt(next);
                    if (tile == null || !tile.IsWalkable) continue;

                    // Allow passing through only if unoccupied or if it's the target tile
                    if (tile.IsOccupied && next != targetPos) continue;

                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!found) return null;

            // Reconstruct path
            List<GridTile> path = new List<GridTile>();
            Vector2Int curr = targetPos;

            while (curr != startPos)
            {
                path.Add(GetTileAt(curr));
                curr = cameFrom[curr];
            }

            path.Reverse();
            return path;
        }

        #endregion

        #region Visual Highlighting

        /// <summary>
        /// Clears all highlights across all registered grid tiles.
        /// </summary>
        public void ClearAllHighlights()
        {
            foreach (var kvp in tiles)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.ResetHighlight();
                }
            }
        }

        /// <summary>
        /// Applies the specified highlight type to a collection of tiles.
        /// </summary>
        public void HighlightTiles(IEnumerable<GridTile> targetTiles, TileHighlightType highlightType)
        {
            if (targetTiles == null) return;
            foreach (GridTile tile in targetTiles)
            {
                if (tile != null)
                {
                    tile.SetHighlight(highlightType);
                }
            }
        }

        #endregion
    }
}
