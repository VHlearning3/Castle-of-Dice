using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Treasure Tower (Vili, 2026-10-07):
    /// <list type="bullet">
    /// <item>The south gate back to the Castle Hall is the top end of the spiral stairs that climb from the hall
    /// (BlenderSources/TowerMirror_build.py → Tower_Stairs_Top.fbx): a round arch, a landing and steps winding
    /// down inside the stair turret. Stepping onto the landing takes the hero down through the gate's
    /// DoorTeleporter.</item>
    /// <item>A hidden room behind the north wall with a great mirror (<see cref="PlanarMirror"/>). It opens when
    /// the Tower challenge is complete (<see cref="TowerMirrorRoom"/>): the gold heaped against the wall fades
    /// and the wall sinks into the floor.</item>
    /// </list>
    /// Edits only the gate group and the Mirror_Room root in Zone_6_Tower; running it again rebuilds them.
    /// </summary>
    public static class TowerMirrorSetup
    {
        public const string ScenePath = "Assets/Scenes/Zone_6_Tower.unity";
        public const string GateName = "Door_Tower_CastleHall";
        public const string RoomRootName = "Mirror_Room";
        public const string StairsModelPath = CastleDoorSetup.ModelFolder + "/Tower_Stairs_Top.fbx";
        public const string StairsPrefabPath = CastleDoorSetup.PrefabFolder + "/Tower_Stairs_Top.prefab";
        public const string MirrorModelPath = "Assets/Models/Tower/Tower_Mirror.fbx";
        public const string DressingModelPath = "Assets/Models/Tower/Mirror_Room_Dressing.fbx";
        public const string PrefabFolder = "Assets/PREFABS/Tower";
        public const string MirrorPrefabPath = PrefabFolder + "/Tower_Mirror.prefab";
        public const string DressingPrefabPath = PrefabFolder + "/Mirror_Room_Dressing.prefab";
        public const string MaterialFolder = "Assets/Materials/Tower";
        public const string StoneMaterialPath = "Assets/Materials/Walls/M_Tower_Stone.mat";
        public const string FloorMaterialPath = "Assets/Materials/Zones/M_Zone_Floor_Tower.mat";
        public const string MeshFolder = "Assets/Models/Walls/Tower";

        // The hidden room, in world space: inner x -13..13, z 16..34, walls 8 m like the tower's
        public const float RoomHalfWidth = 13f;
        public const float RoomSouth = 16f;
        public const float RoomNorth = 34f;
        public const float WallHeight = 8f;
        public const float WallThickness = 1.2f;
        public const float TileMetres = HallWallsSetup.TileMetres;

        [MenuItem("CastleOfDice/Tower Stairs + Mirror Room", false, 17)]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
        }

        public static void BatchSetup()
        {
            SetupAll();
            EditorApplication.Exit(0);
        }

        public static void SetupAll()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            Material stone = BuildStoneMaterial();
            BuildStairsPrefab(stone);
            BuildMirrorPrefab();
            BuildDressingPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform gate = FindRoot(scene, GateName);
            if (gate == null)
            {
                Debug.LogError("[TowerMirrorSetup] The Tower's gate group is missing.");
                return;
            }
            PlaceStairs(gate, stone);
            BuildRoom(scene, stone);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[TowerMirrorSetup] Tower: stairs top in the south gate, mirror room behind the north wall.");
        }

        #region Materials

        /// <summary>The hall's even cut stone, darkened toward the tower's brown.</summary>
        private static Material BuildStoneMaterial()
        {
            Material hall = HallWallsSetup.BuildMaterialIfMissing();
            Material m = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
            if (m == null)
            {
                m = new Material(hall);
                AssetDatabase.CreateAsset(m, StoneMaterialPath);
            }
            m.CopyPropertiesFromMaterial(hall);
            var tint = new Color(0.66f, 0.56f, 0.47f);
            m.SetColor("_BaseColor", tint);
            m.color = tint;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material GlassMaterial()
        {
            string path = MaterialFolder + "/M_Mirror_Glass.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("CastleOfDice/PlanarMirror");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material DoorMat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(CastleDoorSetup.MaterialFolder + "/" + name + ".mat");
        }

        #endregion

        #region Prefabs

        private static void BuildStairsPrefab(Material stone)
        {
            Import(StairsModelPath, new Dictionary<string, Material>
            {
                { "Tower_Wall", stone },
                { "Stair_Tower", stone },
                { "Door_Stone", DoorMat("M_Door_Stone") },
                { "Door_StoneTrim", DoorMat("M_Door_StoneTrim") },
                { "Door_Iron", DoorMat("M_Door_Iron") },
                { "Door_Passage", DoorMat("M_Door_Passage") },
                { "Stair_Stone", DoorMat("M_Stair_Stone") },
                { "Stair_Roof", DoorMat("M_Stair_Roof") },
            });

            GameObject root = WrapModel(StairsModelPath, "Tower_Stairs_Top", "Room_Side", Vector3.back);
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.EndsWith("_Facade") || mf.name.EndsWith("_Turret"))
                {
                    mf.gameObject.layer = 7; // Obstacle
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
            }
            // Torch-warm light down in the turret, so the steps read as they wind into the dark
            AddLight(root.transform, "Stair_Light", new Vector3(0.4f, 1.2f, 3.2f), new Color(1f, 0.64f, 0.32f), 7f, 2.2f);
            PrefabUtility.SaveAsPrefabAsset(root, StairsPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildMirrorPrefab()
        {
            Import(MirrorModelPath, new Dictionary<string, Material>
            {
                { "Door_Stone", DoorMat("M_Door_Stone") },
                { "Door_StoneTrim", DoorMat("M_Door_StoneTrim") },
                { "Mirror_Gold", Lit("M_Mirror_Gold", new Color(0.93f, 0.68f, 0.24f), 0.62f, 0.85f) },
                { "Mirror_GoldDark", Lit("M_Mirror_GoldDark", new Color(0.45f, 0.29f, 0.09f), 0.45f, 0.8f) },
                { "Mirror_Gem", Lit("M_Mirror_Gem", new Color(0.5f, 0.24f, 0.9f), 0.8f, 0.1f, new Color(0.25f, 0.08f, 0.5f)) },
                { "Mirror_Glass", GlassMaterial() },
            });

            GameObject root = WrapModel(MirrorModelPath, "Tower_Mirror", "Front", Vector3.back);
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name == "Mirror_Frame")
                {
                    mf.gameObject.layer = 7;
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                else if (mf.name == "Mirror_Glass")
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    var mirror = mf.gameObject.AddComponent<PlanarMirror>();
                    // The glass faces the room: the prefab's -Z
                    mirror.LocalNormal = mf.transform.InverseTransformDirection(Vector3.back).normalized;
                }
            }
            // A cool light before the glass, so the gold frame and the room's reflection both read
            AddLight(root.transform, "Mirror_Light", new Vector3(0f, 4.2f, -4.5f), new Color(0.72f, 0.78f, 1f), 11f, 1.6f);
            PrefabUtility.SaveAsPrefabAsset(root, MirrorPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildDressingPrefab()
        {
            Import(DressingModelPath, new Dictionary<string, Material>
            {
                { "Door_Stone", DoorMat("M_Door_Stone") },
                { "Door_StoneTrim", DoorMat("M_Door_StoneTrim") },
                { "Door_Iron", DoorMat("M_Door_Iron") },
                { "Mirror_Carpet", Lit("M_Mirror_Carpet", new Color(0.42f, 0.07f, 0.09f), 0.05f, 0f) },
                { "Mirror_CarpetHem", Lit("M_Mirror_CarpetHem", new Color(0.78f, 0.56f, 0.2f), 0.2f, 0.3f) },
                { "Mirror_Flame", Lit("M_Mirror_Flame", new Color(1f, 0.6f, 0.2f), 0f, 0f, new Color(2.2f, 1.1f, 0.3f)) },
            });

            GameObject root = WrapModel(DressingModelPath, "Mirror_Room_Dressing", "North", Vector3.forward);
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name == "Room_Stone")
                {
                    mf.gameObject.layer = 7;
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                else if (mf.name == "Room_Flames")
                {
                    mf.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            foreach (string mark in new[] { "Brazier_L", "Brazier_R" })
            {
                Transform t = Find(root.transform, mark);
                if (t == null) continue;
                Light l = AddLight(root.transform, mark + "_Light", root.transform.InverseTransformPoint(t.position), new Color(1f, 0.62f, 0.3f), 10f, 2.2f);
                l.gameObject.AddComponent<ZoneAmbientMotion>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, DressingPrefabPath);
            Object.DestroyImmediate(root);
        }

        #endregion

        #region Scene

        private static void PlaceStairs(Transform gate, Material stone)
        {
            foreach (string old in new[] { CastleDoorSetup.DoorModelName, HallDoorsSetup.StairsModelName })
            {
                Transform t;
                while ((t = gate.Find(old)) != null) Object.DestroyImmediate(t.gameObject);
            }

            var stairs = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StairsPrefabPath), gate);
            stairs.name = HallDoorsSetup.StairsModelName;
            stairs.transform.localPosition = Vector3.zero;
            stairs.transform.localRotation = Quaternion.identity;
            stairs.transform.localScale = Vector3.one;

            // The trigger fills the arch's passage, just short of the landing
            DoorTeleporter door = gate.GetComponentInChildren<DoorTeleporter>(true);
            if (door != null)
            {
                door.transform.localPosition = new Vector3(0f, 1.5f, 1.3f);
                var box = door.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = Vector3.zero;
                    box.size = new Vector3(3.2f, 3f, 2.4f);
                    EditorUtility.SetDirty(box);
                }
                EditorUtility.SetDirty(door.transform);
            }

            // The wall pieces either side of the arch take the same even stone, with world-space UVs
            Transform fill = gate.Find(CastleDoorSetup.WallFillName);
            if (fill != null)
            {
                foreach (MeshRenderer r in fill.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    Bounds b = r.bounds;
                    mf.sharedMesh = BoxMesh(GateName + "_" + r.name, b.center, b.size, r.transform, false);
                    r.sharedMaterial = stone;
                }
            }
        }

        private static void BuildRoom(Scene scene, Material stone)
        {
            Transform old = FindRoot(scene, RoomRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var rootGo = new GameObject(RoomRootName);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            var room = new GameObject("Room");
            room.transform.SetParent(rootGo.transform, false);

            float x0 = -RoomHalfWidth - WallThickness, x1 = RoomHalfWidth + WallThickness;
            float zS = RoomSouth - 1f, zN = RoomNorth + WallThickness;
            // Floor: the tower's tiles, carried on at the same size and in step with the tower floor
            Material floor = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            Wall(room.transform, "Room_Floor", new Vector3(0f, -0.5f, (zS + zN) * 0.5f), new Vector3(x1 - x0, 1f, zN - zS), floor, true);
            Wall(room.transform, "Room_Wall_West", new Vector3((x0 - RoomHalfWidth) * 0.5f, WallHeight * 0.5f, (zS + zN) * 0.5f), new Vector3(WallThickness, WallHeight, zN - zS), stone, false);
            Wall(room.transform, "Room_Wall_East", new Vector3((x1 + RoomHalfWidth) * 0.5f, WallHeight * 0.5f, (zS + zN) * 0.5f), new Vector3(WallThickness, WallHeight, zN - zS), stone, false);
            Wall(room.transform, "Room_Wall_North", new Vector3(0f, WallHeight * 0.5f, (RoomNorth + zN) * 0.5f), new Vector3(x1 - x0, WallHeight, WallThickness), stone, false);
            // South: the tower's north wall (x -8..8) is the doorway; these close the sides beyond it
            Wall(room.transform, "Room_Wall_South_L", new Vector3(-(8f + RoomHalfWidth) * 0.5f, WallHeight * 0.5f, (zS + RoomSouth) * 0.5f), new Vector3(RoomHalfWidth - 8f, WallHeight, RoomSouth - zS), stone, false);
            Wall(room.transform, "Room_Wall_South_R", new Vector3((8f + RoomHalfWidth) * 0.5f, WallHeight * 0.5f, (zS + RoomSouth) * 0.5f), new Vector3(RoomHalfWidth - 8f, WallHeight, RoomSouth - zS), stone, false);

            var mirror = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MirrorPrefabPath), room.transform);
            mirror.name = "Great_Mirror";
            mirror.transform.position = new Vector3(0f, 0f, RoomNorth);
            var dressing = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DressingPrefabPath), room.transform);
            dressing.name = "Dressing";
            dressing.transform.position = new Vector3(0f, 0f, 15f);

            // Soft fill so the far corners are not black
            AddLight(room.transform, "Room_Fill_Light", new Vector3(0f, 6.5f, 24f), new Color(0.8f, 0.72f, 0.95f), 18f, 0.9f);

            var opener = rootGo.AddComponent<TowerMirrorRoom>();
            opener.Room = room;
            Transform north = FindRoot(scene, "Walls")?.Find("Tower_Wall_North");
            opener.BlockingWall = north;
            opener.GoldHoard = new[] { FindDeep(scene, "Gold_Piles"), FindDeep(scene, "Gold_Glow") };
            room.SetActive(false);
        }

        private static GameObject Wall(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool floor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(name, center, size, go.transform, floor);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>
        /// A box mesh in <paramref name="space"/>'s local coordinates with UVs from world position: walls take
        /// (along the face, height) at one texture repeat per <see cref="TileMetres"/>, tops take (x, z). For the
        /// floor the top takes the tower floor's own mapping (30 m across, the material repeats it), so its tiles
        /// run on from the tower's.
        /// </summary>
        private static Mesh BoxMesh(string name, Vector3 center, Vector3 size, Transform space, bool floor)
        {
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in dirs)
            {
                Vector3 a = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 u = Vector3.Cross(n, a).normalized;
                Vector3 v = Vector3.Cross(n, u).normalized;
                int start = verts.Count;
                Vector3[] corners =
                {
                    n - u - v, n + u - v, n + u + v, n - u + v,
                };
                foreach (Vector3 c in corners)
                {
                    Vector3 world = center + Vector3.Scale(c, h);
                    verts.Add(space.InverseTransformPoint(world));
                    norms.Add(space.InverseTransformDirection(n));
                    Vector2 uv;
                    if (Mathf.Abs(n.y) > 0.5f)
                    {
                        uv = floor ? new Vector2((world.x + 15f) / 30f, (world.z + 15f) / 30f) : new Vector2(world.x, world.z) / TileMetres;
                    }
                    else
                    {
                        Vector3 along = Vector3.Cross(Vector3.up, n);
                        uv = new Vector2(Vector3.Dot(world, along), world.y) / TileMetres;
                    }
                    uvs.Add(uv);
                }
                // Wind so the face points along n (Unity: cross(b - a, c - a) is the front normal)
                Vector3 e1 = corners[1] - corners[0], e2 = corners[2] - corners[0];
                if (Vector3.Dot(Vector3.Cross(e1, e2), n) > 0f)
                {
                    tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
                else
                {
                    tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            string path = MeshFolder + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        #endregion

        #region Helpers

        private static void Import(string path, Dictionary<string, Material> mats)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[TowerMirrorSetup] No model at {path}.");
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in mats)
            {
                if (pair.Value != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
            importer.SaveAndReimport();
        }

        /// <summary>Wraps a model in a root and turns it so the named marker lies along <paramref name="dir"/>.</summary>
        private static GameObject WrapModel(string modelPath, string name, string marker, Vector3 dir)
        {
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            Transform m = Find(inst.transform, marker);
            if (m != null)
            {
                Vector3 to = m.position - root.transform.position;
                to.y = 0f;
                float yaw = Mathf.Round(Vector3.SignedAngle(to, dir, Vector3.up) / 90f) * 90f;
                inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            return root;
        }

        private static Light AddLight(Transform parent, string name, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == name) return r.transform;
            }
            return null;
        }

        private static GameObject FindDeep(Scene scene, string name)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                Transform t = Find(r.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        #endregion
    }
}
