#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.DevTools
{
    /// <summary>
    /// Debug overlay that shows every combat activation area (DungeonRoomController trigger boxes) and every
    /// enemy, including the ones still hidden until their trigger fires, drawn through walls with name labels.
    /// A line joins each enemy to the trigger that wakes it. Toggle with [F3]. Built only into the Editor and
    /// development builds: on by default in the Editor, off in development builds, absent from release WebGL.
    /// </summary>
    public class EncounterDebugOverlay : MonoBehaviour
    {
        public static EncounterDebugOverlay Instance { get; private set; }

        /// <summary>Whether the overlay is drawn.</summary>
        public static bool Visible { get; set; } = Application.isEditor;

        private const float RefreshInterval = 1f;
        private const float MarkerRadius = 0.6f;
        private const float MarkerHeight = 2.4f;
        private const int CircleSegments = 20;

        private static readonly Color PendingRoomColor = new Color(1f, 0.85f, 0.15f, 1f);
        private static readonly Color ActiveRoomColor = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color ClearedRoomColor = new Color(0.35f, 0.9f, 0.45f, 0.7f);
        private static readonly Color HiddenEnemyColor = new Color(1f, 0.35f, 1f, 1f);
        private static readonly Color AwakeEnemyColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color DeadEnemyColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);

        private struct EnemyEntry
        {
            public GameObject Go;
            public EnemyUnit Unit;
            public DungeonRoomController Room;
        }

        private readonly List<DungeonRoomController> rooms = new List<DungeonRoomController>();
        private readonly List<EnemyEntry> enemies = new List<EnemyEntry>();
        private readonly HashSet<GameObject> roomEnemySet = new HashSet<GameObject>();
        private float nextRefresh;
        private Material lineMaterial;
        private GUIStyle labelStyle;
        private GUIStyle hintStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[EncounterDebugOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<EncounterDebugOverlay>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            nextRefresh = 0f;
        }

        private void Update()
        {
            if (GameInput.IsDebugOverlayHotkeyPressed())
            {
                Visible = !Visible;
                Debug.Log($"[EncounterDebugOverlay] {(Visible ? "Shown" : "Hidden")} (F3).");
            }

            if (Visible && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshInterval;
                Refresh();
            }
        }

        /// <summary>Collects the rooms and enemies of the loaded scenes, active or not.</summary>
        public void Refresh()
        {
            rooms.Clear();
            enemies.Clear();
            roomEnemySet.Clear();

            rooms.AddRange(FindObjectsByType<DungeonRoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            for (int r = 0; r < rooms.Count; r++)
            {
                List<GameObject> list = rooms[r].roomEnemies;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    GameObject go = list[i];
                    if (go == null || !roomEnemySet.Add(go)) continue;
                    enemies.Add(new EnemyEntry { Go = go, Unit = go.GetComponentInChildren<EnemyUnit>(true), Room = rooms[r] });
                }
            }

            // Enemies outside any room (e.g. summoned reinforcements, ambushers placed by hand)
            EnemyUnit[] loose = FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < loose.Length; i++)
            {
                if (loose[i] == null) continue;
                GameObject go = loose[i].gameObject;
                if (roomEnemySet.Contains(go) || IsUnderRoomEnemy(go.transform)) continue;
                if (!go.scene.IsValid()) continue; // prefab assets
                enemies.Add(new EnemyEntry { Go = go, Unit = loose[i], Room = null });
            }
        }

        private bool IsUnderRoomEnemy(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                if (roomEnemySet.Contains(p.gameObject)) return true;
            }
            return false;
        }

        public int RoomCount => rooms.Count;
        public int EnemyCount => enemies.Count;

        #region Drawing

        private void EnsureMaterial()
        {
            if (lineMaterial != null) return;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always); // seen through walls and roofs
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!Visible || cam == null || cam.cameraType != CameraType.Game) return;
            EnsureMaterial();
            if (lineMaterial == null) return;

            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            for (int r = 0; r < rooms.Count; r++)
            {
                DungeonRoomController room = rooms[r];
                if (room == null) continue;
                BoxCollider box = room.GetComponent<BoxCollider>();
                if (box == null) continue;
                GL.Color(RoomColor(room, box));
                DrawBox(room.transform, box.center, box.size);
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyEntry e = enemies[i];
                if (e.Go == null) continue;
                Vector3 feet = e.Go.transform.position;
                GL.Color(EnemyColor(e));
                DrawCircle(feet, MarkerRadius);
                DrawCircle(feet + Vector3.up * MarkerHeight, MarkerRadius * 0.5f);
                Line(feet, feet + Vector3.up * MarkerHeight);

                if (e.Room != null && e.Go != null && !e.Go.activeInHierarchy)
                {
                    BoxCollider box = e.Room.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        Color c = HiddenEnemyColor;
                        c.a = 0.35f;
                        GL.Color(c);
                        Line(feet + Vector3.up * MarkerHeight, e.Room.transform.TransformPoint(box.center));
                    }
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        private static Color RoomColor(DungeonRoomController room, BoxCollider box)
        {
            if (room.IsCleared) return ClearedRoomColor;
            if (room.CurrentState == RoomState.CombatActive || room.IsAwaitingBossDialogue) return ActiveRoomColor;
            return box.enabled ? PendingRoomColor : ActiveRoomColor;
        }

        private static Color EnemyColor(EnemyEntry e)
        {
            if (e.Unit != null && e.Go.activeInHierarchy && !e.Unit.IsAlive) return DeadEnemyColor;
            return e.Go.activeInHierarchy ? AwakeEnemyColor : HiddenEnemyColor;
        }

        private static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int s = 1; s <= CircleSegments; s++)
            {
                float a = s * Mathf.PI * 2f / CircleSegments;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Line(prev, next);
                prev = next;
            }
        }

        private static void DrawBox(Transform t, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = t.TransformPoint(center + new Vector3(-h.x, -h.y, -h.z));
            Vector3 p100 = t.TransformPoint(center + new Vector3(h.x, -h.y, -h.z));
            Vector3 p010 = t.TransformPoint(center + new Vector3(-h.x, h.y, -h.z));
            Vector3 p110 = t.TransformPoint(center + new Vector3(h.x, h.y, -h.z));
            Vector3 p001 = t.TransformPoint(center + new Vector3(-h.x, -h.y, h.z));
            Vector3 p101 = t.TransformPoint(center + new Vector3(h.x, -h.y, h.z));
            Vector3 p011 = t.TransformPoint(center + new Vector3(-h.x, h.y, h.z));
            Vector3 p111 = t.TransformPoint(center + new Vector3(h.x, h.y, h.z));

            Line(p000, p100); Line(p100, p101); Line(p101, p001); Line(p001, p000);
            Line(p010, p110); Line(p110, p111); Line(p111, p011); Line(p011, p010);
            Line(p000, p010); Line(p100, p110); Line(p101, p111); Line(p001, p011);
        }

        #endregion

        #region Labels

        private void OnGUI()
        {
            if (!Visible) return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                labelStyle.normal.textColor = Color.white;
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                hintStyle.normal.textColor = new Color(1f, 0.85f, 0.3f, 1f);
            }

            GUI.Label(new Rect(10f, Screen.height - 24f, 520f, 20f),
                $"Encounter debug [F3]: {rooms.Count} trigger areas, {enemies.Count} enemies (magenta = hidden until triggered)", hintStyle);

            Camera cam = Camera.main;
            if (cam == null) return;

            for (int r = 0; r < rooms.Count; r++)
            {
                DungeonRoomController room = rooms[r];
                if (room == null) continue;
                BoxCollider box = room.GetComponent<BoxCollider>();
                if (box == null) continue;
                Vector3 top = room.transform.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f);
                string key = !string.IsNullOrEmpty(room.bossIdentifier) ? " boss " + room.bossIdentifier
                    : !string.IsNullOrEmpty(room.roomKey) ? " " + room.roomKey : string.Empty;
                string state = room.IsCleared ? "cleared" : room.CurrentState == RoomState.CombatActive ? "in combat"
                    : box.enabled ? "armed" : "triggered";
                DrawLabel(cam, top, $"TRIGGER {room.roomLocation}{key} ({state})", RoomColor(room, box));
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyEntry e = enemies[i];
                if (e.Go == null) continue;
                string name = e.Unit != null && !string.IsNullOrEmpty(e.Unit.UnitName) ? e.Unit.UnitName : e.Go.name;
                string state = !e.Go.activeInHierarchy ? "hidden" : e.Unit != null && !e.Unit.IsAlive ? "dead"
                    : e.Unit != null ? $"{e.Unit.CurrentHP}/{e.Unit.MaxHP} HP" : "active";
                DrawLabel(cam, e.Go.transform.position + Vector3.up * (MarkerHeight + 0.3f), $"{name} ({state})", EnemyColor(e));
            }
        }

        private void DrawLabel(Camera cam, Vector3 world, string text, Color color)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;

            Vector2 size = labelStyle.CalcSize(new GUIContent(text));
            Rect rect = new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y, size.x, size.y);
            Color old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 1f);
            GUI.Label(rect, text, labelStyle);
            GUI.color = old;
        }

        #endregion
    }
}
#endif
