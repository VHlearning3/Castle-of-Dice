using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Crown Hall Final Boss: The Gargoyle King.
    /// Phase 1: Heavy physical attacks and a telegraphed earthquake: he marks a 3x3 area under the hero in red,
    /// and it shakes on his next turn, so the hero can step out (critical review B4). The quake never hurts him.
    /// Phase 2: Stone Form at 50% HP (+3 AC) and a petrifying gaze every other turn: the hero makes a DC 13
    /// CON save or cannot move on their next turn. Othelia's ring breaks the stone armour for 2 turns (C3).
    /// Pre-combat hook: Weakened attack damage (-3) for the first 3 turns if DC 16 Intimidation check succeeded.
    /// </summary>
    public class GargoyleKingBoss : EnemyUnit
    {
        #region Constants

        /// <summary>AC the Stone Form adds.</summary>
        public const int StoneFormArmorBonus = 3;

        /// <summary>CON save against the petrifying gaze.</summary>
        public const int GazeSaveDC = 13;

        /// <summary>Earthquake damage dice (2d6).</summary>
        public const int QuakeDiceCount = 2;
        public const int QuakeDiceSides = 6;

        #endregion

        #region Serialized Fields

        [Header("Phase Settings")]
        [Tooltip("True when the boss is in Phase 2 (Stone Form).")]
        [SerializeField] private bool isStoneFormActive = false;

        [Header("Hazard Spawning")]
        [Tooltip("Number of random tile hazards spawned during ground stomp (unused since the earthquake is telegraphed).")]
        [SerializeField] private int rockfallHazardCount = 3;

        [Tooltip("Flat damage added to the earthquake's 2d6.")]
        [SerializeField] private int rockfallDamage = 0;

        [Header("Dialogue Intimidation Hook")]
        [Tooltip("Debuff tag applied if DC 16 Intimidation was passed.")]
        [SerializeField] private string intimidationTag = "GargoyleKingIntimidated";

        [Tooltip("Number of turns the intimidation debuff lasts.")]
        [SerializeField] private int intimidationDurationTurns = 3;

        #endregion

        #region Private State

        private int remainingIntimidationTurns = 0;
        private bool hasEnteredPhase2 = false;
        private bool wasIntimidated = false;

        // Telegraphed earthquake: tiles marked this turn shake on the King's next turn
        private readonly List<GridTile> markedQuakeTiles = new List<GridTile>(9);
        private bool quakePending;
        private Vector2Int quakeCenter;

        // Phase 2 gaze every other turn, and Othelia's ring
        private int gazeCountdown;
        private int ringBrokenArmorTurns;
        private bool ringUsed;

        // Story choices (critical review C8, C9): the rogue's stolen crown, a spared Malakor's help or betrayal
        private bool crownStolen;
        private bool? malakorHelps;
        private bool choicesAnnounced;

        /// <summary>AC lost while the rogue holds his crown.</summary>
        public const int StolenCrownArmorPenalty = 2;

        /// <summary>HP Malakor's counter-rune tears from the King when the spared mage helps.</summary>
        public const int MalakorHelpDamage = 15;

        #endregion

        #region Public Properties

        /// <summary>Whether Stone Form (Phase 2) is active.</summary>
        public bool IsStoneFormActive => isStoneFormActive;

        /// <summary>Remaining turns of attack weakness from intimidation.</summary>
        public int RemainingIntimidationTurns => remainingIntimidationTurns;

        /// <summary>True while an earthquake is marked and will strike on the King's next turn.</summary>
        public bool IsQuakePending => quakePending;

        /// <summary>Centre of the marked earthquake area.</summary>
        public Vector2Int QuakeCenter => quakeCenter;

        /// <summary>Turns Othelia's ring keeps the stone armour broken.</summary>
        public int RingBrokenArmorTurns => ringBrokenArmorTurns;

        /// <summary>Whether Othelia's ring can still be shown to the King (Stone Form, not used yet).</summary>
        public bool CanBeShownTheRing => IsAlive && isStoneFormActive && !ringUsed;

        /// <summary>Stone Form adds +3 AC unless Othelia's ring has broken it.</summary>
        public override int ArmorClass =>
            base.ArmorClass + (isStoneFormActive && ringBrokenArmorTurns <= 0 ? StoneFormArmorBonus : 0)
            - (crownStolen ? StolenCrownArmorPenalty : 0);

        /// <summary>
        /// Effective attack damage: reduced by 3 if intimidated during the first 3 rounds.
        /// </summary>
        public int EffectiveAttackDamage
        {
            get
            {
                int dmg = AttackDamage;
                return remainingIntimidationTurns > 0 ? Mathf.Max(2, dmg - 3) : dmg;
            }
        }

        #endregion

        #region Events

        /// <summary>Fired when the boss transitions into Phase 2 Stone Form.</summary>
        public static event Action<GargoyleKingBoss> OnStoneFormActivated;

        /// <summary>Fired when a retry puts the boss back from Stone Form into his first phase.</summary>
        public static event Action<GargoyleKingBoss> OnStoneFormReset;

        /// <summary>Fired when the earthquake strikes: (boss, tiles that shook).</summary>
        public static event Action<GargoyleKingBoss, List<Vector2Int>> OnGroundStompTriggered;

        /// <summary>Fired when the King marks where the next earthquake will strike: (boss, centre).</summary>
        public static event Action<GargoyleKingBoss, Vector2Int> OnQuakeTelegraphed;

        #endregion

        #region Initialization

        public override void InitializeUnit()
        {
            unitName = "The Gargoyle King";
            maxHP = 66;
            currentHP = maxHP;
            armorClass = 15;
            attackDamage = 9;
            attackBonus = 5;
            movementRange = 2;
            ConfigureDamageDice(2, 6, 2); // average 9, as the old flat hit

            // A retry after the hero falls faces the first-phase King again, not the Stone Form
            bool wasInStoneForm = isStoneFormActive;
            hasEnteredPhase2 = false;
            isStoneFormActive = false;
            remainingIntimidationTurns = 0;
            gazeCountdown = 0;
            ringBrokenArmorTurns = 0;
            ringUsed = false;
            ClearQuakeMarks();
            if (wasInStoneForm)
            {
                OnStoneFormReset?.Invoke(this);
            }

            base.InitializeUnit();

            CheckIntimidationDebuff();
            ApplyStoryChoices();
        }

        private void ApplyStoryChoices()
        {
            crownStolen = StoryFlags.Has(BossChoices.CrownStolenFlag);
            malakorHelps = BossChoices.ResolveMalakorAtThrone();
            choicesAnnounced = false;

            if (malakorHelps == true)
            {
                currentHP = Mathf.Max(1, maxHP - MalakorHelpDamage);
                NotifyHealthChanged();
            }
            else if (malakorHelps == false)
            {
                StatusEffects?.ApplyEffect(StatusEffectType.ManaShield, 2);
            }
        }

        private void AnnounceStoryChoices()
        {
            if (choicesAnnounced) return;
            choicesAnnounced = true;
            if (crownStolen) Say("Without his crown the King's stone skin is thinner (-2 AC).");
            if (malakorHelps == true) Say("Malakor appears at the door: his counter-rune tears at the King's stone!");
            else if (malakorHelps == false) Say("Malakor appears at the door... and shields the King! \"Forgive me. He promised me forever.\"");
        }

        private void CheckIntimidationDebuff()
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && (dialogue.HasCombatDebuff(intimidationTag) || dialogue.HasCombatDebuff("IntimidateBoss")))
            {
                wasIntimidated = true;
                dialogue.ConsumeCombatDebuff(intimidationTag);
                dialogue.ConsumeCombatDebuff("IntimidateBoss");
            }

            // The hero who won the intimidation check keeps that edge when retrying the fight
            if (wasIntimidated)
            {
                remainingIntimidationTurns = intimidationDurationTurns;
                Debug.Log($"[GargoyleKing] Intimidation succeeded! The Gargoyle King hesitates (-3 DMG) for {remainingIntimidationTurns} turns.");
            }
        }

        #endregion

        #region Combat Lifecycle

        public override void TakeDamage(int amount, bool isCritical = false)
        {
            // Phase 2: Stone Form deflection/immunity to pure magic
            if (isStoneFormActive)
            {
                // In Stone Form, armor is further reinforced
                Debug.Log($"[GargoyleKing] The Stone Form absorbs the blow! Hardened granite resists the strike.");
            }

            base.TakeDamage(amount, isCritical);

            // Phase 2 transition trigger at 50% HP
            if (IsAlive && !hasEnteredPhase2 && currentHP <= (maxHP / 2))
            {
                EnterStoneForm();
            }
        }

        public override void Die()
        {
            ClearQuakeMarks();
            base.Die();
        }

        public override void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            if (gridManager == null) gridManager = GridManager.Instance;
            AnnounceStoryChoices();

            // The earthquake marked last turn strikes now; otherwise he marks where the next one lands
            ExecuteGroundStomp(gridManager);

            // Phase 2: the petrifying gaze every other turn
            if (isStoneFormActive && IsAlive)
            {
                if (gazeCountdown <= 0)
                {
                    CastPetrifyingGaze(FindClosestPlayer(gridManager));
                    gazeCountdown = 2;
                }
                gazeCountdown--;
            }

            base.ExecuteTurnAction(gridManager, abilityExecutor);
        }

        protected override void OnTurnActionFinished()
        {
            base.OnTurnActionFinished();

            // Decrement intimidation debuff countdown (after the attack, which may walk first)
            if (remainingIntimidationTurns > 0)
            {
                remainingIntimidationTurns--;
                if (remainingIntimidationTurns == 0)
                {
                    Debug.Log("[GargoyleKing] The Gargoyle King shakes off his fear. Attack damage fully restored!");
                }
            }

            if (ringBrokenArmorTurns > 0)
            {
                ringBrokenArmorTurns--;
                if (ringBrokenArmorTurns == 0)
                {
                    Say("The stone creeps back over the King's chest.");
                }
            }
        }

        public override int RollAttackDamage()
        {
            int damage = base.RollAttackDamage();
            return remainingIntimidationTurns > 0 ? Mathf.Max(2, damage - 3) : damage;
        }

        #endregion

        #region Phase 2: Stone Form

        /// <summary>
        /// Activates Phase 2 Stone Form. Increases Armor Class and grants spell reflection/resistance.
        /// </summary>
        public void EnterStoneForm()
        {
            hasEnteredPhase2 = true;
            isStoneFormActive = true;
            gazeCountdown = 1; // the first gaze comes on his next turn

            Debug.Log("[GargoyleKing] PHASE 2: The Gargoyle King's skin petrifies into enchanted granite! STONE FORM ACTIVATED.");
            StatusEffects?.ApplyEffect(StatusEffectType.ManaShield, durationTurns: 2);

            OnStoneFormActivated?.Invoke(this);
        }

        /// <summary>
        /// The petrifying gaze: the hero makes a DC 13 CON save (it can be rerolled with a scroll) or turns
        /// to stone up to the knees and cannot move on their next turn.
        /// </summary>
        public void CastPetrifyingGaze(PlayerUnit target)
        {
            if (target == null || !target.IsAlive) return;

            Say($"The King's eyes flare grey. {target.UnitName} must resist his petrifying gaze (CON DC {GazeSaveDC})!");
            int conBonus = HeroAttributes.GetModifier(target, HeroAttribute.Constitution);
            RerollableRoll.Roll(conBonus, GazeSaveDC, AdvantageType.None, save =>
            {
                if (!target.IsAlive) return;
                if (save.isSuccess)
                {
                    Say($"{target.UnitName} shakes off the gaze.");
                }
                else
                {
                    Say($"Stone creeps up {target.UnitName}'s legs: no moving next turn!");
                    target.StatusEffects?.ApplyEffect(StatusEffectType.Immobilized, 1);
                }
            }, "Petrifying Gaze / Constitution");
        }

        /// <summary>
        /// Othelia's ring (critical review C3): the King remembers his queen and his stone armour cracks for
        /// <paramref name="turns"/> turns. Works once, in Stone Form.
        /// </summary>
        public bool BreakStoneArmorWithRing(int turns = 2)
        {
            if (!CanBeShownTheRing) return false;
            ringUsed = true;
            ringBrokenArmorTurns = Mathf.Max(1, turns);
            Say("\"Isolde... my queen?\" The King stares at the ring, and his stone armour cracks away!");
            return true;
        }

        #endregion

        #region Seismic Ground Stomp

        /// <summary>
        /// The telegraphed earthquake. If an area is marked, it shakes now: 2d6 to everyone standing in it
        /// except the King himself. If not, the King marks a 3x3 area around the hero that will shake on his
        /// next turn, giving the hero one turn to get out.
        /// </summary>
        public void ExecuteGroundStomp(GridManager gridManager)
        {
            if (gridManager == null) return;

            if (quakePending)
            {
                StrikeQuake(gridManager);
                return;
            }

            PlayerUnit hero = FindClosestPlayer(gridManager);
            if (hero == null) return;
            MarkQuake(gridManager, hero.GridPosition);
        }

        /// <summary>Marks the 3x3 area around <paramref name="center"/> for next turn's earthquake.</summary>
        public void MarkQuake(GridManager gridManager, Vector2Int center)
        {
            ClearQuakeMarks();
            quakePending = true;
            quakeCenter = center;
            foreach (GridTile tile in gridManager.GetArea3x3(center))
            {
                if (tile == null || !tile.IsWalkable) continue;
                tile.HazardWarning = true;
                markedQuakeTiles.Add(tile);
            }
            Say("The King raises his fists: the ground marked in red will shake on his next turn!");
            OnQuakeTelegraphed?.Invoke(this, center);
        }

        private void StrikeQuake(GridManager gridManager)
        {
            List<Vector2Int> shaken = new List<Vector2Int>(markedQuakeTiles.Count);
            for (int i = 0; i < markedQuakeTiles.Count; i++)
            {
                if (markedQuakeTiles[i] != null) shaken.Add(markedQuakeTiles[i].GridPosition);
            }
            Vector2Int center = quakeCenter;
            ClearQuakeMarks();

            int damage = DiceSystem.RollDamage(QuakeDiceCount, QuakeDiceSides) + rockfallDamage;
            Debug.Log($"[GargoyleKing] The Gargoyle King stomps the ground! Rocks rain down on the marked area for {damage}.");
            if (Application.isPlaying) AbilityVfx.PlayExplosion(gridManager.GetWorldPosition(center));

            // Everyone in the shaken area except the King (read unit positions, not just tile occupancy)
            List<CombatUnit> victims = new List<CombatUnit>();
            if (TurnManager.Instance != null)
            {
                IReadOnlyList<CombatUnit> units = TurnManager.Instance.ActiveUnits;
                for (int i = 0; i < units.Count; i++)
                {
                    CombatUnit unit = units[i];
                    if (unit == null || unit == this || !unit.IsAlive) continue;
                    if (shaken.Contains(unit.GridPosition)) victims.Add(unit);
                }
            }
            else
            {
                for (int i = 0; i < shaken.Count; i++)
                {
                    GridTile tile = gridManager.GetTileAt(shaken[i]);
                    CombatUnit unit = tile != null ? tile.OccupyingUnit : null;
                    if (unit != null && unit != this && unit.IsAlive && !victims.Contains(unit)) victims.Add(unit);
                }
            }

            for (int i = 0; i < victims.Count; i++)
            {
                Say($"The earthquake hits {victims[i].UnitName} for {damage}!");
                victims[i].TakeDamage(damage);
            }
            if (victims.Count == 0) Say("The earthquake shakes empty ground.");

            OnGroundStompTriggered?.Invoke(this, shaken);
        }

        private void ClearQuakeMarks()
        {
            for (int i = 0; i < markedQuakeTiles.Count; i++)
            {
                if (markedQuakeTiles[i] != null) markedQuakeTiles[i].HazardWarning = false;
            }
            markedQuakeTiles.Clear();
            quakePending = false;
        }

        private void Say(string message)
        {
            Debug.Log("[GargoyleKing] " + message);
            UI.CombatUIController.Instance?.LogCombatMessage(message);
        }

        #endregion
    }
}
