using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// User Interface Controller for tactical turn-based combat.
    /// Manages the 4-slot ability action bar, End Turn button, turn phase banners,
    /// and a scrolling combat activity log.
    /// </summary>
    public class CombatUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Root & Containers")]
        [Tooltip("Root GameObject containing the combat action bar and turn controls (e.g. CombatActionBar).")]
        [SerializeField] private GameObject combatActionBar;

        [Header("Ability Action Bar (4 Slots)")]
        [Tooltip("Button components corresponding to the 4 class abilities.")]
        [SerializeField] private List<Button> abilityButtons = new List<Button>(4);

        [Tooltip("Icon images for the 4 ability buttons.")]
        [SerializeField] private List<Image> abilityIcons = new List<Image>(4);

        [Tooltip("Labels displaying ability names on the 4 buttons.")]
        [SerializeField] private List<TMP_Text> abilityNames = new List<TMP_Text>(4);

        [Tooltip("Labels displaying ability range on the 4 buttons.")]
        [SerializeField] private List<TMP_Text> abilityRanges = new List<TMP_Text>(4);

        [Header("Turn Controls & Banner")]
        [Tooltip("Button to manually conclude the hero's turn.")]
        [SerializeField] private Button endTurnButton;

        [Tooltip("Banner text indicating turn phase (PLAYER TURN / ENEMY TURN / VICTORY / DEFEAT).")]
        [SerializeField] private TMP_Text turnBannerText;

        [Tooltip("Round counter display text (e.g. 'Round 1').")]
        [SerializeField] private TMP_Text roundCounterText;

        [Header("Combat Log")]
        [Tooltip("Text component displaying scrolling combat activity messages.")]
        [SerializeField] private TMP_Text combatLogText;

        [Tooltip("Maximum lines displayed in the combat log before pruning.")]
        [SerializeField] private int maxLogLines = 20;

        #endregion

        #region Private State

        private PlayerUnit activePlayer;
        private readonly List<string> logHistory = new List<string>();
        private int selectedAbilitySlot = -1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (combatActionBar == null)
            {
                Transform barTransform = transform.Find("CombatActionBar")
                    ?? transform.Find("ActionBar")
                    ?? transform.Find("CombatPanel");

                if (barTransform != null)
                {
                    combatActionBar = barTransform.gameObject;
                }
                else if (abilityButtons.Count > 0 && abilityButtons[0] != null)
                {
                    combatActionBar = abilityButtons[0].transform.parent?.gameObject;
                }
            }

            // Hide action bar by default until combat is active
            SetCombatBarVisible(false);

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            // Hook up ability slot clicks
            for (int i = 0; i < abilityButtons.Count; i++)
            {
                int slotIndex = i;
                abilityButtons[i].onClick.AddListener(() => OnAbilitySlotClicked(slotIndex));
            }
        }

        private void OnEnable()
        {
            GameManager.OnPlayModeChanged += HandlePlayModeChanged;
            TurnManager.OnTurnStateChanged += HandleTurnStateChanged;
            TurnManager.OnUnitTurnStarted += HandleUnitTurnStarted;
            TurnManager.OnCombatEnded += HandleCombatEnded;
            CombatUnit.OnAnyUnitDamaged += HandleUnitDamaged;
            GridTile.OnTileClicked += HandleTileClicked;
        }

        private void OnDisable()
        {
            GameManager.OnPlayModeChanged -= HandlePlayModeChanged;
            TurnManager.OnTurnStateChanged -= HandleTurnStateChanged;
            TurnManager.OnUnitTurnStarted -= HandleUnitTurnStarted;
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            CombatUnit.OnAnyUnitDamaged -= HandleUnitDamaged;
            GridTile.OnTileClicked -= HandleTileClicked;
        }

        private void Start()
        {
            LocatePlayer();
            RefreshAbilityBar();

            // Ensure action bar reflects initial game mode
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Combat)
            {
                SetCombatBarVisible(false);
            }
        }

        #endregion

        #region Player & Ability Bar Setup

        private void LocatePlayer()
        {
            if (activePlayer == null)
            {
                activePlayer = FindAnyObjectByType<PlayerUnit>();
            }
        }

        /// <summary>
        /// Populates the 4 action buttons with abilities from the hero's CharacterClassSO loadout.
        /// </summary>
        public void RefreshAbilityBar()
        {
            LocatePlayer();

            if (activePlayer == null) return;

            IReadOnlyList<AbilitySO> abilities = activePlayer.ActiveAbilities;

            for (int i = 0; i < abilityButtons.Count; i++)
            {
                if (i < abilities.Count && abilities[i] != null)
                {
                    AbilitySO ability = abilities[i];
                    abilityButtons[i].gameObject.SetActive(true);

                    if (abilityNames.Count > i && abilityNames[i] != null)
                    {
                        abilityNames[i].text = ability.AbilityName;
                    }

                    if (abilityRanges.Count > i && abilityRanges[i] != null)
                    {
                        abilityRanges[i].text = ability.TargetType == AbilityTargetType.Self ? "Self" : $"Rng: {ability.Range}";
                    }

                    if (abilityIcons.Count > i && abilityIcons[i] != null)
                    {
                        if (ability.AbilityIcon != null)
                        {
                            abilityIcons[i].sprite = ability.AbilityIcon;
                            abilityIcons[i].enabled = true;
                        }
                    }

                    // Enable/disable based on whether action has already been used
                    abilityButtons[i].interactable = !activePlayer.HasActedThisTurn && TurnManager.Instance?.CurrentState == TurnState.PlayerTurn;
                }
                else
                {
                    abilityButtons[i].gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region UI Event Callbacks

        private void OnAbilitySlotClicked(int slotIndex)
        {
            LocatePlayer();
            if (activePlayer == null || activePlayer.HasActedThisTurn) return;

            AbilitySO ability = activePlayer.GetAbility(slotIndex);
            if (ability == null) return;

            selectedAbilitySlot = slotIndex;
            LogCombatMessage($"Selected: {ability.AbilityName}. Target a grid cell.");

            // If it's a Self ability, execute immediately
            if (ability.TargetType == AbilityTargetType.Self)
            {
                activePlayer.UseAbility(slotIndex, activePlayer.GridPosition, AbilityExecutor.Instance);
                selectedAbilitySlot = -1;
                RefreshAbilityBar();
                return;
            }

            // Highlight targetable area
            HighlightAbilityTargets(ability);
        }

        private void HighlightAbilityTargets(AbilitySO ability)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || activePlayer == null) return;

            grid.ClearAllHighlights();
            List<GridTile> targetableTiles = grid.GetTilesInRadius(activePlayer.GridPosition, ability.Range);
            grid.HighlightTiles(targetableTiles, TileHighlightType.TargetArea);
        }

        private void HandleTileClicked(GridTile tile)
        {
            if (tile == null || selectedAbilitySlot < 0) return;
            LocatePlayer();
            if (activePlayer == null || activePlayer.HasActedThisTurn) return;

            // Execute selected ability on clicked tile
            bool success = activePlayer.UseAbility(selectedAbilitySlot, tile.GridPosition, AbilityExecutor.Instance);
            if (success)
            {
                selectedAbilitySlot = -1;
                GridManager.Instance?.ClearAllHighlights();
                RefreshAbilityBar();
            }
        }

        private void OnEndTurnClicked()
        {
            selectedAbilitySlot = -1;
            GridManager.Instance?.ClearAllHighlights();
            TurnManager.Instance?.EndPlayerTurn();
        }

        #endregion

        #region Turn & State Listeners

        private void HandlePlayModeChanged(GamePlayMode mode)
        {
            if (mode == GamePlayMode.Combat)
            {
                bool isPlayerTurn = TurnManager.Instance != null && TurnManager.Instance.IsCombatActive && TurnManager.Instance.CurrentState == TurnState.PlayerTurn;
                SetCombatBarVisible(isPlayerTurn);
            }
            else
            {
                SetCombatBarVisible(false);
            }
        }

        private void HandleTurnStateChanged(TurnState newState)
        {
            bool showBar = (newState == TurnState.PlayerTurn) && (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive);
            SetCombatBarVisible(showBar);

            if (turnBannerText != null)
            {
                turnBannerText.text = newState switch
                {
                    TurnState.PlayerTurn => "PLAYER TURN",
                    TurnState.EnemyTurn => "ENEMY TURN",
                    TurnState.ResolveAbilities => "RESOLVING ACTIONS...",
                    TurnState.Victory => "VICTORY!",
                    TurnState.Defeat => "DEFEAT",
                    _ => ""
                };
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = (newState == TurnState.PlayerTurn);
            }

            RefreshAbilityBar();
        }

        private void HandleUnitTurnStarted(CombatUnit unit)
        {
            if (roundCounterText != null && TurnManager.Instance != null)
            {
                roundCounterText.text = $"Round {TurnManager.Instance.TurnCounter}";
            }

            if (unit != null)
            {
                LogCombatMessage($"--- Turn: {unit.UnitName} ---");
            }

            bool isPlayer = unit is PlayerUnit;
            SetCombatBarVisible(isPlayer);
            RefreshAbilityBar();
        }

        private void HandleUnitDamaged(CombatUnit unit, int damage, bool isCritical)
        {
            string critLabel = isCritical ? " [CRITICAL HIT!]" : "";
            LogCombatMessage($"{unit.UnitName} took {damage} damage{critLabel}. (HP: {unit.CurrentHP}/{unit.MaxHP})");
        }

        private void HandleCombatEnded(bool isVictory)
        {
            SetCombatBarVisible(false);
            string outcome = isVictory ? "VICTORY! All foes vanquished." : "DEFEAT! Party defeated.";
            LogCombatMessage(outcome);
            RefreshAbilityBar();
        }

        /// <summary>
        /// Controls visibility of the combat action bar and associated turn controls.
        /// </summary>
        public void SetCombatBarVisible(bool visible)
        {
            if (combatActionBar != null)
            {
                combatActionBar.SetActive(visible);
            }

            if (!visible)
            {
                selectedAbilitySlot = -1;
                GridManager.Instance?.ClearAllHighlights();
            }
        }

        #endregion

        #region Combat Activity Logging

        /// <summary>
        /// Appends a new line of text to the combat log display.
        /// </summary>
        public void LogCombatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            logHistory.Add(message);
            if (logHistory.Count > maxLogLines)
            {
                logHistory.RemoveAt(0);
            }

            if (combatLogText != null)
            {
                combatLogText.text = string.Join("\n", logHistory);
            }
        }

        /// <summary>
        /// Clears all entries from the combat log.
        /// </summary>
        public void ClearLog()
        {
            logHistory.Clear();
            if (combatLogText != null)
            {
                combatLogText.text = "";
            }
        }

        #endregion
    }
}
