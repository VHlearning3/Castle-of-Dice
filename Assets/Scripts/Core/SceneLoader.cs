using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Persistent, WebGL-safe Asynchronous Scene Loader.
    /// Manages zero-stutter transitions between the 7 zone scenes, handles atomic player placement
    /// via StartSpawnPoint, synchronizes PlayerDataSO persistence, and crossfades zone music.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        #region Singleton

        public static SceneLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureFadeOverlay();
        }

        #endregion

        #region Serialized Fields

        [Header("Persistence")]
        [Tooltip("PlayerDataSO holding milestone level, permanent attribute improvements, and upgraded abilities.")]
        [SerializeField] private PlayerDataSO playerData;

        [Header("Transition Settings")]
        [Tooltip("Minimum transition duration in seconds for smooth loading pacing.")]
        [SerializeField] private float minimumTransitionTime = 0.5f;

        #endregion

        #region Private State

        private bool isLoading = false;
        private float loadingProgress = 0f;
        private string activeTargetSpawnName = string.Empty;
        private CanvasGroup fadeCanvasGroup;

        private static readonly string[] s_d20Tips = new string[]
        {
            "Rolling a Natural 20 guarantees a critical hit with double weapon damage!",
            "Rogues automatically roll with Advantage (2d20) when picking locks.",
            "Resting at the Great Hall's Runestone Shrine fully restores your health and saves your progress.",
            "Combat encounters reward 2 to 10 scrap metal pieces used for blacksmith weapon upgrades.",
            "Shadow Step allows the Rogue to teleport up to 3 tiles and gain Advantage on the next attack.",
            "Defeating either the Cursed Commander or Shadow Mage Malakor unlocks access to the Crown Hall."
        };

        #endregion

        #region Public Properties

        /// <summary>Whether a scene is currently being loaded asynchronously.</summary>
        public bool IsLoading => isLoading;

        /// <summary>Normalized loading progress from 0.0 to 1.0.</summary>
        public float LoadingProgress => loadingProgress;

        /// <summary>Reference to the active PlayerDataSO.</summary>
        public PlayerDataSO PlayerData
        {
            get => playerData;
            set => playerData = value;
        }

        #endregion

        #region Events

        /// <summary>Fired when asynchronous scene load begins: (targetSceneName).</summary>
        public static event Action<string> OnSceneLoadStarted;

        /// <summary>Fired when asynchronous scene load and player positioning complete: (loadedSceneName).</summary>
        public static event Action<string> OnSceneLoadCompleted;

        #endregion

        #region Public API

        /// <summary>
        /// Asynchronously loads a target zone scene and places the player at the scene's StartSpawnPoint.
        /// </summary>
        public void LoadScene(string sceneName, string targetSpawnName = "")
        {
            if (isLoading)
            {
                Debug.LogWarning($"[SceneLoader] Cannot load '{sceneName}'; already loading a scene.");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, targetSpawnName, null));
        }

        /// <summary>
        /// Asynchronously loads a target zone scene with an optional completion callback.
        /// </summary>
        public void LoadSceneAsync(string sceneName, Action onCompleted = null, string targetSpawnName = "")
        {
            if (isLoading)
            {
                Debug.LogWarning($"[SceneLoader] Cannot load '{sceneName}'; already loading a scene.");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, targetSpawnName, onCompleted));
        }

        /// <summary>
        /// Returns a random tactical tip from the d20 loading matrix.
        /// </summary>
        public static string GetRandomLoadingTip()
        {
            int idx = UnityEngine.Random.Range(0, s_d20Tips.Length);
            return s_d20Tips[idx];
        }

        #endregion

        #region Loading Coroutine & Fade

        private void EnsureFadeOverlay()
        {
            if (fadeCanvasGroup != null) return;

            GameObject overlayObj = new GameObject("SceneLoader_FadeOverlay");
            overlayObj.transform.SetParent(transform, false);

            Canvas canvas = overlayObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            CanvasScaler scaler = overlayObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            fadeCanvasGroup = overlayObj.AddComponent<CanvasGroup>();
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.interactable = false;
            fadeCanvasGroup.blocksRaycasts = false;

            GameObject imgObj = new GameObject("BlackBackdrop", typeof(RectTransform));
            imgObj.transform.SetParent(overlayObj.transform, false);
            RectTransform rt = imgObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            Image img = imgObj.AddComponent<Image>();
            img.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            img.raycastTarget = false;
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
        {
            EnsureFadeOverlay();
            if (fadeCanvasGroup == null) yield break;

            fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.01f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;
            fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.5f;
        }

        private IEnumerator LoadSceneRoutine(string sceneName, string targetSpawnName, Action onCompleted)
        {
            isLoading = true;
            loadingProgress = 0f;
            activeTargetSpawnName = targetSpawnName;

            Debug.Log($"<color=#38bdf8><b>[SceneLoader] Starting async load of scene '{sceneName}'...</b></color>");
            OnSceneLoadStarted?.Invoke(sceneName);

            // Step A: Fade out screen via CanvasGroup
            yield return StartCoroutine(FadeRoutine(0f, 1f, 0.25f));

            // 1. Sync current player state to PlayerDataSO before leaving current scene
            EnsurePlayerDataReference();
            PlayerUnit existingPlayer = FindAnyObjectByType<PlayerUnit>();
            if (existingPlayer != null && playerData != null)
            {
                playerData.SyncFromPlayer(existingPlayer);
            }

            float startTime = Time.realtimeSinceStartup;

            // 2. Load scene asynchronously
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                Debug.LogError($"[SceneLoader] Failed to initiate async load for scene: '{sceneName}'. Is it added to Build Settings?");
                isLoading = false;
                yield return StartCoroutine(FadeRoutine(1f, 0f, 0.2f));
                yield break;
            }

            while (!asyncOp.isDone)
            {
                loadingProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                yield return null;
            }

            loadingProgress = 1.0f;

            // Enforce minimum transition duration for visual smoothness
            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < minimumTransitionTime)
            {
                yield return new WaitForSecondsRealtime(minimumTransitionTime - elapsed);
            }

            // 3. Post-load initialization in the new scene (reposition player, update location, snap camera)
            ResolveNewSceneState(sceneName);

            // Step B: Fade in screen and restore player control
            yield return StartCoroutine(FadeRoutine(1f, 0f, 0.25f));

            isLoading = false;
            OnSceneLoadCompleted?.Invoke(sceneName);
            onCompleted?.Invoke();

            Debug.Log($"<color=#4ade80><b>[SceneLoader] Successfully loaded scene '{sceneName}' and restored player.</b></color>");
        }

        private void ResolveNewSceneState(string sceneName)
        {
            // 1. Map scene name to GameLocation and notify GameManager
            GameLocation location = ParseLocationFromSceneName(sceneName);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetLocation(location);
                GameManager.Instance.SetMode(GamePlayMode.Exploration);
            }

            // 2. Position player at the designated spawn point or StartSpawnPoint
            Transform targetSpawn = FindTargetSpawnTransform(activeTargetSpawnName);
            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            PlayerExplorationMovement movement = FindAnyObjectByType<PlayerExplorationMovement>();

            if (targetSpawn != null && player != null)
            {
                Vector3 spawnPos = targetSpawn.position;
                Quaternion spawnRot = targetSpawn.rotation;

                StartSpawnPoint ssp = targetSpawn.GetComponent<StartSpawnPoint>();
                if (ssp != null)
                {
                    spawnPos = ssp.GetPlayerSpawnPosition(player.GetComponent<CharacterController>());
                }

                if (movement != null)
                {
                    movement.TeleportTo(spawnPos, spawnRot);
                }
                else
                {
                    CharacterController cc = player.GetComponent<CharacterController>();
                    bool ccWasEnabled = cc != null && cc.enabled;
                    if (cc != null) cc.enabled = false;

                    try
                    {
                        player.transform.position = spawnPos;
                        player.transform.rotation = spawnRot;
                        Physics.SyncTransforms();
                    }
                    finally
                    {
                        if (cc != null && ccWasEnabled) cc.enabled = true;
                    }
                }

                player.ClearTile();
                Debug.Log($"[SceneLoader] Player positioned at spawn '{targetSpawn.name}' ({spawnPos}).");
            }

            // 3. Restore persisted hero stats from PlayerDataSO
            if (player != null && playerData != null)
            {
                playerData.ApplyToPlayer(player);
            }

            // 4. Center camera
            CameraFollow cam = FindAnyObjectByType<CameraFollow>();
            if (cam != null)
            {
                cam.SnapToTarget();
            }
        }

        private Transform FindTargetSpawnTransform(string spawnName)
        {
            if (!string.IsNullOrEmpty(spawnName))
            {
                GameObject specificSpawn = GameObject.Find(spawnName);
                if (specificSpawn != null) return specificSpawn.transform;
            }

            // Fallback to active StartSpawnPoint
            StartSpawnPoint startSpawn = FindAnyObjectByType<StartSpawnPoint>();
            if (startSpawn != null)
            {
                return startSpawn.transform;
            }

            // Fallback to tag
            GameObject tagged = GameObject.FindWithTag("Respawn");
            if (tagged != null) return tagged.transform;

            return null;
        }

        private GameLocation ParseLocationFromSceneName(string sceneName)
        {
            string lower = sceneName.ToLowerInvariant();
            if (lower.Contains("village") || lower.Contains("cellar")) return GameLocation.Village;
            if (lower.Contains("forest")) return GameLocation.Forest;
            if (lower.Contains("courtyard")) return GameLocation.Courtyard;
            if (lower.Contains("library")) return GameLocation.Library;
            if (lower.Contains("hall")) return GameLocation.CastleHall;
            if (lower.Contains("tower")) return GameLocation.Tower;
            if (lower.Contains("throne")) return GameLocation.CrownHall;

            return GameLocation.Village;
        }

        private void EnsurePlayerDataReference()
        {
            if (playerData == null)
            {
                playerData = PlayerDataSO.Session;
            }
            else if (playerData != PlayerDataSO.Session)
            {
                PlayerDataSO.Session = playerData;
            }
        }

        #endregion
    }
}
