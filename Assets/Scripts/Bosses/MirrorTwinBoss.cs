using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// The secret boss in the Tower's mirror room (Vili, 2026-10-08): the tavern elf's evil twin, who steps out of
    /// the great mirror with a black trident.
    /// - Trident thrust: reach 2 tiles, 1d10+3.
    /// - Chain lightning from the trident's head: range 5 with line of sight, 2d6+2, every other turn.
    /// - Below half health she laughs and the trident blazes: +1 to hit and lightning every turn.
    /// </summary>
    public class MirrorTwinBoss : EnemyUnit
    {
        public const string DisplayName = "The Mirror Twin";
        public const int ThrustReach = 2;
        public const int LightningRange = 5;

        // STR, DEX, CON, INT, WIS, CHA: quick, sharp and proud
        private static readonly int[] TwinSaves = { 1, 4, 2, 2, 2, 4 };


        private int lightningCooldown;
        private bool enraged;
        private bool castingLightning;

        protected override int[] SaveProfile => TwinSaves;

        /// <summary>Whether her second phase has begun.</summary>
        public bool IsEnraged => enraged;

        /// <summary>Turns until she can call the lightning again (0 = ready).</summary>
        public int LightningCooldown => lightningCooldown;

        public override void InitializeUnit()
        {
            unitName = DisplayName;
            maxHP = 70; // Vili 2026-10-08: the secret route's final boss, was 58
            currentHP = maxHP;
            armorClass = 15;
            attackDamage = 9;
            attackBonus = 6;
            movementRange = 4;
            ConfigureDamageDice(1, 10, 3);
            ConfigureAttackRange(1);
            ConfigureInitiative(3);
            lightningCooldown = 0;
            enraged = false;
            castingLightning = false;
            base.InitializeUnit();
        }

        public override int RollAttackDamage()
        {
            if (castingLightning) return Mathf.Max(1, DiceSystem.RollDamage(2, 6) + 2);
            return base.RollAttackDamage();
        }

        public override void TakeDamage(int amount, bool isCritical = false)
        {
            base.TakeDamage(amount, isCritical);
            if (!enraged && IsAlive && currentHP <= maxHP / 2) Enrage();
        }

        private void Enrage()
        {
            enraged = true;
            attackBonus += 1;
            lightningCooldown = 0;
            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} laughs as her trident blazes violet: lightning every turn now!");
            if (Application.isPlaying)
            {
                FloatingCombatText.Instance?.ShowText(transform.position + Vector3.up * 3.6f, "MIRROR FURY", new Color(0.8f, 0.5f, 1f));
            }
            TrySetAnimatorTrigger("Laugh");
            TridentLightning lightning = GetComponentInChildren<TridentLightning>();
            if (lightning != null) lightning.Intensity = 1.8f;
        }

        public override void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            if (gridManager == null) gridManager = GridManager.Instance;
            PlayerUnit target = gridManager != null ? FindClosestPlayer(gridManager) : null;
            if (!IsAlive || gridManager == null || target == null || !target.IsAlive)
            {
                EndOfTurn();
                return;
            }

            int distance = gridManager.GetDistance(gridPosition, target.GridPosition);
            if (CanCastAt(target, gridManager) && distance > 1)
            {
                CastLightning(target);
                return;
            }
            if (CanThrust(target, gridManager))
            {
                Thrust(target);
                return;
            }

            // Close in, then strike with whatever reaches
            MoveTowardsTarget(target.GridPosition, gridManager);
            WhenWalkFinished(() =>
            {
                if (!IsAlive || target == null || !target.IsAlive) { EndOfTurn(); return; }
                if (CanThrust(target, gridManager)) Thrust(target);
                else if (CanCastAt(target, gridManager)) CastLightning(target);
                else EndOfTurn();
            });
        }

        private bool CanThrust(PlayerUnit target, GridManager grid)
        {
            int d = grid.GetDistance(gridPosition, target.GridPosition);
            return d <= 1 || (d <= ThrustReach && grid.HasLineOfSight(gridPosition, target.GridPosition));
        }

        private bool CanCastAt(PlayerUnit target, GridManager grid)
        {
            if (lightningCooldown > 0) return false;
            int d = grid.GetDistance(gridPosition, target.GridPosition);
            return d <= LightningRange && grid.HasLineOfSight(gridPosition, target.GridPosition);
        }

        private void Thrust(PlayerUnit target)
        {
            FaceTowards(target.transform.position);
            PlayAttackAnimation();
            // The thrust lands as the trident comes forward in the clip
            HoldAsWalking(0.55f, () =>
            {
                if (IsAlive && target != null && target.IsAlive) ResolveStrike(target, "thrusts her trident at", false);
                EndOfTurn();
            });
        }

        private void CastLightning(PlayerUnit target)
        {
            FaceTowards(target.transform.position);
            if (!TrySetAnimatorTrigger("Cast")) PlayAttackAnimation();
            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} raises her trident. Lightning crackles!");
            HoldAsWalking(0.85f, () =>
            {
                if (IsAlive && target != null && target.IsAlive)
                {
                    TridentLightning lightning = GetComponentInChildren<TridentLightning>();
                    if (lightning != null) lightning.Strike(target.transform.position + Vector3.up * 1.4f);
                    castingLightning = true;
                    ResolveStrike(target, "hurls chain lightning at", true);
                    castingLightning = false;
                }
                lightningCooldown = enraged ? 1 : 2;
                EndOfTurn();
            });
        }

        /// <summary>
        /// One d20 attack: cover counts only for the lightning; the hero's Shield Wall and Retaliation answer only a
        /// trident thrust from the next tile.
        /// </summary>
        private void ResolveStrike(PlayerUnit target, string verb, bool lightning)
        {
            GridManager grid = GridManager.Instance;
            AdvantageType advantage = CombatRules.AttackAdvantage(this, target);
            int cover = lightning && grid != null ? grid.GetCoverArmorBonus(gridPosition, target.GridPosition) : 0;
            DiceResult hit = DiceSystem.RollD20(AttackBonus, target.ArmorClass + cover, advantage);
            Debug.Log($"[MirrorTwin] {unitName} {verb} {target.UnitName}: {hit}");
            UI.CombatUIController.LogAttackRoll(unitName, target.UnitName, hit);

            if (hit.isSuccess)
            {
                int damage = hit.isCriticalSuccess ? RollCriticalDamage() : RollAttackDamage();
                target.TakeDamage(damage, hit.isCriticalSuccess);
            }

            bool adjacent = grid == null || grid.GetDistance(gridPosition, target.GridPosition) <= 1;
            if (!lightning && adjacent)
            {
                if (!hit.isSuccess) target.ResolveCounterAttack(this);
                if (IsAlive && target.IsAlive) target.ResolveRetaliation(this);
            }
        }

        private void EndOfTurn()
        {
            if (lightningCooldown > 0) lightningCooldown--;
            OnTurnActionFinished();
        }

        public override void MakeOpportunityAttack(PlayerUnit target)
        {
            if (!IsAlive || target == null || !target.IsAlive) return;
            FaceTowards(target.transform.position);
            PlayAttackAnimation();
            ResolveStrike(target, "jabs her trident at the fleeing", false);
        }
    }
}
