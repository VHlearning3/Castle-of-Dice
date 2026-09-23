using System;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Abstract base class for all combatants on the battlefield (Heroes, Minions, Bosses).
    /// Manages core combat statistics, health events, tile occupancy, status effects, and movement.
    /// </summary>
    [RequireComponent(typeof(StatusEffectController))]
    public abstract class CombatUnit : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Unit Identity")]
        [SerializeField] protected string unitName = "Combatant";

        [Header("Combat Statistics")]
        [SerializeField] protected int maxHP = 20;
        [SerializeField] protected int currentHP = 20;
        [SerializeField] protected int armorClass = 12;
        [SerializeField] protected int movementRange = 3;

        [Header("Components")]
        [SerializeField] protected StatusEffectController statusEffects;

        #endregion

        #region Protected / Private State

        protected Vector2Int gridPosition;
        protected GridTile currentTile;
        protected bool isDead = false;

        #endregion

        #region Public Properties

        /// <summary>Display name of the unit.</summary>
        public string UnitName => unitName;

        /// <summary>Maximum hit points.</summary>
        public int MaxHP => maxHP;

        /// <summary>Current remaining hit points.</summary>
        public int CurrentHP => currentHP;

        /// <summary>Armor Class determining attack DC for attackers.</summary>
        public virtual int ArmorClass => armorClass;

        /// <summary>
        /// Movement distance in grid tiles per turn, modified by status effects like Frostbite.
        /// </summary>
        public virtual int MovementRange
        {
            get
            {
                if (statusEffects != null)
                {
                    return statusEffects.GetEffectiveMovementRange(movementRange);
                }
                return movementRange;
            }
        }

        /// <summary>Current grid coordinate (X, Y).</summary>
        public Vector2Int GridPosition => gridPosition;

        /// <summary>Reference to the GridTile currently occupied by this unit.</summary>
        public GridTile CurrentTile => currentTile;

        /// <summary>Whether the unit is alive and able to participate in combat.</summary>
        public bool IsAlive => !isDead && currentHP > 0;

        /// <summary>Reference to the attached StatusEffectController component.</summary>
        public StatusEffectController StatusEffects => statusEffects;

        #endregion

        #region Events

        /// <summary>Fired whenever health changes: (currentHP, maxHP).</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Fired when this unit dies.</summary>
        public event Action<CombatUnit> OnUnitDied;

        /// <summary>Global event fired when any combatant receives damage: (unit, damageAmount, isCritical).</summary>
        public static event Action<CombatUnit, int, bool> OnAnyUnitDamaged;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            if (statusEffects == null)
            {
                statusEffects = GetComponent<StatusEffectController>();
            }

            currentHP = maxHP;
        }

        protected virtual void Start()
        {
            InitializeUnit();
        }

        #endregion

        #region Combat Lifecycle

        /// <summary>
        /// Configures initial stats and registers the unit onto the grid at its starting position.
        /// </summary>
        public virtual void InitializeUnit()
        {
            currentHP = Mathf.Clamp(currentHP, 1, maxHP);
            isDead = false;

            // Only locate starting tile if combat is actively running and the unit is on the same floor as the grid
            if (GridManager.Instance != null && currentTile == null &&
                (GameManager.Instance != null && GameManager.Instance.CurrentMode == GamePlayMode.Combat))
            {
                float gridY = GridManager.Instance.transform.position.y;
                if (Mathf.Abs(transform.position.y - gridY) <= 3.5f)
                {
                    Vector2Int initialPos = GridManager.Instance.GetGridPosition(transform.position);
                    GridTile startingTile = GridManager.Instance.GetTileAt(initialPos);
                    if (startingTile != null)
                    {
                        MoveToTile(startingTile);
                    }
                }
            }
        }

        /// <summary>
        /// Ensures that currentTile and gridPosition are accurately synchronized with the unit's transform position on the grid.
        /// </summary>
        public virtual void EnsureTilePosition()
        {
            if (GridManager.Instance == null || GridManager.Instance.Tiles.Count == 0) return;

            // Do not snap units to the grid if not in Combat mode (e.g. during free village exploration)
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Combat) return;

            // Do not snap across different floor elevations (e.g. surface village at Y=1 vs cellar at Y=-15)
            float gridY = GridManager.Instance.transform.position.y;
            if (Mathf.Abs(transform.position.y - gridY) > 3.5f) return;

            Vector2Int currentPosOnGrid = GridManager.Instance.GetGridPosition(transform.position);
            if (currentTile == null || !currentTile.gameObject.activeInHierarchy || gridPosition != currentPosOnGrid)
            {
                GridTile pTile = GridManager.Instance.GetTileAt(currentPosOnGrid);
                if (pTile == null || !pTile.IsWalkable)
                {
                    pTile = GridManager.Instance.FindClosestWalkableTile(transform.position);
                }
                if (pTile != null)
                {
                    MoveToTile(pTile);
                }
            }
            else if (currentTile != null)
            {
                currentTile.OccupyingUnit = this;
                currentTile.IsOccupied = true;
            }
        }

        /// <summary>
        /// Applies incoming damage, checks Mana Shield absorption, reduces current HP, and evaluates death.
        /// </summary>
        /// <param name="amount">Amount of raw damage to deal.</param>
        /// <param name="isCritical">Whether the incoming hit was a Natural 20 critical strike.</param>
        public virtual void TakeDamage(int amount, bool isCritical = false)
        {
            if (!IsAlive || amount <= 0) return;

            // Check Mana Shield absorption
            if (statusEffects != null && statusEffects.ConsumeManaShield())
            {
                Debug.Log($"[CombatUnit] {unitName}'s Mana Shield completely absorbed {amount} damage!");
                return;
            }

            currentHP = Mathf.Max(0, currentHP - amount);
            Debug.Log($"[CombatUnit] {unitName} took {amount} damage{(isCritical ? " (CRITICAL HIT!)" : "")}. Remaining HP: {currentHP}/{maxHP}");

            OnHealthChanged?.Invoke(currentHP, maxHP);
            OnAnyUnitDamaged?.Invoke(this, amount, isCritical);

            if (currentHP <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Restores hit points up to maximum health.
        /// </summary>
        /// <param name="amount">Hit points restored.</param>
        public virtual void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            currentHP = Mathf.Min(maxHP, currentHP + amount);
            Debug.Log($"[CombatUnit] {unitName} healed for {amount} HP. Current HP: {currentHP}/{maxHP}");

            OnHealthChanged?.Invoke(currentHP, maxHP);
        }

        /// <summary>
        /// Releases the currently occupied tile without moving to a new tile.
        /// Useful when transitioning to free-form exploration mode.
        /// </summary>
        public virtual void ClearTile()
        {
            if (currentTile != null && currentTile.OccupyingUnit == this)
            {
                currentTile.OccupyingUnit = null;
                currentTile.IsOccupied = false;
            }
            currentTile = null;
        }

        /// <summary>
        /// Moves this unit to an unoccupied grid tile and updates spatial coordinates.
        /// </summary>
        public virtual void MoveToTile(GridTile tile)
        {
            if (tile == null) return;

            // Release previous tile
            if (currentTile != null && currentTile.OccupyingUnit == this)
            {
                currentTile.OccupyingUnit = null;
                currentTile.IsOccupied = false;
            }

            // Assign new tile
            currentTile = tile;
            currentTile.OccupyingUnit = this;
            currentTile.IsOccupied = true;
            gridPosition = tile.GridPosition;

            // Update physical transform safely if CharacterController is present
            CharacterController cc = GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (ccWasEnabled) cc.enabled = false;

            try
            {
                Vector3 targetWorldPos = GridManager.Instance != null
                    ? GridManager.Instance.GetWorldPosition(gridPosition)
                    : tile.transform.position;

                // Adjust vertical position so the unit stands cleanly on top of the tile surface
                if (cc != null)
                {
                    float bottomOffset = (cc.height * 0.5f) - cc.center.y;
                    if (bottomOffset > 0f)
                    {
                        targetWorldPos.y += bottomOffset;
                    }
                }
                else
                {
                    Collider col = GetComponent<Collider>();
                    if (col != null)
                    {
                        targetWorldPos.y += col.bounds.extents.y;
                    }
                }

                transform.position = targetWorldPos;
                Physics.SyncTransforms();
            }
            finally
            {
                if (ccWasEnabled) cc.enabled = true;
            }
        }

        /// <summary>
        /// Handles death cleanup, frees the occupied tile, and triggers death notifications.
        /// </summary>
        public virtual void Die()
        {
            if (isDead) return;

            isDead = true;
            currentHP = 0;

            if (currentTile != null && currentTile.OccupyingUnit == this)
            {
                currentTile.OccupyingUnit = null;
                currentTile.IsOccupied = false;
            }

            Debug.Log($"[CombatUnit] {unitName} has died.");
            OnUnitDied?.Invoke(this);

            gameObject.SetActive(false);
        }

        #endregion
    }
}
