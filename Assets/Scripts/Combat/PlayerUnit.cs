using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Represents the player-controlled hero in combat.
    /// Manages class identity (Warrior, Mage, Rogue), active ability slots, permanent gear bonuses,
    /// and ability activation on the battlefield grid.
    /// </summary>
    public class PlayerUnit : CombatUnit
    {
        #region Serialized Fields

        [Header("Class Data")]
        [Tooltip("ScriptableObject containing base stats, lore, and 4 class abilities.")]
        [SerializeField] private CharacterClassSO characterClass;

        [Header("Equipment & Permanent Modifiers")]
        [Tooltip("Permanent weapon damage bonus purchased from Blacksmith Baldur (+1 per upgrade).")]
        [SerializeField] private int permanentWeaponDamageBonus = 0;

        [Tooltip("Permanent armor defense bonus purchased from Blacksmith Baldur (+1 AC per upgrade).")]
        [SerializeField] private int permanentArmorClassBonus = 0;

        #endregion

        #region Private State

        private readonly List<AbilitySO> activeAbilities = new List<AbilitySO>(4);
        private int primaryAttributeBonus = 3;
        private bool hasActedThisTurn = false;
        private bool hasMovedThisTurn = false;

        // D&D action economy (Vili, 2026-10-06): a bonus action per turn, a reaction per round, Dash, Disengage
        private bool hasUsedBonusActionThisTurn;
        private bool hasUsedReactionThisRound;
        private bool isDashing;
        private bool isDisengaging;

        // Death's Door: once per fight a blow that would drop the hero gets a death save instead
        private bool deathsDoorUsedThisFight;

        // Class baseline captured by InitializeUnit; progression bonuses are stored relative to it
        private int baseMaxHP;
        private int baseAttributeBonus;
        private bool hasBaseline;
        private bool isApplyingProgression;

        // Extra damage from a Poison Vial, added to the first hit of the current fight
        private int poisonCoatingBonus;

        // Turns left before an ability can be used again, by base ability ID (critical review B1)
        private readonly Dictionary<string, int> cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> cooldownKeyBuffer = new List<string>(4);

        #endregion

        #region Progression Data

        /// <summary>
        /// Session progression store. The hero pulls its class and saved bonuses from it on
        /// initialization and pushes every upgrade back, so scene loads and saves never stack bonuses.
        /// </summary>
        public static PlayerDataSO ProgressionData
        {
            get => PlayerDataSO.Session;
            set => PlayerDataSO.Session = value;
        }

        /// <summary>
        /// Max HP gained above the class baseline from Hero's Resilience and the Giant Elixir. The HP every level
        /// gives (<see cref="LevelHitPoints"/>) is not part of it: it follows from the level.
        /// </summary>
        public int MaxHPBonus => hasBaseline ? Mathf.Max(0, maxHP - baseMaxHP - LevelHitPoints) : 0;

        /// <summary>Max HP each level above the first gives automatically: warrior 6, rogue 5, mage 4.</summary>
        public static int HitPointsPerLevel(CharacterClassType classType)
        {
            switch (classType)
            {
                case CharacterClassType.Warrior: return 6;
                case CharacterClassType.Rogue: return 5;
                default: return 4;
            }
        }

        /// <summary>Max HP the hero's levels give on top of the class's starting HP.</summary>
        public int LevelHitPoints => characterClass != null ? HitPointsPerLevel(characterClass.ClassType) * (Mathf.Max(1, level) - 1) : 0;

        /// <summary>Primary attribute points gained above the class baseline.</summary>
        public int AttributeBonusModifier => hasBaseline ? Mathf.Max(0, primaryAttributeBonus - baseAttributeBonus) : 0;

        /// <summary>
        /// Fills <paramref name="buffer"/> with the ability slots that are upgraded to Rank 2.
        /// </summary>
        public void GetUpgradedAbilitySlots(List<int> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (IsRank2(activeAbilities[i]))
                {
                    buffer.Add(i);
                }
            }
        }

        /// <summary>
        /// Sets progression to absolute values on top of the class baseline. Idempotent: applying the
        /// same data twice (scene load + save load) yields the same stats.
        /// </summary>
        public void ApplyProgression(int savedLevel, int maxHpBonus, int attributeBonus, int weaponBonus, int armorBonus, IReadOnlyList<int> upgradedSlots)
        {
            if (!hasBaseline)
            {
                CaptureBaseline();
            }

            isApplyingProgression = true;
            try
            {
                Level = savedLevel;
                EnsureFifthAbility();

                int oldMax = maxHP;
                maxHP = baseMaxHP + LevelHitPoints + Mathf.Max(0, maxHpBonus);
                currentHP = Mathf.Clamp(currentHP + (maxHP - oldMax), 1, maxHP);

                primaryAttributeBonus = baseAttributeBonus + Mathf.Max(0, attributeBonus);
                permanentWeaponDamageBonus = Mathf.Max(0, weaponBonus);
                permanentArmorClassBonus = Mathf.Max(0, armorBonus);

                if (upgradedSlots != null)
                {
                    for (int i = 0; i < upgradedSlots.Count; i++)
                    {
                        int slot = upgradedSlots[i];
                        if (slot >= 0 && slot < activeAbilities.Count && !IsRank2(activeAbilities[slot]))
                        {
                            UpgradeAbilityToRank2(slot);
                        }
                    }
                }
            }
            finally
            {
                isApplyingProgression = false;
            }

            NotifyHealthChanged();
        }

        /// <summary>
        /// From level 4 the class's fifth ability (Retaliation, Arcane Chains, Poison Cloud) joins the bar.
        /// </summary>
        private void EnsureFifthAbility()
        {
            if (level < FifthAbilityLevel || characterClass == null) return;
            AbilitySO fifth = characterClass.Level4Ability;
            if (fifth == null) return;

            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (activeAbilities[i] != null && activeAbilities[i].BaseAbilityID == fifth.AbilityID) return;
            }
            activeAbilities.Add(fifth);
            Debug.Log($"[PlayerUnit] {unitName} learns {fifth.AbilityName} (level {level}).");
        }

        private void CaptureBaseline()
        {
            // Without a class asset the serialized stats are the baseline; capture them only once so
            // re-initialization after bonuses were applied does not fold the bonuses into the baseline.
            if (characterClass == null && hasBaseline) return;

            baseMaxHP = characterClass != null ? characterClass.BaseMaxHealth : maxHP;
            baseAttributeBonus = characterClass != null ? characterClass.PrimaryAttributeBonus : primaryAttributeBonus;
            hasBaseline = true;
        }

        private void PushProgressionToData()
        {
            if (isApplyingProgression) return;

            PlayerDataSO data = ProgressionData;
            if (data != null)
            {
                data.SyncFromPlayer(this);
            }
        }

        private static bool IsRank2(AbilitySO ability)
        {
            return ability != null && ability.IsRank2;
        }

        #endregion

        #region Public Properties

        /// <summary>Assigned hero class ScriptableObject.</summary>
        public CharacterClassSO CharacterClass => characterClass;

        /// <summary>Active 4-slot ability loadout.</summary>
        public IReadOnlyList<AbilitySO> ActiveAbilities => activeAbilities;

        /// <summary>Permanent weapon bonus (+1 damage).</summary>
        public int WeaponDamageBonus => permanentWeaponDamageBonus;

        /// <summary>Permanent armor bonus (+1 AC).</summary>
        public int ArmorClassBonus => permanentArmorClassBonus;

        /// <summary>
        /// Total effective Armor Class including class baseline, permanent gear bonuses and the Oathkeeper Shield.
        /// </summary>
        public override int ArmorClass => base.ArmorClass + permanentArmorClassBonus + DifficultySettings.HeroArmorBonus
            + Economy.MagicItems.ArmorBonus;

        /// <summary>
        /// Tiles the hero can walk this turn: the class's movement and the Boots of Striding, halved by Frostbite,
        /// doubled by Dash.
        /// </summary>
        public override int MovementRange
        {
            get
            {
                int baseRange = movementRange + Economy.MagicItems.MovementBonus;
                int range = StatusEffects != null ? StatusEffects.GetEffectiveMovementRange(baseRange) : baseRange;
                return isDashing ? range * 2 : range;
            }
        }

        /// <summary>Primary attribute modifier (+2 to +5) added to D20 checks.</summary>
        public int PrimaryAttributeBonus => primaryAttributeBonus;

        /// <summary>Whether the player has used their combat action this turn.</summary>
        public bool HasActedThisTurn
        {
            get => hasActedThisTurn;
            set => hasActedThisTurn = value;
        }

        /// <summary>Whether the player has moved on the grid this turn.</summary>
        public bool HasMovedThisTurn
        {
            get => hasMovedThisTurn;
            set => hasMovedThisTurn = value;
        }

        /// <summary>Whether the hero has used their bonus action this turn.</summary>
        public bool HasUsedBonusActionThisTurn
        {
            get => hasUsedBonusActionThisTurn;
            set => hasUsedBonusActionThisTurn = value;
        }

        /// <summary>Whether the hero's reaction (an attack on a fleeing enemy) is spent until their next turn.</summary>
        public bool HasUsedReactionThisRound
        {
            get => hasUsedReactionThisRound;
            set => hasUsedReactionThisRound = value;
        }

        /// <summary>True after Dash this turn: the move reaches twice as far.</summary>
        public bool IsDashing => isDashing;

        /// <summary>True after Disengage this turn: walking away provokes no free attacks.</summary>
        public bool IsDisengaging => isDisengaging;

        /// <summary>Whether this fight's Death's Door save has been used.</summary>
        public bool DeathsDoorUsedThisFight => deathsDoorUsedThisFight;

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            OnHealthChanged += ReportHealthToSession;

            // The Shadow Cloak's dusky look while it is worn
            if (Application.isPlaying && GetComponent<World.ShadowCloakVisual>() == null)
            {
                gameObject.AddComponent<World.ShadowCloakVisual>();
            }

            // Putting a magic item on or off changes AC and movement: the HUD re-reads the hero
            Economy.MagicItems.OnWornChanged += HandleMagicItemWorn;
        }

        private void OnDestroy()
        {
            OnHealthChanged -= ReportHealthToSession;
            Economy.MagicItems.OnWornChanged -= HandleMagicItemWorn;
        }

        private void HandleMagicItemWorn(string itemId)
        {
            if (this != null) NotifyHealthChanged();
        }

        // Keeps the zone-to-zone HP in the session store up to date (critical review A9)
        private void ReportHealthToSession(int current, int max)
        {
            if (isApplyingProgression || !hasBaseline) return;
            PlayerDataSO session = ProgressionData;
            if (session != null)
            {
                session.CurrentHP = current > 0 && current < max ? current : -1;
            }
        }

        /// <summary>
        /// Retaliation (warrior, level 4): while it is up, an enemy that attacks the hero in melee takes the
        /// ability's damage (1d8 + STR + blacksmith bonus) right back.
        /// </summary>
        public void ResolveRetaliation(EnemyUnit attacker)
        {
            if (!IsAlive || attacker == null || !attacker.IsAlive || StatusEffects == null) return;
            if (!StatusEffects.HasEffect(StatusEffectType.Retaliation)) return;

            AbilitySO retaliation = null;
            for (int i = 0; i < activeAbilities.Count; i++)
            {
                if (activeAbilities[i] != null && activeAbilities[i].BaseAbilityID == "warrior_retaliation")
                {
                    retaliation = activeAbilities[i];
                    break;
                }
            }

            int damage = retaliation != null
                ? retaliation.RollDamage(primaryAttributeBonus, permanentWeaponDamageBonus)
                : DiceSystem.RollDamage(1, 8) + primaryAttributeBonus;
            Debug.Log($"[PlayerUnit] {unitName} retaliates against {attacker.UnitName} for {damage}!");
            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} retaliates against {attacker.UnitName} for {damage}!");
            attacker.TakeDamage(damage);
        }

        /// <summary>
        /// Walks to <paramref name="tile"/>. In combat, every enemy the hero walks away from (adjacent at the
        /// start, out of reach at the end) gets a free attack first (critical review B2). Teleports
        /// (Blink, Shadow Step) move with MoveToTile and never provoke one.
        /// </summary>
        public override void WalkToTile(GridTile tile, Action onArrived = null)
        {
            if (tile != null)
            {
                ResolveOpportunityAttacks(tile.GridPosition);
                if (!IsAlive) return;
            }
            base.WalkToTile(tile, onArrived);
        }

        /// <summary>
        /// Enemies that would get a free attack if the hero walked to <paramref name="destination"/>. None after
        /// Disengage.
        /// </summary>
        public void CollectOpportunityAttackers(Vector2Int destination, List<EnemyUnit> result)
        {
            result.Clear();
            if (isDisengaging) return;
            TurnManager tm = TurnManager.Instance;
            GridManager grid = GridManager.Instance;
            if (tm == null || !tm.IsCombatActive || grid == null) return;

            IReadOnlyList<CombatUnit> units = tm.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (!(units[i] is EnemyUnit enemy) || !enemy.IsAlive || !enemy.isActiveAndEnabled) continue;
                if (grid.GetDistance(enemy.GridPosition, gridPosition) > 1) continue;
                if (grid.GetDistance(enemy.GridPosition, destination) <= 1) continue;
                result.Add(enemy);
            }
        }

        private readonly List<EnemyUnit> opportunityBuffer = new List<EnemyUnit>(2);

        private void ResolveOpportunityAttacks(Vector2Int destination)
        {
            CollectOpportunityAttackers(destination, opportunityBuffer);
            for (int i = 0; i < opportunityBuffer.Count && IsAlive; i++)
            {
                EnemyUnit enemy = opportunityBuffer[i];
                UI.CombatUIController.Instance?.LogCombatMessage($"{enemy.UnitName} gets a free attack as {unitName} walks away!");
                enemy.MakeOpportunityAttack(this);
            }
        }

        #region Reaction: Opportunity Attack

        /// <summary>
        /// The hero's melee weapon attack for reactions: the first ability when it is a melee strike (Sword Slash,
        /// Poison Dagger). The mage carries no melee weapon, so she makes no opportunity attacks.
        /// </summary>
        public AbilitySO ReactionWeapon
        {
            get
            {
                AbilitySO first = GetAbility(0);
                if (first == null || !first.DealsDamage || first.TargetType != AbilityTargetType.SingleTarget) return null;
                return first.Range <= 1 ? first : null;
            }
        }

        /// <summary>
        /// Whether the hero gets a reaction attack on <paramref name="mover"/> walking to
        /// <paramref name="destination"/>: it starts next to the hero and ends out of reach, the hero has a melee
        /// weapon and has not used their reaction since their last turn.
        /// </summary>
        public bool CanReactTo(EnemyUnit mover, Vector2Int destination)
        {
            if (!IsAlive || mover == null || !mover.IsAlive || hasUsedReactionThisRound) return false;
            if (ReactionWeapon == null) return false;
            TurnManager tm = TurnManager.Instance;
            GridManager grid = GridManager.Instance;
            if (tm == null || !tm.IsCombatActive || grid == null) return false;
            if (grid.GetDistance(mover.GridPosition, gridPosition) > 1) return false;
            return grid.GetDistance(destination, gridPosition) > 1;
        }

        /// <summary>
        /// An enemy walking out of reach: every hero who can react strikes at it once (Vili, 2026-10-06).
        /// </summary>
        public static void ResolveReactionAgainst(EnemyUnit mover, Vector2Int destination)
        {
            TurnManager tm = TurnManager.Instance;
            if (mover == null || tm == null || !tm.IsCombatActive) return;

            IReadOnlyList<CombatUnit> units = tm.ActiveUnits;
            for (int i = 0; i < units.Count && mover.IsAlive; i++)
            {
                if (units[i] is PlayerUnit hero && hero.CanReactTo(mover, destination))
                {
                    hero.MakeOpportunityAttack(mover);
                }
            }
        }

        /// <summary>
        /// The reaction attack itself: d20 + attack bonus against the enemy's AC with the hero's melee ability,
        /// rolled at once (no reroll pause on the enemy's turn).
        /// </summary>
        public void MakeOpportunityAttack(EnemyUnit target)
        {
            AbilitySO weapon = ReactionWeapon;
            if (weapon == null || target == null || !target.IsAlive) return;
            hasUsedReactionThisRound = true;

            FaceTowards(target.transform.position);
            if (UnitAnimator != null && !TrySetAnimatorTrigger(weapon.BaseAbilityID)) UnitAnimator.SetTrigger("Attack");

            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} strikes at {target.UnitName} as it pulls away (reaction)!");
            AdvantageType advantage = CombatRules.AttackAdvantage(this, target);
            StatusEffects?.ConsumeAdvantageNextAttack();
            DiceResult hit = DiceSystem.RollD20(HeroAttributes.GetAttackBonus(this), target.ArmorClass, advantage);
            UI.CombatUIController.LogAttackRoll(unitName, target.UnitName, hit);
            if (!hit.isSuccess) return;

            int damage = weapon.RollDamage(primaryAttributeBonus, permanentWeaponDamageBonus + ConsumePoisonCoating());
            if (hit.isCriticalSuccess) damage *= 2;
            target.TakeDamage(damage, hit.isCriticalSuccess);
            if (target.IsAlive && weapon.AppliedEffect != StatusEffectType.None)
            {
                target.StatusEffects?.ApplyEffect(weapon.AppliedEffect, Mathf.Max(1, weapon.EffectDurationTurns));
            }
        }

        #endregion

        #region Death's Door

        /// <summary>DC of the Death's Door save.</summary>
        public const int DeathSaveDC = 10;

        /// <summary>Fired when a Death's Door save is resolved: (hero, survived).</summary>
        public static event Action<PlayerUnit, bool> OnDeathsDoorResolved;

        /// <summary>
        /// A blow that would drop the hero to 0 HP in a fight first gets a Death's Door save, once per fight
        /// (Vili, 2026-10-06): d20 (+ the Oathkeeper Shield) vs DC 10. 10 or more and the hero stays up with
        /// 1 HP; a natural 20 brings back 1d4 + level HP; less and the hero falls. A failed save can be rerolled.
        /// </summary>
        public override void TakeDamage(int amount, bool isCritical = false)
        {
            if (!ShouldUseDeathsDoor(amount))
            {
                base.TakeDamage(amount, isCritical);
                return;
            }

            deathsDoorUsedThisFight = true;

            // The blow lands, leaving the hero on the brink at 1 HP while the save is rolled
            if (currentHP > 1) base.TakeDamage(currentHP - 1, isCritical);
            if (!IsAlive) return;

            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} is at Death's Door! (death save, DC {DeathSaveDC})");
            RerollableRoll.Roll(HeroAttributes.GetDeathSaveBonus(), DeathSaveDC, AdvantageType.None, ResolveDeathsDoor, "Death's Door");
        }

        private bool ShouldUseDeathsDoor(int amount)
        {
            if (deathsDoorUsedThisFight || !IsAlive || amount <= 0 || amount < currentHP) return false;
            if (StatusEffects != null && StatusEffects.ManaShieldCharges > 0) return false; // the shield takes it
            TurnManager tm = TurnManager.Instance;
            // Real fights only: a village fist fight that knocks the hero down simply ends
            return tm != null && tm.IsCombatActive && tm.AwardsVictoryScrap;
        }

        private void ResolveDeathsDoor(DiceResult save)
        {
            if (!IsAlive) return;

            if (save.isSuccess)
            {
                int healed = 0;
                if (save.isCriticalSuccess)
                {
                    healed = DiceSystem.RollDice(4) + level;
                    Heal(healed);
                }
                string message = healed > 0
                    ? $"Natural 20! {unitName} surges back from Death's Door with {CurrentHP} HP!"
                    : $"{unitName} refuses to fall and clings on with 1 HP!";
                UI.CombatUIController.Instance?.LogCombatMessage(message);
                if (Application.isPlaying)
                {
                    World.FloatingCombatText.Instance?.ShowText(transform.position + Vector3.up * 2.4f, "DEATH'S DOOR!", new Color(1f, 0.85f, 0.4f));
                    AbilityVfx.PlayPotionHeal(this);
                }
                OnDeathsDoorResolved?.Invoke(this, true);
                return;
            }

            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} fails the death save and falls.");
            OnDeathsDoorResolved?.Invoke(this, false);
            base.TakeDamage(currentHP);
        }

        #endregion

        #region Basic Actions: Dash, Dodge, Disengage

        /// <summary>
        /// Whether the rogue's Cunning Action applies: for her Dash and Disengage cost the bonus action.
        /// </summary>
        public bool HasCunningAction => characterClass != null && characterClass.ClassType == CharacterClassType.Rogue;

        /// <summary>What Dash or Disengage would cost now (the rogue uses her bonus action while she has it).</summary>
        public ActionCost GetMobilityActionCost()
        {
            return HasCunningAction && !hasUsedBonusActionThisTurn ? ActionCost.BonusAction : ActionCost.Action;
        }

        private bool CanPay(ActionCost cost)
        {
            return cost == ActionCost.BonusAction ? !hasUsedBonusActionThisTurn : !hasActedThisTurn;
        }

        private void Pay(ActionCost cost)
        {
            if (cost == ActionCost.BonusAction) hasUsedBonusActionThisTurn = true;
            else hasActedThisTurn = true;
        }

        /// <summary>Whether Dash can be taken: not yet dashing and the action (or the rogue's bonus action) is free.</summary>
        public bool CanDash => IsAlive && !isDashing && CanPay(GetMobilityActionCost());

        /// <summary>Whether Disengage can be taken.</summary>
        public bool CanDisengage => IsAlive && !isDisengaging && CanPay(GetMobilityActionCost());

        /// <summary>Whether Dodge can be taken (always the action).</summary>
        public bool CanDodge => IsAlive && !hasActedThisTurn
            && (StatusEffects == null || !StatusEffects.HasEffect(StatusEffectType.Dodging));

        /// <summary>
        /// Dash: this turn's move reaches twice as far. Taken after moving, it gives a second move.
        /// </summary>
        public bool Dash()
        {
            if (!CanDash) return false;
            Pay(GetMobilityActionCost());
            if (hasMovedThisTurn) hasMovedThisTurn = false; // a second move at normal speed
            else isDashing = true;
            return true;
        }

        /// <summary>Disengage: walking away this turn provokes no free attacks.</summary>
        public bool Disengage()
        {
            if (!CanDisengage) return false;
            Pay(GetMobilityActionCost());
            isDisengaging = true;
            return true;
        }

        /// <summary>Dodge: attacks against the hero have Disadvantage and DEX saves Advantage until the next turn.</summary>
        public bool Dodge()
        {
            if (!CanDodge) return false;
            hasActedThisTurn = true;
            StatusEffects?.ApplyEffect(StatusEffectType.Dodging, 1);
            return true;
        }

        #endregion

        /// <summary>
        /// Sets the hero's HP (clamped to 1..MaxHP), e.g. the health carried over from the last zone or a save.
        /// </summary>
        public void SetCurrentHP(int hp)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Clamp(hp, 1, maxHP);
            NotifyHealthChanged();
        }

        public override void InitializeUnit()
        {
            // Each zone scene has its own hero prefab: adopt the class chosen for this adventure
            PlayerDataSO session = ProgressionData;
            if (session != null && session.SelectedClass != null)
            {
                characterClass = session.SelectedClass;
            }

            if (characterClass != null)
            {
                unitName = characterClass.CharacterName;
                maxHP = characterClass.BaseMaxHealth + LevelHitPoints;
                currentHP = maxHP;
                armorClass = characterClass.BaseArmorClass;
                movementRange = characterClass.BaseMovementRange;
                primaryAttributeBonus = characterClass.PrimaryAttributeBonus;

                activeAbilities.Clear();
                if (characterClass.StartingAbilities != null)
                {
                    foreach (var ability in characterClass.StartingAbilities)
                    {
                        if (ability != null)
                        {
                            // Rogue rework: Lockpick is an exploration passive, not a combat ability
                            if (ability.AbilityID.IndexOf("lockpick", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                continue;
                            }
                            activeAbilities.Add(ability);
                        }
                    }
                }
            }

            // Show Sir Roland, Elira or Corvo to match the class
            if (characterClass != null)
            {
                HeroClassModels models = GetComponent<HeroClassModels>();
                if (models != null) models.Apply(characterClass.ClassType);
            }

            CaptureBaseline();
            ResetTurnFlags();
            base.InitializeUnit();

            // Pull persisted bonuses (absolute, so re-initialization never stacks them)
            if (session != null)
            {
                session.ApplyToPlayer(this);
            }
        }

        /// <summary>
        /// Applies a new CharacterClassSO dynamically (e.g. during hero selection screen).
        /// </summary>
        public void SetCharacterClass(CharacterClassSO newClass)
        {
            characterClass = newClass;
            if (ProgressionData != null)
            {
                ProgressionData.SelectedClass = newClass;
            }
            InitializeUnit();
        }

        #endregion

        #region Level & Progression Upgrades

        [Header("Progression & Milestone")]
        [Tooltip("Current hero level: 1 (Starting), 2 (Blooded Adventurer), 3 (Castle Veteran), 4 (Champion of Oakhaven), 5 (Curse Breaker).")]
        [SerializeField] private int level = 1;

        /// <summary>Highest hero level (critical review B8).</summary>
        public const int MaxLevel = 5;

        /// <summary>Level that unlocks the class's fifth ability.</summary>
        public const int FifthAbilityLevel = 4;

        /// <summary>
        /// Current hero milestone level (1..5). Gaining a level adds the class's hit points per level to Max HP
        /// and the same to the hero's current HP (Vili, 2026-10-06).
        /// </summary>
        public int Level
        {
            get => level;
            set
            {
                int oldLevelHP = LevelHitPoints;
                level = Mathf.Clamp(value, 1, MaxLevel);

                if (!isApplyingProgression && hasBaseline)
                {
                    int gained = LevelHitPoints - oldLevelHP;
                    if (gained != 0)
                    {
                        maxHP = Mathf.Max(1, maxHP + gained);
                        if (IsAlive) currentHP = Mathf.Clamp(currentHP + Mathf.Max(0, gained), 1, maxHP);
                        NotifyHealthChanged();
                    }
                }
                EnsureFifthAbility();
            }
        }

        /// <summary>
        /// Hero's Resilience: Increases Max HP by 5 and fully restores HP.
        /// </summary>
        public void ApplyHeroResilience(int hpIncrease = 5)
        {
            // 1. Increase the max cap
            maxHP += hpIncrease;

            // 2. Use the base class Heal method to fill the health.
            // Heal() automatically clamps the value, logs the action, and calls NotifyHealthChanged().
            Heal(maxHP);

            Debug.Log($"[PlayerUnit] Hero's Resilience chosen! Max HP increased by {hpIncrease} to {maxHP}.");
            PushProgressionToData();
        }
        /// <summary>
        /// Attribute Bonus Growth: Adds permanent bonus (+1) to primary attribute (d20 checks & damage).
        /// </summary>
        public void AddAttributeBonus(int amount = 1)
        {
            primaryAttributeBonus += amount;
            Debug.Log($"[PlayerUnit] Primary Attribute Bonus increased by {amount}. New bonus: +{primaryAttributeBonus}");
            PushProgressionToData();
        }

        /// <summary>
        /// Ability Empowerment (Rank 2): Upgrades the ability in the specified slot (0..3).
        /// Every ability gains +3 potency; Shield Wall, Mana Shield, Blink, Shadow Step and Smoke Bomb
        /// get their own upgrade too (see <see cref="AbilitySO.GetRank2Summary"/>).
        /// </summary>
        public bool UpgradeAbilityToRank2(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= activeAbilities.Count || activeAbilities[slotIndex] == null)
            {
                Debug.LogWarning($"[PlayerUnit] Cannot upgrade ability slot {slotIndex}: invalid index or empty slot.");
                return false;
            }

            AbilitySO original = activeAbilities[slotIndex];
            if (original.IsRank2)
            {
                Debug.LogWarning($"[PlayerUnit] Ability {original.AbilityName} is already Rank 2.");
                return false;
            }

            AbilitySO rank2 = original.CreateRank2();
            activeAbilities[slotIndex] = rank2;
            Debug.Log($"[PlayerUnit] Upgraded slot {slotIndex} ({rank2.AbilityName}) to Rank 2! New BaseValue: {rank2.BaseValue}");
            PushProgressionToData();
            return true;
        }

        #endregion

        #region Permanent Upgrades

        /// <summary>
        /// Increases permanent weapon damage (purchased from Blacksmith Baldur).
        /// </summary>
        public void AddWeaponDamageBonus(int amount)
        {
            permanentWeaponDamageBonus += amount;
            Debug.Log($"[PlayerUnit] Weapon damage bonus increased by {amount}. Total bonus: +{permanentWeaponDamageBonus}");
            PushProgressionToData();
        }

        /// <summary>
        /// Increases permanent Armor Class (purchased from Blacksmith Baldur).
        /// </summary>
        public void AddArmorClassBonus(int amount)
        {
            permanentArmorClassBonus += amount;
            Debug.Log($"[PlayerUnit] Armor Class bonus increased by {amount}. Total AC: {ArmorClass}");
            PushProgressionToData();
        }

        #endregion

        #region Poison Coating

        /// <summary>Extra damage waiting on the blade for the next hit (0 when uncoated).</summary>
        public int PoisonCoatingBonus => poisonCoatingBonus;

        /// <summary>
        /// Coats the weapon with poison: the next hit this fight deals the given extra damage.
        /// </summary>
        public void ApplyPoisonCoating(int bonusDamage)
        {
            poisonCoatingBonus = Mathf.Max(0, bonusDamage);
        }

        /// <summary>
        /// Returns the coating bonus and removes it, so only one hit benefits.
        /// </summary>
        public int ConsumePoisonCoating()
        {
            int bonus = poisonCoatingBonus;
            poisonCoatingBonus = 0;
            return bonus;
        }

        /// <summary>Wipes an unused coating when the fight ends.</summary>
        public void ClearPoisonCoating()
        {
            poisonCoatingBonus = 0;
        }

        #endregion

        #region Ability Execution

        /// <summary>
        /// Checks if an ability slot can be used: its cost (the action, or the bonus action for Iron Will, Blink
        /// and Shadow Step) is still free this turn and the ability is off cooldown.
        /// </summary>
        public bool CanUseAbility(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= activeAbilities.Count || activeAbilities[slotIndex] == null) return false;
            if (!CanPay(ActionEconomy.GetCost(activeAbilities[slotIndex]))) return false;
            return GetCooldownRemaining(slotIndex) <= 0;
        }

        /// <summary>What the ability in <paramref name="slotIndex"/> costs.</summary>
        public ActionCost GetAbilityCost(int slotIndex) => ActionEconomy.GetCost(GetAbility(slotIndex));

        /// <summary>Whether the cost of the ability in <paramref name="slotIndex"/> is already spent this turn.</summary>
        public bool IsAbilityCostSpent(int slotIndex) => !CanPay(GetAbilityCost(slotIndex));

        /// <summary>Turns left before the ability in <paramref name="slotIndex"/> can be used again (0 = ready).</summary>
        public int GetCooldownRemaining(int slotIndex)
        {
            AbilitySO ability = GetAbility(slotIndex);
            if (ability == null) return 0;
            return cooldowns.TryGetValue(ability.BaseAbilityID, out int turns) ? turns : 0;
        }

        /// <summary>Counts every cooldown down by one turn (called when the hero's turn starts).</summary>
        public void TickCooldowns()
        {
            cooldownKeyBuffer.Clear();
            cooldownKeyBuffer.AddRange(cooldowns.Keys);
            for (int i = 0; i < cooldownKeyBuffer.Count; i++)
            {
                string key = cooldownKeyBuffer[i];
                int left = cooldowns[key] - 1;
                if (left <= 0) cooldowns.Remove(key);
                else cooldowns[key] = left;
            }
        }

        /// <summary>Every ability is ready again (a new fight starts or one ends).</summary>
        public void ResetCooldowns()
        {
            cooldowns.Clear();
        }

        private void StartCooldown(AbilitySO ability)
        {
            int turns = AbilityCooldowns.GetCooldownTurns(ability);
            if (turns > 0)
            {
                cooldowns[ability.BaseAbilityID] = turns;
            }
        }

        /// <summary>
        /// Returns the ability mapped to the specified action slot (0..3).
        /// </summary>
        public AbilitySO GetAbility(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < activeAbilities.Count)
            {
                return activeAbilities[slotIndex];
            }
            return null;
        }

        /// <summary>
        /// Executes an ability from slot index targeting a specified grid position.
        /// </summary>
        public bool UseAbility(int slotIndex, Vector2Int targetGridPos, AbilityExecutor executor)
        {
            if (!CanUseAbility(slotIndex))
            {
                Debug.Log($"[PlayerUnit] Cannot use ability in slot {slotIndex}. Action already taken or slot empty.");
                return false;
            }

            AbilitySO ability = activeAbilities[slotIndex];
            if (executor == null)
            {
                executor = AbilityExecutor.Instance;
            }

            if (executor == null)
            {
                Debug.LogError("[PlayerUnit] No AbilityExecutor available to resolve ability.");
                return false;
            }

            bool success = executor.ExecuteAbility(this, ability, targetGridPos);
            if (success)
            {
                Pay(ActionEconomy.GetCost(ability));
                StartCooldown(ability);
            }

            return success;
        }

        /// <summary>
        /// Hits an explosive barrel with a damaging ability in range (critical review B6). Spends the action
        /// and sets the barrel off. Returns false when the ability can't reach or hurt it.
        /// </summary>
        public bool AttackBarrel(int slotIndex, ExplosiveBarrel barrel)
        {
            if (!CanUseAbility(slotIndex) || barrel == null || barrel.HasExploded || barrel.Tile == null) return false;

            AbilitySO ability = activeAbilities[slotIndex];
            if (!ability.DealsDamage || ability.TargetType == AbilityTargetType.Self || ability.TargetsEmptyTile) return false;

            GridManager grid = GridManager.Instance;
            if (grid == null || grid.GetDistance(gridPosition, barrel.Tile.GridPosition) > ability.Range) return false;

            Pay(ActionEconomy.GetCost(ability));
            StartCooldown(ability);
            FaceTowards(barrel.transform.position);
            if (UnitAnimator != null && !TrySetAnimatorTrigger(ability.BaseAbilityID))
            {
                UnitAnimator.SetTrigger(ability.TargetType == AbilityTargetType.Area3x3 ? "CastSpell" : "Attack");
            }
            barrel.Explode();
            return true;
        }

        /// <summary>
        /// Resets turn action and movement flags when the player's turn begins: the action, the bonus action,
        /// the move and the reaction come back, and Dash / Disengage end.
        /// </summary>
        public void ResetTurnFlags()
        {
            hasActedThisTurn = false;
            hasMovedThisTurn = false;
            hasUsedBonusActionThisTurn = false;
            hasUsedReactionThisRound = false;
            isDashing = false;
            isDisengaging = false;
        }

        /// <summary>
        /// The hero's turn begins: everything in <see cref="ResetTurnFlags"/>, and a Dodge from last turn ends.
        /// </summary>
        public void BeginOwnTurn()
        {
            ResetTurnFlags();
            StatusEffects?.RemoveEffect(StatusEffectType.Dodging);
        }

        /// <summary>A new fight: Death's Door is ready again.</summary>
        public void ResetForNewFight()
        {
            ResetTurnFlags();
            deathsDoorUsedThisFight = false;
        }

        #endregion
    }
}
