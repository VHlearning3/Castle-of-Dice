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

        /// <summary>Max HP gained above the class baseline (Hero's Resilience, Giant Elixir).</summary>
        public int MaxHPBonus => hasBaseline ? Mathf.Max(0, maxHP - baseMaxHP) : 0;

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
                maxHP = baseMaxHP + Mathf.Max(0, maxHpBonus);
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
        /// Total effective Armor Class including class baseline and permanent gear bonuses.
        /// </summary>
        public override int ArmorClass => base.ArmorClass + permanentArmorClassBonus + DifficultySettings.HeroArmorBonus;

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

        #endregion

        #region Initialization

        protected override void Awake()
        {
            base.Awake();
            OnHealthChanged += ReportHealthToSession;
        }

        private void OnDestroy()
        {
            OnHealthChanged -= ReportHealthToSession;
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

        /// <summary>Enemies that would get a free attack if the hero walked to <paramref name="destination"/>.</summary>
        public void CollectOpportunityAttackers(Vector2Int destination, List<EnemyUnit> result)
        {
            result.Clear();
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
                maxHP = characterClass.BaseMaxHealth;
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
        [Tooltip("Current hero level: 1 (Starting), 2 (Castle Veteran), 3 (Arcane Crusher), 4 (Tower Champion), 5 (Curse Breaker).")]
        [SerializeField] private int level = 1;

        /// <summary>Highest hero level (critical review B8).</summary>
        public const int MaxLevel = 5;

        /// <summary>Level that unlocks the class's fifth ability.</summary>
        public const int FifthAbilityLevel = 4;

        /// <summary>Current hero milestone level (1..5).</summary>
        public int Level
        {
            get => level;
            set
            {
                level = Mathf.Clamp(value, 1, MaxLevel);
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
        /// Checks if an ability slot can be used (action left this turn and the ability is off cooldown).
        /// </summary>
        public bool CanUseAbility(int slotIndex)
        {
            if (hasActedThisTurn) return false;
            if (slotIndex < 0 || slotIndex >= activeAbilities.Count || activeAbilities[slotIndex] == null) return false;
            return GetCooldownRemaining(slotIndex) <= 0;
        }

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
                hasActedThisTurn = true;
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

            hasActedThisTurn = true;
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
        /// Resets turn action and movement flags when the player's turn begins.
        /// </summary>
        public void ResetTurnFlags()
        {
            hasActedThisTurn = false;
            hasMovedThisTurn = false;
        }

        #endregion
    }
}
