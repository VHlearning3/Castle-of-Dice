using System;
using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Tangible spawn point component represented as a movable cube in the scene.
    /// Defines where the player starts when launching the game.
    /// The user can freely position and rotate this cube in the Unity Editor.
    /// </summary>
    [SelectionBase]
    [ExecuteAlways]
    public class StartSpawnPoint : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Spawn Settings")]
        [Tooltip("If true, automatically places the player at this spawn point when the game starts.")]
        [SerializeField] private bool snapPlayerOnStart = true;

        [Tooltip("If true, places the player standing directly on top of the cube's upper surface.")]
        [SerializeField] private bool spawnOnCubeTop = true;

        [Tooltip("Optional vertical offset added to the player's spawn elevation.")]
        [SerializeField] private float playerHeightOffset = 0.0f;

        [Header("Play Mode Aesthetics")]
        [Tooltip("Automatically hides the visual MeshRenderer when entering Play mode so the marker is invisible during gameplay.")]
        [SerializeField] private bool hideMeshInPlayMode = true;

        [Tooltip("Automatically disables the cube's Collider in Play mode so it never obstructs player movement.")]
        [SerializeField] private bool disableCollisionInPlayMode = true;

        [Header("Scene View Visuals")]
        [Tooltip("Color of the spawn box and gizmos in Scene view.")]
        [SerializeField] private Color gizmoColor = new Color(0.15f, 0.9f, 0.25f, 0.75f);

        [Tooltip("Draw a directional arrow indicating the player's initial forward-facing orientation.")]
        [SerializeField] private bool showFacingArrow = true;

        #endregion

        #region Private State

        private bool hasSpawnedPlayer = false;

        #endregion

        #region Singleton & Access

        public static StartSpawnPoint Instance { get; private set; }

        /// <summary>
        /// Retrieves the active StartSpawnPoint in the scene.
        /// </summary>
        public static StartSpawnPoint GetSpawnPoint()
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<StartSpawnPoint>();
            }
            return Instance;
        }

        public bool SnapPlayerOnStart => snapPlayerOnStart;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Instance = this;

                if (hideMeshInPlayMode)
                {
                    Renderer rend = GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.enabled = false;
                    }
                }

                if (disableCollisionInPlayMode)
                {
                    Collider col = GetComponent<Collider>();
                    if (col != null)
                    {
                        col.enabled = false;
                    }
                }

                if (snapPlayerOnStart && !hasSpawnedPlayer)
                {
                    SpawnPlayerHere();
                }
            }
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                if (snapPlayerOnStart)
                {
                    SpawnPlayerHere();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Spawn Logic

        /// <summary>
        /// Calculates the exact world position where the player's center will be placed.
        /// </summary>
        public Vector3 GetPlayerSpawnPosition(CharacterController cc = null)
        {
            float playerCenterOffset = 1.0f; // Default center offset for standard 2m tall capsule with center at 0
            if (cc != null)
            {
                playerCenterOffset = (cc.height * 0.5f) - cc.center.y;
            }

            Vector3 targetPos = transform.position;
            if (spawnOnCubeTop)
            {
                targetPos.y += (transform.localScale.y * 0.5f) + playerCenterOffset + playerHeightOffset;
            }
            else
            {
                targetPos.y += playerCenterOffset + playerHeightOffset;
            }

            return targetPos;
        }

        /// <summary>
        /// Instantly places the player at this spawn location and orientation.
        /// </summary>
        [ContextMenu("Snap Player to Spawn Position")]
        public void SpawnPlayerHere()
        {
            PlayerExplorationMovement playerMovement = FindAnyObjectByType<PlayerExplorationMovement>();
            Transform playerTransform = playerMovement != null ? playerMovement.transform : null;

            if (playerTransform == null)
            {
                PlayerUnit pUnit = FindAnyObjectByType<PlayerUnit>();
                if (pUnit != null) playerTransform = pUnit.transform;
            }

            if (playerTransform == null)
            {
                GameObject playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null) playerTransform = playerGo.transform;
            }

            if (playerTransform == null)
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning("[StartSpawnPoint] No Player found in scene to spawn at StartSpawn.");
                }
                return;
            }

            // Clear any lingering tactical tile assignment and ensure exploration mode
            PlayerUnit pUnitComp = playerTransform.GetComponent<PlayerUnit>();
            if (pUnitComp != null)
            {
                pUnitComp.ClearTile();
            }

            if (CastleOfTheD20.Core.GameManager.Instance != null &&
                CastleOfTheD20.Core.GameManager.Instance.CurrentMode != CastleOfTheD20.Core.GamePlayMode.Exploration)
            {
                CastleOfTheD20.Core.GameManager.Instance.SetMode(CastleOfTheD20.Core.GamePlayMode.Exploration);
            }

            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            Vector3 targetPosition = GetPlayerSpawnPosition(cc);
            Quaternion targetRotation = transform.rotation;

            if (playerMovement != null)
            {
                playerMovement.TeleportTo(targetPosition, targetRotation);
            }
            else
            {
                bool ccWasEnabled = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;

                try
                {
                    playerTransform.position = targetPosition;
                    playerTransform.rotation = targetRotation;
                    Physics.SyncTransforms();
                }
                finally
                {
                    if (cc != null && ccWasEnabled) cc.enabled = true;
                }
            }

            // Immediately sync camera without lag
            CameraFollow cam = FindAnyObjectByType<CameraFollow>();
            if (cam != null)
            {
                cam.SnapToTarget();
            }

            hasSpawnedPlayer = true;
            Debug.Log($"[StartSpawnPoint] Player spawned at ({targetPosition.x:F2}, {targetPosition.y:F2}, {targetPosition.z:F2}) facing {transform.eulerAngles.y:F0} deg.");
        }

        /// <summary>
        /// Direct spawn API called from player scripts during early initialization.
        /// </summary>
        public void SpawnPlayer(PlayerExplorationMovement playerMovement)
        {
            if (playerMovement == null) return;
            CharacterController cc = playerMovement.GetComponent<CharacterController>();
            Vector3 targetPosition = GetPlayerSpawnPosition(cc);

            PlayerUnit pUnitComp = playerMovement.GetComponent<PlayerUnit>();
            if (pUnitComp != null)
            {
                pUnitComp.ClearTile();
            }

            playerMovement.TeleportTo(targetPosition, transform.rotation);
            hasSpawnedPlayer = true;
        }

        #endregion

        #region Scene View Gizmos

        private void OnDrawGizmos()
        {
            // Draw spawn footprint box
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Color fillColor = gizmoColor;
            fillColor.a = 0.25f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            // Draw player preview capsule
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 previewCenter = GetPlayerSpawnPosition(null);

            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.7f);
            Gizmos.DrawWireSphere(previewCenter + Vector3.up * 0.5f, 0.4f);
            Gizmos.DrawWireSphere(previewCenter - Vector3.up * 0.5f, 0.4f);
            Gizmos.DrawLine(previewCenter + Vector3.forward * 0.4f + Vector3.up * 0.5f, previewCenter + Vector3.forward * 0.4f - Vector3.up * 0.5f);
            Gizmos.DrawLine(previewCenter - Vector3.forward * 0.4f + Vector3.up * 0.5f, previewCenter - Vector3.forward * 0.4f - Vector3.up * 0.5f);
            Gizmos.DrawLine(previewCenter + Vector3.right * 0.4f + Vector3.up * 0.5f, previewCenter + Vector3.right * 0.4f - Vector3.up * 0.5f);
            Gizmos.DrawLine(previewCenter - Vector3.right * 0.4f + Vector3.up * 0.5f, previewCenter - Vector3.right * 0.4f - Vector3.up * 0.5f);

            // Draw facing direction arrow
            if (showFacingArrow)
            {
                float topY = transform.position.y + (transform.localScale.y * 0.5f) + 0.05f;
                Vector3 arrowStart = new Vector3(transform.position.x, topY, transform.position.z);
                float arrowLength = Mathf.Max(transform.localScale.z, 1.0f) * 1.2f;
                Vector3 arrowEnd = arrowStart + transform.forward * arrowLength;

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(arrowStart, arrowEnd);

                Vector3 arrowHeadL = arrowEnd - transform.forward * 0.35f + transform.right * 0.25f;
                Vector3 arrowHeadR = arrowEnd - transform.forward * 0.35f - transform.right * 0.25f;
                Gizmos.DrawLine(arrowEnd, arrowHeadL);
                Gizmos.DrawLine(arrowEnd, arrowHeadR);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.green;
            labelStyle.fontSize = 12;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            Vector3 labelPos = transform.position + Vector3.up * (transform.localScale.y * 0.5f + 2.4f);
            UnityEditor.Handles.Label(labelPos, $"START SPAWN\nFacing: {transform.eulerAngles.y:F0}°", labelStyle);
        }
#endif

        #endregion
    }
}
