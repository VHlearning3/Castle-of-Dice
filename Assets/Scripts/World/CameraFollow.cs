using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Baldur's Gate 3 style camera (Vili, 2026-10-07), the same in exploration and combat:
    /// [Q]/[E] or a middle-mouse (or right-mouse) drag turn it around the hero, the scroll wheel zooms and tilts
    /// (flatter close in, more top-down far out), WASD and the arrows pan it away from the hero and [Home]
    /// centres it again. It also centres itself when the hero is sent somewhere new.
    /// Walls, roofs, ceilings and doorways between the camera and the hero are cut away (drawn as shadows only and
    /// ignored by clicks, see <see cref="IsCutAway"/>); terrain and room shells around the hero pull the camera in.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Target Tracking")]
        [Tooltip("The target Transform to follow (typically the player hero).")]
        [SerializeField] private Transform target;

        [Tooltip("If true, automatically searches for a PlayerUnit or GameObject tagged 'Player' if target is unassigned.")]
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Offset & Positioning")]
        [Tooltip("The zone's starting view: its length sets the starting zoom, its direction the starting heading.")]
        [SerializeField] private Vector3 offset = new Vector3(-6f, 8f, -8f);

        [Header("Smoothing & Damping")]
        [Tooltip("Approximate time in seconds for the view to catch up with the hero.")]
        [Range(0.01f, 1.0f)]
        [SerializeField] private float smoothTime = 0.2f;

        [Tooltip("Maximum movement speed for the camera in units per second.")]
        [SerializeField] private float maxSpeed = 50f;

        [Header("Rotation & Orientation")]
        [Tooltip("Height above the hero's feet that the camera looks at.")]
        [SerializeField] private float lookAtHeightOffset = 1.0f;

        [Header("Turning")]
        [Tooltip("Degrees per second while [Q] or [E] is held.")]
        [SerializeField] private float keyTurnSpeed = 110f;

        [Tooltip("Degrees per pixel of middle- or right-mouse drag.")]
        [SerializeField] private float dragTurnSensitivity = 0.25f;

        [Tooltip("Also turn the camera with a right-mouse drag (the game's older control).")]
        [SerializeField] private bool allowRightClickTurn = true;

        [Header("Zoom & Tilt")]
        [Tooltip("Camera distance from the hero when fully zoomed in.")]
        [SerializeField] private float minDistance = 5f;

        [Tooltip("Camera distance from the hero when fully zoomed out.")]
        [SerializeField] private float maxDistance = 24f;

        [Tooltip("Downward tilt in degrees when fully zoomed in (flatter, closer to the hero).")]
        [SerializeField] private float minPitch = 26f;

        [Tooltip("Downward tilt in degrees when fully zoomed out (more top-down).")]
        [SerializeField] private float maxPitch = 62f;

        [Tooltip("Zoom change per scroll wheel notch (0..1 of the whole range).")]
        [SerializeField] private float zoomStep = 0.08f;

        [Header("Panning")]
        [Tooltip("Pan speed as a share of the camera distance per second.")]
        [SerializeField] private float panSpeedPerDistance = 0.9f;

        [Tooltip("How far (in metres) the view may be panned away from the hero.")]
        [SerializeField] private float maxPanDistance = 30f;

        [Header("Walls & Interiors")]
        [Tooltip("Radius of the sight check between the hero and the camera.")]
        [SerializeField] private float sightRadius = 0.25f;

        [Tooltip("Closest the camera may be pulled in to the hero by terrain or a room shell.")]
        [SerializeField] private float minPulledDistance = 1.8f;

        [Tooltip("Meshes without colliders this close to a door (metres, on the ground) are cut away like walls: door frames, exit signs, gatehouses.")]
        [SerializeField] private float doorwayDecorRadius = 6f;

        #endregion

        #region Private State

        private const int UnitLayer = 9;
        private const int InteractableLayer = 8;
        private const int GridTileLayer = 6;
        private const float CutAwayHoldTime = 0.25f;
        private const int MaxCutAwayRenderers = 60;
        private const float DoorwayPieceMinSize = 1.2f;  // braziers, torches and trim stay as they are
        private const float DoorwayPieceMaxSize = 24f;   // ground, roads and whole battlement rings are never cut
        private const int MaxDoorwayRenderers = 400;
        private const float DoorwayRescanDelay = 1f;     // exit signs and other runtime pieces are built after the load

        private float currentYaw;
        private float targetYaw;
        private float yawVelocity;
        private float currentZoom = 0.5f;
        private float targetZoom = 0.5f;
        private float zoomVelocity;
        private Vector3 panOffset;
        private bool recentring;
        private Vector3 smoothFocus;
        private Vector3 focusVelocity;
        private float currentPull = float.MaxValue;
        private bool isInitialized;
        private bool isDragging;
        private int lastDestinationVersion = -1;
        private PlayerExplorationMovement cachedPlayerMovement;

        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private readonly Dictionary<Collider, Renderer[]> occluderCache = new Dictionary<Collider, Renderer[]>();
        private readonly Dictionary<Renderer, CutAwayRenderer> cutRenderers = new Dictionary<Renderer, CutAwayRenderer>();
        private readonly List<Renderer> cutRendererList = new List<Renderer>();
        private readonly Dictionary<Collider, float> cutColliders = new Dictionary<Collider, float>();
        private readonly List<Collider> cutColliderList = new List<Collider>();
        private static readonly Renderer[] NoRenderers = new Renderer[0];
        private readonly List<Renderer> doorwayRenderers = new List<Renderer>();
        private static readonly List<Collider> colliderBuffer = new List<Collider>();
        private bool doorwaysCollected;
        private int doorwayScanFrame;
        private float doorwayRescanAt = -1f;

        private struct CutAwayRenderer
        {
            public ShadowCastingMode OriginalMode;
            public float LastSeen;
        }

        private static CameraFollow s_active;

        #endregion

        #region Public Properties

        /// <summary>Current target Transform being followed.</summary>
        public Transform Target => target;

        /// <summary>The zone's starting view offset (sets the starting zoom and heading).</summary>
        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        /// <summary>Smooth damping time in seconds for following the hero.</summary>
        public float SmoothTime
        {
            get => smoothTime;
            set => smoothTime = Mathf.Max(0.001f, value);
        }

        /// <summary>Current horizontal heading angle (yaw) of the camera in degrees.</summary>
        public float CurrentYaw => currentYaw;

        /// <summary>Zoom between 0 (closest, flattest) and 1 (farthest, most top-down).</summary>
        public float Zoom => currentZoom;

        /// <summary>Whether the view has been panned away from the hero.</summary>
        public bool IsPanned => panOffset.sqrMagnitude > 0.01f;

        /// <summary>
        /// True while <paramref name="col"/> is cut away because it stands between the camera and the hero.
        /// Click handlers skip such colliders so the player can click through a hidden wall.
        /// </summary>
        public static bool IsCutAway(Collider col)
        {
            return col != null && s_active != null && s_active.cutColliders.ContainsKey(col);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (target == null && autoFindPlayer)
            {
                FindPlayerTarget();
            }

            InitializeView();
        }

        private void Start()
        {
            if (target != null)
            {
                SnapToTarget();
            }
        }

        private void OnEnable()
        {
            s_active = this;
            DungeonRoomController.OnRoomCombatStarted += HandleRoomCombatStarted;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ScheduleDoorwayScan();
        }

        private void OnDisable()
        {
            DungeonRoomController.OnRoomCombatStarted -= HandleRoomCombatStarted;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RestoreAllCutAway();
            doorwayRenderers.Clear();
            if (s_active == this) s_active = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScheduleDoorwayScan();
        }

        /// <summary>
        /// Turns the camera to look from the hero towards the room's hostiles when an encounter starts,
        /// so the enemies are on screen instead of behind the camera (e.g. after climbing down the cellar ladder).
        /// </summary>
        private void HandleRoomCombatStarted(DungeonRoomController room)
        {
            if (room == null || target == null || room.roomEnemies == null) return;

            Vector3 enemyCenter = Vector3.zero;
            int count = 0;
            for (int i = 0; i < room.roomEnemies.Count; i++)
            {
                GameObject enemy = room.roomEnemies[i];
                if (enemy != null && enemy.activeInHierarchy)
                {
                    enemyCenter += enemy.transform.position;
                    count++;
                }
            }
            if (count == 0) return;

            Vector3 toEnemies = enemyCenter / count - target.position;
            toEnemies.y = 0f;
            if (toEnemies.sqrMagnitude < 0.01f) return;

            FaceDirection(toEnemies);
        }

        /// <summary>
        /// Instantly orients the camera so it looks along <paramref name="worldDirection"/> behind the target.
        /// </summary>
        public void FaceDirection(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;

            currentYaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
            targetYaw = currentYaw;
            yawVelocity = 0f;
            SnapToTarget();
        }

        private const float PlayerSearchInterval = 0.5f;
        private float nextPlayerSearchTime;

        private void LateUpdate()
        {
            if (target == null)
            {
                // Scene-wide searches are expensive; retry on a short interval instead of every frame
                if (autoFindPlayer && Time.unscaledTime >= nextPlayerSearchTime)
                {
                    nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;
                    FindPlayerTarget();
                }

                if (target == null) return;
            }

            if (!isInitialized)
            {
                InitializeView();
            }

            float dt = Time.unscaledDeltaTime;
            if (CameraInputAllowed())
            {
                HandleTurnInput(dt);
                HandleZoomInput();
                HandlePanInput(dt);
            }
            else
            {
                isDragging = false;
            }

            WatchHeroOrders();

            if (recentring)
            {
                panOffset = Vector3.Lerp(panOffset, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
                if (panOffset.sqrMagnitude < 0.0025f)
                {
                    panOffset = Vector3.zero;
                    recentring = false;
                }
            }

            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, 0.08f, Mathf.Infinity, dt);
            currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, 0.12f, Mathf.Infinity, dt);

            Vector3 desiredFocus = target.position + panOffset;
            smoothFocus = Vector3.SmoothDamp(smoothFocus, desiredFocus, ref focusVelocity, smoothTime, maxSpeed, Time.deltaTime);

            PlaceCamera(smoothFocus, dt, false);
        }

        #endregion

        #region Input

        private static bool CameraInputAllowed()
        {
            if (!GameInput.IsExplorationInputEnabled) return false;
            if (Time.timeScale <= 0f) return false;
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GamePlayMode.Dialogue) return false;
            return true;
        }

        private void HandleTurnInput(float dt)
        {
            float keyAxis = GameInput.GetCameraRotateAxis();
            if (Mathf.Abs(keyAxis) > 0.01f)
            {
                targetYaw += keyAxis * keyTurnSpeed * dt;
            }

            if (GameInput.GetMiddleMouseButtonDown() || (allowRightClickTurn && GameInput.GetRightMouseButtonDown()))
            {
                isDragging = !IsPointerOverInteractiveUI();
            }

            bool held = GameInput.GetMiddleMouseButton() || (allowRightClickTurn && GameInput.GetRightMouseButton());
            if (!held)
            {
                isDragging = false;
            }
            else if (isDragging)
            {
                Vector2 mouseDelta = GameInput.GetMouseDelta();
                if (Mathf.Abs(mouseDelta.x) > 0.01f)
                {
                    targetYaw += mouseDelta.x * dragTurnSensitivity;
                    currentYaw = targetYaw;
                    yawVelocity = 0f;
                }
            }

            targetYaw = Mathf.Repeat(targetYaw, 360f);
            currentYaw = Mathf.Repeat(currentYaw, 360f);
        }

        private void HandleZoomInput()
        {
            float scroll = GameInput.GetMouseScrollDelta().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            if (IsPointerOverScrollableUI()) return;

            // Scroll up (positive) zooms in. Browsers and platforms report very different step sizes,
            // so each frame with scrolling counts as one notch.
            targetZoom = Mathf.Clamp01(targetZoom - Mathf.Sign(scroll) * zoomStep);
        }

        private void HandlePanInput(float dt)
        {
            Vector2 pan = GameInput.GetCameraPanVector();
            if (pan.sqrMagnitude > 0.01f)
            {
                recentring = false;
                Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
                Vector3 move = yawRotation * new Vector3(pan.x, 0f, pan.y);
                if (move.sqrMagnitude > 1f) move.Normalize();
                panOffset += move * (panSpeedPerDistance * CurrentDistance() * dt);
                panOffset.y = 0f;
                if (panOffset.magnitude > maxPanDistance) panOffset = panOffset.normalized * maxPanDistance;
            }

            if (GameInput.IsRecentreCameraPressed())
            {
                Recentre();
            }
        }

        /// <summary>Re-centres the view on the hero when they are sent somewhere new or steered with a gamepad.</summary>
        private void WatchHeroOrders()
        {
            if (cachedPlayerMovement == null) return;

            int version = cachedPlayerMovement.DestinationVersion;
            if (version != lastDestinationVersion)
            {
                if (lastDestinationVersion >= 0 && IsPanned) Recentre();
                lastDestinationVersion = version;
            }
            if (cachedPlayerMovement.IsSteeredDirectly && IsPanned) Recentre();
        }

        /// <summary>Zooms to <paramref name="zoom01"/> (0 = closest and flattest, 1 = farthest and most top-down).</summary>
        public void SetZoom(float zoom01, bool instant = false)
        {
            targetZoom = Mathf.Clamp01(zoom01);
            if (instant)
            {
                currentZoom = targetZoom;
                zoomVelocity = 0f;
            }
        }

        /// <summary>Smoothly brings a panned view back onto the hero (the [Home] key).</summary>
        public void Recentre()
        {
            recentring = true;
        }

        private bool IsPointerOverInteractiveUI()
        {
            EventSystem es = EventSystem.current;
            if (es == null || !es.IsPointerOverGameObject()) return false;
            return PointerHitsUI(es, false);
        }

        private bool IsPointerOverScrollableUI()
        {
            EventSystem es = EventSystem.current;
            if (es == null || !es.IsPointerOverGameObject()) return false;
            return PointerHitsUI(es, true);
        }

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerDataSystem;

        // Only runs on the frame a drag or scroll starts over UI, so the event system raycast is rare
        private bool PointerHitsUI(EventSystem es, bool scrollOnly)
        {
            if (pointerData == null || pointerDataSystem != es)
            {
                pointerData = new PointerEventData(es);
                pointerDataSystem = es;
            }
            pointerData.position = GameInput.GetMousePosition();
            uiHits.Clear();
            es.RaycastAll(pointerData, uiHits);
            for (int i = 0; i < uiHits.Count; i++)
            {
                GameObject go = uiHits[i].gameObject;
                if (go == null) continue;
                if (go.GetComponentInParent<ScrollRect>() != null) return true;
                if (!scrollOnly && go.GetComponentInParent<Selectable>() != null) return true;
            }
            return false;
        }

        #endregion

        #region Placement & Walls

        private float CurrentDistance()
        {
            return Mathf.Lerp(minDistance, maxDistance, currentZoom);
        }

        private void PlaceCamera(Vector3 focus, float dt, bool snap)
        {
            float pitch = Mathf.Lerp(minPitch, maxPitch, currentZoom);
            float distance = CurrentDistance();

            Vector3 lookPoint = focus + Vector3.up * lookAtHeightOffset;
            Quaternion rotation = Quaternion.Euler(pitch, currentYaw, 0f);
            Vector3 back = rotation * Vector3.back;

            // Cut away what stands between the hero and the camera; pull in for what cannot be cut away
            Vector3 heroLook = target.position + Vector3.up * Mathf.Max(lookAtHeightOffset, 1.2f);
            float pulled = UpdateWalls(heroLook, lookPoint + back * distance, focus);

            if (snap || pulled < currentPull) currentPull = pulled;
            else currentPull = Mathf.MoveTowards(currentPull, pulled, 10f * dt);

            float finalDistance = Mathf.Clamp(Mathf.Min(distance, currentPull), Mathf.Min(minPulledDistance, distance), distance);
            transform.position = lookPoint + back * finalDistance;
            transform.rotation = rotation;
        }

        /// <summary>Returns how far from the look point the camera may sit before terrain or a room shell blocks it.</summary>
        private float UpdateWalls(Vector3 heroLook, Vector3 cameraPoint, Vector3 focus)
        {
            float now = Time.unscaledTime;
            float allowed = float.MaxValue;

            Vector3 toCamera = cameraPoint - heroLook;
            float length = toCamera.magnitude;
            if (length > 0.01f)
            {
                Vector3 dir = toCamera / length;
                int mask = ~((1 << UnitLayer) | (1 << InteractableLayer) | (1 << GridTileLayer) | (1 << 2));
                int count = Physics.SphereCastNonAlloc(heroLook, sightRadius, dir, sightHits, length, mask, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = sightHits[i];
                    Collider col = hit.collider;
                    if (col == null || hit.distance <= 0f) continue;
                    if (col.transform.IsChildOf(target)) continue;
                    if (col.GetComponentInParent<CombatUnit>() != null) continue;

                    if (col is TerrainCollider)
                    {
                        allowed = Mathf.Min(allowed, PulledDistanceFor(hit, heroLook, focus));
                        continue;
                    }

                    Renderer[] renderers = GetOccluderRenderers(col);
                    if (renderers.Length == 0) continue; // invisible blockers do not hide anything

                    bool cutAny = false;
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        Renderer rend = renderers[r];
                        if (rend == null) continue;

                        // A shell the hero stands inside (a whole room mesh) cannot be hidden without hiding the floor
                        if (rend.bounds.Contains(heroLook)) continue;

                        CutAway(rend, now);
                        cutAny = true;
                    }

                    if (cutAny) cutColliders[col] = now;
                    else allowed = Mathf.Min(allowed, PulledDistanceFor(hit, heroLook, focus));
                }

                CutAwayDoorways(heroLook, cameraPoint, now);
            }

            RestoreStaleCutAway(now);
            return allowed;
        }

        /// <summary>
        /// Doorways have no solid colliders, so the sight check never meets them: a hero who had just come through
        /// a castle door stood with the camera outside it, hidden by the door frame and its "To ..." sign
        /// (Vili, 2026-10-07). Their meshes are collected once per scene and tested by their boxes instead.
        /// </summary>
        private void CutAwayDoorways(Vector3 heroLook, Vector3 cameraPoint, float now)
        {
            if ((!doorwaysCollected && Time.frameCount >= doorwayScanFrame) || (doorwayRescanAt > 0f && now >= doorwayRescanAt))
            {
                if (doorwayRescanAt > 0f && now >= doorwayRescanAt) doorwayRescanAt = -1f;
                CollectDoorwayRenderers();
            }

            for (int i = doorwayRenderers.Count - 1; i >= 0; i--)
            {
                Renderer rend = doorwayRenderers[i];
                if (rend == null)
                {
                    doorwayRenderers.RemoveAt(i);
                    continue;
                }
                if (!rend.enabled || !rend.gameObject.activeInHierarchy) continue;

                Bounds box = rend.bounds;
                box.Expand(sightRadius * 2f);
                if (!SegmentHitsBox(heroLook, cameraPoint, box)) continue;

                // The world box of a turned or arched piece is loose: the piece's own box decides
                Matrix4x4 toLocal = rend.transform.worldToLocalMatrix;
                if (!SegmentHitsBox(toLocal.MultiplyPoint3x4(heroLook), toLocal.MultiplyPoint3x4(cameraPoint), rend.localBounds)) continue;

                CutAway(rend, now);
            }
        }

        // First scan on the next frame, after the new scene's Start() calls have built the exit signs
        private void ScheduleDoorwayScan()
        {
            doorwaysCollected = false;
            doorwayScanFrame = Time.frameCount + 1;
            doorwayRescanAt = Time.unscaledTime + DoorwayRescanDelay;
        }

        /// <summary>
        /// The pieces of every door (frame, leaves, exit sign) and the dressing around it without colliders of
        /// its own (gatehouse lintels, arches). People, chests and pickups stay visible.
        /// </summary>
        private void CollectDoorwayRenderers()
        {
            doorwaysCollected = true;
            doorwayRenderers.Clear();

            DoorTeleporter[] doors = FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None);
            if (doors.Length == 0) return;

            float radiusSqr = doorwayDecorRadius * doorwayDecorRadius;
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length && doorwayRenderers.Count < MaxDoorwayRenderers; i++)
            {
                MeshRenderer rend = renderers[i];
                if (rend.shadowCastingMode == ShadowCastingMode.ShadowsOnly && !cutRenderers.ContainsKey(rend)) continue;
                int layer = rend.gameObject.layer;
                if (layer == UnitLayer || layer == GridTileLayer) continue;
                if (target != null && rend.transform.IsChildOf(target)) continue;

                Bounds box = rend.bounds;
                float size = Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
                if (size < DoorwayPieceMinSize || size > DoorwayPieceMaxSize) continue;
                if (rend.GetComponentInParent<CombatUnit>() != null) continue;

                Interactable owner = rend.GetComponentInParent<Interactable>();
                if (owner != null && !(owner is DoorTeleporter)) continue;
                if (owner == null && (HasSolidCollider(rend.transform) || !NearAnyDoor(box, doors, radiusSqr))) continue;

                doorwayRenderers.Add(rend);
            }
        }

        // Pieces with a collider of their own are found by the sight check
        private static bool HasSolidCollider(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                p.GetComponents(colliderBuffer);
                for (int i = 0; i < colliderBuffer.Count; i++)
                {
                    if (colliderBuffer[i].enabled && !colliderBuffer[i].isTrigger) return true;
                }
            }
            return false;
        }

        private static bool NearAnyDoor(Bounds box, DoorTeleporter[] doors, float radiusSqr)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null) continue;
                Vector3 door = doors[i].transform.position;
                door.y = box.center.y;
                if (box.SqrDistance(door) <= radiusSqr) return true;
            }
            return false;
        }

        /// <summary>Whether the segment from <paramref name="from"/> to <paramref name="to"/> passes through <paramref name="box"/>.</summary>
        public static bool SegmentHitsBox(Vector3 from, Vector3 to, Bounds box)
        {
            Vector3 min = box.min;
            Vector3 max = box.max;
            Vector3 dir = to - from;
            float enter = 0f;
            float exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float origin = from[axis];
                float step = dir[axis];
                if (Mathf.Abs(step) < 1e-6f)
                {
                    if (origin < min[axis] || origin > max[axis]) return false;
                    continue;
                }

                float t1 = (min[axis] - origin) / step;
                float t2 = (max[axis] - origin) / step;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }
                if (t1 > enter) enter = t1;
                if (t2 < exit) exit = t2;
                if (enter > exit) return false;
            }
            return true;
        }

        // The sight check runs from the hero, but the camera is placed from the (maybe panned) look point
        private float PulledDistanceFor(RaycastHit hit, Vector3 heroLook, Vector3 focus)
        {
            Vector3 hitPoint = heroLook + (hit.point - heroLook).normalized * Mathf.Max(0f, hit.distance - 0.2f);
            Vector3 lookPoint = focus + Vector3.up * lookAtHeightOffset;
            return Vector3.Distance(lookPoint, hitPoint);
        }

        private Renderer[] GetOccluderRenderers(Collider col)
        {
            if (occluderCache.TryGetValue(col, out Renderer[] cached)) return cached;

            Renderer[] found = col.GetComponentsInChildren<Renderer>();
            if (found.Length == 0 && col.transform.parent != null)
            {
                Renderer parentRenderer = col.transform.parent.GetComponent<Renderer>();
                if (parentRenderer != null) found = new[] { parentRenderer };
            }

            // Keep only solid meshes; particles, lines, text and huge groups stay as they are
            int keep = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] is MeshRenderer || found[i] is SkinnedMeshRenderer) found[keep++] = found[i];
            }
            Renderer[] result;
            if (keep == 0 || keep > MaxCutAwayRenderers) result = NoRenderers;
            else if (keep == found.Length) result = found;
            else
            {
                result = new Renderer[keep];
                System.Array.Copy(found, result, keep);
            }

            occluderCache[col] = result;
            return result;
        }

        private void CutAway(Renderer rend, float now)
        {
            if (cutRenderers.TryGetValue(rend, out CutAwayRenderer info))
            {
                info.LastSeen = now;
                cutRenderers[rend] = info;
                return;
            }

            if (rend.shadowCastingMode == ShadowCastingMode.ShadowsOnly) return; // already shadow-only by design
            cutRenderers[rend] = new CutAwayRenderer { OriginalMode = rend.shadowCastingMode, LastSeen = now };
            cutRendererList.Add(rend);
            rend.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        private void RestoreStaleCutAway(float now)
        {
            for (int i = cutRendererList.Count - 1; i >= 0; i--)
            {
                Renderer rend = cutRendererList[i];
                if (rend == null)
                {
                    cutRenderers.Remove(rend);
                    cutRendererList.RemoveAt(i);
                    continue;
                }

                CutAwayRenderer info = cutRenderers[rend];
                if (now - info.LastSeen < CutAwayHoldTime) continue;

                rend.shadowCastingMode = info.OriginalMode;
                cutRenderers.Remove(rend);
                cutRendererList.RemoveAt(i);
            }

            cutColliderList.Clear();
            foreach (KeyValuePair<Collider, float> pair in cutColliders)
            {
                if (pair.Key == null || now - pair.Value >= CutAwayHoldTime) cutColliderList.Add(pair.Key);
            }
            for (int i = 0; i < cutColliderList.Count; i++)
            {
                cutColliders.Remove(cutColliderList[i]);
            }
        }

        private void RestoreAllCutAway()
        {
            for (int i = 0; i < cutRendererList.Count; i++)
            {
                Renderer rend = cutRendererList[i];
                if (rend != null && cutRenderers.TryGetValue(rend, out CutAwayRenderer info))
                {
                    rend.shadowCastingMode = info.OriginalMode;
                }
            }
            cutRendererList.Clear();
            cutRenderers.Clear();
            cutColliders.Clear();
        }

        #endregion

        #region Direction Tracking

        private void InitializeView()
        {
            if (target == null) return;

            cachedPlayerMovement = target.GetComponent<PlayerExplorationMovement>();
            lastDestinationVersion = cachedPlayerMovement != null ? cachedPlayerMovement.DestinationVersion : -1;

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                currentYaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            }
            else
            {
                currentYaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
            }

            targetYaw = currentYaw;

            // Start at the zone's own view distance
            float startDistance = offset.magnitude > 0.5f ? offset.magnitude : 14f;
            targetZoom = currentZoom = Mathf.Clamp01(Mathf.InverseLerp(minDistance, maxDistance, startDistance));
            isInitialized = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets a new target to follow, with an option to immediately snap to it.
        /// </summary>
        /// <param name="newTarget">Target Transform to track.</param>
        /// <param name="snapImmediately">If true, skips interpolation and places the camera at the target immediately.</param>
        public void SetTarget(Transform newTarget, bool snapImmediately = false)
        {
            target = newTarget;
            cachedPlayerMovement = newTarget != null ? newTarget.GetComponent<PlayerExplorationMovement>() : null;
            lastDestinationVersion = cachedPlayerMovement != null ? cachedPlayerMovement.DestinationVersion : -1;
            focusVelocity = Vector3.zero;
            yawVelocity = 0f;

            if (newTarget != null && !isInitialized)
            {
                InitializeView();
            }

            if (snapImmediately && target != null)
            {
                SnapToTarget();
            }
        }

        /// <summary>
        /// Instantly places the camera on the target without smoothing and clears any panning.
        /// Useful during level transitions, respawns, or teleport abilities (e.g. Blink).
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            if (!isInitialized) InitializeView();

            focusVelocity = Vector3.zero;
            yawVelocity = 0f;
            zoomVelocity = 0f;
            currentZoom = targetZoom;
            currentYaw = targetYaw;
            panOffset = Vector3.zero;
            recentring = false;
            smoothFocus = target.position;
            currentPull = float.MaxValue;

            PlaceCamera(smoothFocus, 0f, true);
        }

        #endregion

        #region Target Discovery

        /// <summary>
        /// Searches the scene for a PlayerUnit or GameObject tagged 'Player'.
        /// </summary>
        private void FindPlayerTarget()
        {
            PlayerUnit playerUnit = FindAnyObjectByType<PlayerUnit>();
            if (playerUnit != null)
            {
                target = playerUnit.transform;
                InitializeView();
                return;
            }

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
                InitializeView();
            }
        }

        #endregion
    }
}
