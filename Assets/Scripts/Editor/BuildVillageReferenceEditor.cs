using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Rebuilds the above-ground part of Zone_1_VillageAndCellar after Vili's reference picture:
    /// stone hall with banners (NW), market hut (N), tavern with a patio (NE), a cobblestone ring road around
    /// crop beds and market stalls, the forge, well and cart (W), cottages around the ring, a dirt arrival road
    /// leaving to the SW with banner posts, the forest gate up the north path, and a ring of trees.
    /// Gameplay objects (NPCs, practice chest, save shrine, cellar hatch, spawns, gate trigger) are kept and moved;
    /// the cellar at y = -50 is not touched. Safe to run again: it replaces the previous Village_Reference root.
    /// </summary>
    public static class BuildVillageReferenceEditor
    {
        private const string ScenePath = "Assets/Scenes/Zone_1_VillageAndCellar.unity";
        private const string RootName = "Village_Reference";
        private const string PrefabDir = "Assets/PREFABS/Village";
        private const string MeshDir = "Assets/PREFABS/Village/Meshes";
        private const string MatDir = "Assets/Materials/Village";

        // Ring road (ellipse around the crop beds and market stalls)
        private const float RingRadiusX = 17f;
        private const float RingRadiusZ = 15f;
        private const float RingWidth = 4.2f;

        // Playable area: flat ground inside, invisible fence just beyond the tree line's inner edge
        private const float FlatRadius = 50f;
        private const float BoundaryRadius = 47f;

        private static readonly Vector3 GatePos = new Vector3(5f, 0f, 45.5f);

        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly List<Vector4> keepOut = new List<Vector4>(); // x, z, radius (tree/grass scatter avoids these)
        private static System.Random rng;

        [MenuItem("CastleOfDice/Rebuild Village (Reference Layout)", false, 11)]
        public static void BuildInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Rebuild Village", "Open " + ScenePath + " first.", "OK");
                return;
            }
            Build();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[BuildVillageReferenceEditor] Village rebuilt in the open scene (save to keep).");
        }

        /// <summary>Batch-mode entry: opens Zone 1, rebuilds the village and saves the scene.</summary>
        public static void BatchBuild()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildVillageReferenceEditor] Zone 1 village rebuilt and saved.");
        }

        /// <summary>Batch-mode entry: rebuild, save and take the review screenshots in one editor run.</summary>
        public static void BatchBuildAndScreenshots()
        {
            BatchBuild();
            BatchScreenshots();
        }

        public static void Build()
        {
            rng = new System.Random(1337);
            mats.Clear();
            prefabs.Clear();
            keepOut.Clear();

            EnsureFolder(PrefabDir);
            EnsureFolder(MeshDir);
            EnsureFolder(MatDir);
            CreateMaterials();
            CreatePropPrefabs();

            DetachGameplayObjects();
            RemoveOldVillage();

            GameObject root = new GameObject(RootName);
            BuildGround(root.transform);
            BuildRoads(root.transform);
            BuildStoneHall(root.transform);
            BuildMarketHut(root.transform);
            BuildTavern(root.transform);
            BuildWestYard(root.transform);
            BuildCottages(root.transform);
            BuildFields(root.transform);
            BuildMarket(root.transform);
            BuildNorthGate(root.transform);
            BuildLamps(root.transform);
            BuildTrees(root.transform);
            BuildGrass(root.transform);
            BuildBoundary(root.transform);
            PlaceGameplayObjects();
        }

        #region Old village cleanup

        private static readonly string[] GameplayNames =
        {
            "NPC_Othelia", "NPC_Mirabel", "NPC_Baldur", "NPC_Barnaby", "Village_Cat", "Village_Lockpick_Practice_Chest",
            "Rune_Save_Shrine", "Village_Cellar_Hatch", "Village_PlayerExitPoint", "StartSpawn", "PlayerHero", "Door_Village_Forest",
            "Spawn_From_Forest"
        };

        private static readonly string[] OldRootNames =
        {
            "Village_Layout", "Procedural_Village", "Walls", "CentralHall_Floor", "Tavern", "Forge", "Tree_1", "Tree_1 (1)",
            "street_light", "Stone_house_3d_model", RootName
        };

        /// <summary>
        /// Moves every gameplay object to the scene root so deleting the old layout containers keeps them.
        /// A root-level copy wins: a nested duplicate (e.g. a gate re-made by the old village builder) is deleted with its container.
        /// </summary>
        private static void DetachGameplayObjects()
        {
            foreach (string n in GameplayNames)
            {
                if (FindRoot(n) != null) continue;
                GameObject go = FindInScene(n);
                if (go != null) go.transform.SetParent(null, true);
            }
        }

        private static void RemoveOldVillage()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (string n in OldRootNames)
                {
                    if (go.name == n)
                    {
                        Object.DestroyImmediate(go);
                        break;
                    }
                }
            }
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        private static GameObject FindInScene(string name)
        {
            GameObject top = FindRoot(name);
            if (top != null) return top;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = FindDeep(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
                Transform d = FindDeep(c, name);
                if (d != null) return d;
            }
            return null;
        }

        #endregion

        #region Materials

        private static void CreateMaterials()
        {
            Mat("Grass", new Color(0.45f, 0.68f, 0.26f));
            Mat("GrassDark", new Color(0.33f, 0.55f, 0.20f));
            Mat("Dirt", new Color(0.66f, 0.50f, 0.32f));
            Mat("Soil", new Color(0.36f, 0.24f, 0.15f));
            Mat("Wood", new Color(0.52f, 0.34f, 0.19f));
            Mat("WoodDark", new Color(0.33f, 0.21f, 0.12f));
            Mat("Metal", new Color(0.45f, 0.47f, 0.50f), 0.45f, 0.6f);
            Mat("Gold", new Color(0.86f, 0.68f, 0.24f), 0.5f, 0.7f);
            Mat("BannerPurple", new Color(0.42f, 0.17f, 0.50f));
            Mat("StripeRed", new Color(0.78f, 0.17f, 0.15f));
            Mat("StripeWhite", new Color(0.93f, 0.90f, 0.82f));
            Mat("StripeBlue", new Color(0.23f, 0.40f, 0.70f));
            Mat("Canvas", new Color(0.86f, 0.80f, 0.66f));
            Mat("Wheat", new Color(0.93f, 0.76f, 0.30f));
            Mat("Hay", new Color(0.90f, 0.75f, 0.36f));
            Mat("Pumpkin", new Color(0.93f, 0.47f, 0.12f));
            Mat("LeafGreen", new Color(0.30f, 0.58f, 0.20f));
            Mat("Cabbage", new Color(0.55f, 0.76f, 0.35f));
            Mat("Apple", new Color(0.80f, 0.15f, 0.12f));
            Mat("Sack", new Color(0.80f, 0.70f, 0.52f));
        }

        private static Material Mat(string key, Color color, float smoothness = 0.12f, float metallic = 0f)
        {
            string path = MatDir + "/M_Village_" + key + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }

        #endregion

        #region Low-poly meshes

        private static Mesh lowSphere;
        private static Mesh lowCylinder;
        private static Mesh cube;

        private static void CreateMeshes()
        {
            lowSphere = SaveMesh(BuildLowPolySphere(7, 5), "LowPoly_Sphere");
            lowCylinder = SaveMesh(BuildLowPolyCylinder(8), "LowPoly_Cylinder");
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Faceted sphere of radius 0.5 (unshared vertices so every face is flat shaded).</summary>
        private static Mesh BuildLowPolySphere(int segments, int rings)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                float t0 = Mathf.PI * r / rings, t1 = Mathf.PI * (r + 1) / rings;
                for (int s = 0; s < segments; s++)
                {
                    float p0 = 2f * Mathf.PI * s / segments, p1 = 2f * Mathf.PI * (s + 1) / segments;
                    Vector3 a = SpherePoint(t0, p0), b = SpherePoint(t0, p1), c = SpherePoint(t1, p0), d = SpherePoint(t1, p1);
                    if (r > 0) AddTri(verts, tris, a, b, c);
                    if (r < rings - 1) AddTri(verts, tris, b, d, c);
                }
            }
            return FinishMesh(verts, tris);
        }

        private static Vector3 SpherePoint(float theta, float phi)
        {
            return new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
        }

        /// <summary>Faceted cylinder of radius 0.5 and height 1, centred on the origin, axis along Y.</summary>
        private static Mesh BuildLowPolyCylinder(int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 top = new Vector3(0, 0.5f, 0), bottom = new Vector3(0, -0.5f, 0);
            for (int s = 0; s < sides; s++)
            {
                float a0 = 2f * Mathf.PI * s / sides, a1 = 2f * Mathf.PI * (s + 1) / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                AddTri(verts, tris, top, p1 + top, p0 + top);
                AddTri(verts, tris, bottom, p0 + bottom, p1 + bottom);
                AddTri(verts, tris, p0 + bottom, p0 + top, p1 + top);
                AddTri(verts, tris, p0 + bottom, p1 + top, p1 + bottom);
            }
            return FinishMesh(verts, tris);
        }

        private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        private static Mesh FinishMesh(List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var uvs = new Vector2[verts.Count];
            for (int i = 0; i < verts.Count; i++) uvs[i] = new Vector2(verts[i].x * 0.1f, verts[i].z * 0.1f);
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        #endregion

        #region Primitive helpers

        private static GameObject Part(string name, Transform parent, Mesh mesh, string matKey, Vector3 localPos, Vector3 scale, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mats[matKey];
            return go;
        }

        private static GameObject Box(string name, Transform parent, string matKey, Vector3 localPos, Vector3 scale, Vector3 euler = default)
        {
            return Part(name, parent, cube, matKey, localPos, scale, euler);
        }

        private static GameObject Prefab(string name)
        {
            if (prefabs.TryGetValue(name, out GameObject p)) return p;
            p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/" + name + ".prefab");
            if (p == null) Debug.LogWarning("[BuildVillageReferenceEditor] Missing prefab " + name);
            prefabs[name] = p;
            return p;
        }

        /// <summary>Instantiates a project prefab (keeps its colliders).</summary>
        private static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            GameObject p = Prefab(prefab);
            if (p == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * p.transform.rotation;
            go.transform.localScale = p.transform.localScale * scale;
            return go;
        }

        /// <summary>Copies only the visible meshes of a prefab (no colliders): for walk-through grass, crops and road stones.</summary>
        private static GameObject PlaceVisual(string prefab, Transform parent, Vector3 pos, float yaw, Vector3 scale, string overrideMat = null)
        {
            GameObject p = Prefab(prefab);
            if (p == null) return null;
            var go = new GameObject(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            foreach (MeshRenderer mr in p.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject part = go;
                if (mr.transform != p.transform)
                {
                    part = new GameObject(mr.name);
                    part.transform.SetParent(go.transform, false);
                    part.transform.localPosition = p.transform.InverseTransformPoint(mr.transform.position);
                    part.transform.localRotation = Quaternion.Inverse(p.transform.rotation) * mr.transform.rotation;
                    part.transform.localScale = mr.transform.lossyScale;
                }
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = part.AddComponent<MeshRenderer>();
                if (overrideMat != null)
                {
                    var arr = new Material[mr.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mats[overrideMat];
                    r.sharedMaterials = arr;
                }
                else
                {
                    r.sharedMaterials = mr.sharedMaterials;
                }
            }
            return go;
        }

        private static GameObject PlaceVillage(string villagePrefab, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + villagePrefab + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Yaw that turns a prefab's front (its -Z side, where the doors are) towards a point.</summary>
        private static float FaceYaw(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
        }

        private static Vector3 Front(float yaw) { return Quaternion.Euler(0f, yaw, 0f) * Vector3.back; }
        private static Vector3 Right(float yaw) { return Quaternion.Euler(0f, yaw, 0f) * Vector3.left; }

        private static float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        private static void KeepOut(Vector3 p, float radius) { keepOut.Add(new Vector4(p.x, p.z, radius, 0f)); }

        private static bool IsFree(Vector3 p, float pad)
        {
            for (int i = 0; i < keepOut.Count; i++)
            {
                float dx = p.x - keepOut[i].x, dz = p.z - keepOut[i].y;
                float r = keepOut[i].z + pad;
                if (dx * dx + dz * dz < r * r) return false;
            }
            return true;
        }

        #endregion

        #region Prop prefabs

        private static void CreatePropPrefabs()
        {
            CreateMeshes();

            SavePrefab("Village_BannerPole", root =>
            {
                Part("Pole", root, lowCylinder, "WoodDark", new Vector3(0, 2.1f, 0), new Vector3(0.16f, 4.2f, 0.16f));
                Box("Crossbar", root, "WoodDark", new Vector3(0, 3.95f, 0.12f), new Vector3(1.2f, 0.1f, 0.1f));
                Box("Cloth", root, "BannerPurple", new Vector3(0, 3.0f, 0.14f), new Vector3(1.0f, 1.8f, 0.05f));
                Box("Cloth_Tip", root, "BannerPurple", new Vector3(0, 1.98f, 0.14f), new Vector3(0.7f, 0.7f, 0.05f), new Vector3(0, 0, 45f));
                Box("Trim", root, "Gold", new Vector3(0, 3.7f, 0.16f), new Vector3(1.02f, 0.1f, 0.06f));
                Part("Finial", root, lowSphere, "Gold", new Vector3(0, 4.3f, 0), Vector3.one * 0.24f);
                var col = root.gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 2.1f, 0); col.radius = 0.2f; col.height = 4.2f;
            });

            SavePrefab("Village_TableSet", root =>
            {
                Box("Table_Top", root, "Wood", new Vector3(0, 0.85f, 0), new Vector3(2.2f, 0.12f, 1.0f));
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0 ? -0.95f : 0.95f), sz = (i < 2 ? -0.38f : 0.38f);
                    Box("Table_Leg", root, "WoodDark", new Vector3(sx, 0.4f, sz), new Vector3(0.1f, 0.8f, 0.1f));
                }
                for (int b = -1; b <= 1; b += 2)
                {
                    Box("Bench_Seat", root, "Wood", new Vector3(0, 0.48f, b * 0.9f), new Vector3(2.2f, 0.09f, 0.38f));
                    Box("Bench_Leg", root, "WoodDark", new Vector3(-0.9f, 0.22f, b * 0.9f), new Vector3(0.1f, 0.44f, 0.32f));
                    Box("Bench_Leg", root, "WoodDark", new Vector3(0.9f, 0.22f, b * 0.9f), new Vector3(0.1f, 0.44f, 0.32f));
                }
                Part("Mug", root, lowCylinder, "Wood", new Vector3(0.5f, 1.0f, 0.1f), new Vector3(0.14f, 0.2f, 0.14f));
                Part("Mug", root, lowCylinder, "Wood", new Vector3(-0.4f, 1.0f, -0.15f), new Vector3(0.14f, 0.2f, 0.14f));
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.45f, 0); col.size = new Vector3(2.3f, 0.9f, 2.3f);
            });

            SavePrefab("Village_Bench", root =>
            {
                Box("Seat", root, "Wood", new Vector3(0, 0.48f, 0), new Vector3(1.8f, 0.09f, 0.42f));
                Box("Leg", root, "WoodDark", new Vector3(-0.75f, 0.22f, 0), new Vector3(0.1f, 0.44f, 0.36f));
                Box("Leg", root, "WoodDark", new Vector3(0.75f, 0.22f, 0), new Vector3(0.1f, 0.44f, 0.36f));
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.3f, 0); col.size = new Vector3(1.9f, 0.6f, 0.5f);
            });

            SavePrefab("Village_HayBale", root =>
            {
                Part("Bale", root, lowCylinder, "Hay", new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.3f, 1.1f), new Vector3(0, 0, 90f));
                Part("Band", root, lowCylinder, "Wood", new Vector3(0.3f, 0.55f, 0), new Vector3(1.13f, 0.08f, 1.13f), new Vector3(0, 0, 90f));
                Part("Band", root, lowCylinder, "Wood", new Vector3(-0.3f, 0.55f, 0), new Vector3(1.13f, 0.08f, 1.13f), new Vector3(0, 0, 90f));
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.55f, 0); col.size = new Vector3(1.3f, 1.1f, 1.1f);
            });

            SavePrefab("Village_Pumpkin", root =>
            {
                Part("Pumpkin", root, lowSphere, "Pumpkin", new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.42f, 0.6f));
                Box("Stem", root, "LeafGreen", new Vector3(0, 0.45f, 0), new Vector3(0.06f, 0.14f, 0.06f));
            });

            SavePrefab("Village_Cabbage", root =>
            {
                Part("Head", root, lowSphere, "Cabbage", new Vector3(0, 0.22f, 0), new Vector3(0.5f, 0.42f, 0.5f));
                Part("Leaf", root, lowSphere, "LeafGreen", new Vector3(0, 0.1f, 0), new Vector3(0.7f, 0.18f, 0.7f));
            });

            SavePrefab("Village_Sack", root =>
            {
                Part("Sack", root, lowSphere, "Sack", new Vector3(0, 0.32f, 0), new Vector3(0.55f, 0.65f, 0.5f));
                Part("Neck", root, lowCylinder, "Sack", new Vector3(0, 0.68f, 0), new Vector3(0.2f, 0.16f, 0.2f));
            });

            SavePrefab("Village_Axe", root =>
            {
                Box("Handle", root, "Wood", new Vector3(0, 0.05f, 0), new Vector3(0.07f, 0.07f, 0.95f));
                Box("Head", root, "Metal", new Vector3(0, 0.05f, 0.42f), new Vector3(0.05f, 0.24f, 0.18f));
            });

            SavePrefab("Village_StripedCanopy", root =>
            {
                // 3.4 m wide, 2.2 m deep, sloping down to the front (-Z)
                for (int i = 0; i < 7; i++)
                {
                    float x = -1.46f + i * 0.487f;
                    Box("Stripe", root, i % 2 == 0 ? "StripeRed" : "StripeWhite", new Vector3(x, 2.65f, -1.1f), new Vector3(0.49f, 0.06f, 2.3f), new Vector3(-14f, 0, 0));
                    Box("Valance", root, i % 2 == 0 ? "StripeRed" : "StripeWhite", new Vector3(x, 2.17f, -2.22f), new Vector3(0.49f, 0.35f, 0.05f));
                }
                Box("Post_L", root, "WoodDark", new Vector3(-1.6f, 1.1f, -2.15f), new Vector3(0.12f, 2.2f, 0.12f));
                Box("Post_R", root, "WoodDark", new Vector3(1.6f, 1.1f, -2.15f), new Vector3(0.12f, 2.2f, 0.12f));
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 1.1f, -2.15f); col.size = new Vector3(3.4f, 2.2f, 0.2f);
            });

            CreateStall("Village_MarketStall_Red", "StripeRed", "StripeWhite", 0);
            CreateStall("Village_MarketStall_Blue", "StripeBlue", "StripeWhite", 1);
            CreateStall("Village_MarketStall_Plain", "Canvas", "Canvas", 2);
            CreateStall("Village_MarketStall_Red2", "StripeRed", "StripeWhite", 3);

            SavePrefab("Village_Cart", root =>
            {
                NestPrefab("Trolley", root, new Vector3(0, 0.62f, 0), 0f);
                NestPrefab("Trolley_wheel", root, new Vector3(1.05f, 0.34f, 0.15f), 0f);
                NestPrefab("Trolley_wheel", root, new Vector3(-1.05f, 0.34f, 0.15f), 180f);
                Box("Shaft_L", root, "WoodDark", new Vector3(0.55f, 0.6f, 2.2f), new Vector3(0.09f, 0.09f, 2.0f), new Vector3(8f, 0, 0));
                Box("Shaft_R", root, "WoodDark", new Vector3(-0.55f, 0.6f, 2.2f), new Vector3(0.09f, 0.09f, 2.0f), new Vector3(8f, 0, 0));
                NestPrefab("Barrel", root, new Vector3(0.3f, 1.05f, -0.5f), 0f, 0.45f);
                NestPrefab("Box_1", root, new Vector3(-0.35f, 1.0f, 0.5f), 20f, 0.45f);
            });

            SavePrefab("Village_Woodpile", root =>
            {
                for (int i = 0; i < 4; i++) NestPrefab("Log_1", root, new Vector3(0, 0.3f, -0.9f + i * 0.6f), 90f);
                for (int i = 0; i < 3; i++) NestPrefab("Log_1", root, new Vector3(0, 0.82f, -0.6f + i * 0.6f), 90f);
                NestPrefab("Log_1", root, new Vector3(0, 1.32f, 0f), 90f);
                NestPrefab("Stump", root, new Vector3(1.9f, 0f, 0.4f), 30f, 0.6f);
                GameObject axe = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Village_Axe.prefab"), root) as GameObject;
                axe.transform.localPosition = new Vector3(1.85f, 0.95f, 0.35f);
                axe.transform.localRotation = Quaternion.Euler(-55f, 40f, 0f);
                NestPrefab("Log_3", root, new Vector3(1.2f, 0.12f, 1.4f), 15f);
                NestPrefab("Log_2", root, new Vector3(1.6f, 0.12f, -0.8f), 70f);
            });

            CreateCropBed("Village_CropBed_Wheat", 0);
            CreateCropBed("Village_CropBed_Pumpkin", 1);
            CreateCropBed("Village_CropBed_Vegetable", 2);
            CreateCropBed("Village_CropBed_Cabbage", 3);
            AssetDatabase.SaveAssets();
        }

        private static void CreateStall(string name, string stripeA, string stripeB, int goods)
        {
            SavePrefab(name, root =>
            {
                for (int i = 0; i < 4; i++)
                {
                    float sx = i % 2 == 0 ? -1.25f : 1.25f, sz = i < 2 ? -0.75f : 0.75f;
                    Box("Post", root, "WoodDark", new Vector3(sx, 1.15f + (sz > 0 ? 0.15f : 0f), sz), new Vector3(0.1f, 2.3f + (sz > 0 ? 0.3f : 0f), 0.1f));
                }
                for (int i = 0; i < 6; i++)
                {
                    Box("Canopy", root, i % 2 == 0 ? stripeA : stripeB, new Vector3(-1.25f + 0.21f + i * 0.42f, 2.47f, 0f), new Vector3(0.43f, 0.05f, 1.9f), new Vector3(-9f, 0, 0));
                    Box("Valance", root, i % 2 == 0 ? stripeA : stripeB, new Vector3(-1.25f + 0.21f + i * 0.42f, 2.2f, -0.95f), new Vector3(0.43f, 0.3f, 0.04f));
                }
                Box("Counter", root, "Wood", new Vector3(0, 0.45f, -0.45f), new Vector3(2.5f, 0.9f, 0.7f));
                Box("Counter_Top", root, "WoodDark", new Vector3(0, 0.93f, -0.45f), new Vector3(2.6f, 0.07f, 0.8f));
                // Goods on the counter
                for (int i = 0; i < 5; i++)
                {
                    float x = -1.0f + i * 0.5f;
                    switch ((goods + i) % 3)
                    {
                        case 0: Part("Apples", root, lowSphere, "Apple", new Vector3(x, 1.05f, -0.45f), new Vector3(0.36f, 0.2f, 0.36f)); break;
                        case 1: Part("Cabbage", root, lowSphere, "Cabbage", new Vector3(x, 1.08f, -0.45f), new Vector3(0.3f, 0.26f, 0.3f)); break;
                        default: Part("Pumpkin", root, lowSphere, "Pumpkin", new Vector3(x, 1.08f, -0.45f), new Vector3(0.34f, 0.26f, 0.34f)); break;
                    }
                }
                NestPrefab("Barrel", root, new Vector3(1.7f, 0f, -0.2f), 0f, 0.6f);
                NestPrefab(goods % 2 == 0 ? "Box_1" : "Box_2", root, new Vector3(-1.75f, 0f, -0.1f), 15f, 0.5f);
                GameObject sack = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Village_Sack.prefab"), root) as GameObject;
                sack.transform.localPosition = new Vector3(1.4f, 0f, -1.1f);
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.9f, 0); col.size = new Vector3(2.7f, 1.8f, 1.7f);
            });
        }

        private static void CreateCropBed(string name, int kind)
        {
            SavePrefab(name, root =>
            {
                Box("Soil", root, "Soil", new Vector3(0, 0.08f, 0), new Vector3(4.4f, 0.16f, 3.2f));
                Box("Frame_N", root, "WoodDark", new Vector3(0, 0.12f, 1.65f), new Vector3(4.6f, 0.24f, 0.12f));
                Box("Frame_S", root, "WoodDark", new Vector3(0, 0.12f, -1.65f), new Vector3(4.6f, 0.24f, 0.12f));
                Box("Frame_E", root, "WoodDark", new Vector3(2.25f, 0.12f, 0), new Vector3(0.12f, 0.24f, 3.2f));
                Box("Frame_W", root, "WoodDark", new Vector3(-2.25f, 0.12f, 0), new Vector3(0.12f, 0.24f, 3.2f));
                var bedRng = new System.Random(kind * 31 + 7);
                for (int row = 0; row < 4; row++)
                {
                    for (int c = 0; c < 6; c++)
                    {
                        Vector3 p = new Vector3(-1.75f + c * 0.7f, 0.16f, -1.15f + row * 0.77f);
                        float yaw = (float)bedRng.NextDouble() * 360f;
                        switch (kind)
                        {
                            case 0:
                                for (int k = 0; k < 4; k++)
                                {
                                    float ox = ((float)bedRng.NextDouble() - 0.5f) * 0.35f, oz = ((float)bedRng.NextDouble() - 0.5f) * 0.35f;
                                    float h = 0.75f + (float)bedRng.NextDouble() * 0.3f;
                                    Vector3 tilt = new Vector3(((float)bedRng.NextDouble() - 0.5f) * 16f, yaw + k * 40f, ((float)bedRng.NextDouble() - 0.5f) * 16f);
                                    Box("Stalk", root, "Wheat", p + new Vector3(ox, h * 0.5f, oz), new Vector3(0.05f, h, 0.05f), tilt);
                                    Box("Ear", root, "Hay", p + new Vector3(ox, h + 0.08f, oz), new Vector3(0.11f, 0.26f, 0.11f), tilt);
                                }
                                break;
                            case 1:
                                if ((row + c) % 2 == 0)
                                {
                                    var pk = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Village_Pumpkin.prefab"), root) as GameObject;
                                    pk.transform.localPosition = p; pk.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                                }
                                else
                                {
                                    var leaf = PlaceVisual("Plant", root, Vector3.zero, yaw, Vector3.one * 0.35f);
                                    leaf.transform.localPosition = p;
                                }
                                break;
                            case 2:
                                var plant = PlaceVisual("Plant", root, Vector3.zero, yaw, Vector3.one * 0.4f);
                                plant.transform.localPosition = p;
                                if (c % 2 == 0) Part("Tomato", root, lowSphere, row % 2 == 0 ? "Apple" : "Pumpkin", p + new Vector3(0.1f, 0.15f, 0.05f), Vector3.one * 0.16f);
                                break;
                            default:
                                var cab = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Village_Cabbage.prefab"), root) as GameObject;
                                cab.transform.localPosition = p; cab.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                                break;
                        }
                    }
                }
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.12f, 0); col.size = new Vector3(4.6f, 0.24f, 3.4f);
            });
        }

        private static void NestPrefab(string name, Transform parent, Vector3 localPos, float yaw, float scale = 1f)
        {
            GameObject p = Prefab(name);
            if (p == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * p.transform.rotation;
            go.transform.localScale = p.transform.localScale * scale;
        }

        private static void SavePrefab(string name, System.Action<Transform> build)
        {
            var root = new GameObject(name);
            build(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        #endregion

        #region Ground and roads

        private static void BuildGround(Transform root)
        {
            const float half = 120f, step = 5f;
            int n = Mathf.RoundToInt(2f * half / step);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int ix = 0; ix < n; ix++)
            {
                for (int iz = 0; iz < n; iz++)
                {
                    Vector3 a = GroundPoint(-half + ix * step, -half + iz * step);
                    Vector3 b = GroundPoint(-half + (ix + 1) * step, -half + iz * step);
                    Vector3 c = GroundPoint(-half + ix * step, -half + (iz + 1) * step);
                    Vector3 d = GroundPoint(-half + (ix + 1) * step, -half + (iz + 1) * step);
                    if (((ix + iz) & 1) == 0) { AddTri(verts, tris, a, c, d); AddTri(verts, tris, a, d, b); }
                    else { AddTri(verts, tris, a, c, b); AddTri(verts, tris, b, c, d); }
                }
            }
            Mesh mesh = SaveMesh(FinishMesh(verts, tris), "Village_Ground");

            var ground = new GameObject("Village_Ground");
            ground.transform.SetParent(root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = mats["Grass"];
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Flat inside the playable area (the combat grid needs level ground), rolling faceted hills outside.</summary>
        private static Vector3 GroundPoint(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float y = 0f;
            if (r > FlatRadius)
            {
                float t = Mathf.Clamp01((r - FlatRadius) / 25f);
                y = t * t * 6f + Hash(x, z) * 1.4f * t;
            }
            else if (r > 6f)
            {
                y = (Hash(x, z) - 0.5f) * 0.02f; // barely-there facets
            }
            return new Vector3(x, y, z);
        }

        private static float Hash(float x, float z)
        {
            float h = Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static Vector3 RingPoint(float angle, float offset)
        {
            // Offset is along the ellipse normal (positive = outward)
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Vector3 p = new Vector3(RingRadiusX * c, 0f, RingRadiusZ * s);
            Vector3 n = new Vector3(RingRadiusZ * c, 0f, RingRadiusX * s).normalized;
            return p + n * offset;
        }

        private static void BuildRoads(Transform root)
        {
            var roads = new GameObject("Roads").transform;
            roads.SetParent(root, false);

            // Dirt bed of the ring road
            var ring = new List<Vector3>();
            for (int i = 0; i <= 96; i++) ring.Add(RingPoint(i / 96f * Mathf.PI * 2f, 0f));
            Ribbon("Ring_Dirt", roads, ring, RingWidth + 0.8f, 0.025f, "Dirt");

            // Cobblestones in three rows
            var stones = new GameObject("Ring_Cobblestones").transform;
            stones.SetParent(roads, false);
            string[] tiles = { "Tile_1", "Tile_2", "Tile_3", "Tile_4" };
            float perimeter = Mathf.PI * (3f * (RingRadiusX + RingRadiusZ) - Mathf.Sqrt((3f * RingRadiusX + RingRadiusZ) * (RingRadiusX + 3f * RingRadiusZ)));
            int count = Mathf.RoundToInt(perimeter / 1.3f);
            for (int row = -1; row <= 1; row++)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = (i + (row == 0 ? 0.5f : 0f)) / count * Mathf.PI * 2f;
                    Vector3 p = RingPoint(a, row * 1.3f);
                    Vector3 tangent = RingPoint(a + 0.01f, row * 1.3f) - p;
                    float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg + Rand(-8f, 8f);
                    PlaceVisual(tiles[rng.Next(tiles.Length)], stones, p + new Vector3(0, -0.2f, 0), yaw, Vector3.one);
                }
            }

            // Dirt arrival road leaving to the south-west, out of the village
            var sw = new List<Vector3>
            {
                RingPoint(Mathf.Deg2Rad * 222f, 0f), new Vector3(-20f, 0, -17f), new Vector3(-27f, 0, -24f),
                new Vector3(-35f, 0, -31f), new Vector3(-45f, 0, -40f), new Vector3(-58f, 0, -50f), new Vector3(-75f, 0, -60f)
            };
            Ribbon("Road_SouthWest", roads, sw, 4.6f, 0.03f, "Dirt");
            KeepOutPath(sw, 4.5f);

            // Path north, between the market hut and the tavern, to the forest gate
            var north = new List<Vector3>
            {
                RingPoint(Mathf.Deg2Rad * 80f, 0f), new Vector3(3.6f, 0, 20f), new Vector3(4.6f, 0, 28f),
                new Vector3(5f, 0, 36f), GatePos + new Vector3(0, 0, 2f), GatePos + new Vector3(0, 0, 16f)
            };
            Ribbon("Path_North", roads, north, 3.4f, 0.03f, "Dirt");
            KeepOutPath(north, 3.5f);

            // Short yard paths to the forge and the hall
            Ribbon("Path_Forge", roads, new List<Vector3> { RingPoint(Mathf.PI, 0f), new Vector3(-24f, 0, -1f), new Vector3(-28f, 0, -3f) }, 3.2f, 0.028f, "Dirt");
            Ribbon("Path_Hall", roads, new List<Vector3> { RingPoint(Mathf.Deg2Rad * 128f, 0f), new Vector3(-14f, 0, 16f), new Vector3(-17f, 0, 19f) }, 3.0f, 0.028f, "Dirt");
            KeepOut(Vector3.zero, RingRadiusX + 3f);
        }

        private static void KeepOutPath(List<Vector3> pts, float radius)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                for (float t = 0; t < 1f; t += 0.2f) KeepOut(Vector3.Lerp(pts[i], pts[i + 1], t), radius);
            }
        }

        private static void Ribbon(string name, Transform parent, List<Vector3> pts, float width, float y, string matKey)
        {
            // Densify the polyline so curves stay smooth
            var dense = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(pts[i], pts[i + 1]) / 1.5f));
                for (int s = 0; s < steps; s++) dense.Add(Vector3.Lerp(pts[i], pts[i + 1], s / (float)steps));
            }
            dense.Add(pts[pts.Count - 1]);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector3 dir = (i < dense.Count - 1 ? dense[i + 1] - dense[i] : dense[i] - dense[i - 1]);
                if (i > 0 && i < dense.Count - 1) dir = dense[i + 1] - dense[i - 1];
                dir.y = 0; dir.Normalize();
                Vector3 side = new Vector3(-dir.z, 0, dir.x) * (width * 0.5f * (1f + (Hash(dense[i].x, dense[i].z) - 0.5f) * 0.15f));
                Vector3 l = dense[i] + side, r = dense[i] - side;
                l.y = GroundPoint(l.x, l.z).y + y; r.y = GroundPoint(r.x, r.z).y + y;
                if (i > 0) { AddTri(verts, tris, prevL, l, r); AddTri(verts, tris, prevL, r, prevR); }
                prevL = l; prevR = r;
            }
            Mesh mesh = SaveMesh(FinishMesh(verts, tris), "Village_" + name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mats[matKey];
        }

        #endregion

        #region Buildings

        private static readonly Vector3 HallPos = new Vector3(-21f, 0f, 24f);
        private static readonly Vector3 TavernPos = new Vector3(16f, 0f, 24f);
        private static readonly Vector3 ForgePos = new Vector3(-34f, 0f, -5f);
        private static readonly Vector3 WitchHousePos = new Vector3(21f, 0f, -21f);

        private static void BuildStoneHall(Transform root)
        {
            var area = new GameObject("Area_StoneHall").transform;
            area.SetParent(root, false);
            float yaw = FaceYaw(HallPos, new Vector3(-6f, 0, 8f));
            Place("Large_house", area, HallPos, yaw);
            KeepOut(HallPos, 9f);

            Vector3 front = Front(yaw), right = Right(yaw);
            Vector3 door = HallPos + front * 6.5f;
            PlaceVillage("Village_BannerPole", area, door + right * 4.6f, yaw);
            PlaceVillage("Village_BannerPole", area, door - right * 4.6f, yaw);
            PlaceVillage("Village_BannerPole", area, HallPos + front * 4f + right * 9f, yaw);
            Place("Bush_2", area, HallPos + front * 5f - right * 7.5f, 20f);
            PlaceVisual("Flowers_2", area, door + right * 2.6f + front * 0.6f, 0f, Vector3.one);
            PlaceVisual("Flowers_1", area, door - right * 2.6f + front * 0.6f, 90f, Vector3.one);
        }

        private static void BuildMarketHut(Transform root)
        {
            var area = new GameObject("Area_MarketHut").transform;
            area.SetParent(root, false);
            Vector3 pos = new Vector3(-4.5f, 0f, 28f);
            float yaw = FaceYaw(pos, new Vector3(-2f, 0, 10f));
            Place("Small_house", area, pos, yaw);
            KeepOut(pos, 6f);
            Vector3 front = Front(yaw), right = Right(yaw);
            PlaceVillage("Village_StripedCanopy", area, pos + front * 4.1f, yaw);
            Place("Barrel", area, pos + front * 5.2f + right * 2.4f, 10f, 0.7f);
            Place("Barrel", area, pos + front * 5.0f + right * 3.3f, 60f, 0.7f);
            Place("Box_1", area, pos + front * 5.6f - right * 2.4f, 25f, 0.6f);
            Place("Box_3", area, pos + front * 4.6f - right * 3.6f, -10f, 0.5f);
            PlaceVillage("Village_Bench", area, pos + front * 7.6f + right * 1.0f, yaw);
            Place("Tree_2_1", area, pos + new Vector3(-6f, 0, 4f), 40f);
            Place("Tree_1", area, pos + new Vector3(16f, 0, 7f), 130f, 0.9f);
        }

        private static void BuildTavern(Transform root)
        {
            var area = new GameObject("Area_Tavern").transform;
            area.SetParent(root, false);
            float yaw = FaceYaw(TavernPos, new Vector3(4f, 0, 8f));
            Place("Tavern", area, TavernPos, yaw);
            KeepOut(TavernPos, 8f);

            Vector3 front = Front(yaw), right = Right(yaw);
            Vector3 patio = TavernPos + front * 8.2f + right * 3.2f;
            // Low plank deck under the patio tables
            var deck = new GameObject("Patio_Deck").transform;
            deck.SetParent(area, false);
            deck.position = patio;
            deck.rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int row = 0; row < 8; row++)
            {
                Box("Plank", deck, row % 2 == 0 ? "Wood" : "WoodDark", new Vector3(0f, 0.04f, (row - 3.5f) * 0.8f), new Vector3(8.4f, 0.08f, 0.76f));
            }
            PlaceVillage("Village_TableSet", area, patio + right * 2.2f - front * 1.2f, yaw + 90f);
            PlaceVillage("Village_TableSet", area, patio - right * 2.2f - front * 1.2f, yaw + 90f);
            PlaceVillage("Village_TableSet", area, patio + right * 0.2f + front * 1.6f, yaw + 90f);
            Place("Barrel", area, TavernPos + front * 4.8f - right * 5.4f, 0f, 0.8f);
            Place("Barrel", area, TavernPos + front * 5.6f - right * 4.4f, 45f, 0.8f);
            Place("Lamppost", area, patio + right * 4.8f + front * 2.6f, yaw);
            KeepOut(patio, 5f);
            Place("Tree_4", area, TavernPos + new Vector3(10f, 0, -9f), 200f, 0.8f);
        }

        private static void BuildWestYard(Transform root)
        {
            var area = new GameObject("Area_ForgeYard").transform;
            area.SetParent(root, false);

            // Thatched cottage with barrels and hay
            Vector3 cottage = new Vector3(-33f, 0f, 12f);
            float cYaw = FaceYaw(cottage, new Vector3(-18f, 0, 4f));
            Place("Baker_house", area, cottage, cYaw);
            KeepOut(cottage, 7.5f);
            Vector3 cf = Front(cYaw), cr = Right(cYaw);
            Place("Barrel", area, cottage + cf * 3.5f + cr * 5.6f, 0f, 0.8f);
            Place("Barrel", area, cottage + cf * 4.5f + cr * 5.0f, 30f, 0.8f);
            Place("Barrel", area, cottage + cf * 3.9f + cr * 5.3f + Vector3.up * 1.1f, 15f, 0.8f);
            PlaceVillage("Village_HayBale", area, cottage + cf * 6.6f - cr * 2.5f, cYaw + 20f);
            PlaceVillage("Village_HayBale", area, cottage + cf * 7.0f - cr * 4.0f, cYaw - 15f);
            PlaceVillage("Village_HayBale", area, cottage + cf * 6.8f - cr * 3.2f + Vector3.up * 1.05f, cYaw + 5f);
            Place("Fence", area, cottage + cf * 6.5f + cr * 2.6f, cYaw);

            // Forge with glowing fire, anvil and firewood
            float fYaw = FaceYaw(ForgePos, new Vector3(-16f, 0, -2f));
            Place("Forge", area, ForgePos, fYaw);
            KeepOut(ForgePos, 8f);
            Vector3 ff = Front(fYaw), fr = Right(fYaw);
            Place("Anvil", area, ForgePos + ff * 7.2f + fr * 1.5f, fYaw + 90f);
            Place("Hammer", area, ForgePos + ff * 7.2f + fr * 3.2f + Vector3.up * 0.05f, fYaw + 30f, 0.5f);
            PlaceVillage("Village_Woodpile", area, ForgePos + ff * 4.5f - fr * 6.5f, fYaw + 90f, 0.8f);
            var fire = new GameObject("Forge_FireGlow");
            fire.transform.SetParent(area, false);
            fire.transform.position = ForgePos + ff * 4.8f + fr * 3.0f + Vector3.up * 1.4f;
            var light = fire.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.52f, 0.18f);
            light.intensity = 4f;
            light.range = 7f;

            // Well and cart
            Place("Well", area, new Vector3(-23f, 0f, 3.5f), 20f);
            KeepOut(new Vector3(-23f, 0f, 3.5f), 2.5f);
            PlaceVillage("Village_Cart", area, new Vector3(-24.5f, 0f, -12.5f), 35f);
            KeepOut(new Vector3(-24.5f, 0f, -12.5f), 3f);
            Place("Bush_1", area, new Vector3(-28f, 0f, 22f), 0f);
        }

        private static void BuildCottages(Transform root)
        {
            var area = new GameObject("Area_Cottages").transform;
            area.SetParent(root, false);

            Cottage(area, "Baker_house", new Vector3(32f, 0f, 7f), 7.5f);
            Cottage(area, "Small_house", new Vector3(31f, 0f, -6f), 6f);
            Cottage(area, "Baker_house", new Vector3(-13f, 0f, -27f), 7.5f);
            Cottage(area, "Small_house", new Vector3(4f, 0f, -27.5f), 6f);

            // Mirabel's green-roofed house on the lower right
            float wYaw = FaceYaw(WitchHousePos, new Vector3(8f, 0, -6f));
            Place("Witch_house", area, WitchHousePos, wYaw);
            KeepOut(WitchHousePos, 7f);
            Vector3 wf = Front(wYaw), wr = Right(wYaw);
            Place("Cauldron", area, WitchHousePos + wf * 5.5f + wr * 3.2f, 0f, 0.8f);
            PlaceVisual("Flowers_2", area, WitchHousePos + wf * 4.8f - wr * 3.2f, 0f, Vector3.one);
            PlaceVisual("Mushroom_1", area, WitchHousePos + wf * 4.2f - wr * 4.3f, 0f, Vector3.one * 0.6f);

            // Crates and logs by the right-hand cottages
            Place("Box_2", area, new Vector3(27f, 0f, 1.2f), 80f, 0.6f);
            Place("Box_1", area, new Vector3(27.6f, 0f, -0.6f), 10f, 0.55f);
            Place("Log", area, new Vector3(26.5f, 0.35f, -12.5f), 30f, 0.8f);
            Place("Log_1", area, new Vector3(25f, 0.2f, -13.5f), 70f);
            Place("Barrel", area, new Vector3(-7.5f, 0f, -23f), 0f, 0.75f);
            Place("Box_1", area, new Vector3(-17.5f, 0f, -21.5f), 30f, 0.55f);
            Place("Box_3", area, new Vector3(-18.5f, 0f, -23.5f), 75f, 0.5f);
            Place("Barrel", area, new Vector3(9.2f, 0f, -24.2f), 0f, 0.7f);

            PlaceVillage("Village_Woodpile", area, new Vector3(4f, 0f, -38f), 15f);
            KeepOut(new Vector3(4f, 0f, -38f), 3.5f);
        }

        private static void Cottage(Transform parent, string prefab, Vector3 pos, float keepOutRadius)
        {
            Place(prefab, parent, pos, FaceYaw(pos, Vector3.zero));
            KeepOut(pos, keepOutRadius);
        }

        #endregion

        #region Fields and market

        private static void BuildFields(Transform root)
        {
            var area = new GameObject("Area_Fields").transform;
            area.SetParent(root, false);
            PlaceVillage("Village_CropBed_Wheat", area, new Vector3(1f, 0f, 9f), 0f);
            PlaceVillage("Village_CropBed_Wheat", area, new Vector3(-5.5f, 0f, 5f), 0f);
            PlaceVillage("Village_CropBed_Pumpkin", area, new Vector3(7.2f, 0f, 4.5f), 0f);
            PlaceVillage("Village_CropBed_Vegetable", area, new Vector3(1f, 0f, 0.6f), 0f);
            PlaceVillage("Village_CropBed_Cabbage", area, new Vector3(-5.5f, 0f, -3.6f), 0f);
            Place("Bush_1", area, new Vector3(-12.5f, 0f, 6.5f), 30f);
            Place("Bush_2", area, new Vector3(-11f, 0f, 9.5f), 70f);
            Place("Bush_2", area, new Vector3(-13.5f, 0f, 3.2f), 140f);
            Place("Bush_1", area, new Vector3(11.5f, 0f, 9f), 200f, 0.8f);
        }

        private static void BuildMarket(Transform root)
        {
            var area = new GameObject("Area_Market").transform;
            area.SetParent(root, false);
            string[] stalls = { "Village_MarketStall_Plain", "Village_MarketStall_Red", "Village_MarketStall_Blue", "Village_MarketStall_Red2" };
            float[] xs = { -9.5f, -3.4f, 2.8f, 9.2f };
            float[] zs = { -6.6f, -8.3f, -8.3f, -6.2f };
            for (int i = 0; i < stalls.Length; i++)
            {
                Vector3 p = new Vector3(xs[i], 0f, zs[i]);
                PlaceVillage(stalls[i], area, p, FaceYaw(p, new Vector3(xs[i] * 1.2f, 0, -20f)));
            }
            Place("Barrel", area, new Vector3(-6.4f, 0f, -9.6f), 0f, 0.6f);
            Place("Barrel", area, new Vector3(5.9f, 0f, -9.8f), 0f, 0.6f);
            Place("Box_2", area, new Vector3(12.5f, 0f, -3.8f), 60f, 0.5f);
        }

        #endregion

        #region Gate, lamps, trees, grass, boundary

        private static void BuildNorthGate(Transform root)
        {
            var area = new GameObject("Area_ForestGate").transform;
            area.SetParent(root, false);
            Place("Arch", area, GatePos, 0f, 1.2f);
            Place("Wall_1", area, GatePos + new Vector3(-6.0f, 0, 0), 0f);
            Place("Wall_1", area, GatePos + new Vector3(6.0f, 0, 0), 0f);
            Place("Pillar_ruined_1", area, GatePos + new Vector3(-9f, 0, 0.3f), 0f);
            Place("Pillar_ruined_2", area, GatePos + new Vector3(9f, 0, 0.3f), 40f);
            PlaceVillage("Village_BannerPole", area, GatePos + new Vector3(-3.6f, 0, -1.6f), 0f);
            PlaceVillage("Village_BannerPole", area, GatePos + new Vector3(3.6f, 0, -1.6f), 0f);
            KeepOut(GatePos, 10f);
        }

        private static void BuildLamps(Transform root)
        {
            var area = new GameObject("Lamps").transform;
            area.SetParent(root, false);
            float[] angles = { 20f, 100f, 160f, 200f, 250f, 300f, 340f };
            foreach (float a in angles)
            {
                Vector3 p = RingPoint(a * Mathf.Deg2Rad, RingWidth * 0.5f + 0.9f);
                Place("street_light", area, p, FaceYaw(p, Vector3.zero) + 180f);
            }
            // Banner posts along the south-west arrival road
            PlaceVillage("Village_BannerPole", area, new Vector3(-29.5f, 0f, -22f), -135f);
            PlaceVillage("Village_BannerPole", area, new Vector3(-24.5f, 0f, -29.5f), -135f);
        }

        private static void BuildTrees(Transform root)
        {
            var area = new GameObject("Trees").transform;
            area.SetParent(root, false);
            string[] edge = { "Pine_tree", "Pine_tree_2", "Pine_tree_2_1", "Tree_1", "Tree_2", "Tree_2_1", "Tree_4", "Pine_tree_2" };
            var placed = new List<Vector3>();
            int attempts = 0;
            while (placed.Count < 150 && attempts < 6000)
            {
                attempts++;
                float ang = Rand(0f, Mathf.PI * 2f);
                float r = Mathf.Lerp(37f, 82f, Mathf.Pow((float)rng.NextDouble(), 1.6f));
                Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!IsFree(p, 2.5f)) continue;
                bool crowded = false;
                float minGap = r < 50f ? 6.5f : 5.5f;
                foreach (Vector3 q in placed) if ((q - p).sqrMagnitude < minGap * minGap) { crowded = true; break; }
                if (crowded) continue;
                p.y = GroundPoint(p.x, p.z).y;
                Place(edge[rng.Next(edge.Length)], area, p, Rand(0f, 360f), Rand(0.8f, 1.15f));
                placed.Add(p);
            }
            for (int i = 0; i < 40; i++)
            {
                float ang = Rand(0f, Mathf.PI * 2f);
                float r = Rand(34f, 48f);
                Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!IsFree(p, 1.5f)) continue;
                Place(rng.Next(3) == 0 ? "Rock_1" : (rng.Next(2) == 0 ? "Bush_1" : "Bush_2"), area, p, Rand(0f, 360f), Rand(0.6f, 1f));
            }
        }

        private static void BuildGrass(Transform root)
        {
            var area = new GameObject("Grass_Tufts").transform;
            area.SetParent(root, false);
            string[] tufts = { "Grass_1", "Grass_2", "Grass_3", "Grass_2", "Flowers_1" };
            for (int i = 0; i < 260; i++)
            {
                float ang = Rand(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt((float)rng.NextDouble()) * 55f;
                Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                bool insideRing = (p.x * p.x) / ((RingRadiusX - 3f) * (RingRadiusX - 3f)) + (p.z * p.z) / ((RingRadiusZ - 3f) * (RingRadiusZ - 3f)) < 1f;
                if (!insideRing && !IsFree(p, 0.5f)) continue;
                if (insideRing && (Mathf.Abs(p.x) < 10f && p.z > -2f && p.z < 11f || p.z < -5f)) continue;
                p.y = GroundPoint(p.x, p.z).y;
                float s = Rand(0.7f, 1.3f);
                PlaceVisual(tufts[rng.Next(tufts.Length)], area, p, Rand(0f, 360f), new Vector3(s, s, s));
            }
        }

        private static void BuildBoundary(Transform root)
        {
            var area = new GameObject("Village_Boundary").transform;
            area.SetParent(root, false);
            const int segments = 36;
            float segLen = 2f * Mathf.PI * BoundaryRadius / segments + 0.6f;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var wall = new GameObject("Boundary_" + i);
                wall.transform.SetParent(area, false);
                wall.transform.position = new Vector3(Mathf.Cos(a) * BoundaryRadius, 2f, Mathf.Sin(a) * BoundaryRadius);
                wall.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                var col = wall.AddComponent<BoxCollider>();
                col.size = new Vector3(segLen, 4f, 1f);
            }
        }

        #endregion

        #region Gameplay objects

        private static void PlaceGameplayObjects()
        {
            float hallYaw = FaceYaw(HallPos, new Vector3(-6f, 0, 8f));
            Vector3 hallDoor = HallPos + Front(hallYaw) * 7.5f;
            Move("NPC_Othelia", hallDoor + Right(hallYaw) * 1.5f, Vector3.zero);

            float fYaw = FaceYaw(ForgePos, new Vector3(-16f, 0, -2f));
            Vector3 anvil = ForgePos + Front(fYaw) * 7.2f + Right(fYaw) * 1.5f;
            Move("NPC_Baldur", anvil + Front(fYaw) * 1.8f - Right(fYaw) * 0.6f, Vector3.zero);
            Move("Village_Lockpick_Practice_Chest", anvil - Right(fYaw) * 3.6f - Front(fYaw) * 0.6f, anvil + Front(fYaw) * 6f);

            float tYaw = FaceYaw(TavernPos, new Vector3(4f, 0, 8f));
            Vector3 patio = TavernPos + Front(tYaw) * 8.2f + Right(tYaw) * 3.2f;
            Move("NPC_Barnaby", patio - Right(tYaw) * 4.6f + Front(tYaw) * 0.4f, Vector3.zero);

            // Cellar hatch on the tavern's west side, with the ladder landing spot just in front of it
            Vector3 hatch = TavernPos - Right(tYaw) * 7.2f + Front(tYaw) * 1.2f;
            GameObject hatchGo = FindInScene("Village_Cellar_Hatch");
            if (hatchGo != null)
            {
                hatchGo.transform.position = new Vector3(hatch.x, 0.05f, hatch.z);
                hatchGo.transform.rotation = Quaternion.Euler(0f, tYaw, 0f);
            }
            GameObject exitPoint = FindInScene("Village_PlayerExitPoint");
            if (exitPoint != null)
            {
                exitPoint.transform.position = hatch + Front(tYaw) * 2.2f + Vector3.up * 1.2f;
                exitPoint.transform.rotation = Quaternion.Euler(0f, tYaw + 180f, 0f);
                GameObject ladder = FindInScene("Cellar_Exit_Ladder");
                DoorTeleporter ladderDoor = ladder != null ? ladder.GetComponent<DoorTeleporter>() : null;
                if (ladderDoor != null && ladderDoor.Destination == null)
                {
                    ladderDoor.Destination = exitPoint.transform;
                    EditorUtility.SetDirty(ladderDoor);
                }
            }

            float wYaw = FaceYaw(WitchHousePos, new Vector3(8f, 0, -6f));
            Move("NPC_Mirabel", WitchHousePos + Front(wYaw) * 6f - Right(wYaw) * 0.8f, Vector3.zero);

            Move("Village_Cat", new Vector3(-20.6f, 0f, 1.6f), new Vector3(-23f, 0, 3.5f));
            Move("Rune_Save_Shrine", new Vector3(-11.6f, 0f, -0.8f), Vector3.zero);

            Vector3 start = new Vector3(-31f, 0.2f, -27.5f);
            GameObject startGo = FindInScene("StartSpawn");
            if (startGo != null)
            {
                startGo.transform.position = start;
                startGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }
            GameObject hero = FindInScene("PlayerHero");
            if (hero != null)
            {
                hero.transform.position = new Vector3(start.x, 0.05f, start.z);
                hero.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }
            GameObject cam = FindInScene("Main Camera");
            if (cam != null)
            {
                cam.transform.position = new Vector3(start.x - 6f, 8f, start.z - 8f);
                cam.transform.LookAt(new Vector3(start.x, 1f, start.z));
            }

            GameObject gate = FindInScene("Door_Village_Forest");
            if (gate != null)
            {
                gate.transform.position = GatePos + new Vector3(0f, 1.5f, -1.8f);
                gate.transform.rotation = Quaternion.identity;
            }
            GameObject fromForest = FindInScene("Spawn_From_Forest");
            if (fromForest == null) fromForest = new GameObject("Spawn_From_Forest");
            fromForest.transform.position = GatePos + new Vector3(0f, 0.5f, -6f);
            fromForest.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Village lights: the old warm fill lights sat over the old square; keep them but soften the centre one
            GameObject centerLight = FindInScene("SanctuaryLight_Center");
            if (centerLight != null)
            {
                Light l = centerLight.GetComponent<Light>();
                if (l != null) l.intensity = 1.2f;
            }
        }

        private static void Move(string name, Vector3 pos, Vector3 lookAt)
        {
            GameObject go = FindInScene(name);
            if (go == null)
            {
                Debug.LogWarning("[BuildVillageReferenceEditor] Gameplay object not found: " + name);
                return;
            }
            go.transform.position = new Vector3(pos.x, 0f, pos.z);
            Vector3 d = lookAt - pos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        #endregion

        #region Screenshots

        /// <summary>Batch-mode screenshots: an overview from roughly the reference picture's angle plus a gameplay view.</summary>
        public static void BatchScreenshots()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory("Screenshots");
            var camGo = new GameObject("ScreenshotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
            Shot(cam, new Vector3(0f, 62f, -62f), new Vector3(0f, 0f, 2f), "Screenshots/village_overview.png", 1920, 1200);
            Shot(cam, new Vector3(-6f, 34f, -46f), new Vector3(-4f, 0f, 2f), "Screenshots/village_closer.png", 1920, 1080);
            Shot(cam, new Vector3(-37f, 8.5f, -35.5f), new Vector3(-28f, 1.5f, -22f), "Screenshots/village_start_view.png", 1600, 900);
            Shot(cam, new Vector3(-12f, 9f, -2f), new Vector3(-28f, 1.5f, -2f), "Screenshots/village_forge_view.png", 1600, 900);
            Shot(cam, new Vector3(2f, 9f, 6f), new Vector3(14f, 1.5f, 20f), "Screenshots/village_tavern_view.png", 1600, 900);
            Object.DestroyImmediate(camGo);
        }

        private static void Shot(Camera cam, Vector3 pos, Vector3 look, string file, int w, int h)
        {
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
