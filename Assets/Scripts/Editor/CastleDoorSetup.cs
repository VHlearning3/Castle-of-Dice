using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// The wooden castle doors modelled in Blender (BlenderSources/CastleDoors.blend, CastleDoors_build.py):
    /// a double door and a single door in a pale stone arch, each leaf hinged at its outer edge.
    /// <list type="bullet">
    /// <item>Imports both FBX files with project URP materials and saves Door_Double / Door_Single prefabs
    /// (SwingDoor, stone blockers, PillarTop marks for the exit braziers).</item>
    /// <item>Swaps the old arch and pillars of every zone-changing gate in the castle zones (Courtyard,
    /// Library, Castle Hall, Tower, Throne Room) for the double door. The DoorTeleporter, its trigger and
    /// the runtime "To &lt;zone&gt;" sign stay.</item>
    /// <item>Gives the Library its secret side door (single door) to the Hidden Trail, with its own spawn.</item>
    /// </list>
    /// Running it again rebuilds only the door models it placed.
    /// </summary>
    public static class CastleDoorSetup
    {
        public const string ModelFolder = "Assets/Models/Doors";
        public const string DoubleModelPath = ModelFolder + "/Door_Double.fbx";
        public const string SingleModelPath = ModelFolder + "/Door_Single.fbx";
        public const string MaterialFolder = "Assets/Materials/Doors";
        public const string PrefabFolder = "Assets/PREFABS/Doors";
        public const string DoublePrefabPath = PrefabFolder + "/Door_Double.prefab";
        public const string SinglePrefabPath = PrefabFolder + "/Door_Single.prefab";

        /// <summary>Child of a gate group holding the placed door prefab.</summary>
        public const string DoorModelName = "Door_Model";

        /// <summary>Wall pieces either side of the frame that close the gap to the zone's wall.</summary>
        public const string WallFillName = "Door_Wall_Fill";

        /// <summary>Library: the secret side door to the Hidden Trail.</summary>
        public const string LibrarySecretDoorName = "Door_Library_SecretSide";

        /// <summary>Half the opening width of each model in Blender metres (hinge to hinge).</summary>
        private const float DoubleHalfOpening = 1.5f;
        private const float SingleHalfOpening = 0.8f;

        /// <summary>Scale of the double door in the castle gates: a 4.8 m opening, about 7 m to the arch's peak.</summary>
        public const float CastleDoorScale = 1.6f;

        /// <summary>Scale of the single door (Library side door, Hidden Trail end): a 2.2 m opening.</summary>
        public const float SingleDoorScale = 1.4f;

        public static readonly string[] CastleScenes =
        {
            "Zone_3_CastleCourtyard", "Zone_4_Library", "Zone_5_CastleHall", "Zone_6_Tower", "Zone_7_ThroneRoom"
        };

        [MenuItem("CastleOfDice/Castle Doors (Double + Single)", false, 15)]
        public static void SetupMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
        }

        /// <summary>Batch-mode entry: prefabs, castle gates and the Library side door.</summary>
        public static void BatchSetup()
        {
            SetupAll();
        }

        public static void SetupAll()
        {
            BuildPrefabs();
            foreach (string scene in CastleScenes)
            {
                ApplyToScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CastleDoorSetup] Castle doors placed in zones 3-7.");
        }

        #region Models, materials and prefabs

        public static void BuildPrefabs()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            var mats = new Dictionary<string, Material>
            {
                { "Door_Stone", Lit("M_Door_Stone", new Color(0.80f, 0.77f, 0.70f), 0.12f) },
                { "Door_StoneTrim", Lit("M_Door_StoneTrim", new Color(0.68f, 0.65f, 0.58f), 0.12f) },
                { "Door_Wood", Lit("M_Door_Wood", new Color(0.50f, 0.29f, 0.14f), 0.22f) },
                { "Door_WoodDark", Lit("M_Door_WoodDark", new Color(0.20f, 0.11f, 0.05f), 0.1f) },
                { "Door_Iron", Lit("M_Door_Iron", new Color(0.16f, 0.16f, 0.17f), 0.45f, 0.6f) },
                { "Door_Passage", Lit("M_Door_Passage", new Color(0.03f, 0.025f, 0.025f), 0f, 0f, true) },
            };
            ImportModel(DoubleModelPath, mats);
            ImportModel(SingleModelPath, mats);
            BuildPrefab(DoubleModelPath, DoublePrefabPath, "Door_Double", DoubleHalfOpening);
            BuildPrefab(SingleModelPath, SinglePrefabPath, "Door_Single", SingleHalfOpening);
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic = 0f, bool twoSided = false)
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
            if (twoSided)
            {
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.doubleSidedGI = true;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void ImportModel(string path, Dictionary<string, Material> mats)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[CastleDoorSetup] No model at {path}.");
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
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Wraps the model in a prefab whose +Z points through the door (toward the passage) and whose
        /// front faces -Z, adds the SwingDoor, the stone blockers either side of the opening and the
        /// PillarTop marks where the zone-exit braziers stand.
        /// </summary>
        private static void BuildPrefab(string modelPath, string prefabPath, string name, float halfOpening)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return;

            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);

            Transform frame = null, passage = null;
            var leaves = new List<Transform>();
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("_Frame")) frame = t;
                else if (t.name.EndsWith("_Passage")) passage = t;
                else if (t.name.Contains("_Leaf")) leaves.Add(t);
            }
            if (frame == null || passage == null || leaves.Count == 0)
            {
                Debug.LogError($"[CastleDoorSetup] {modelPath} is missing its frame, passage or leaves.");
                Object.DestroyImmediate(root);
                return;
            }

            // Turn the model so the passage lies behind the door (+Z)
            Vector3 toPassage = passage.GetComponent<Renderer>().bounds.center - root.transform.position;
            toPassage.y = 0f;
            float yaw = Mathf.Round(Vector3.SignedAngle(toPassage, Vector3.forward, Vector3.up) / 90f) * 90f;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Renderer passageRenderer = passage.GetComponent<Renderer>();
            passageRenderer.shadowCastingMode = ShadowCastingMode.Off;
            passageRenderer.receiveShadows = false;

            var door = root.AddComponent<SwingDoor>();
            door.Configure(leaves.ToArray(), passage);

            // Stone either side of the opening blocks the hero; the opening itself stays free
            Bounds fb = LocalBounds(root.transform, frame.GetComponent<MeshFilter>());
            var blockers = new GameObject("Frame_Blockers");
            blockers.layer = 7; // Obstacle
            blockers.transform.SetParent(root.transform, false);
            float depth = Mathf.Min(fb.size.z, 1.1f);
            for (int side = -1; side <= 1; side += 2)
            {
                float inner = halfOpening, outer = side < 0 ? -fb.min.x : fb.max.x;
                var box = blockers.AddComponent<BoxCollider>();
                box.center = new Vector3(side * (inner + outer) * 0.5f, fb.max.y * 0.5f, 0f);
                box.size = new Vector3(outer - inner, fb.max.y, depth);
            }
            var lintel = blockers.AddComponent<BoxCollider>();
            float archTop = fb.max.y * 0.62f;
            lintel.center = new Vector3(0f, (archTop + fb.max.y) * 0.5f, 0f);
            lintel.size = new Vector3(halfOpening * 2f, fb.max.y - archTop, depth);

            // Pillar tops: the highest vertices of the frame on each side
            Mesh mesh = frame.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] verts = mesh.vertices;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * frame.localToWorldMatrix;
            float top = float.MinValue;
            foreach (Vector3 v in verts) top = Mathf.Max(top, toRoot.MultiplyPoint3x4(v).y);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (Vector3 v in verts)
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(v);
                    if (p.y > top - 0.02f && Mathf.Sign(p.x) == side) { sum += p; n++; }
                }
                if (n == 0) continue;
                var mark = new GameObject(ZoneExitSign.PillarTopPrefix + (side < 0 ? "_L" : "_R"));
                mark.transform.SetParent(root.transform, false);
                mark.transform.localPosition = sum / n;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[CastleDoorSetup] Saved {prefabPath} (frame {fb.size}, {leaves.Count} leaves, model yaw {yaw}).");
        }

        private static Bounds LocalBounds(Transform root, MeshFilter mf)
        {
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds b = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(m.MultiplyPoint3x4(c));
            }
            return b;
        }

        #endregion

        #region Castle gates

        private static void ApplyToScene(string sceneName)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            GameObject doublePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoublePrefabPath);
            int count = 0;
            foreach (DoorTeleporter door in Object.FindObjectsByType<DoorTeleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(door.TargetSceneName)) continue;
                Transform group = door.transform.parent;
                if (group == null || !group.name.StartsWith("Door_")) continue;

                // The Library's old secret passage back to the Forest becomes the side door to the Hidden Trail
                if (sceneName == "Zone_4_Library" && (group.name == "Door_Library_SecretPath" || group.name == LibrarySecretDoorName))
                {
                    continue;
                }

                // The Castle Hall's Tower gate is the foot of a spiral staircase (HallDoorsSetup), not a door
                if (group.Find(HallDoorsSetup.StairsModelName) != null) continue;

                PlaceDoor(group, door, doublePrefab, CastleDoorScale, DoubleHalfOpening);
                count++;
            }

            if (sceneName == "Zone_4_Library")
            {
                SetupLibrarySideDoor(scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CastleDoorSetup] {sceneName}: {count} gates now carry the double door.");
        }

        /// <summary>Replaces a gate group's arch and pillars with a door prefab and fits the trigger to the opening.</summary>
        public static GameObject PlaceDoor(Transform group, DoorTeleporter door, GameObject prefab, float scale, float halfOpening)
        {
            Material wallMat = FindWallMaterial(group);
            foreach (string old in new[] { "Arch_Model", "DecoPillar_L", "DecoPillar_R", DoorModelName, WallFillName })
            {
                Transform t;
                while ((t = group.Find(old)) != null) Object.DestroyImmediate(t.gameObject);
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            model.name = DoorModelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;

            // Fill the rest of the wall gap either side of the frame so no daylight shows past the pillars
            Bounds fb = LocalBoundsOfRenderers(group, model.transform);
            if (wallMat != null)
            {
                var fill = new GameObject(WallFillName);
                fill.transform.SetParent(group, false);
                fill.layer = 7;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = side < 0 ? "Fill_L" : "Fill_R";
                    cube.layer = 7;
                    cube.transform.SetParent(fill.transform, false);
                    float inner = side < 0 ? -fb.min.x : fb.max.x;
                    const float width = 4f, height = 8f;
                    cube.transform.localPosition = new Vector3(side * (inner + width * 0.5f - 0.15f), height * 0.5f, 0.35f);
                    cube.transform.localScale = new Vector3(width, height, 2.1f);
                    cube.GetComponent<Renderer>().sharedMaterial = wallMat;
                }
            }

            if (door != null)
            {
                door.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var box = door.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = Vector3.zero;
                    box.size = new Vector3(halfOpening * 2f * scale + 0.4f, 4f, 3f);
                    EditorUtility.SetDirty(box);
                }
            }
            return model;
        }

        private static Bounds LocalBoundsOfRenderers(Transform space, Transform root)
        {
            bool found = false;
            Bounds b = default;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.EndsWith("_Passage")) continue;
                Bounds lb = LocalBounds(space, mf);
                if (!found) { b = lb; found = true; }
                else b.Encapsulate(lb);
            }
            return b;
        }

        /// <summary>The zone's wall material: the renderer under a "Walls" root closest to the gate.</summary>
        private static Material FindWallMaterial(Transform group)
        {
            GameObject walls = null;
            foreach (GameObject r in group.gameObject.scene.GetRootGameObjects())
            {
                if (r.name == "Walls") { walls = r; break; }
            }
            if (walls == null) return null;
            Material best = null;
            float bestDist = float.MaxValue;
            foreach (Renderer r in walls.GetComponentsInChildren<Renderer>())
            {
                float d = (r.bounds.ClosestPoint(group.position) - group.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = r.sharedMaterial; }
            }
            return best;
        }

        #endregion

        #region Library secret side door

        /// <summary>
        /// The Library's old secret passage (west wall) gives way to a single wooden side door, slightly off
        /// centre, leading back out to the Hidden Trail. The hero arrives in front of it at its own spawn.
        /// </summary>
        private static void SetupLibrarySideDoor(Scene scene)
        {
            Vector3 doorPos = new Vector3(-44.5f, 0f, 9f);
            Quaternion doorRot = Quaternion.Euler(0f, 270f, 0f);

            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == "Door_Library_SecretPath" || r.name == LibrarySecretDoorName
                    || r.name == "Spawn_From_SecretPath" || r.name == SecretForestPath.LibrarySpawnFromTrail)
                {
                    Object.DestroyImmediate(r);
                }
            }

            var group = new GameObject(LibrarySecretDoorName);
            SceneManager.MoveGameObjectToScene(group, scene);
            group.transform.SetPositionAndRotation(doorPos, doorRot);
            var trigger = new GameObject(LibrarySecretDoorName + "_Trigger");
            trigger.transform.SetParent(group.transform, false);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var door = trigger.AddComponent<DoorTeleporter>();
            door.InitializeSceneTeleporter(SecretForestPath.SceneName, SecretForestPath.SpawnFromLibrary, "Forest",
                "Slip out through the secret side door", 4.5f, true);

            GameObject singlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SinglePrefabPath);
            PlaceDoor(group.transform, door, singlePrefab, SingleDoorScale, SingleHalfOpening);
            // The side door sits in the wall, not in a gap: no fill pieces
            Transform fill = group.transform.Find(WallFillName);
            if (fill != null) Object.DestroyImmediate(fill.gameObject);

            CutWallForSideDoor(scene, doorPos);

            var spawn = new GameObject(SecretForestPath.LibrarySpawnFromTrail);
            SceneManager.MoveGameObjectToScene(spawn, scene);
            spawn.transform.SetPositionAndRotation(doorPos + doorRot * new Vector3(0f, 0.5f, -5.5f), Quaternion.Euler(0f, 90f, 0f));
            spawn.AddComponent<StartSpawnPoint>();
            var sc = spawn.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(2f, 2f, 2f);
        }

        /// <summary>
        /// Opens a doorway in the west wall segment behind the side door: the solid wall is split into a
        /// piece either side and one above the frame's opening, so the dark passage shows through.
        /// </summary>
        private static void CutWallForSideDoor(Scene scene, Vector3 doorPos)
        {
            GameObject walls = null;
            foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == "Walls") walls = r;
            if (walls == null) return;

            Transform wall = null;
            foreach (Transform t in walls.transform)
            {
                Renderer r = t.GetComponent<Renderer>();
                if (r == null) continue;
                Bounds b = r.bounds;
                if (doorPos.x >= b.min.x - 0.1f && doorPos.x <= b.max.x + 0.1f && doorPos.z > b.min.z && doorPos.z < b.max.z)
                {
                    wall = t;
                    break;
                }
            }
            if (wall == null || wall.name.EndsWith("_Cut")) return;

            Bounds wb = wall.GetComponent<Renderer>().bounds;
            Material mat = wall.GetComponent<Renderer>().sharedMaterial;
            const float halfGap = 1.2f, gapTop = 4.9f;
            var parts = new List<(string, Vector3, Vector3)>
            {
                ("_Cut_S", new Vector3(wb.center.x, wb.center.y, (wb.min.z + doorPos.z - halfGap) * 0.5f), new Vector3(wb.size.x, wb.size.y, doorPos.z - halfGap - wb.min.z)),
                ("_Cut_N", new Vector3(wb.center.x, wb.center.y, (wb.max.z + doorPos.z + halfGap) * 0.5f), new Vector3(wb.size.x, wb.size.y, wb.max.z - doorPos.z - halfGap)),
                ("_Cut_Top", new Vector3(wb.center.x, (gapTop + wb.max.y) * 0.5f, doorPos.z), new Vector3(wb.size.x, wb.max.y - gapTop, halfGap * 2f)),
            };
            foreach (var (suffix, center, size) in parts)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = wall.name + suffix;
                cube.layer = wall.gameObject.layer;
                cube.transform.SetParent(walls.transform, true);
                cube.transform.position = center;
                cube.transform.localScale = size;
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(cube, GameObjectUtility.GetStaticEditorFlags(wall.gameObject));
            }
            Object.DestroyImmediate(wall.gameObject);
        }

        #endregion

        #region Helpers

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        #endregion
    }
}
