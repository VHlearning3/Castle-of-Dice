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

        #region Singleton & Access

        private static CombatUIController instance;

        /// <summary>
        /// Singleton instance accessor. Lazily discovers the component in the active scene
        /// (including inactive objects) or under any Canvas hierarchy.
        /// </summary>
        public static CombatUIController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<CombatUIController>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var canvas in canvases)
                        {
                            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.name.IndexOf("CombatActionBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    child.name.IndexOf("CombatPanel", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    instance = child.GetComponent<CombatUIController>() ?? child.gameObject.AddComponent<CombatUIController>();
                                    break;
                                }
                            }
                            if (instance != null) break;
                        }
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Private State

        private CanvasGroup canvasGroup;
        private PlayerUnit activePlayer;
        private readonly List<string> logHistory = new List<string>();
        private int selectedAbilitySlot = -1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            // Locate or initialize CanvasGroup for flicker-free show/hide without disabling GameObject
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

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
                endTurnButton.onClick.RemoveAllListeners();
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            // Hook up ability slot clicks
            for (int i = 0; i < abilityButtons.Count; i++)
            {
                int slotIndex = i;
                if (abilityButtons[i] != null)
                {
                    abilityButtons[i].onClick.RemoveAllListeners();
                    abilityButtons[i].onClick.AddListener(() => OnAbilitySlotClicked(slotIndex));
                }

                if (abilityNames.Count > i && abilityNames[i] != null)
                {
                    abilityNames[i].margin = Vector4.zero;
                    abilityNames[i].enableAutoSizing = true;
                    abilityNames[i].fontSizeMin = 11f;
                    abilityNames[i].fontSizeMax = 22f;
                    abilityNames[i].textWrappingMode = TextWrappingModes.Normal;
                    abilityNames[i].raycastTarget = false;
                }
                if (abilityRanges.Count > i && abilityRanges[i] != null)
                {
                    abilityRanges[i].margin = Vector4.zero;
                    abilityRanges[i].enableAutoSizing = true;
                    abilityRanges[i].fontSizeMin = 10f;
                    abilityRanges[i].fontSizeMax = 18f;
                    abilityRanges[i].raycastTarget = false;
                }
            }

            // Sanitize combat text components
            if (turnBannerText != null)
            {
                turnBannerText.margin = Vector4.zero;
                turnBannerText.enableAutoSizing = true;
                turnBannerText.fontSizeMin = 16f;
                turnBannerText.fontSizeMax = 36f;
                turnBannerText.textWrappingMode = TextWrappingModes.Normal;
                turnBannerText.raycastTarget = false;
            }

            if (roundCounterText != null)
            {
                roundCounterText.margin = Vector4.zero;
                roundCounterText.enableAutoSizing = true;
                roundCounterText.fontSizeMin = 12f;
                roundCounterText.fontSizeMax = 24f;
                roundCounterText.raycastTarget = false;
            }

            if (endTurnButton != null)
            {
                TMP_Text endText = endTurnButton.GetComponentInChildren<TMP_Text>(true);
                if (endText != null)
                {
                    endText.margin = Vector4.zero;
                    endText.enableAutoSizing = true;
                    endText.fontSizeMin = 12f;
                    endText.fontSizeMax = 24f;
                    endText.raycastTarget = false;
                }
            }

            if (combatLogText != null)
            {
                combatLogText.margin = Vector4.zero;
                combatLogText.enableAutoSizing = true;
                combatLogText.fontSizeMin = 12f;
                combatLogText.fontSizeMax = 20f;
                combatLogText.textWrappingMode = TextWrappingModes.Normal;
                combatLogText.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
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

        private GridTile lastHoveredTile;
        private Camera combatCamera;
        private readonly List<UnityEngine.EventSystems.RaycastResult> uiRaycastList = new List<UnityEngine.EventSystems.RaycastResult>();

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentMode != GamePlayMode.Combat)
            {
                ClearHoveredTile();
                return;
            }

            if (TurnManager.Instance == null || TurnManager.Instance.CurrentState != TurnState.PlayerTurn)
            {
                ClearHoveredTile();
                return;
            }

            HandleCombatTileRaycast();
        }

        private void ClearHoveredTile()
        {
            if (lastHoveredTile != null)
            {
                lastHoveredTile.TriggerUnhover();
                lastHoveredTile = null;
            }
        }

        private void HandleCombatTileRaycast()
        {
            // Do not raycast through interactive UI elements (buttons, dialogs, action bar)
            if (IsPointerOverUI())
            {
                ClearHoveredTile();
                return;
            }

            if (combatCamera == null)
            {
                combatCamera = Camera.main ?? FindAnyObjectByType<Camera>();
                if (combatCamera == null) return;
            }

            Ray ray = combatCamera.ScreenPointToRay(GameInput.GetMousePosition());
            GridTile targetTile = null;

            if (Physics.Raycast(ray, out RaycastHit hit, 250f))
            {
                // First check if a tile was directly hit
                targetTile = hit.collider.GetComponentInParent<GridTile>();

                // If hit a combat unit, target the tile occupied by that unit
                if (targetTile == null)
                {
                    CombatUnit hitUnit = hit.collider.GetComponentInParent<CombatUnit>();
                    if (hitUnit != null)
                    {
                        targetTile = hitUnit.CurrentTile ?? (GridManager.Instance != null ? GridManager.Instance.GetTileAt(hitUnit.GridPosition) : null);
                    }
                }
            }

            // Update hover state
            if (targetTile != lastHoveredTile)
            {
                if (lastHoveredTile != null)
                {
                    lastHoveredTile.TriggerUnhover();
                }

                if (targetTile != null)
                {
                    targetTile.TriggerHover();
                }

                lastHoveredTile = targetTile;
            }

            // Detect left click on tile
            if (targetTile != null && GameInput.GetLeftMouseButtonDown())
            {
                HandleTileClicked(targetTile);
            }
        }

        private bool IsPointerOverUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null) return false;
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return false;

            var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = GameInput.GetMousePosition()
            };

            uiRaycastList.Clear();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, uiRaycastList);

            for (int i = 0; i < uiRaycastList.Count; i++)
            {
                GameObject obj = uiRaycastList[i].gameObject;
                if (obj == null) continue;

                if (obj.GetComponentInParent<Selectable>() != null ||
                    obj.GetComponentInParent<Button>() != null ||
                    obj.GetComponentInParent<TMP_InputField>() != null)
                {
                    return true;
                }

                string n = obj.name.ToLowerInvariant();
                if (n.Contains("dialogue") || n.Contains("shop") || n.Contains("modal") || n.Contains("popup") || n.Contains("button"))
                {
                    return true;
                }
            }

            return false;
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

            // Clicking the same slot toggles it off
            if (selectedAbilitySlot == slotIndex)
            {
                selectedAbilitySlot = -1;
                GridManager.Instance?.ClearAllHighlights();
                if (!activePlayer.HasMovedThisTurn && GridManager.Instance != null)
                {
                    var reachable = GridManager.Instance.GetReachableTiles(activePlayer.GridPosition, activePlayer.MovementRange);
                    GridManager.Instance.HighlightTiles(reachable, TileHighlightType.Reachable);
                }
                RefreshAbilityBar();
                return;
            }

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
            if (tile == null) return;
            LocatePlayer();
            if (activePlayer == null) return;

            // 1. If an ability is actively selected, execute it on target tile
            if (selectedAbilitySlot >= 0)
            {
                if (activePlayer.HasActedThisTurn) return;

                bool success = activePlayer.UseAbility(selectedAbilitySlot, tile.GridPosition, AbilityExecutor.Instance);
                if (success)
                {
                    selectedAbilitySlot = -1;
                    GridManager.Instance?.ClearAllHighlights();
                    RefreshAbilityBar();

                    // Restore reachable tiles highlight if player still has movement
                    if (!activePlayer.HasMovedThisTurn && GridManager.Instance != null)
                    {
                        var reachable = GridManager.Instance.GetReachableTiles(activePlayer.GridPosition, activePlayer.MovementRange);
                        GridManager.Instance.HighlightTiles(reachable, TileHighlightType.Reachable);
                    }
                }
                return;
            }

            // 2. If no ability is selected, handle tactical movement
            if (!activePlayer.HasMovedThisTurn && tile.IsWalkable && !tile.IsOccupied && GridManager.Instance != null)
            {
                // Ensure activePlayer's current tile is known
                if (activePlayer.CurrentTile == null)
                {
                    Vector2Int pPos = GridManager.Instance.GetGridPosition(activePlayer.transform.position);
                    GridTile pTile = GridManager.Instance.GetTileAt(pPos);
                    if (pTile != null)
                    {
                        activePlayer.MoveToTile(pTile);
                        activePlayer.ResetTurnFlags();
                    }
                }

                var reachable = GridManager.Instance.GetReachableTiles(activePlayer.GridPosition, activePlayer.MovementRange);
                if (reachable.Contains(tile))
                {
                    activePlayer.MoveToTile(tile);
                    activePlayer.HasMovedThisTurn = true;
                    GridManager.Instance.ClearAllHighlights();
                    RefreshAbilityBar();
                    LogCombatMessage($"{activePlayer.UnitName} moved to tile ({tile.GridPosition.x}, {tile.GridPosition.y}).");
                }
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
        /// Utilizes CanvasGroup to fade in/out cleanly without deactivating the host script GameObject.
        /// </summary>
        public void SetCombatBarVisible(bool visible)
        {
            if (visible)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                if (combatActionBar != null && !combatActionBar.activeSelf)
                {
                    combatActionBar.SetActive(true);
                }

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }

                transform.SetAsLastSibling();
                if (combatActionBar != null && combatActionBar != gameObject)
                {
                    combatActionBar.transform.SetAsLastSibling();
                }
            }
            else
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }

                if (combatActionBar != null && combatActionBar != gameObject)
                {
                    combatActionBar.SetActive(false);
                }
                else if (combatActionBar == gameObject && canvasGroup == null)
                {
                    combatActionBar.SetActive(false);
                }

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
