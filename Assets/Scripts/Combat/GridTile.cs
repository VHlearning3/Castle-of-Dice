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

        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color hoveredColor = new Color(1f, 0.92f, 0.016f, 0.7f);
        [SerializeField] private Color reachableColor = new Color(0.2f, 0.6f, 1f, 0.6f);
        [SerializeField] private Color targetAreaColor = new Color(1f, 0.4f, 0.1f, 0.75f);
        [SerializeField] private Color enemyTargetColor = new Color(0.9f, 0.15f, 0.15f, 0.8f);

        #endregion

        #region Private State

        private CombatUnit occupyingUnit;
        private TileHighlightType currentHighlight = TileHighlightType.Normal;
        private MaterialPropertyBlock propBlock;

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

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            tileRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", targetColor);
            propBlock.SetColor("_Color", targetColor);
            tileRenderer.SetPropertyBlock(propBlock);
        }

        #endregion
    }
}
