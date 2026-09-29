using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Temporary combat body for a village NPC the hero picked a fight with from dialogue.
    /// Lives on its own GameObject that carries the NPC for the length of the fight, so the villager itself
    /// never gains combat components (CombatUnit and StatusEffectController require each other and could not
    /// be removed from the NPC again). The fight happens on the spot where the NPC stands.
    /// A beaten NPC is knocked out rather than killed: they go down, get back up where they stood,
    /// and their dialogue and shop keep working.
    /// </summary>
    public class NpcBrawlerUnit : EnemyUnit
    {
        #region Constants

        /// <summary>Seconds a beaten NPC stays down before getting back up.</summary>
        public const float RecoveryDelay = 2.5f;

        private const int BrawlGridSize = 12;
        private const float BrawlTileSize = 1.6f;

        #endregion

        #region Private State

        private VillageNPC npc;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private bool isKnockedOut;

        // Where the NPC sat in the hierarchy before the fight, restored exactly afterwards
        private Transform npcParent;
        private int npcSiblingIndex;
        private Vector3 npcLocalPosition;
        private Quaternion npcLocalRotation;
        private Vector3 npcLocalScale;

        private GridManager grid;
        private Vector3 gridHomePosition;
        private bool ownsGrid;
        private GridManager previousGridInstance;

        #endregion

        #region Public Properties

        /// <summary>The brawl currently in progress (or waiting on the Defeat modal), if any.</summary>
        public static NpcBrawlerUnit Active { get; private set; }

        /// <summary>The villager this combat body carries.</summary>
        public VillageNPC Npc => npc;

        /// <summary>Whether the NPC lost and is lying on the ground.</summary>
        public bool IsKnockedOut => isKnockedOut;

        /// <summary>Where the NPC stood when the fight was picked; they return here afterwards.</summary>
        public Vector3 HomePosition => homePosition;

        #endregion

        #region Brawl Lifecycle

        /// <summary>
        /// Turns <paramref name="npc"/> into a hostile combatant and starts turn-based combat against the hero
        /// on a 12x12 grid around the two of them. Returns null if a fight is already running.
        /// </summary>
        public static NpcBrawlerUnit Begin(VillageNPC npc, PlayerUnit player)
        {
            if (npc == null || player == null || !player.IsAlive) return null;
            if (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive) return null;
            if (Active != null)
            {
                // The last villager beaten is still lying down: get them up so the next fight can start
                if (!Active.IsKnockedOut) return null;
                Active.Recover();
            }

            GameObject body = new GameObject($"Brawl_{npc.name}");
            Scene npcScene = npc.gameObject.scene;
            if (npcScene.IsValid() && npcScene.isLoaded && body.scene != npcScene)
            {
                SceneManager.MoveGameObjectToScene(body, npcScene);
            }
            body.transform.SetPositionAndRotation(npc.transform.position, npc.transform.rotation);

            // RequireComponent adds the StatusEffectController alongside
            NpcBrawlerUnit unit = body.AddComponent<NpcBrawlerUnit>();
            unit.Configure(npc);
            Active = unit;
            TurnManager.OnCombatEnded += unit.HandleCombatEnded;

            Debug.Log($"[NpcBrawlerUnit] {player.UnitName} picked a fight with {unit.UnitName}!");
            unit.StartBrawl(player);
            return unit;
        }

        /// <summary>
        /// Copies the NPC's brawl stats onto this unit and picks the NPC up so it moves with this body.
        /// </summary>
        public void Configure(VillageNPC owner)
        {
            npc = owner;
            unitName = string.IsNullOrWhiteSpace(owner.NpcName) ? "Villager" : owner.NpcName;
            maxHP = Mathf.Max(1, owner.BrawlMaxHP);
            currentHP = maxHP;
            armorClass = owner.BrawlArmorClass;
            attackBonus = owner.BrawlAttackBonus;
            attackDamage = Mathf.Max(1, owner.BrawlDamage);
            attackRange = 1;
            movementRange = 3;
            isDead = false;
            isKnockedOut = false;

            homePosition = transform.position;
            homeRotation = transform.rotation;

            Transform npcTransform = owner.transform;
            npcParent = npcTransform.parent;
            npcSiblingIndex = npcTransform.GetSiblingIndex();
            npcLocalPosition = npcTransform.localPosition;
            npcLocalRotation = npcTransform.localRotation;
            npcLocalScale = npcTransform.localScale;
            npcTransform.SetParent(transform, true);

            owner.IsInteractable = false;
        }

        private void StartBrawl(PlayerUnit player)
        {
            // Try Again reuses the grid laid out for the first round
            if (grid == null)
            {
                grid = ResolveGrid();
                GenerateBrawlGrid(player);
            }

            PlaceOnGrid(player);
            PlaceOnGrid(this);

            GameManager.Instance?.SetState(GamePlayMode.Combat);
            CombatUIController.Instance?.EnsureActiveAndReady(true);
            MusicManager.Instance?.PlayCombatMusicForBoss("", GameLocation.Village.ToString());

            // A villager can be fought again and again, so the fight pays no scrap
            TurnManager turnManager = TurnManager.EnsureInstance();
            if (turnManager != null)
            {
                turnManager.StartCombat(new List<CombatUnit> { player, this }, awardVictoryScrap: false);
            }

            // Square up: both fighters and the camera face each other
            FaceTowards(player.transform.position);
            player.FaceTowards(transform.position);
            CameraFollow cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.FaceDirection(transform.position - player.transform.position);
            }
        }

        /// <summary>
        /// Restarts the brawl after the hero lost and chose Try Again: the NPC is back on their feet at full health.
        /// </summary>
        public void Restart(PlayerUnit player)
        {
            if (player == null) return;

            StopAllCoroutines();
            ClearTile();
            isDead = false;
            isKnockedOut = false;
            currentHP = maxHP;
            if (StatusEffects != null) StatusEffects.ClearAllEffects();
            transform.SetPositionAndRotation(homePosition, homeRotation);
            NotifyHealthChanged();

            StartBrawl(player);
        }

        /// <summary>
        /// Ends the brawl: the NPC gets back up where they stood, becomes talkable again, and this temporary
        /// combat body is destroyed.
        /// </summary>
        public void Recover()
        {
            StopAllCoroutines();
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            if (Active == this) Active = null;

            ClearTile();
            ReleaseGrid();
            isDead = false;
            isKnockedOut = false;
            currentHP = maxHP;

            // Grab the NPC's animator before letting go of the NPC
            Animator animator = UnitAnimator;
            ReleaseNpc();
            if (animator != null && animator.isActiveAndEnabled)
            {
                // Leave the knocked-out pose and return to idle
                animator.Rebind();
                animator.Update(0f);
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void ReleaseNpc()
        {
            if (npc == null) return;

            Transform npcTransform = npc.transform;
            npcTransform.SetParent(npcParent != null ? npcParent : null, false);
            npcTransform.SetSiblingIndex(npcSiblingIndex);
            npcTransform.localPosition = npcLocalPosition;
            npcTransform.localRotation = npcLocalRotation;
            npcTransform.localScale = npcLocalScale;

            npc.IsInteractable = true;
            Debug.Log($"[NpcBrawlerUnit] {unitName} dusts themselves off and gets back up.");
        }

        private void HandleCombatEnded(bool isVictory)
        {
            if (Active != this) return;

            // Defeat waits for the Defeat modal: Try Again calls Restart, Return to Village calls Recover
            if (!isVictory) return;

            // Clear the grid now; the NPC stays down for a moment before getting back up
            ClearTile();
            ReleaseGrid();
            if (Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(RecoverAfterDelay());
            }
        }

        private IEnumerator RecoverAfterDelay()
        {
            yield return new WaitForSeconds(RecoveryDelay);
            Recover();
        }

        private void OnDestroy()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            if (Active == this) Active = null;
        }

        #endregion

        #region Animation

        /// <summary>
        /// Swings at the hero with the NPC's attack clip before the D20 attack roll resolves.
        /// </summary>
        protected override void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            if (target != null && target.IsAlive && HasAnimatorParameter("Attack"))
            {
                UnitAnimator.SetTrigger("Attack");
            }
            base.PerformAttack(target, abilityExecutor);
        }

        /// <summary>
        /// A beaten villager is knocked out, not removed: they fall over where they stand.
        /// </summary>
        protected override void HideOnDeath()
        {
            isKnockedOut = true;

            // NPCs with a death clip already fell over through the "Die" trigger; tip the others over by hand
            if (!HasAnimatorParameter("Die"))
            {
                transform.rotation = Quaternion.AngleAxis(-90f, transform.right) * transform.rotation;
            }
            Debug.Log($"[NpcBrawlerUnit] {unitName} is knocked out!");
        }

        private bool HasAnimatorParameter(string parameterName)
        {
            Animator animator = UnitAnimator;
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName) return true;
            }
            return false;
        }

        #endregion

        #region Grid Placement

        private void GenerateBrawlGrid(PlayerUnit player)
        {
            Vector3 center = (player.transform.position + homePosition) * 0.5f;
            center.y = homePosition.y;

            // Keep both fighters out of the floor raycast and the obstacle scan, so the grid lies on the ground
            // (not on someone's head) and the NPC's own tile stays walkable
            List<Collider> hidden = new List<Collider>();
            HideColliders(player.gameObject, hidden);
            HideColliders(gameObject, hidden);
            try
            {
                grid.GenerateGridAt(center, BrawlGridSize, BrawlGridSize, BrawlTileSize);
            }
            finally
            {
                for (int i = 0; i < hidden.Count; i++)
                {
                    if (hidden[i] != null) hidden[i].enabled = true;
                }
            }
        }

        private static void HideColliders(GameObject root, List<Collider> hidden)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].enabled)
                {
                    colliders[i].enabled = false;
                    hidden.Add(colliders[i]);
                }
            }
        }

        private void PlaceOnGrid(CombatUnit unit)
        {
            GridTile tile = grid.GetTileAt(grid.GetGridPosition(unit.transform.position));
            if (tile == null || !tile.IsWalkable || (tile.IsOccupied && tile.OccupyingUnit != unit))
            {
                tile = grid.FindClosestWalkableTile(unit.transform.position);
            }
            if (tile != null)
            {
                unit.MoveToTile(tile);
            }
        }

        /// <summary>
        /// Borrows the scene's combat grid (the village scene shares one with the cellar) or creates a
        /// temporary one when none is active.
        /// </summary>
        private GridManager ResolveGrid()
        {
            GridManager existing = GridManager.Instance;
            if (existing == null)
            {
                existing = FindAnyObjectByType<GridManager>();
            }

            if (existing != null && existing.enabled && existing.gameObject.activeInHierarchy)
            {
                GridManager.Instance = existing;
                gridHomePosition = existing.transform.position;
                ownsGrid = false;
                return existing;
            }

            // An inactive registered grid would make the new one destroy itself in Awake
            previousGridInstance = GridManager.Instance;
            GridManager.Instance = null;

            GameObject gridObject = new GameObject("VillageBrawlGrid");
            GridManager created = gridObject.AddComponent<GridManager>();
            GridManager.Instance = created;
            ownsGrid = true;
            return created;
        }

        private void ReleaseGrid()
        {
            if (grid == null) return;

            grid.ClearAllHighlights();
            grid.ClearGrid();

            if (ownsGrid)
            {
                if (GridManager.Instance == grid) GridManager.Instance = previousGridInstance;
                if (Application.isPlaying) Destroy(grid.gameObject);
                else DestroyImmediate(grid.gameObject);
            }
            else
            {
                // Hand the shared grid back where it was so the cellar encounter finds it untouched
                grid.transform.position = gridHomePosition;
            }

            grid = null;
            ownsGrid = false;
            previousGridInstance = null;
        }

        #endregion
    }
}
