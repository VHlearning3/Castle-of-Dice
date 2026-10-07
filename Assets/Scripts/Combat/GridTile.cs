using System;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Visual highlight state for a grid cell during exploration and combat.
    /// </summary>
    public enum TileHighlightType
    {
        /// <summary>Default unhighlighted state.</summary>
        Normal,

        /// <summary>Mouse cursor is currently hovering over the tile.</summary>
        Hovered,

        /// <summary>Tile is within valid movement range of the active unit.</summary>
        Reachable,

        /// <summary>Tile is within targeted ability impact zone (e.g. 3x3 Fireball area).</summary>
        TargetArea,

        /// <summary>Tile contains an attackable hostile unit.</summary>
        EnemyTarget
    }

    /// <summary>
    /// Cover a blocked tile gives against ranged attacks across it (critical review B6).
    /// </summary>
    public enum TileCover
    {
        /// <summary>Open ground.</summary>
        None,

        /// <summary>Low obstacle (crate, barrel, table): +2 AC against ranged attacks across it.</summary>
        Half,

        /// <summary>Tall obstacle (pillar, bookshelf, wall): blocks ranged attacks across it.</summary>
        Full
    }

    /// <summary>
    /// Lasting ground effects on a walkable tile (critical review B6).
    /// </summary>
    public enum TileTerrain
    {
        /// <summary>Plain floor.</summary>
        None,

        /// <summary>Flames left by Fireball or a barrel blast: 1d4 fire damage at the start of a turn spent on it.</summary>
        Burning,

        /// <summary>Slippery ice left by Frostbite: DEX DC 10 at the start of a turn or no moving that turn.</summary>
        Ice
    }

    /// <summary>
    /// Represents an individual cell on the tactical combat grid.
    /// Handles coordinate tracking, occupation status, mouse interaction, and visual highlight states.
    /// </summary>
    [SelectionBase]
    public class GridTile : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Grid Coordinates")]
        [SerializeField] private Vector2Int gridPosition;

        [Header("Walkability & Obstacles")]
        [Tooltip("Whether units can traverse or stand on this tile.")]
        [SerializeField] private bool isWalkable = true;

        [Header("Visual Highlighting")]
        [Tooltip("MeshRenderer or SpriteRenderer used to display tile highlight colors.")]
        [SerializeField] private Renderer tileRenderer;

        // Alpha is the strength of the tile's edge; the see-through overlay fills the inside far fainter
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color hoveredColor = new Color(1f, 0.92f, 0.2f, 1f);
        [SerializeField] private Color reachableColor = new Color(0.3f, 0.7f, 1f, 0.95f);
        [SerializeField] private Color targetAreaColor = new Color(1f, 0.5f, 0.15f, 1f);
        [SerializeField] private Color enemyTargetColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color hazardWarningColor = new Color(1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color burningColor = new Color(1f, 0.5f, 0.1f, 0.95f);
        [SerializeField] private Color iceColor = new Color(0.6f, 0.9f, 1f, 0.95f);

        #endregion

        #region Private State

        private CombatUnit occupyingUnit;
        private TileHighlightType currentHighlight = TileHighlightType.Normal;
        private MaterialPropertyBlock propBlock;

        private TileCover cover = TileCover.None;
        private TileTerrain terrain = TileTerrain.None;
        private int terrainRounds;
        private bool hazardWarning;
        private ExplosiveBarrel barrel;

        private const string OverlayMaterialResource = "Combat/M_GridTile_Overlay";
        private const int OverlayTextureSize = 64;
        private static Material s_overlayMaterial;

        #endregion

        #region Public Properties

        /// <summary>Grid coordinate (X, Y).</summary>
        public Vector2Int GridPosition => gridPosition;

        /// <summary>True if a combat unit currently stands on this cell.</summary>
        public bool IsOccupied
        {
            get => occupyingUnit != null && occupyingUnit.IsAlive;
            set
            {
                if (!value) occupyingUnit = null;
            }
        }

        /// <summary>Reference to the combat unit occupying this cell, or null if vacant.</summary>
        public CombatUnit OccupyingUnit
        {
            get => occupyingUnit;
            set => occupyingUnit = value;
        }

        /// <summary>Whether this tile can be traversed by units.</summary>
        public bool IsWalkable
        {
            get => isWalkable;
            set => isWalkable = value;
        }

        /// <summary>Current highlight mode applied to this tile.</summary>
        public TileHighlightType CurrentHighlight => currentHighlight;

        /// <summary>Cover this (blocked) tile gives against ranged attacks across it.</summary>
        public TileCover Cover
        {
            get => cover;
            set => cover = value;
        }

        /// <summary>Lasting ground effect on this tile.</summary>
        public TileTerrain Terrain => terrain;

        /// <summary>Rounds the ground effect has left.</summary>
        public int TerrainRounds => terrainRounds;

        /// <summary>
        /// Marked red as the target of an attack that lands next round (the Gargoyle King's earthquake).
        /// Stays visible through highlight resets until cleared.
        /// </summary>
        public bool HazardWarning
        {
            get => hazardWarning;
            set
            {
                hazardWarning = value;
                ApplyHighlightColor(currentHighlight);
            }
        }

        /// <summary>The explosive barrel standing on this tile, if any.</summary>
        public ExplosiveBarrel Barrel
        {
            get => barrel != null ? barrel : null;
            set => barrel = value;
        }

        /// <summary>
        /// Covers the tile with flames or ice for <paramref name="rounds"/> rounds (None clears it).
        /// Blocked tiles never take ground effects.
        /// </summary>
        public void SetTerrain(TileTerrain newTerrain, int rounds)
        {
            if (!isWalkable && newTerrain != TileTerrain.None) return;
            terrain = rounds > 0 ? newTerrain : TileTerrain.None;
            terrainRounds = terrain == TileTerrain.None ? 0 : rounds;

            // A tile hidden as an obstacle has no renderer to show the effect on
            ApplyHighlightColor(currentHighlight);
        }

        /// <summary>Counts the ground effect down by one round.</summary>
        public void TickTerrain()
        {
            if (terrain == TileTerrain.None) return;
            terrainRounds--;
            if (terrainRounds <= 0)
            {
                SetTerrain(TileTerrain.None, 0);
            }
        }

        #endregion

        #region Events

        /// <summary>Fired when any tile is clicked by the player.</summary>
        public static event Action<GridTile> OnTileClicked;

        /// <summary>Fired when mouse enters this tile.</summary>
        public static event Action<GridTile> OnTileHovered;

        /// <summary>Fired when mouse exits this tile.</summary>
        public static event Action<GridTile> OnTileUnhovered;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (tileRenderer == null)
            {
                tileRenderer = GetComponentInChildren<Renderer>();
            }

            propBlock = new MaterialPropertyBlock();

            // Baldur's Gate 3 style: the floor shows through the tile, only its edge is drawn strongly
            if (Application.isPlaying && tileRenderer != null)
            {
                tileRenderer.sharedMaterial = OverlayMaterial;
                tileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tileRenderer.receiveShadows = false;
            }

            // Ensure a thick BoxCollider exists for reliable physics raycasting
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                // Remove thin MeshCollider if present on primitive Quad
                MeshCollider mc = GetComponent<MeshCollider>();
                if (mc != null)
                {
                    if (Application.isPlaying) Destroy(mc);
                    else DestroyImmediate(mc);
                }
                box = gameObject.AddComponent<BoxCollider>();
            }
            box.size = new Vector3(1f, 1f, 0.4f);
            box.center = new Vector3(0f, 0f, -0.2f);
            box.isTrigger = false;

            ApplyHighlightColor(TileHighlightType.Normal);
        }

        private void OnMouseEnter()
        {
            OnTileHovered?.Invoke(this);

            if (currentHighlight == TileHighlightType.Normal)
            {
                ApplyHighlightColor(TileHighlightType.Hovered);
            }
        }

        private void OnMouseExit()
        {
            OnTileUnhovered?.Invoke(this);

            // Revert back to the persistent highlight state
            ApplyHighlightColor(currentHighlight);
        }

        private void OnMouseDown()
        {
            if (!GameInput.GetLeftMouseButtonDown()) return;
            OnTileClicked?.Invoke(this);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes tile coordinates and initial walkability state.
        /// </summary>
        public void Initialize(Vector2Int position, bool walkable = true)
        {
            gridPosition = position;
            isWalkable = walkable;
            name = $"Tile_{position.x}_{position.y}";
        }

        /// <summary>
        /// Sets a persistent highlight state on this tile (e.g. reachable, target area, enemy).
        /// </summary>
        public void SetHighlight(TileHighlightType type)
        {
            currentHighlight = type;
            ApplyHighlightColor(type);
        }

        /// <summary>
        /// Alias for SetHighlight.
        /// </summary>
        public void ApplyHighlight(TileHighlightType type)
        {
            SetHighlight(type);
        }

        /// <summary>
        /// Resets the tile highlight back to normal.
        /// </summary>
        public void ResetHighlight()
        {
            SetHighlight(TileHighlightType.Normal);
        }

        /// <summary>
        /// Programmatically triggers hover for non-mouse inputs (touch/gamepad).
        /// </summary>
        public void TriggerHover() => OnMouseEnter();

        /// <summary>
        /// Programmatically triggers unhover.
        /// </summary>
        public void TriggerUnhover() => OnMouseExit();

        /// <summary>
        /// Programmatically triggers a tile click.
        /// </summary>
        public void TriggerClick() => OnMouseDown();

        #endregion

        #region Private Utilities

        /// <summary>
        /// The see-through material every combat tile shares: alpha blended, with a texture whose edge
        /// is opaque and whose inside is faint, so the tile colour reads as an outline over the floor.
        /// </summary>
        public static Material OverlayMaterial
        {
            get
            {
                if (s_overlayMaterial != null) return s_overlayMaterial;

                Material template = Resources.Load<Material>(OverlayMaterialResource);
                if (template == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    template = new Material(shader);
                }

                Texture2D tex = BuildOverlayTexture();
                s_overlayMaterial = new Material(template) { name = "GridTile_Overlay", mainTexture = tex };
                if (s_overlayMaterial.HasProperty("_BaseMap")) s_overlayMaterial.SetTexture("_BaseMap", tex);
                return s_overlayMaterial;
            }
        }

        /// <summary>Alpha of the overlay texture at a pixel: 1 on the edge, a soft glow inside it, faint in the middle.</summary>
        public static float OverlayAlpha(int x, int y)
        {
            int last = OverlayTextureSize - 1;
            int edge = Mathf.Min(Mathf.Min(x, last - x), Mathf.Min(y, last - y));
            if (edge < 3) return 1f;
            if (edge < 8) return Mathf.Lerp(0.5f, 0.12f, (edge - 3) / 5f);
            return 0.12f;
        }

        private static Texture2D BuildOverlayTexture()
        {
            Texture2D tex = new Texture2D(OverlayTextureSize, OverlayTextureSize, TextureFormat.RGBA32, true)
            {
                name = "GridTile_Overlay",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            Color32[] pixels = new Color32[OverlayTextureSize * OverlayTextureSize];
            for (int y = 0; y < OverlayTextureSize; y++)
            {
                for (int x = 0; x < OverlayTextureSize; x++)
                {
                    pixels[y * OverlayTextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(OverlayAlpha(x, y) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private void ApplyHighlightColor(TileHighlightType type)
        {
            if (tileRenderer == null) return;

            Color targetColor = type switch
            {
                TileHighlightType.Hovered => hoveredColor,
                TileHighlightType.Reachable => reachableColor,
                TileHighlightType.TargetArea => targetAreaColor,
                TileHighlightType.EnemyTarget => enemyTargetColor,
                _ => normalColor
            };

            // Warnings and ground effects show through the plain floor colour
            if (type == TileHighlightType.Normal)
            {
                if (hazardWarning) targetColor = hazardWarningColor;
                else if (terrain == TileTerrain.Burning) targetColor = burningColor;
                else if (terrain == TileTerrain.Ice) targetColor = iceColor;
            }
            else if (hazardWarning && type == TileHighlightType.Reachable)
            {
                targetColor = Color.Lerp(reachableColor, hazardWarningColor, 0.6f);
            }

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            tileRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", targetColor);
            propBlock.SetColor("_Color", targetColor);
            tileRenderer.SetPropertyBlock(propBlock);
        }

        #endregion
    }
}
