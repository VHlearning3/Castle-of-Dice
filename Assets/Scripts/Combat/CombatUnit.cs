using System;
using UnityEngine;

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

            // Locate starting tile if already placed in scene
            if (GridManager.Instance != null && currentTile == null)
            {
                Vector2Int initialPos = GridManager.Instance.GetGridPosition(transform.position);
                GridTile startingTile = GridManager.Instance.GetTileAt(initialPos);
                if (startingTile != null)
                {
                    MoveToTile(startingTile);
                }
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
                if (GridManager.Instance != null)
                {
                    transform.position = GridManager.Instance.GetWorldPosition(gridPosition);
                }
                else
                {
                    transform.position = tile.transform.position;
                }
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
