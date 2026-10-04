using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Puts Vili's gate models on the Forest Path lockpick gate (DC 13, Rogue only) in place:
    /// the closed medieval gate with its lock stands there while the lock holds, and the open
    /// stone archway replaces it once Corvo picks the lock. The old procedural stone frame and
    /// gold lock are removed; the gate's invisible box stays as the click target and blocker
    /// until it is picked. Running it again only refreshes the "Secret_Gate_Models" group.
    /// </summary>
    public static class ForestGateSetup
    {
        public const string ScenePath = "Assets/Scenes/Zone_2_ForestPath.unity";
        public const string LockedModelPath = "Assets/medieval+gate+3d+model.fbx";
        public const string UnlockedModelPath = "Assets/stone+archway+NO+LOCK.fbx";
        public const string LockedTexturePath = "Assets/medieval+gate+3d+model_basecolor.jpg";
        public const string UnlockedTexturePath = "Assets/stone+archway+NO+LOCK_basecolor.jpg";
        public const string MaterialFolder = "Assets/Materials/Gates";
        public const string GateName = "Locked_Secret_Gate";
        public const string ModelsName = "Secret_Gate_Models";
        public const string LockedName = "Gate_Locked";
        public const string UnlockedName = "Gate_Open_Archway";

        /// <summary>Width across the path (world z), the same span the old stone frame covered.</summary>
        private const float TargetWidth = 9.4f;

        [MenuItem("CastleOfDice/Setup Forest Lockpick Gate", false, 14)]
        public static void SetupMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Setup();
        }

        /// <summary>Batch-mode entry: sets up the gate, saves the scene and writes review screenshots.</summary>
        public static void BatchSetupAndScreenshots()
        {
            Setup();
            Screenshots();
        }

        public static void Setup()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject gate = GameObject.Find(GateName);
            if (gate == null)
            {
                Debug.LogError($"[ForestGateSetup] {GateName} not found in {ScenePath}.");
                return;
            }
            LockpickInteraction lockpick = gate.GetComponent<LockpickInteraction>();

            // The old procedural gateway (Zone_Dressing) and its gold lock give way to the models
            GameObject oldFrame = GameObject.Find("Secret_Gate_Frame");
            if (oldFrame != null) Object.DestroyImmediate(oldFrame);
            Transform oldLock = gate.transform.Find("Gold_Lock");
            if (oldLock != null) Object.DestroyImmediate(oldLock.gameObject);

            Transform parent = gate.transform.parent;
            Transform oldModels = parent != null ? parent.Find(ModelsName) : null;
            if (oldModels != null) Object.DestroyImmediate(oldModels.gameObject);

            GameObject models = new GameObject(ModelsName);
            models.transform.SetParent(parent, false);
            Vector3 g = gate.transform.position;

            Material lockedMat = EnsureMaterial("M_Gate_Locked", LockedTexturePath);
            Material openMat = EnsureMaterial("M_Gate_OpenArchway", UnlockedTexturePath);
            GameObject locked = PlaceModel(scene, LockedModelPath, LockedName, models.transform, g, lockedMat);
            GameObject open = PlaceModel(scene, UnlockedModelPath, UnlockedName, models.transform, g, openMat);

            // Both models share the same stone wings either side of the arch; keep the hero out of the stone
            const float opening = 2.6f, wallHeight = 4.2f, wallDepth = 1.4f;
            float wing = (TargetWidth - opening) * 0.5f;
            GameObject blockers = new GameObject("Gate_Wall_Blockers");
            blockers.transform.SetParent(models.transform, false);
            blockers.transform.position = new Vector3(g.x, 0f, g.z);
            blockers.layer = 7; // Obstacle
            for (int side = -1; side <= 1; side += 2)
            {
                BoxCollider box = blockers.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, wallHeight * 0.5f, side * (opening * 0.5f + wing * 0.5f));
                box.size = new Vector3(wallDepth, wallHeight, wing);
            }

            // The box keeps its collider for clicking and blocking; the models are what the player sees
            Renderer slab = gate.GetComponent<Renderer>();
            if (slab != null) slab.enabled = false;

            if (lockpick != null)
            {
                var so = new SerializedObject(lockpick);
                so.FindProperty("lockedModel").objectReferenceValue = locked;
                so.FindProperty("unlockedModel").objectReferenceValue = open;
                so.FindProperty("clearPassageOnUnlock").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (locked != null) locked.SetActive(true);
            if (open != null) open.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ForestGateSetup] Forest lockpick gate models placed and Zone 2 saved.");
        }

        /// <summary>Instantiates a model, turns its wide side across the path, scales it to the gateway and stands it on the ground.</summary>
        private static GameObject PlaceModel(Scene scene, string path, string name, Transform parent, Vector3 gatePos, Material mat)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[ForestGateSetup] Model not found at {path}.");
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;

            Bounds raw = WorldBounds(go);
            Debug.Log($"[ForestGateSetup] {name} raw bounds size {raw.size}, center {raw.center}");

            // The path runs along world x, so the gate's wide side must face along z
            float yaw = raw.size.x > raw.size.z ? 90f : 0f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float width = Mathf.Max(raw.size.x, raw.size.z);
            float scale = width > 0.001f ? TargetWidth / width : 1f;
            go.transform.localScale = Vector3.one * scale;

            Bounds b = WorldBounds(go);
            go.transform.position = new Vector3(gatePos.x - b.center.x, -b.min.y, gatePos.z - b.center.z);

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }

            Bounds placed = WorldBounds(go);
            Debug.Log($"[ForestGateSetup] {name} yaw {yaw}, scale {scale:F3}, placed bounds size {placed.size}, center {placed.center}");
            return go;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Material EnsureMaterial(string name, string texturePath)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        #region Screenshots

        /// <summary>Review shots of the gate locked and opened, from the path side and from the game camera angle.</summary>
        public static void Screenshots()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject gate = GameObject.Find(GateName);
            GameObject locked = GameObject.Find(LockedName);
            Transform models = locked != null ? locked.transform.parent : null;
            Transform open = models != null ? models.Find(UnlockedName) : null;
            if (gate == null || locked == null || open == null) return;

            Directory.CreateDirectory("Screenshots/Gates");
            var camGo = new GameObject("ScreenshotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.farClipPlane = 400f;
            GameObject main = GameObject.Find("Main Camera");
            Camera mc = main != null ? main.GetComponent<Camera>() : null;
            if (mc != null) { cam.clearFlags = mc.clearFlags; cam.backgroundColor = mc.backgroundColor; }
            if (main != null) main.SetActive(false);

            Vector3 g = gate.transform.position;
            Vector3 front = new Vector3(g.x + 11f, 3.5f, g.z - 3f);
            Vector3 look = new Vector3(g.x, 3.2f, g.z);
            // Same offset as the in-game CameraFollow (-6, 12, -12) around a hero standing at the gate
            Vector3 hero = new Vector3(g.x + 2.5f, 0f, g.z);
            Vector3 game = hero + new Vector3(-6f, 12f, -12f);

            locked.SetActive(true); open.gameObject.SetActive(false);
            Shot(cam, front, look, "Screenshots/Gates/gate_locked_front.png");
            Shot(cam, game, hero + Vector3.up, "Screenshots/Gates/gate_locked_game.png");
            locked.SetActive(false); open.gameObject.SetActive(true);
            Shot(cam, front, look, "Screenshots/Gates/gate_open_front.png");
            Shot(cam, game, hero + Vector3.up, "Screenshots/Gates/gate_open_game.png");
            Object.DestroyImmediate(camGo);
        }

        private static void Shot(Camera cam, Vector3 pos, Vector3 look, string file)
        {
            const int w = 1600, h = 900;
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        #endregion
    }
}
