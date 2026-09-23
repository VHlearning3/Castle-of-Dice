using System;
using System.Collections.Generic;
using UnityEngine;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Singleton manager responsible for the tactical battlefield grid.
    /// Handles coordinate mappings, tile lookup, pathfinding, distance metrics, and area-of-effect calculations.
    /// </summary>
    [SelectionBase]
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

        [Header("Transform Alignment & Scaling")]
        [Tooltip("If true, the grid center aligns with transform.position. If false, transform.position is the (0,0) corner.")]
        [SerializeField] private bool centerGridOnTransform = true;

        [Tooltip("If true, the grid dynamically calculates coordinates from transform.position and transform.localScale.")]
        [SerializeField] private bool useTransformAsGridOrigin = true;

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

        /// <summary>Base size of each square tile in Unity world units.</summary>
        public float TileSize => tileSize;

        /// <summary>Effective tile size taking transform world scale into account.</summary>
        public float EffectiveTileSize => tileSize * (useTransformAsGridOrigin ? Mathf.Max(0.01f, transform.lossyScale.x) : 1f);

        /// <summary>Read-only dictionary of all registered tiles.</summary>
        public IReadOnlyDictionary<Vector2Int, GridTile> Tiles => tiles;

        /// <summary>World-space origin coordinates corresponding to grid (0, 0).</summary>
        public Vector3 OriginWorldPosition
        {
            get => useTransformAsGridOrigin ? GetWorldPosition(Vector2Int.zero) : originWorldPosition;
            set => originWorldPosition = value;
        }

        /// <summary>Whether the grid is centered on its Transform position.</summary>
        public bool CenterGridOnTransform
        {
            get => centerGridOnTransform;
            set => centerGridOnTransform = value;
        }

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
        /// Generates the grid layout directly in the Scene view while in Editor mode.
        /// Right-click GridManager component in Inspector and select 'Generate Grid In Editor'.
        /// </summary>                              
        [ContextMenu("Generate Grid In Editor")]
        public void GenerateGridInEditor()
        {
            GenerateGrid(width, height, tilePrefab);
        }

        /// <summary>
        /// Generates or centers the tactical combat grid around a specific center position (such as a dungeon room or cellar).
        /// Automatically performs vertical surface raycasting to prevent Z-fighting with floors and detects solid obstacles (pillars, walls).
        /// </summary>
        public void GenerateGridAt(Vector3 centerPosition, int gridWidth, int gridHeight, float newTileSize = 2.0f, GameObject customPrefab = null)
        {
            tileSize = newTileSize > 0.1f ? newTileSize : 2.0f;
            width = gridWidth;
            height = gridHeight;

            // Detect exact floor elevation
            float floorY = centerPosition.y;
            if (Physics.Raycast(centerPosition + Vector3.up * 3.0f, Vector3.down, out RaycastHit hit, 10.0f))
            {
                floorY = hit.point.y + 0.05f; // Place 0.05 units above floor surface to eliminate Z-fighting
            }
            else
            {
                floorY = centerPosition.y + 0.05f;
            }

            if (useTransformAsGridOrigin)
            {
                transform.position = new Vector3(centerPosition.x, floorY, centerPosition.z);
                originWorldPosition = GetWorldPosition(Vector2Int.zero);
            }
            else
            {
                // Calculate origin (0, 0) such that the grid is centered at centerPosition
                float halfSpanX = (gridWidth - 1) * 0.5f * tileSize;
                float halfSpanZ = (gridHeight - 1) * 0.5f * tileSize;
                originWorldPosition = new Vector3(centerPosition.x - halfSpanX, floorY, centerPosition.z - halfSpanZ);
            }

            GenerateGrid(gridWidth, gridHeight, customPrefab);
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

            EnsureTilesParentExists();
            Transform parent = tilesParent != null ? tilesParent : transform;

            // Reset parent local transform to ensure clean alignment
            if (parent != transform)
            {
                parent.localPosition = Vector3.zero;
                parent.localRotation = Quaternion.identity;
                parent.localScale = Vector3.one;
            }

            // Unity's Quad requires 90 degree X rotation to lie flat on the horizontal ground
            Quaternion tileRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Vector3 localPos = GetLocalTilePosition(pos);
                    Vector3 worldPos = transform.TransformPoint(localPos);

                    GameObject tileObj;
                    if (prefabToUse != null)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefabToUse))
                        {
                            tileObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabToUse, parent);
                        }
                        else
                        {
                            tileObj = Instantiate(prefabToUse, parent);
                        }
