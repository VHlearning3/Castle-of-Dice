using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Builds Zone_Tavern (Vili, 2026-10-07): the two-storey wooden tavern behind Oakhaven's tavern door.
    /// The room itself comes from BlenderSources/Tavern_Interior_build.py (Assets/Models/Tavern/Tavern_Interior.fbx);
    /// this adds the game framework (copied from the Great Hall: camera, EventSystem, Managers, hero), colliders,
    /// warm lights, barrels and chests, the door back to the village and the elf waitress on her round.
    /// Only Zone_Tavern is written. The village side of the door is made at runtime by <see cref="TavernEntrance"/>.
    /// </summary>
    public static class TavernSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Zone_Tavern.unity";
        private const string FrameworkScenePath = "Assets/Scenes/Zone_5_CastleHall.unity";
        private const string ModelPath = "Assets/Models/Tavern/Tavern_Interior.fbx";
        private const string PalettePath = "Assets/Models/Tavern/Tavern_Props_Palette.png";
        private const string MaterialFolder = "Assets/Materials/Tavern";
        private const string StoneTexture = "Assets/Textures/Walls/Hall_Wall_Stone.png";
        private const string StoneNormal = "Assets/Textures/Walls/Hall_Wall_Stone_Normal.png";
        private const string PlankTexture = "Assets/Materials/Cellar/Cellar_WoodPlanks.png";
        private const string WaitressPrefab = "Assets/PREFABS/NPCs/NPC_Elf_Waitress.prefab";
        private const string DoorPrefab = "Assets/PREFABS/Doors/Door_Single.prefab";

        private static readonly string[] FrameworkRoots = { "Main Camera", "EventSystem", "Managers", "PlayerHero" };

        // Pieces that get exact mesh colliders (walk surfaces and walls) and simple boxes (furniture)
        private static readonly string[] MeshColliderPrefixes = { "Floor", "Wall_North", "Wall_West", "Wall_East", "Wall_South", "Gallery_", "Fireplace", "Bar", "Kegs", "Bookshelf" };
        private static readonly string[] BoxColliderPrefixes = { "Post_", "Table_", "Candelabra_" };
        private static readonly string[] NoColliderNames = { "Fireplace_Opening", "Bar_Shelves" };

        private static readonly Color WarmLight = new Color(1f, 0.66f, 0.36f);
        private static readonly Color FireLight = new Color(1f, 0.52f, 0.2f);

        [MenuItem("CastleOfDice/Build Tavern (Zone_Tavern only)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TavernSceneBuilder] Exit Play Mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Dictionary<string, Material> mats = SetupMaterials();
            ConfigureModel(mats);
            Scene scene = OpenTavernScene();
            BuildContents(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[TavernSceneBuilder] Zone_Tavern built: " + ScenePath);
        }

        /// <summary>Batch entry (-executeMethod).</summary>
        public static void BatchBuild() => Build();

        #region Materials & model

        private static Dictionary<string, Material> SetupMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Tavern");
            if (AssetImporter.GetAtPath(PalettePath) is TextureImporter pal)
            {
                pal.filterMode = FilterMode.Point;
                pal.mipmapEnabled = false;
                pal.textureCompression = TextureImporterCompression.Uncompressed;
                pal.maxTextureSize = 32;
                pal.SaveAndReimport();
            }

            var mats = new Dictionary<string, Material>
            {
                ["M_Tavern_Stone"] = Lit("M_Tavern_Stone", StoneTexture, new Color(1f, 0.78f, 0.6f), 0.08f, StoneNormal),
                ["M_Tavern_Floor"] = Lit("M_Tavern_Floor", PlankTexture, new Color(1f, 0.9f, 0.8f), 0.18f),
                ["M_Tavern_Timber"] = Lit("M_Tavern_Timber", PlankTexture, new Color(0.56f, 0.42f, 0.34f), 0.12f),
                ["M_Tavern_Props"] = Lit("M_Tavern_Props", PalettePath, Color.white, 0.2f),
                ["M_Tavern_Glow"] = Glow("M_Tavern_Glow", new Color(1f, 0.8f, 0.45f), new Color(1f, 0.6f, 0.22f) * 3.2f),
                ["M_Tavern_Window"] = Glow("M_Tavern_Window", new Color(0.12f, 0.16f, 0.34f), new Color(0.16f, 0.22f, 0.5f) * 0.9f),
                ["M_Tavern_Dark"] = Lit("M_Tavern_Dark", null, new Color(0.02f, 0.02f, 0.025f), 0f),
            };
            return mats;
        }

        private static Material Lit(string name, string texturePath, Color tint, float smoothness, string normalPath = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D tex = texturePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) : null;
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            Texture2D normal = normalPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            mat.SetTexture("_BumpMap", normal);
            if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Glow(string name, Color baseColor, Color emission)
        {
            Material mat = Lit(name, null, baseColor, 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureModel(Dictionary<string, Material> mats)
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
            {
                Debug.LogError("[TavernSceneBuilder] Model missing: " + ModelPath + " (run BlenderSources/Tavern_Interior_build.py)");
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true; // Blender z-up baked into the meshes: every piece keeps an upright, unrotated transform
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (KeyValuePair<string, Material> kv in mats)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            }
            importer.SaveAndReimport();
        }

        #endregion

        #region Scene

        /// <summary>Opens Zone_Tavern (made once as a copy of the Great Hall for its framework) and clears the rest.</summary>
        private static Scene OpenTavernScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                AssetDatabase.CopyAsset(FrameworkScenePath, ScenePath);
                AssetDatabase.Refresh();
            }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(FrameworkRoots, root.name) < 0) Object.DestroyImmediate(root);
            }
            return scene;
        }

        private static void BuildContents(Scene scene)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var room = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            room.name = "Tavern_Interior";
            room.transform.position = Vector3.zero;

            AddColliders(room.transform);
            Transform props = new GameObject("Tavern_Props").transform;
            SceneManager.MoveGameObjectToScene(props.gameObject, scene);
            PlaceProps(room.transform, props);
            PlaceLights(room.transform, scene);
            PlaceDoorAndSpawn(room.transform, scene);
            PlaceWaitress(room.transform, scene);
            SetupFramework(room.transform, scene);
        }

        private static void AddColliders(Transform room)
        {
            foreach (MeshRenderer mr in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                if (n == "Stairs_Ramp")
                {
                    // Walkable slope over the steps: collider only (the camera cuts the parent steps away)
                    var mc = mr.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(mr);
                    continue;
                }
                if (System.Array.IndexOf(NoColliderNames, n) >= 0) continue;
                if (StartsWithAny(n, MeshColliderPrefixes))
                {
                    var mc = mr.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;
                }
                else if (StartsWithAny(n, BoxColliderPrefixes))
                {
                    if (n.StartsWith("Post_"))
                    {
                        // just the post: its knee braces sit overhead and must not block the floor or the stairs top
                        Bounds b = mr.bounds;
                        var holder = new GameObject(n + "_Collider");
                        holder.transform.SetParent(mr.transform, false);
                        holder.transform.SetPositionAndRotation(b.center, Quaternion.identity);
                        var post = holder.AddComponent<BoxCollider>();
                        post.size = new Vector3(0.6f, b.size.y, 0.6f);
                    }
                    else
                    {
                        mr.gameObject.AddComponent<BoxCollider>(); // fits the mesh bounds
                    }
                }
                mr.shadowCastingMode = n.StartsWith("Window") || n == "Fire" || n == "Rug" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
        }

        private static bool StartsWithAny(string n, string[] prefixes)
        {
            foreach (string p in prefixes) if (n.StartsWith(p)) return true;
            return false;
        }

        private static void PlaceProps(Transform room, Transform parent)
        {
            Place(room, parent, "Spot_Barrel_", "Assets/PREFABS/Barrel.prefab", 1.45f, 37f);
            Place(room, parent, "Spot_Chest_", "Assets/PREFABS/Chest.prefab", 1.0f, 90f);
            Place(room, parent, "Spot_Crate_", "Assets/PREFABS/Box_1.prefab", 1.1f, 15f);
        }

        private static void Place(Transform room, Transform parent, string prefix, string prefabPath, float height, float yawStep)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;
            int i = 0;
            foreach (Transform spot in room.GetComponentsInChildren<Transform>(true))
            {
                if (!spot.name.StartsWith(prefix)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = prefix.Replace("Spot_", "").TrimEnd('_') + "_" + (++i);
                go.transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, yawStep * i, 0f));
                go.transform.localScale = Vector3.one;
                Bounds b = RendererBounds(go);
                if (b.size.y > 0.01f) go.transform.localScale = Vector3.one * (height / b.size.y);
                b = RendererBounds(go);
                go.transform.position += Vector3.up * (spot.position.y - b.min.y);
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var box = go.AddComponent<BoxCollider>();
                    Bounds wb = RendererBounds(go);
                    box.center = go.transform.InverseTransformPoint(wb.center);
                    Vector3 s = go.transform.lossyScale;
                    box.size = new Vector3(wb.size.x / s.x, wb.size.y / s.y, wb.size.z / s.z);
                }
            }
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void PlaceLights(Transform room, Scene scene)
        {
            var lights = new GameObject("Tavern_Lights");
            SceneManager.MoveGameObjectToScene(lights, scene);
            foreach (Transform mark in room.GetComponentsInChildren<Transform>(true))
            {
                if (!mark.name.StartsWith("Light_")) continue;
                var go = new GameObject(mark.name);
                go.transform.SetParent(lights.transform, false);
                go.transform.position = mark.position;
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                bool fire = mark.name == "Light_Fire";
                bool upper = mark.name.StartsWith("Light_Upper");
                l.color = fire ? FireLight : WarmLight;
                l.range = fire ? 15f : upper ? 12f : 11f;
                l.intensity = fire ? 6.5f : upper ? 4f : 4.4f;
                LightFlicker flicker = go.AddComponent<LightFlicker>();
                flicker.Amount = fire ? 0.22f : 0.08f;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.27f, 0.2f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.sun = null;
        }

        private static void PlaceDoorAndSpawn(Transform room, Scene scene)
        {
            Transform doorSpot = Find(room, "Spot_Door");
            Transform arrival = Find(room, "Spot_Arrival");

            // Group faces into the south wall; the door's front (-Z) looks into the room
            var group = new GameObject("Door_Tavern_Village");
            SceneManager.MoveGameObjectToScene(group, scene);
            group.transform.SetPositionAndRotation(doorSpot.position, Quaternion.Euler(0f, 180f, 0f));
            var anchor = new GameObject(ZoneExitSign.SignAnchorName);
            anchor.transform.SetParent(group.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 5.9f, -0.35f); // in front of the wall, over the door

            var trigger = new GameObject("Door_Tavern_Village_Trigger");
            trigger.transform.SetParent(group.transform, false);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var door = trigger.AddComponent<DoorTeleporter>();
            door.InitializeSceneTeleporter(TavernEntrance.VillageSceneName, TavernEntrance.SpawnInVillage, "Village",
                "Step out into Oakhaven", 4.5f, true);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefab);
            if (prefab != null)
            {
                CastleDoorSetup.PlaceDoor(group.transform, door, prefab, CastleDoorSetup.SingleDoorScale, 0.8f);
                Transform fill = group.transform.Find(CastleDoorSetup.WallFillName);
                if (fill != null) Object.DestroyImmediate(fill.gameObject);
            }

            var spawn = new GameObject(TavernEntrance.SpawnInTavern);
            SceneManager.MoveGameObjectToScene(spawn, scene);
            spawn.transform.SetPositionAndRotation(arrival.position + Vector3.up * 0.5f, Quaternion.identity);
            spawn.AddComponent<StartSpawnPoint>();
            var sc = spawn.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(2f, 2f, 2f);
        }

        private static void PlaceWaitress(Transform room, Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaitressPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[TavernSceneBuilder] Waitress prefab missing: " + WaitressPrefab);
                return;
            }

            var stops = new List<Transform>();
            for (int i = 1; ; i++)
            {
                Transform s = Find(room, "Spot_Waitress_" + i);
                if (s == null) break;
                stops.Add(s);
            }
            var tables = new List<Transform>();
            foreach (Transform s in stops) tables.Add(NearestTable(room, s.position));

            var elf = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            elf.name = "NPC_Elf_Waitress";
            Vector3 start = stops.Count > 0 ? stops[0].position : Vector3.zero;
            elf.transform.SetPositionAndRotation(start, Quaternion.identity);
            elf.layer = 8; // like other interactables: left out of the walking NavMesh, not cut away by the camera
            TavernWaitress w = elf.AddComponent<TavernWaitress>();
            w.Stops = stops.ToArray();
            w.Tables = tables.ToArray();
        }

        private static Transform NearestTable(Transform room, Vector3 p)
        {
            Transform best = null;
            float bestD = float.MaxValue;
            foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Table_") || t.GetComponent<Renderer>() == null) continue;
                Vector3 c = t.GetComponent<Renderer>().bounds.center;
                if (Mathf.Abs(c.y - p.y) > 2.5f) continue;
                float d = (new Vector3(c.x - p.x, 0f, c.z - p.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private static void SetupFramework(Transform room, Scene scene)
        {
            Transform arrival = Find(room, "Spot_Arrival");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "PlayerHero")
                {
                    root.transform.SetPositionAndRotation(arrival.position, Quaternion.identity);
                }
                else if (root.name == "Main Camera")
                {
                    Camera cam = root.GetComponent<Camera>();
                    if (cam != null)
                    {
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = new Color(0.035f, 0.03f, 0.035f);
                    }
                    root.transform.position = arrival.position + new Vector3(-6f, 12f, -12f);
                }
            }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes) if (s.path == ScenePath) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion
    }
}
