using System;
using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Interactive world object (door, hatch, portal, or ladder) that teleports the player
    /// to a specified destination target. Supports both click-to-interact and walk-into-trigger modes.
    /// </summary>
    public class DoorTeleporter : Interactable
    {
        #region Serialized Fields

        [Header("Teleport Target")]
        [Tooltip("Transform representing the landing destination for the player.")]
        [SerializeField] private Transform destination;

        [Tooltip("Fallback world-space landing coordinates if destination Transform is unassigned.")]
        [SerializeField] private Vector3 fallbackDestination = Vector3.zero;

        [Header("Zone Synchronization")]
        [Tooltip("Optional target zone identifier (Village, Courtyard, Library, CrownHall, Forest, CastleHall, Tower, Cellar).")]
        [SerializeField] private string destinationZone = string.Empty;

        [Header("Async Scene Transition (Optional)")]
        [Tooltip("If set, transitioning through this door loads a new Unity scene asynchronously via SceneLoader.")]
        [SerializeField] private string targetSceneName = string.Empty;

        [Tooltip("Optional spawn point or landing object name in the destination scene.")]
        [SerializeField] private string targetSpawnPointName = string.Empty;

        [Header("Trigger Behavior")]
        [Tooltip("If true, entering the trigger volume automatically teleports the player without requiring a click.")]
        [SerializeField] private bool triggerOnWalk = false;

        [Tooltip("Cooldown period in seconds between teleports to prevent ping-ponging between connected doors.")]
        [SerializeField] private float teleportCooldown = 1.0f;

        [Header("Feedback")]
        [Tooltip("Audio clip played upon teleportation.")]
        [SerializeField] private AudioClip teleportSound;

        #endregion

        #region Private State

        private static float s_lastGlobalTeleportTime = -10f;

        #endregion

        #region Public Properties

        /// <summary>Target landing destination transform.</summary>
        public Transform Destination
        {
            get => destination;
            set => destination = value;
        }

        /// <summary>Target landing world coordinates.</summary>
        public Vector3 TargetSpawnPosition
        {
            get => fallbackDestination;
            set => fallbackDestination = value;
        }

        /// <summary>Optional target zone identifier (Village, Courtyard, Library, CrownHall, Forest, CastleHall, Tower, Cellar).</summary>
        public string DestinationZone
        {
            get => destinationZone;
            set => destinationZone = value;
        }

        /// <summary>Target scene name for cross-scene async transitions.</summary>
        public string TargetSceneName
        {
            get => targetSceneName;
            set => targetSceneName = value;
        }

        /// <summary>Target spawn point name in destination scene.</summary>
        public string TargetSpawnPointName
        {
            get => targetSpawnPointName;
            set => targetSpawnPointName = value;
        }

        /// <summary>Whether walking into the trigger automatically teleports the player.</summary>
        public bool TriggerOnWalk
        {
            get => triggerOnWalk;
            set => triggerOnWalk = value;
        }

        /// <summary>Cooldown duration between teleports.</summary>
        public float TeleportCooldown
        {
            get => teleportCooldown;
            set => teleportCooldown = value;
        }

        /// <summary>
        /// Configures destination and prompt parameters programmatically.
        /// </summary>
        public void InitializeTeleporter(Transform destTarget, string prompt, float radius = 3.5f, bool walkTrigger = false)
        {
            destination = destTarget;
            promptMessage = prompt;
            interactionRadius = radius;
            triggerOnWalk = walkTrigger;
        }

        /// <summary>
        /// Configures cross-scene teleporter programmatically.
        /// </summary>
        public void InitializeSceneTeleporter(string sceneName, string spawnName, string destZone, string prompt, float radius = 3.5f, bool walkTrigger = false)
        {
            targetSceneName = sceneName;
            targetSpawnPointName = spawnName;
            destinationZone = destZone;
            promptMessage = prompt;
            interactionRadius = radius;
            triggerOnWalk = walkTrigger;
        }

        #endregion

        #region Interactable Overrides

        /// <summary>
        /// Initiates teleportation when clicked by the player via raycast interaction.
        /// </summary>
        public override void Interact(PlayerUnit player)
        {
            if (player == null) return;
            PerformTeleport(player.gameObject);
        }

        #endregion

        #region Trigger Collision

        /// <summary>
        /// Automatically teleports the player when stepping into the door trigger volume.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnWalk || !isInteractable) return;
            if (Time.timeSinceLevelLoad < 0.6f) return;

            PlayerUnit player = other.GetComponent<PlayerUnit>() ?? other.GetComponentInParent<PlayerUnit>();
            if (player == null && other.CompareTag("Player"))
            {
                player = FindAnyObjectByType<PlayerUnit>();
            }

            if (player != null)
            {
                PerformTeleport(player.gameObject);
            }
        }

        #endregion

        #region Teleportation Execution

        /// <summary>
        /// Safely relocates the player GameObject to the target destination.
        /// Temporarily disables CharacterController to ensure atomic physics transform updates.
        /// </summary>
        public void PerformTeleport(GameObject playerObj)
        {
            if (playerObj == null) return;

            // Prevent teleportation during initial level load
            if (Time.timeSinceLevelLoad < 0.6f)
            {
                return;
            }

            // Prevent rapid ping-pong transitions between linked doors
            if (Time.time < s_lastGlobalTeleportTime + teleportCooldown)
            {
                return;
            }
            s_lastGlobalTeleportTime = Time.time;

            // Optional audio playback
            if (teleportSound != null)
            {
                if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(teleportSound, playerObj.transform.position);
            }

            // 1. Cross-Scene Async Transition
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                if (!string.IsNullOrEmpty(DestinationZone) && GameManager.Instance != null)
                {
                    if (Enum.TryParse<GameLocation>(DestinationZone, true, out GameLocation loc))
                    {
                        GameManager.Instance.SetLocation(loc);
                    }
                }

                Debug.Log($"[DoorTeleporter] '{name}' initiating async load of scene '{targetSceneName}' (target spawn: '{targetSpawnPointName}').");

                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.LoadScene(targetSceneName, targetSpawnPointName);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
                }
                return;
            }

            // 2. Intra-Scene Transform Teleport
            Vector3 targetPosition = destination != null ? destination.position : fallbackDestination;
            Quaternion targetRotation = destination != null ? destination.rotation : playerObj.transform.rotation;

            // CharacterController in Unity overrides transform.position unless temporarily disabled
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;

            if (ccWasEnabled)
            {
                cc.enabled = false;
            }

            try
            {
                playerObj.transform.position = targetPosition;
                playerObj.transform.rotation = targetRotation;
                Physics.SyncTransforms();

                if (!string.IsNullOrEmpty(DestinationZone) && GameManager.Instance != null)
                {
                    if (Enum.TryParse<GameLocation>(DestinationZone, true, out GameLocation loc))
                    {
                        GameManager.Instance.SetLocation(loc);
                    }
                }

                Debug.Log($"[DoorTeleporter] '{name}' teleported player to {targetPosition}.");
            }
            finally
            {
                if (ccWasEnabled)
                {
                    cc.enabled = true;
                }
            }

            // Optional audio playback
            if (teleportSound != null)
            {
                if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(teleportSound, targetPosition);
            }
        }

        #endregion
    }
}