#else
                        tileObj = Instantiate(prefabToUse, parent);
#endif
                    }
                    else
                    {
                        tileObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        tileObj.transform.SetParent(parent, false);
                    }

                    tileObj.name = $"Tile_{x}_{y}";
                    tileObj.transform.localPosition = localPos;
                    tileObj.transform.localRotation = tileRotation;
                    tileObj.transform.localScale = new Vector3(tileSize * 0.95f, tileSize * 0.95f, 1f);

                    GridTile gridTile = tileObj.GetComponent<GridTile>();
                    if (gridTile == null)
                    {
                        gridTile = tileObj.AddComponent<GridTile>();
                    }

                    // Check for solid environment obstacles (such as stone pillars or perimeter walls)
                    bool isWalkable = true;
                    Vector3 lossy = transform.lossyScale;
                    Vector3 checkCenter = worldPos + transform.up * (1.0f * lossy.y);
                    Vector3 checkHalfExtents = new Vector3(tileSize * 0.35f * lossy.x, 0.9f * lossy.y, tileSize * 0.35f * lossy.z);
                    Collider[] hits = Physics.OverlapBox(checkCenter, checkHalfExtents, transform.rotation);
                    foreach (Collider h in hits)
                    {
                        if (h.isTrigger) continue;
                        if (h.CompareTag("Player") || h.CompareTag("Enemy")) continue;
                        if (h.GetComponent<CombatUnit>() != null) continue;
                        if (h.transform.IsChildOf(tileObj.transform) || h.gameObject == tileObj) continue;

                        // Found a solid structural collider (pillar/wall)
                        isWalkable = false;
                        break;
                    }

                    gridTile.Initialize(pos, isWalkable);
                    if (!isWalkable)
                    {
                        Renderer r = tileObj.GetComponentInChildren<Renderer>();
                        if (r != null) r.enabled = false;
                    }
                    RegisterTile(gridTile);
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if (tilesParent != null)
                {
                    UnityEditor.EditorUtility.SetDirty(tilesParent.gameObject);
                }
                if (gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

        private void EnsureTilesParentExists()
        {
            if (tilesParent == null)
            {
                Transform existing = transform.Find("Tiles");
                if (existing != null)
                {
                    tilesParent = existing;
                }
                else
                {
                    GameObject tilesObj = new GameObject("Tiles");
                    tilesObj.transform.SetParent(transform, false);
                    tilesObj.transform.localPosition = Vector3.zero;
                    tilesObj.transform.localRotation = Quaternion.identity;
                    tilesObj.transform.localScale = Vector3.one;
                    tilesParent = tilesObj.transform;
                }
            }
        }

        /// <summary>
        /// Clears all existing registered tiles and destroys their GameObjects.
        /// Can be called in Play mode or in Edit mode via Inspector context menu.
        /// </summary>
        [ContextMenu("Clear Grid")]
        public void ClearGrid()
        {
            List<GameObject> toDestroy = new List<GameObject>();

            foreach (var kvp in tiles)
            {
                if (kvp.Value != null)
                {
                    toDestroy.Add(kvp.Value.gameObject);
                }
            }

            Transform parent = tilesParent != null ? tilesParent : transform.Find("Tiles");
            if (parent != null)
            {
                GridTile[] childTiles = parent.GetComponentsInChildren<GridTile>(true);
                foreach (GridTile ct in childTiles)
                {
                    if (ct != null && !toDestroy.Contains(ct.gameObject))
                    {
                        toDestroy.Add(ct.gameObject);
                    }
                }
            }

            foreach (GameObject obj in toDestroy)
            {
                if (obj != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        DestroyImmediate(obj);
                    }
                    else
                    {
                        Destroy(obj);
                    }
#else
                    Destroy(obj);
#endif
                }
            }

            tiles.Clear();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if (tilesParent != null)
                {
                    UnityEditor.EditorUtility.SetDirty(tilesParent.gameObject);
                }
                if (gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
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
        /// Converts a grid coordinate into local space relative to the GridManager transform.
        /// </summary>
        public Vector3 GetLocalTilePosition(Vector2Int gridPos)
        {
            float halfX = centerGridOnTransform ? (width - 1) * 0.5f * tileSize : 0f;
            float halfZ = centerGridOnTransform ? (height - 1) * 0.5f * tileSize : 0f;
            return new Vector3(gridPos.x * tileSize - halfX, 0f, gridPos.y * tileSize - halfZ);
        }

        /// <summary>
        /// Converts a grid coordinate into 3D world space coordinates.
        /// When useTransformAsGridOrigin is true, tracks transform.position, rotation, and scale dynamically.
        /// </summary>
        public Vector3 GetWorldPosition(Vector2Int gridPos)
        {
            if (useTransformAsGridOrigin)
            {
                return transform.TransformPoint(GetLocalTilePosition(gridPos));
            }
            return originWorldPosition + new Vector3(gridPos.x * tileSize, 0f, gridPos.y * tileSize);
        }

        /// <summary>
        /// Converts a 3D world space coordinate into the nearest grid coordinate.
        /// When useTransformAsGridOrigin is true, tracks transform.position, rotation, and scale dynamically.
        /// </summary>
        public Vector2Int GetGridPosition(Vector3 worldPos)
        {
            if (useTransformAsGridOrigin)
            {
                Vector3 local = transform.InverseTransformPoint(worldPos);
                float halfX = centerGridOnTransform ? (width - 1) * 0.5f * tileSize : 0f;
                float halfZ = centerGridOnTransform ? (height - 1) * 0.5f * tileSize : 0f;
                int x = Mathf.RoundToInt((local.x + halfX) / tileSize);
                int y = Mathf.RoundToInt((local.z + halfZ) / tileSize);
                return new Vector2Int(x, y);
            }
            Vector3 localLegacy = worldPos - originWorldPosition;
            int xLegacy = Mathf.RoundToInt(localLegacy.x / tileSize);
            int yLegacy = Mathf.RoundToInt(localLegacy.z / tileSize);
            return new Vector2Int(xLegacy, yLegacy);
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

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float halfX = centerGridOnTransform ? (width - 1) * 0.5f * tileSize : 0f;
            float halfZ = centerGridOnTransform ? (height - 1) * 0.5f * tileSize : 0f;
            Vector3 centerLocal = new Vector3(centerGridOnTransform ? 0f : halfX, 0.05f, centerGridOnTransform ? 0f : halfZ);
            Vector3 sizeLocal = new Vector3(width * tileSize, 0.1f, height * tileSize);

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Wireframe bounds of the entire grid
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireCube(centerLocal, sizeLocal);

            // Semi-transparent center fill
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.08f);
            Gizmos.DrawCube(centerLocal, sizeLocal);

            // Cell bounds
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 tLoc = GetLocalTilePosition(new Vector2Int(x, y));
                    Gizmos.DrawWireCube(tLoc + Vector3.up * 0.05f, new Vector3(tileSize * 0.92f, 0.02f, tileSize * 0.92f));
                }
            }

            Gizmos.matrix = oldMatrix;
        }
#endif

        #endregion
    }
}