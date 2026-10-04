using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Dresses zones 2-7 after the floor plans and concept art (Zone concept art gallery, 2026-10-03):
    /// Forest Path, Castle Courtyard, Library, Castle Hall, Tower and Throne Room.
    /// Opens each existing scene and adds a "Zone_Dressing" root with the planned props, floors, lights and mood.
    /// Gameplay objects (doors, spawns, encounter triggers, bosses, enemies, NPCs, chests, herbs) are kept where they are;
    /// only the floor material, a few light colours and the Giant's Elixir position change on them.
    /// Running it again replaces the previous Zone_Dressing root. Zone 1 (the village) is never opened.
    /// </summary>
    public static class ZoneDressingBuilder
    {
        private const string RootName = "Zone_Dressing";
        private const string MatDir = "Assets/Materials/Zones";
        private const string MeshDir = "Assets/Materials/Zones/Meshes";
        private const string TexDir = "Assets/Materials/Zones/Textures";

        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, PropMesh> propCache = new Dictionary<string, PropMesh>();
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static System.Random rng;
        private static Transform root;
        private static Scene scene;

        private static Mesh boxMesh, prism8, prism6, cone8, octaMesh, sphereMesh;

        #region Entry points

        [MenuItem("CastleOfDice/Dress Zones 2-7 (Floor Plans)", false, 12)]
        public static void BuildAllMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        /// <summary>Batch-mode entry: dresses all six zones and saves them.</summary>
        public static void BatchBuild()
        {
            BuildAll();
        }

        /// <summary>Batch-mode entry: dress, save and take review screenshots in one editor run.</summary>
        public static void BatchBuildAndScreenshots()
        {
            BuildAll();
            BatchScreenshots();
        }

        public static void BuildAll()
        {
            Init();
            Dress("Zone_2_ForestPath", 2002, DressForest);
            Dress("Zone_3_CastleCourtyard", 3003, DressCourtyard);
            Dress("Zone_4_Library", 4004, DressLibrary);
            Dress("Zone_5_CastleHall", 5005, DressHall);
            Dress("Zone_6_Tower", 6006, DressTower);
            Dress("Zone_7_ThroneRoom", 7007, DressThroneRoom);
            AssetDatabase.SaveAssets();
            Debug.Log("[ZoneDressingBuilder] Zones 2-7 dressed after the floor plans and saved.");
        }

        private static void Dress(string sceneName, int seed, System.Action build)
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            rng = new System.Random(seed);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName) Object.DestroyImmediate(go);
            }
            root = new GameObject(RootName).transform;
            build();
            MarkStatic(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ZoneDressingBuilder] Dressed " + sceneName);
        }

        private static void MarkStatic(Transform t)
        {
            if (t.GetComponent<ZoneAmbientMotion>() != null && t.GetComponent<ZoneAmbientMotion>().kind == ZoneAmbientMotion.MotionKind.Float) return;
            if (t.GetComponent<ParticleSystem>() != null) return;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            foreach (Transform c in t) MarkStatic(c);
        }

        #endregion

        #region Setup: folders, meshes, materials

        private static void Init()
        {
            mats.Clear();
            propCache.Clear();
            prefabs.Clear();
            EnsureFolder(MatDir);
            EnsureFolder(MeshDir);
            EnsureFolder(TexDir);
            CreateMeshes();
            CreateMaterials();
        }

        private static void CreateMeshes()
        {
            boxMesh = SaveMesh(BuildBox(), "Zone_Box");
            prism8 = SaveMesh(BuildPrism(8, 1f), "Zone_Prism8");
            prism6 = SaveMesh(BuildPrism(6, 1f), "Zone_Prism6");
            cone8 = SaveMesh(BuildPrism(8, 0f), "Zone_Cone8");
            octaMesh = SaveMesh(BuildOcta(), "Zone_Octa");
            sphereMesh = SaveMesh(BuildLowSphere(8, 5), "Zone_LowSphere");
        }

        private static void CreateMaterials()
        {
            Mat("Stone", new Color(0.55f, 0.53f, 0.50f));
            Mat("StoneDark", new Color(0.36f, 0.35f, 0.34f));
            Mat("StoneLight", new Color(0.72f, 0.69f, 0.64f));
            Mat("StoneStatue", Hex("8a9290"), 0.2f);
            Mat("Wood", new Color(0.50f, 0.33f, 0.19f));
            Mat("WoodDark", new Color(0.30f, 0.19f, 0.11f));
            Mat("Iron", new Color(0.24f, 0.25f, 0.27f), 0.4f, 0.6f);
            Mat("Gold", new Color(0.90f, 0.70f, 0.25f), 0.55f, 0.8f);
            Mat("GoldGlow", new Color(0.95f, 0.75f, 0.30f), 0.55f, 0.8f, new Color(0.55f, 0.38f, 0.08f));
            Mat("Black", new Color(0.04f, 0.04f, 0.05f));
            Mat("Flame", new Color(1f, 0.62f, 0.18f), 0f, 0f, new Color(2.6f, 1.2f, 0.25f));
            Mat("FlameTeal", new Color(0.45f, 1f, 0.9f), 0f, 0f, new Color(0.35f, 1.6f, 1.4f));
            Mat("Coals", new Color(0.55f, 0.14f, 0.05f), 0f, 0f, new Color(1.3f, 0.32f, 0.05f));
            Mat("Hay", new Color(0.86f, 0.72f, 0.38f));
            Mat("Cloth", new Color(0.84f, 0.79f, 0.67f));
            Mat("Paper", new Color(0.93f, 0.87f, 0.70f));
            Mat("Candle", new Color(0.95f, 0.92f, 0.82f));
            Mat("BannerPurple", Hex("3a1f4a"));
            Mat("BannerBlue", Hex("2a4a9a"));
            Mat("BannerGold", Hex("e8c35a"), 0.5f, 0.6f);
            Mat("CarpetRed", Hex("a8242e"));
            Mat("CarpetTrim", Hex("8a1a24"));
            Mat("CarpetDark", new Color(0.32f, 0.05f, 0.07f));
            Mat("Leaf", new Color(0.27f, 0.52f, 0.21f));
            Mat("LeafDark", new Color(0.17f, 0.38f, 0.15f));
            Mat("Dirt", Hex("c89a5c"));
            Mat("GemGreen", Hex("8affb0"), 0.6f, 0f, Hex("8affb0") * 1.4f);
            Mat("WaterGreen", Hex("3aa86a"), 0.85f, 0f, Hex("3aa86a") * 1.1f);
            Mat("Arcane", Hex("b06aff"), 0f, 0f, Hex("b06aff") * 2.2f);
            Mat("Cyan", Hex("5ad8ff"), 0.8f, 0f, Hex("5ad8ff") * 1.8f);
            Mat("Lava", Hex("ff6a1a"), 0f, 0f, Hex("ff9a3a") * 1.6f);
            Mat("Charred", new Color(0.08f, 0.05f, 0.04f));
            Mat("WindowWarm", Hex("ffe8b0"), 0.3f, 0f, Hex("ffe8b0") * 1.5f);
            Mat("GlassPurple", Hex("8aa8f0"), 0.6f, 0f, Hex("8aa8f0") * 1.3f);
            Mat("GlassTeal", Hex("7ac0c0"), 0.6f, 0f, Hex("7ac0c0") * 1.3f);
            Mat("GlassBlue", Hex("5a7ad0"), 0.6f, 0f, Hex("5a7ad0") * 1.3f);
            Mat("GlassRed", Hex("c87a9a"), 0.6f, 0f, Hex("c87a9a") * 1.3f);
            Mat("GlassGold", Hex("d0c07a"), 0.6f, 0f, Hex("d0c07a") * 1.3f);
            Mat("BookRed", new Color(0.58f, 0.12f, 0.12f));
            Mat("BookBlue", new Color(0.15f, 0.25f, 0.55f));
            Mat("BookGreen", new Color(0.14f, 0.42f, 0.22f));
            Mat("BookPurple", new Color(0.38f, 0.16f, 0.48f));
            Mat("BookTan", new Color(0.75f, 0.60f, 0.35f));
            Mat("PotionPurple", Hex("c83ad8"), 0.8f, 0f, Hex("c83ad8") * 0.6f);
            Mat("PotionBlue", Hex("3a8ad8"), 0.8f, 0f, Hex("3a8ad8") * 0.6f);
            Mat("PotionGreen", Hex("6ad83a"), 0.8f, 0f, Hex("6ad83a") * 0.6f);
            Mat("PotionRed", Hex("d83a3a"), 0.8f, 0f, Hex("d83a3a") * 0.6f);
            Transparent("CyanBeam", new Color(0.667f, 0.957f, 1f, 0.25f), new Color(0.10f, 0.30f, 0.36f));
            Transparent("GoldBeam", new Color(1f, 0.92f, 0.65f, 0.08f), new Color(0.22f, 0.18f, 0.09f));

            Floor("Forest", new Color(0.40f, 0.60f, 0.24f), new Color(0.39f, 0.585f, 0.235f), new Color(0.395f, 0.59f, 0.237f), 80f, 110f, 5f, 0.03f, false);
            Floor("Courtyard", new Color(0.56f, 0.54f, 0.50f), new Color(0.46f, 0.44f, 0.41f), new Color(0.30f, 0.29f, 0.28f), 90f, 90f, 2.25f);
            Floor("Library", new Color(0.30f, 0.26f, 0.38f), new Color(0.20f, 0.17f, 0.27f), new Color(0.12f, 0.10f, 0.16f), 90f, 90f, 2f);
            Floor("Hall", new Color(0.78f, 0.72f, 0.62f), new Color(0.45f, 0.40f, 0.35f), new Color(0.30f, 0.27f, 0.24f), 80f, 80f, 2f);
            Floor("Tower", new Color(0.45f, 0.41f, 0.37f), new Color(0.36f, 0.33f, 0.30f), new Color(0.22f, 0.20f, 0.18f), 30f, 30f, 1.5f);
            Floor("Throne", new Color(0.55f, 0.64f, 0.63f), new Color(0.36f, 0.43f, 0.43f), new Color(0.22f, 0.27f, 0.27f), 100f, 100f, 2.5f);
        }

        private static Material Mat(string key, Color color, float smoothness = 0.15f, float metallic = 0f, Color? emission = null)
        {
            Material m = LoadOrCreateMat(key);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }

        private static Material Transparent(string key, Color color, Color emission)
        {
            Material m = Mat(key, color, 0f, 0f, emission);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Flagstone floor: a generated 4x4 flagstone texture tiled so each stone is about <paramref name="stone"/> metres.</summary>
        private static void Floor(string key, Color a, Color b, Color grout, float sizeX, float sizeZ, float stone, float jitter = 0.12f, bool facets = true)
        {
            Texture2D tex = FlagstoneTexture("T_Zone_Floor_" + key, a, b, grout, jitter, facets);
            Material m = Mat("Floor_" + key, Color.white, 0.12f);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(sizeX / (4f * stone), sizeZ / (4f * stone)));
            EditorUtility.SetDirty(m);
        }

        private static Material LoadOrCreateMat(string key)
        {
            string path = MatDir + "/M_Zone_" + key + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        private static Texture2D FlagstoneTexture(string name, Color a, Color b, Color grout, float jitter, bool facets)
        {
            string path = TexDir + "/" + name + ".png";
            const int size = 128, cells = 4, cell = size / cells;
            var r = new System.Random(name.Length * 97 + (int)(a.r * 1000));
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var cellColors = new Color[cells, cells];
            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    Color c = ((cx + cy) & 1) == 0 ? a : b;
                    float j = 1f - jitter * 0.5f + (float)r.NextDouble() * jitter;
                    cellColors[cx, cy] = new Color(c.r * j, c.g * j, c.b * j);
                }
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int lx = x % cell, ly = y % cell;
                    bool line = lx < 2 || ly < 2;
                    Color c = line ? grout : cellColors[x / cell, y / cell];
                    // faint facet shading inside each stone keeps the low-poly look
                    if (facets && !line && lx + ly < cell * 0.6f) c *= 1.04f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        #endregion

        #region Mesh generation

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

        private static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(v, t, a, b, c);
            Tri(v, t, a, c, d);
        }

        private static Mesh Finish(List<Vector3> v, List<int> t)
        {
            var m = new Mesh();
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            var uv = new Vector2[v.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x + v[i].y * 0.5f, v[i].z + v[i].y * 0.5f);
            m.uv = uv;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Unit cube centred on the origin.</summary>
        private static Mesh BuildBox()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 p000 = new Vector3(-.5f, -.5f, -.5f), p100 = new Vector3(.5f, -.5f, -.5f), p010 = new Vector3(-.5f, .5f, -.5f), p110 = new Vector3(.5f, .5f, -.5f);
            Vector3 p001 = new Vector3(-.5f, -.5f, .5f), p101 = new Vector3(.5f, -.5f, .5f), p011 = new Vector3(-.5f, .5f, .5f), p111 = new Vector3(.5f, .5f, .5f);
            Quad(v, t, p000, p010, p110, p100); // front (-z)
            Quad(v, t, p101, p111, p011, p001); // back
            Quad(v, t, p001, p011, p010, p000); // left
            Quad(v, t, p100, p110, p111, p101); // right
            Quad(v, t, p010, p011, p111, p110); // top
            Quad(v, t, p001, p000, p100, p101); // bottom
            Mesh m = Finish(v, t);
            // proper box UVs for textured faces
            return m;
        }

        /// <summary>Prism of radius 0.5 from y 0 to 1; topScale 0 makes a cone.</summary>
        private static Mesh BuildPrism(int sides, float topScale)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 bottom = Vector3.zero, top = Vector3.up;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides + Mathf.PI / sides, a1 = Mathf.PI * 2f * (i + 1) / sides + Mathf.PI / sides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * .5f, 0f, Mathf.Sin(a0) * .5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * .5f, 0f, Mathf.Sin(a1) * .5f);
                Vector3 t0 = new Vector3(b0.x * topScale, 1f, b0.z * topScale);
                Vector3 t1 = new Vector3(b1.x * topScale, 1f, b1.z * topScale);
                if (topScale > 0f)
                {
                    Quad(v, t, b0, t0, t1, b1);
                    Tri(v, t, top, t1, t0);
                }
                else
                {
                    Tri(v, t, b0, top, b1);
                }
                Tri(v, t, bottom, b0, b1);
            }
            return Finish(v, t);
        }

        private static Mesh BuildOcta()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 up = new Vector3(0, .5f, 0), dn = new Vector3(0, -.5f, 0);
            Vector3[] ring = { new Vector3(.5f, 0, 0), new Vector3(0, 0, .5f), new Vector3(-.5f, 0, 0), new Vector3(0, 0, -.5f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                Tri(v, t, up, b, a);
                Tri(v, t, dn, a, b);
            }
            return Finish(v, t);
        }

        /// <summary>Faceted sphere of radius 0.5 centred on the origin.</summary>
        private static Mesh BuildLowSphere(int segments, int rings)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                float p0 = Mathf.PI * r / rings, p1 = Mathf.PI * (r + 1) / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a0 = Mathf.PI * 2f * s / segments, a1 = Mathf.PI * 2f * (s + 1) / segments;
                    Vector3 q00 = SpherePt(p0, a0), q01 = SpherePt(p0, a1), q10 = SpherePt(p1, a0), q11 = SpherePt(p1, a1);
                    if (r > 0) Tri(v, t, q00, q01, q10);
                    if (r < rings - 1) Tri(v, t, q01, q11, q10);
                }
            }
            return Finish(v, t);
        }

        private static Vector3 SpherePt(float phi, float theta)
        {
            return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * 0.5f;
        }

        /// <summary>Flat ribbon along a polyline (paths, cracks).</summary>
        private static GameObject Ribbon(string name, Transform parent, List<Vector3> pts, float width, float y, string matKey, float jitter = 0.12f)
        {
            var dense = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(pts[i], pts[i + 1]) / 1.5f));
                for (int s = 0; s < steps; s++) dense.Add(Vector3.Lerp(pts[i], pts[i + 1], s / (float)steps));
            }
            dense.Add(pts[pts.Count - 1]);

            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector3 dir = i < dense.Count - 1 ? dense[i + 1] - dense[i] : dense[i] - dense[i - 1];
                if (i > 0 && i < dense.Count - 1) dir = dense[i + 1] - dense[i - 1];
                dir.y = 0f;
                dir.Normalize();
                float w = width * 0.5f * (1f + ((float)rng.NextDouble() - 0.5f) * 2f * jitter);
                Vector3 side = new Vector3(-dir.z, 0f, dir.x) * w;
                Vector3 l = dense[i] + side, r = dense[i] - side;
                l.y = y; r.y = y;
                if (i > 0) { Tri(v, t, prevL, l, r); Tri(v, t, prevL, r, prevR); }
                prevL = l; prevR = r;
            }
            Mesh mesh = SaveMesh(Finish(v, t), scene.name + "_" + name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats[matKey];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        #endregion

        #region Composite props

        private enum Col { None, Box, Mesh }

        private class PropMesh
        {
            public Mesh mesh;
            public Material[] materials;
        }

        /// <summary>Collects primitive parts per material and bakes them into one mesh with a submesh per material.</summary>
        private class PB
        {
            public readonly Dictionary<string, List<CombineInstance>> parts = new Dictionary<string, List<CombineInstance>>();
            public readonly List<string> order = new List<string>();

            public void Add(Mesh mesh, string mat, Matrix4x4 m)
            {
                if (!parts.TryGetValue(mat, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    parts[mat] = list;
                    order.Add(mat);
                }
                list.Add(new CombineInstance { mesh = mesh, transform = m });
            }

            public void Box(string mat, Vector3 center, Vector3 size, Vector3 euler = default)
            {
                Add(boxMesh, mat, Matrix4x4.TRS(center, Quaternion.Euler(euler), size));
            }

            /// <summary>Upright prism standing on <paramref name="basePos"/>.</summary>
            public void Prism(string mat, Vector3 basePos, float radius, float height, int sides = 8, Vector3 euler = default)
            {
                Add(sides == 6 ? prism6 : prism8, mat, Matrix4x4.TRS(basePos, Quaternion.Euler(euler), new Vector3(radius * 2f, height, radius * 2f)));
            }

            public void Cone(string mat, Vector3 basePos, float radius, float height, Vector3 euler = default)
            {
                Add(cone8, mat, Matrix4x4.TRS(basePos, Quaternion.Euler(euler), new Vector3(radius * 2f, height, radius * 2f)));
            }

            public void Octa(string mat, Vector3 center, Vector3 size, Vector3 euler = default)
            {
                Add(octaMesh, mat, Matrix4x4.TRS(center, Quaternion.Euler(euler), size));
            }

            public void Sphere(string mat, Vector3 center, Vector3 size, Vector3 euler = default)
            {
                Add(sphereMesh, mat, Matrix4x4.TRS(center, Quaternion.Euler(euler), size));
            }

            /// <summary>Flat ring of box segments lying on the floor.</summary>
            public void Ring(string mat, Vector3 center, float radius, float width, float height, int segments)
            {
                float seg = 2f * Mathf.PI * radius / segments * 1.04f;
                for (int i = 0; i < segments; i++)
                {
                    float a = Mathf.PI * 2f * (i + 0.5f) / segments;
                    Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, height * 0.5f, Mathf.Sin(a) * radius);
                    Box(mat, p, new Vector3(width, height, seg), new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
                }
            }

            /// <summary>Flat strip from a to b lying on the floor.</summary>
            public void Line(string mat, Vector3 a, Vector3 b, float width, float height)
            {
                Vector3 d = b - a;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                Box(mat, (a + b) * 0.5f + Vector3.up * height * 0.5f, new Vector3(width, height, d.magnitude + width), new Vector3(0f, yaw, 0f));
            }
        }

        private static PropMesh MakeProp(string name, System.Action<PB> build)
        {
            if (propCache.TryGetValue(name, out PropMesh cached)) return cached;
            var pb = new PB();
            build(pb);
            var subs = new CombineInstance[pb.order.Count];
            var materials = new Material[pb.order.Count];
            var temp = new List<Mesh>();
            for (int i = 0; i < pb.order.Count; i++)
            {
                var sub = new Mesh { indexFormat = IndexFormat.UInt32 };
                sub.CombineMeshes(pb.parts[pb.order[i]].ToArray(), true, true);
                temp.Add(sub);
                subs[i] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
                if (!mats.TryGetValue(pb.order[i], out materials[i])) Debug.LogError("[ZoneDressingBuilder] Missing material key " + pb.order[i]);
            }
            var final = new Mesh { indexFormat = IndexFormat.UInt32 };
            final.CombineMeshes(subs, false, true);
            final.RecalculateBounds();
            foreach (Mesh m in temp) Object.DestroyImmediate(m);
            var p = new PropMesh { mesh = SaveMesh(final, "Prop_" + name), materials = materials };
            propCache[name] = p;
            return p;
        }

        private static GameObject Spawn(PropMesh p, string name, Transform parent, Vector3 pos, float yaw, float scale = 1f, Col col = Col.Box, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = p.materials;
            if (!castShadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            if (col == Col.Box)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = p.mesh.bounds.center;
                bc.size = p.mesh.bounds.size;
            }
            else if (col == Col.Mesh)
            {
                go.AddComponent<MeshCollider>().sharedMesh = p.mesh;
            }
            return go;
        }

        private static GameObject Prop(string meshName, string objName, Transform parent, Vector3 pos, float yaw, System.Action<PB> build, float scale = 1f, Col col = Col.Box, bool castShadows = true)
        {
            return Spawn(MakeProp(meshName, build), objName, parent, pos, yaw, scale, col, castShadows);
        }

        private static GameObject Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            return go;
        }

        private static GameObject Prefab(string name)
        {
            if (prefabs.TryGetValue(name, out GameObject p)) return p;
            p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/" + name + ".prefab");
            if (p == null) Debug.LogWarning("[ZoneDressingBuilder] Missing prefab " + name);
            prefabs[name] = p;
            return p;
        }

        /// <summary>Instantiates a project prefab with its colliders.</summary>
        private static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw, Vector3 scale)
        {
            GameObject p = Prefab(prefab);
            if (p == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * p.transform.rotation;
            go.transform.localScale = Vector3.Scale(p.transform.localScale, scale);
            return go;
        }

        private static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            return Place(prefab, parent, pos, yaw, Vector3.one * scale);
        }

        /// <summary>Copies only the visible meshes of a prefab (no colliders, no scripts).</summary>
        private static GameObject PlaceVisual(string prefab, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            GameObject p = Prefab(prefab);
            if (p == null) return null;
            var go = new GameObject(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (MeshRenderer mr in p.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(mr.name);
                part.transform.SetParent(go.transform, false);
                part.transform.localPosition = p.transform.InverseTransformPoint(mr.transform.position);
                part.transform.localRotation = Quaternion.Inverse(p.transform.rotation) * mr.transform.rotation;
                part.transform.localScale = mr.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            }
            return go;
        }

        private static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float range, float intensity, bool flicker = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            if (flicker)
            {
                var f = go.AddComponent<ZoneAmbientMotion>();
                f.kind = ZoneAmbientMotion.MotionKind.FlickerLight;
                f.amount = 0.18f;
                f.speed = 7f;
            }
            return l;
        }

        private static ZoneAmbientMotion Float(GameObject go, float height, float speed, float spin)
        {
            var f = go.AddComponent<ZoneAmbientMotion>();
            f.kind = ZoneAmbientMotion.MotionKind.Float;
            f.amount = height;
            f.speed = speed;
            f.spin = spin;
            return f;
        }

        /// <summary>Soft floating motes (pollen, dust, sparks) in a box; at most <paramref name="count"/> alive.</summary>
        private static ParticleSystem Particles(string name, Transform parent, Vector3 pos, Vector3 box, int count, float lifetime,
            float speed, float sizeMin, float sizeMax, Color color, float upward)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = count / lifetime;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.randomDirectionAmount = 1f;
            if (upward != 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(upward * 0.6f, upward);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.sharedMaterial = LoadOrCreateParticleMat("Mote", Color.white);
            return ps;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        private static float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        private static T Pick<T>(T[] arr) { return arr[rng.Next(arr.Length)]; }

        #endregion

        #region Scene helpers

        private static GameObject Find(string name)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                Transform t = FindDeep(r.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        private static void SetFloor(string floorObject, string floorMatKey)
        {
            GameObject floor = Find(floorObject);
            if (floor == null) { Debug.LogWarning("[ZoneDressingBuilder] No floor " + floorObject); return; }
            Renderer r = floor.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mats["Floor_" + floorMatKey];
        }

        private static void SetLight(string name, Color color, float intensity)
        {
            GameObject go = Find(name);
            Light l = go != null ? go.GetComponent<Light>() : null;
            if (l == null) return;
            l.color = color;
            l.intensity = intensity;
        }

        /// <summary>Directional light, trilight ambient, linear fog and the camera background for a zone's mood.</summary>
        private static void Mood(Color dir, float dirIntensity, Vector3 dirEuler, Color sky, Color equator, Color ground,
            Color fog, float fogStart, float fogEnd, Color? background)
        {
            GameObject dl = Find("Directional Light");
            if (dl != null)
            {
                Light l = dl.GetComponent<Light>();
                l.color = dir;
                l.intensity = dirIntensity;
                l.shadows = LightShadows.Soft;
                dl.transform.rotation = Quaternion.Euler(dirEuler);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            GameObject camGo = Find("Main Camera");
            Camera cam = camGo != null ? camGo.GetComponent<Camera>() : null;
            if (cam != null && background.HasValue)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = background.Value;
            }
        }

        #endregion

        #region Shared prop recipes

        private static GameObject Brazier(string name, Transform parent, Vector3 pos)
        {
            GameObject b = Prop("Brazier", name, parent, pos, 0f, pb =>
            {
                pb.Prism("Iron", Vector3.zero, 0.45f, 0.15f);
                pb.Prism("Iron", new Vector3(0, 0.15f, 0), 0.13f, 1.0f, 6);
                pb.Prism("Iron", new Vector3(0, 1.08f, 0), 0.6f, 0.32f, 6);
                pb.Prism("Coals", new Vector3(0, 1.38f, 0), 0.5f, 0.06f, 6);
                pb.Cone("Flame", new Vector3(0, 1.4f, 0), 0.34f, 0.8f);
                pb.Cone("Flame", new Vector3(0.18f, 1.4f, 0.1f), 0.2f, 0.55f, new Vector3(0, 30, 6));
                pb.Cone("Flame", new Vector3(-0.15f, 1.4f, -0.12f), 0.18f, 0.5f, new Vector3(0, 70, -8));
            });
            PointLight(name + "_Light", b.transform, pos + new Vector3(0, 1.8f, 0), Hex("ff9a3a"), 10f, 1.6f, true);
            return b;
        }

        private static GameObject StandingTorch(string name, Transform parent, Vector3 pos, bool withLight)
        {
            GameObject t = Prop("StandingTorch", name, parent, pos, 0f, pb =>
            {
                pb.Prism("Iron", Vector3.zero, 0.35f, 0.12f);
                pb.Prism("Iron", new Vector3(0, 0.12f, 0), 0.07f, 2.2f, 6);
                pb.Cone("Iron", new Vector3(0, 2.62f, 0), 0.26f, 0.32f, new Vector3(180, 0, 0));
                pb.Cone("Flame", new Vector3(0, 2.6f, 0), 0.22f, 0.6f);
                pb.Cone("Flame", new Vector3(0.08f, 2.6f, 0.05f), 0.12f, 0.45f, new Vector3(0, 40, 8));
            });
            if (withLight) PointLight(name + "_Light", t.transform, pos + new Vector3(0, 3.1f, 0), new Color(1f, 0.66f, 0.32f), 10f, 2.2f, true);
            return t;
        }

        /// <summary>Wall torch; its front faces local -Z (yaw turns it away from the wall).</summary>
        private static GameObject Sconce(string name, Transform parent, Vector3 wallPos, float yaw, string flameMat, Color? light)
        {
            GameObject s = Prop("Sconce_" + flameMat, name, parent, wallPos, yaw, pb =>
            {
                pb.Box("Iron", new Vector3(0, 0, -0.06f), new Vector3(0.28f, 0.45f, 0.12f));
                pb.Box("Iron", new Vector3(0, -0.05f, -0.3f), new Vector3(0.1f, 0.1f, 0.5f), new Vector3(-20, 0, 0));
                pb.Cone("Iron", new Vector3(0, 0.3f, -0.55f), 0.16f, 0.22f, new Vector3(180, 0, 0));
                pb.Cone(flameMat, new Vector3(0, 0.28f, -0.55f), 0.13f, 0.45f);
            }, 1f, Col.None);
            if (light.HasValue)
            {
                Vector3 front = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0.6f, -0.9f);
                PointLight(name + "_Light", s.transform, wallPos + front, light.Value, 9f, 1.6f, true);
            }
            return s;
        }

        private static GameObject Carpet(string name, Transform parent, Vector3 center, float width, float length, string clothMat, string trimMat = "Gold")
        {
            string mesh = "Carpet_" + clothMat + "_" + trimMat + "_" + width.ToString("0.#") + "x" + length.ToString("0.#");
            return Prop(mesh, name, parent, center, 0f, pb =>
            {
                pb.Box(clothMat, new Vector3(0, 0.01f, 0), new Vector3(width, 0.02f, length));
                pb.Box(trimMat, new Vector3(-width * 0.5f + 0.25f, 0.022f, 0), new Vector3(0.18f, 0.01f, length - 0.3f));
                pb.Box(trimMat, new Vector3(width * 0.5f - 0.25f, 0.022f, 0), new Vector3(0.18f, 0.01f, length - 0.3f));
            }, 1f, Col.None, false);
        }

        private static GameObject Beam(string name, Transform parent, Vector3 basePos, float radius, float height, string mat)
        {
            GameObject b = Prop("Beam_" + mat + "_" + radius.ToString("0.#"), name, parent, basePos, 0f, pb =>
            {
                pb.Prism(mat, Vector3.zero, radius, height);
            }, 1f, Col.None, false);
            b.GetComponent<MeshRenderer>().receiveShadows = false;
            return b;
        }

        private static void Battlements(string name, List<Vector4> walls, float wallTop, float spacing)
        {
            // walls: x0, z0, x1, z1 (centre line of each wall run)
            Prop(scene.name + "_" + name, name, root, Vector3.zero, 0f, pb =>
            {
                foreach (Vector4 w in walls)
                {
                    Vector3 a = new Vector3(w.x, 0, w.y), b = new Vector3(w.z, 0, w.w);
                    float len = Vector3.Distance(a, b);
                    int n = Mathf.Max(1, Mathf.FloorToInt(len / spacing));
                    Vector3 dir = (b - a).normalized;
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = a + dir * (spacing * (i + 0.5f) + (len - n * spacing) * 0.5f);
                        pb.Box("StoneDark", p + Vector3.up * (wallTop + 0.6f), new Vector3(2.3f, 1.2f, spacing * 0.5f), new Vector3(0, yaw, 0));
                    }
                    pb.Box("Stone", (a + b) * 0.5f + Vector3.up * (wallTop + 0.08f), new Vector3(2.4f, 0.16f, len), new Vector3(0, yaw, 0));
                }
            }, 1f, Col.None);
        }

        #endregion

        #region Zone 2: Forest Path

        private static readonly List<Vector3> ForestMainPath = new List<Vector3>
        {
            new Vector3(0f, 0f, -56f), new Vector3(3f, 0f, -40f), new Vector3(-4f, 0f, -25f), new Vector3(2f, 0f, -8f),
            new Vector3(-3f, 0f, 5f), new Vector3(-6f, 0f, 15f), new Vector3(0f, 0f, 30f), new Vector3(4f, 0f, 42f), new Vector3(0f, 0f, 56f)
        };

        private static readonly List<Vector3> ForestSidePath = new List<Vector3>
        {
            new Vector3(-6f, 0f, 15f), new Vector3(-14f, 0f, 17f), new Vector3(-22f, 0f, 15f), new Vector3(-33f, 0f, 15f)
        };

        private static void DressForest()
        {
            SetFloor("Forest_Ground", "Forest");
            // Mood A: sunny day (spec: #fff1cc 1.25 at (45, -35), gradient ambient, light haze)
            Mood(Hex("fff1cc"), 1.25f, new Vector3(45f, -35f, 0f),
                Hex("9ccbea"), Hex("bfd8a0"), Hex("4e6b3a"),
                Hex("dce8d2"), 40f, 130f, null);
            SetLight("ForestLight_South", Hex("8cd973"), 1.2f);
            SetLight("ForestLight_North", Hex("73bfa6"), 1.2f);

            // Winding dirt path from door to door and the side path through the secret gate to the hidden door
            Transform paths = Group("Paths").transform;
            Ribbon("Forest_Path_Main", paths, ForestMainPath, 6f, 0.02f, "Dirt");
            Ribbon("Forest_Path_Side", paths, ForestSidePath, 3.5f, 0.025f, "Dirt");

            // Pine groups (spec positions, kept off the zombie fight grid x -9.6..9.6, z -33.6..-14.4)
            Transform trees = Group("Pine_Groups").transform;
            Vector2[] groups =
            {
                new Vector2(-33f, -45f), new Vector2(-30f, -28f), new Vector2(-34f, 0f), new Vector2(-30f, 28f), new Vector2(-34f, 45f),
                new Vector2(33f, -45f), new Vector2(31f, -25f), new Vector2(34f, 0f), new Vector2(32f, 26f), new Vector2(34f, 46f),
                new Vector2(-12f, 40f), new Vector2(14f, 30f), new Vector2(13.5f, -20f), new Vector2(-14f, -42f), new Vector2(16f, -48f),
                new Vector2(-20f, 46f)
            };
            var occupied = new List<Vector3>();
            foreach (Vector2 g in groups)
            {
                Vector3 c = new Vector3(g.x, 0f, g.y);
                // 2-3 pines within 2.6 m; the inner one leans away from the path side so nothing reaches the grid
                float away = Mathf.Sign(g.x);
                Place("Pine_tree", trees, c, Rand(0f, 360f), Rand(0.9f, 1.3f));
                Place(Pick(new[] { "Pine_tree_2", "Pine_tree_2_1" }), trees, c + new Vector3(away * Rand(1.6f, 2.4f), 0f, Rand(-1.5f, 1.5f)), Rand(0f, 360f), Rand(0.9f, 1.2f));
                if (rng.NextDouble() < 0.6) Place("Pine_tree_2", trees, c + new Vector3(away * Rand(0.5f, 1.5f), 0f, Rand(1.5f, 2.4f)), Rand(0f, 360f), Rand(0.9f, 1.1f));
                occupied.Add(c);
            }

            // Tree line along the walls so the path feels walled in by forest
            Transform edge = Group("Edge_Trees").transform;
            for (float z = -50f; z <= 50f; z += 6.5f)
            {
                if (z < 22f && z > 8f) continue; // west: keep the secret door clear (east too, for symmetry of the clearing)
                Place(Pick(new[] { "Pine_tree", "Pine_tree_2", "Tree_1", "Pine_tree_2_1" }), edge, new Vector3(-36.8f + Rand(-0.6f, 0.6f), 0f, z + Rand(-1f, 1f)), Rand(0f, 360f), Rand(0.8f, 1.15f));
                Place(Pick(new[] { "Pine_tree", "Pine_tree_2", "Tree_4", "Pine_tree_2_1" }), edge, new Vector3(36.8f + Rand(-0.6f, 0.6f), 0f, z + Rand(-1f, 1f)), Rand(0f, 360f), Rand(0.8f, 1.15f));
            }
            for (float x = -34f; x <= 34f; x += 6.5f)
            {
                if (Mathf.Abs(x) < 10f) continue; // gate openings
                Place(Pick(new[] { "Pine_tree", "Pine_tree_2" }), edge, new Vector3(x + Rand(-1f, 1f), 0f, 50.6f), Rand(0f, 360f), Rand(0.8f, 1.1f));
                Place(Pick(new[] { "Pine_tree", "Pine_tree_2" }), edge, new Vector3(x + Rand(-1f, 1f), 0f, -50.6f), Rand(0f, 360f), Rand(0.8f, 1.1f));
            }

            // Rocks, logs and stumps
            Transform rocks = Group("Rocks_And_Logs").transform;
            Place("Rock_1", rocks, new Vector3(12.5f, 0f, -35f), 200f, 0.8f);
            Place("Rock_1", rocks, new Vector3(-8f, 0f, 22f), 30f, 0.8f);
            Place("Rock_3", rocks, new Vector3(9f, 0f, 20f), 110f, 0.5f);
            Place("Rock_4", rocks, new Vector3(-9f, 0f, -44f), 75f, 0.45f);
            Place("Log", rocks, new Vector3(-16f, 0.3f, -6f), 25f, 1f);
            Place("Log", rocks, new Vector3(21f, 0.3f, -32f), 140f, 0.9f);
            Place("Log", rocks, new Vector3(-21f, 0.3f, 33f), 75f, 1f);
            Place("Stump", rocks, new Vector3(8f, 0f, 12f), 0f, 1f);
            Place("Stump", rocks, new Vector3(-13f, 0f, -37f), 90f, 0.9f);
            Place("Stump", rocks, new Vector3(24f, 0f, 40f), 45f, 1.1f);
            Place("Stump", rocks, new Vector3(-24f, 0f, -9f), 160f, 1f);

            // Lantern post by the path; its warm light belongs to the night variant, so it is off in the day mood
            Transform lamps = Group("Lantern_Post").transform;
            Place("Lamppost", lamps, new Vector3(4.5f, 0f, -2f), 200f, 0.65f);
            Light lantern = PointLight("Lantern_Light", lamps, new Vector3(4.5f, 2.8f, -2f), Hex("ffb04a"), 10f, 1.6f, true);
            lantern.gameObject.SetActive(false);

            DressSecretGate();

            // Drifting pollen in the sunlight (mood A)
            Particles("Pollen", root, new Vector3(0f, 3f, 0f), new Vector3(70f, 4f, 100f), 35, 9f, 0.35f, 0.06f, 0.12f, new Color(1f, 0.96f, 0.75f, 0.8f), 0f);

            // Undergrowth: grass, ferns, flowers, mushrooms and bushes, kept off the path and away from gameplay spots
            Transform under = Group("Undergrowth").transform;
            var keepOut = new List<Vector4>
            {
                new Vector4(-12f, -20f, 2f, 0), new Vector4(15f, 8f, 2f, 0), new Vector4(-8f, 32f, 2f, 0),
                new Vector4(0f, -44f, 3f, 0), new Vector4(0f, 44f, 3f, 0), new Vector4(-28f, 15f, 6f, 0),
                new Vector4(4.5f, -2f, 1.5f, 0)
            };
            Scatter(under, new[] { "Grass_1", "Grass_2", "Grass_3" }, 170, 0.9f, 1.4f, keepOut, occupied, 2.6f);
            Scatter(under, new[] { "Fern" }, 34, 0.7f, 1.1f, keepOut, occupied, 3.2f);
            Scatter(under, new[] { "Flowers_1", "Flowers_2", "Flower" }, 40, 0.8f, 1.2f, keepOut, occupied, 2.8f);
            Scatter(under, new[] { "Mushroom_1", "Mushroom_2", "Mushroom_3", "Mushroom_4" }, 30, 0.4f, 0.7f, keepOut, occupied, 2.6f);
            Scatter(under, new[] { "Bush_1", "Bush_2" }, 24, 0.8f, 1.2f, keepOut, occupied, 3.5f);
        }

        private static void Scatter(Transform parent, string[] prefabNames, int count, float minScale, float maxScale,
            List<Vector4> keepOut, List<Vector3> treeGroups, float pathClearance)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries < count * 20)
            {
                tries++;
                Vector3 p = new Vector3(Rand(-37f, 37f), 0f, Rand(-52f, 52f));
                if (DistToPolyline(p, ForestMainPath) < pathClearance || DistToPolyline(p, ForestSidePath) < pathClearance) continue;
                bool blocked = false;
                foreach (Vector4 k in keepOut)
                {
                    if ((new Vector2(p.x - k.x, p.z - k.y)).sqrMagnitude < k.z * k.z) { blocked = true; break; }
                }
                if (blocked) continue;
                PlaceVisual(Pick(prefabNames), parent, p, Rand(0f, 360f), Rand(minScale, maxScale));
                placed++;
            }
        }

        private static float DistToPolyline(Vector3 p, List<Vector3> line)
        {
            float best = float.MaxValue;
            Vector2 q = new Vector2(p.x, p.z);
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = new Vector2(line[i].x, line[i].z), b = new Vector2(line[i + 1].x, line[i + 1].z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }
            return best;
        }

        /// <summary>Ivy-covered stone gateway around the lockpick gate (DC 13) with a glowing gold lock.</summary>
        private static void DressSecretGate()
        {
            GameObject gate = Find("Locked_Secret_Gate");
            if (gate == null) { Debug.LogWarning("[ZoneDressingBuilder] Locked_Secret_Gate not found"); return; }
            // Vili's gate models (ForestGateSetup) dress this gate now; keep the old frame off it
            if (gate.transform.parent != null && gate.transform.parent.Find(ForestGateSetup.ModelsName) != null) return;
            Vector3 g = gate.transform.position; // (-25, 2.5, 15), slab 1.2 x 5 x 6 along z
            Renderer gr = gate.GetComponent<Renderer>();
            if (gr != null) gr.sharedMaterial = mats["WoodDark"];

            Transform oldLock = gate.transform.Find("Gold_Lock");
            if (oldLock != null) Object.DestroyImmediate(oldLock.gameObject);

            Transform frame = Group("Secret_Gate_Frame").transform;
            Prop("SecretGate_Frame", "Gate_Stonework", frame, new Vector3(g.x, 0f, g.z), 0f, pb =>
            {
                pb.Box("Stone", new Vector3(0, 3.1f, -3.8f), new Vector3(1.8f, 6.2f, 1.6f));
                pb.Box("Stone", new Vector3(0, 3.1f, 3.8f), new Vector3(1.8f, 6.2f, 1.6f));
                pb.Box("StoneLight", new Vector3(0, 6.6f, 0), new Vector3(2.1f, 1.0f, 9.4f));
                pb.Box("StoneDark", new Vector3(0, 7.6f, -2.4f), new Vector3(2.2f, 0.3f, 5.6f), new Vector3(-28, 0, 0));
                pb.Box("StoneDark", new Vector3(0, 7.6f, 2.4f), new Vector3(2.2f, 0.3f, 5.6f), new Vector3(28, 0, 0));
                // planks and iron straps on the path (east) face
                for (int i = 0; i < 5; i++) pb.Box("Wood", new Vector3(0.62f, 2.5f, -2.4f + i * 1.2f), new Vector3(0.06f, 4.8f, 1.1f));
                pb.Box("Iron", new Vector3(0.66f, 1.2f, 0), new Vector3(0.05f, 0.25f, 5.8f));
                pb.Box("Iron", new Vector3(0.66f, 3.8f, 0), new Vector3(0.05f, 0.25f, 5.8f));
                // ivy
                for (int i = 0; i < 46; i++)
                {
                    float side = i % 2 == 0 ? -3.8f : 3.8f;
                    Vector3 p = i < 30
                        ? new Vector3(Rand(0.7f, 1.0f), Rand(0.5f, 6.4f), side + Rand(-0.7f, 0.7f))
                        : new Vector3(Rand(0.8f, 1.1f), Rand(6.1f, 7.2f), Rand(-4.4f, 4.4f));
                    pb.Box(i % 3 == 0 ? "LeafDark" : "Leaf", p, new Vector3(0.12f, Rand(0.35f, 0.6f), Rand(0.35f, 0.6f)), new Vector3(Rand(-30, 30), Rand(-20, 20), Rand(-40, 40)));
                }
            }, 1f, Col.None);

            // Gold lock on the door itself (vanishes with it if the gate is ever removed)
            GameObject lockGo = Prop("SecretGate_Lock", "Gold_Lock", frame, new Vector3(g.x + 0.65f, 2.4f, g.z), 0f, pb =>
            {
                pb.Box("GoldGlow", Vector3.zero, new Vector3(0.12f, 0.5f, 0.45f));
                pb.Box("Black", new Vector3(0.07f, -0.05f, 0), new Vector3(0.02f, 0.18f, 0.07f));
                pb.Box("GoldGlow", new Vector3(0, 0.33f, 0), new Vector3(0.08f, 0.22f, 0.3f));
            }, 1f, Col.None);
            lockGo.transform.SetParent(gate.transform, true);
            PointLight("Gold_Lock_Glow", frame, new Vector3(-24.2f, 2.6f, 15f), Hex("ffd76a"), 6f, 1.2f, true);
        }

        #endregion

        #region Zone 3: Castle Courtyard

        private static void DressCourtyard()
        {
            SetFloor("Courtyard_Floor", "Courtyard");
            // Mood A: storm. Cold grey light, green cursed glow, lightning.
            Mood(Hex("9aa8bf"), 0.75f, new Vector3(50f, -30f, 0f),
                Hex("3a4452"), Color.Lerp(Hex("3a4452"), Hex("2e3440"), 0.5f), Hex("2e3440"),
                Hex("5e6672"), 25f, 110f, new Color(0.20f, 0.23f, 0.28f));
            SetLight("CourtyardLight_Center", new Color(0.75f, 0.95f, 0.85f), 1.6f);
            SetLight("CourtyardLight_North", new Color(0.45f, 1f, 0.62f), 1.8f);

            var lightningGo = new GameObject("Lightning_Flash");
            lightningGo.transform.SetParent(root, false);
            lightningGo.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            Light lightning = lightningGo.AddComponent<Light>();
            lightning.type = LightType.Directional;
            lightning.color = new Color(0.85f, 0.9f, 1f);
            lightning.intensity = 0f; // dark until a flash; the flash peak is the motion's amount
            lightning.shadows = LightShadows.None;
            var flash = lightningGo.AddComponent<ZoneAmbientMotion>();
            flash.kind = ZoneAmbientMotion.MotionKind.Lightning;
            flash.flashInterval = 11f;
            flash.amount = 1.65f; // with the 0.75 sun this peaks at the spec's 2.4

            Battlements("Courtyard_Battlements", new List<Vector4>
            {
                new Vector4(-45f, -45f, -45f, 45f), new Vector4(45f, -45f, 45f, 45f),
                new Vector4(-45f, -45f, -5f, -45f), new Vector4(5f, -45f, 45f, -45f),
                new Vector4(-45f, 45f, -5f, 45f), new Vector4(5f, 45f, 45f, 45f)
            }, 8f, 2.6f);

            // Corner towers with green-glowing windows
            Transform towers = Group("Corner_Towers").transform;
            Vector2[] corners = { new Vector2(-41f, 41f), new Vector2(41f, 41f), new Vector2(-41f, -41f), new Vector2(41f, -41f) };
            foreach (Vector2 c in corners)
            {
                // 9 x 14 x 9, enclosing the existing corner pillar
                Prop("Courtyard_Tower", "Tower", towers, new Vector3(c.x, 0f, c.y), 0f, pb =>
                {
                    pb.Box("Stone", new Vector3(0, 6.5f, 0), new Vector3(9f, 13f, 9f));
                    pb.Box("StoneDark", new Vector3(0, 13.1f, 0), new Vector3(9.6f, 0.4f, 9.6f));
                    for (int s = 0; s < 4; s++)
                    {
                        Quaternion q = Quaternion.Euler(0, s * 90f, 0);
                        for (int m = -1; m <= 1; m++) pb.Box("StoneDark", q * new Vector3(m * 3.3f, 13.75f, -4.4f), new Vector3(1.7f, 1.3f, 0.8f), new Vector3(0, s * 90f, 0));
                        for (int w = 0; w < 3; w++) pb.Box("GemGreen", q * new Vector3(0f, 4f + w * 3.5f, -4.53f), new Vector3(0.9f, 1.6f, 0.1f), new Vector3(0, s * 90f, 0));
                    }
                });
                PointLight("Tower_Glow", towers, new Vector3(c.x * 0.85f, 8f, c.y * 0.85f), new Color(0.4f, 1f, 0.6f), 12f, 1.4f);
            }

            // Gatehouse at the Castle Hall door: two 4 x 10 x 6 towers, a lintel and a raised portcullis
            Transform gate = Group("Gatehouse").transform;
            foreach (float x in new[] { -7f, 7f })
            {
                Prop("Courtyard_GateTower", "Gate_Tower", gate, new Vector3(x, 0f, 41f), 0f, pb =>
                {
                    pb.Box("Stone", new Vector3(0, 5f, 0), new Vector3(4f, 10f, 6f));
                    pb.Box("StoneDark", new Vector3(-1.2f, 10.6f, -2.75f), new Vector3(1f, 1.2f, 0.5f));
                    pb.Box("StoneDark", new Vector3(1.2f, 10.6f, -2.75f), new Vector3(1f, 1.2f, 0.5f));
                    pb.Box("StoneDark", new Vector3(-1.75f, 10.6f, 0f), new Vector3(0.5f, 1.2f, 1f));
                    pb.Box("StoneDark", new Vector3(1.75f, 10.6f, 0f), new Vector3(0.5f, 1.2f, 1f));
                    pb.Box("GemGreen", new Vector3(0, 7f, -3.03f), new Vector3(0.7f, 1.3f, 0.08f));
                });
            }
            Prop("Courtyard_GateLintel", "Gate_Lintel_And_Portcullis", gate, new Vector3(0f, 0f, 43f), 0f, pb =>
            {
                pb.Box("StoneLight", new Vector3(0, 8.9f, 0), new Vector3(10f, 2.2f, 2.4f));
                for (int m = -2; m <= 2; m++) pb.Box("StoneDark", new Vector3(m * 2.1f, 10.6f, -0.8f), new Vector3(1.2f, 1.2f, 0.8f));
                for (int i = -4; i <= 4; i++) pb.Box("Iron", new Vector3(i * 1.05f, 7f, -0.2f), new Vector3(0.16f, 2.2f, 0.16f));
                for (int j = 0; j < 3; j++) pb.Box("Iron", new Vector3(0, 6.3f + j * 0.7f, -0.2f), new Vector3(9.2f, 0.14f, 0.14f));
                for (int i = -4; i <= 4; i++) pb.Cone("Iron", new Vector3(i * 1.05f, 5.65f, -0.2f), 0.1f, 0.25f, new Vector3(180, 0, 0));
            }, 1f, Col.None);

            // Torn banners on the north wall, hanging 1.5-7.5 m
            Transform banners = Group("Banners").transform;
            foreach (float x in new[] { -30f, -18f, 18f, 30f })
            {
                Prop("Banner_Purple", "Banner", banners, new Vector3(x, 7.5f, 43.85f), 0f, pb =>
                {
                    pb.Box("Iron", Vector3.zero, new Vector3(3.4f, 0.1f, 0.1f));
                    pb.Box("BannerPurple", new Vector3(0, -2.6f, -0.08f), new Vector3(3f, 5.2f, 0.05f));
                    pb.Box("BannerPurple", new Vector3(-0.75f, -5.25f, -0.08f), new Vector3(1.06f, 1.06f, 0.05f), new Vector3(0, 0, 45));
                    pb.Box("BannerPurple", new Vector3(0.75f, -5.25f, -0.08f), new Vector3(1.06f, 1.06f, 0.05f), new Vector3(0, 0, 45));
                    pb.Box("Black", new Vector3(0.7f, -3.9f, -0.1f), new Vector3(0.5f, 0.9f, 0.02f), new Vector3(0, 0, 20));
                    pb.Box("Black", new Vector3(-0.9f, -1.2f, -0.1f), new Vector3(0.3f, 0.6f, 0.02f), new Vector3(0, 0, -15));
                    pb.Octa("GemGreen", new Vector3(0, -2.3f, -0.13f), new Vector3(0.9f, 1.4f, 0.1f));
                }, 1f, Col.None);
            }

            // Four braziers at the corners of the fight area
            Transform braziers = Group("Braziers").transform;
            foreach (Vector3 p in new[] { new Vector3(-12f, 0f, 22f), new Vector3(12f, 0f, 22f), new Vector3(-12f, 0f, -2f), new Vector3(12f, 0f, -2f) })
            {
                Brazier("Brazier", braziers, p);
            }

            // Weapon racks on the west and east edges, training dummies in the south-west
            Transform training = Group("Training_Yard").transform;
            foreach (Vector3 p in new[] { new Vector3(-42f, 0f, 9f), new Vector3(-42f, 0f, 15f), new Vector3(42f, 0f, 9f), new Vector3(42f, 0f, 15f) })
            {
                Prop("WeaponRack", "Weapon_Rack", training, p, p.x < 0 ? 90f : -90f, pb =>
                {
                    pb.Box("WoodDark", new Vector3(-2.4f, 1f, 0), new Vector3(0.2f, 2f, 0.2f));
                    pb.Box("WoodDark", new Vector3(2.4f, 1f, 0), new Vector3(0.2f, 2f, 0.2f));
                    pb.Box("Wood", new Vector3(0, 1.8f, 0), new Vector3(5f, 0.14f, 0.14f));
                    pb.Box("Wood", new Vector3(0, 0.4f, -0.35f), new Vector3(5f, 0.12f, 0.3f));
                    for (int i = 0; i < 7; i++)
                    {
                        float x = -1.8f + i * 0.6f;
                        pb.Box("Wood", new Vector3(x, 1.25f, -0.18f), new Vector3(0.07f, 2.5f, 0.07f), new Vector3(-10, 0, 0));
                        pb.Cone("Iron", new Vector3(x, 2.48f, -0.0f), 0.07f, 0.32f, new Vector3(-10, 0, 0));
                    }
                    pb.Prism("BannerPurple", new Vector3(-2.4f, 1.1f, -0.2f), 0.5f, 0.08f, 8, new Vector3(-90, 0, 0));
                    pb.Prism("Iron", new Vector3(-2.4f, 1.1f, -0.27f), 0.15f, 0.06f, 8, new Vector3(-90, 0, 0));
                    pb.Prism("BannerPurple", new Vector3(2.4f, 1.1f, -0.2f), 0.5f, 0.08f, 8, new Vector3(-90, 0, 0));
                    pb.Prism("Iron", new Vector3(2.4f, 1.1f, -0.27f), 0.15f, 0.06f, 8, new Vector3(-90, 0, 0));
                });
            }
            foreach (float z in new[] { -10f, -20f, -30f })
            {
                Prop("TrainingDummy", "Training_Dummy", training, new Vector3(-36f, 0f, z), 90f, pb =>
                {
                    pb.Box("Wood", new Vector3(0, 0.05f, 0), new Vector3(1.2f, 0.1f, 0.18f));
                    pb.Box("Wood", new Vector3(0, 0.05f, 0), new Vector3(0.18f, 0.1f, 1.2f));
                    pb.Prism("Wood", Vector3.zero, 0.1f, 2.1f, 6);
                    pb.Box("Wood", new Vector3(0, 1.55f, 0), new Vector3(1.5f, 0.12f, 0.12f));
                    pb.Prism("Hay", new Vector3(0, 0.85f, 0), 0.36f, 0.95f, 6);
                    pb.Octa("Hay", new Vector3(0, 2.05f, 0), new Vector3(0.5f, 0.6f, 0.5f));
                    pb.Box("CarpetRed", new Vector3(0, 1.3f, -0.33f), new Vector3(0.3f, 0.3f, 0.04f));
                });
            }

            // The cursed fountain between the entrance and the Commander's dialogue spot
            Transform fountain = Group("Cursed_Fountain").transform;
            Prop("CursedFountain", "Fountain", fountain, new Vector3(0f, 0f, -26f), 22.5f, pb =>
            {
                // Basin r 5.5, rim 0.8 high, with a broken statue (3.5 m) in the middle
                float R = 5.15f;
                float side = 2f * R * Mathf.Tan(Mathf.PI / 8f) + 0.6f;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    Vector3 p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.35f, -R);
                    pb.Box("Stone", p, new Vector3(side, 0.7f, 0.7f), new Vector3(0, a, 0));
                    pb.Box("StoneLight", p + Vector3.up * 0.4f, new Vector3(side, 0.1f, 0.9f), new Vector3(0, a, 0));
                }
                pb.Prism("StoneDark", Vector3.zero, 5.0f, 0.15f);
                pb.Prism("WaterGreen", new Vector3(0, 0.15f, 0), 4.9f, 0.45f);
                pb.Prism("Stone", new Vector3(0, 0.15f, 0), 1.1f, 0.8f, 6);
                pb.Box("StoneStatue", new Vector3(-0.2f, 1.45f, 0), new Vector3(0.45f, 1.0f, 0.5f));
                pb.Box("StoneStatue", new Vector3(0.2f, 1.45f, 0), new Vector3(0.45f, 1.0f, 0.5f));
                pb.Box("StoneStatue", new Vector3(0, 2.4f, 0), new Vector3(1.0f, 1.0f, 0.65f), new Vector3(0, 0, 4));
                pb.Box("StoneStatue", new Vector3(-0.62f, 2.3f, -0.15f), new Vector3(0.28f, 0.9f, 0.3f), new Vector3(-25, 0, 8));
                pb.Box("StoneStatue", new Vector3(0.12f, 3.15f, 0.05f), new Vector3(0.5f, 0.45f, 0.5f), new Vector3(0, 20, 18)); // cracked, tilted head
                pb.Box("StoneDark", new Vector3(0.75f, 0.35f, -1.6f), new Vector3(0.5f, 0.3f, 0.45f), new Vector3(10, 30, 20)); // broken-off arm in the water
                pb.Octa("GemGreen", new Vector3(0, 2.55f, -0.36f), new Vector3(0.35f, 0.5f, 0.12f));
            }, 1f, Col.Mesh);
            PointLight("Fountain_Glow", fountain, new Vector3(0f, 1.5f, -26f), Hex("6aff9a"), 12f, 1.5f, true);

            // Rain that follows the hero (about 600 streaks), so it is dense on screen but cheap
            GameObject hero = Find("PlayerHero");
            if (hero != null)
            {
                Transform oldRain = hero.transform.Find("Rain");
                if (oldRain != null) Object.DestroyImmediate(oldRain.gameObject);
                CreateRain(hero.transform, new Vector3(0f, 16f, 4f), new Vector3(36f, 1f, 36f));
            }
        }

        private static void CreateRain(Transform parent, Vector3 localPos, Vector3 area)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 20f;
            main.startSize3D = true;
            main.startSizeX = 0.05f;
            main.startSizeY = 0.9f;
            main.startSizeZ = 0.05f;
            main.startColor = new Color(0.784f, 0.831f, 0.91f, 0.35f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 540f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.5f;
            r.velocityScale = 0.05f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            Material rain = LoadOrCreateParticleMat("Rain", new Color(0.8f, 0.86f, 1f, 0.5f));
            r.sharedMaterial = rain;
        }

        private static Material LoadOrCreateParticleMat(string key, Color c)
        {
            string path = MatDir + "/M_Zone_" + key + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                m = new Material(s);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            if (key == "Mote")
            {
                Texture2D soft = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                if (soft != null) m.SetTexture("_BaseMap", soft);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            mats[key] = m;
            return m;
        }

        #endregion

        #region Zone 4: Library

        private static readonly string[] BookMats = { "BookRed", "BookBlue", "BookGreen", "BookPurple", "BookTan" };

        /// <summary>Double-sided bookshelf, 4.5 m long along local X, 1.2 m deep, 5 m tall, packed with books.</summary>
        private static PropMesh Bookshelf(int variant)
        {
            return MakeProp("Bookshelf_" + variant, pb =>
            {
                var r = new System.Random(variant * 31 + 7);
                float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
                pb.Box("WoodDark", new Vector3(-2.25f, 2.5f, 0), new Vector3(0.15f, 5f, 1.2f));
                pb.Box("WoodDark", new Vector3(2.25f, 2.5f, 0), new Vector3(0.15f, 5f, 1.2f));
                pb.Box("WoodDark", new Vector3(0, 5.0f, 0), new Vector3(4.7f, 0.18f, 1.3f));
                pb.Box("WoodDark", new Vector3(0, 0.1f, 0), new Vector3(4.5f, 0.2f, 1.2f));
                pb.Box("Wood", new Vector3(0, 2.5f, 0), new Vector3(4.4f, 4.8f, 0.06f));
                float[] levels = { 0.2f, 1.15f, 2.1f, 3.05f, 4.0f };
                foreach (float y in levels)
                {
                    pb.Box("Wood", new Vector3(0, y, 0), new Vector3(4.4f, 0.07f, 1.2f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float x = -2.12f;
                        while (x < 2.05f)
                        {
                            float w = R(0.07f, 0.16f);
                            if (r.NextDouble() < 0.06) { x += R(0.15f, 0.35f); continue; }
                            float h = R(0.5f, 0.8f);
                            float lean = r.NextDouble() < 0.08 ? R(-14f, 14f) : 0f;
                            string mat = BookMats[r.Next(BookMats.Length)];
                            pb.Box(mat, new Vector3(x + w * 0.5f, y + 0.035f + h * 0.5f, side * 0.31f), new Vector3(w, h, 0.46f), new Vector3(0, 0, lean));
                            x += w + 0.01f;
                        }
                    }
                }
            });
        }

        private static void DressLibrary()
        {
            SetFloor("Library_Floor", "Library");
            // Mood A: arcane violet (spec: dim sun, flat ambient, exponential fog) plus mood B's moonlit window
            Mood(Hex("5a4a8a"), 0.35f, new Vector3(60f, -25f, 0f),
                Hex("22163a"), Hex("22163a"), Hex("22163a"),
                Hex("1a1030"), 35f, 110f, new Color(0.06f, 0.04f, 0.10f));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("22163a");
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;

            // Existing bookshelves: keep their colliders, swap the plain blocks for full shelves
            Transform shelves = Group("Bookshelves").transform;
            foreach (string n in new[] { "Bookshelf_1", "Bookshelf_2", "Bookshelf_3", "Bookshelf_4" })
            {
                GameObject old = Find(n);
                if (old == null) continue;
                Renderer r = old.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                Vector3 c = old.transform.position;
                for (int i = -1; i <= 1; i++)
                {
                    GameObject s = Spawn(Bookshelf((i + 2) % 2), n + "_Unit", shelves, new Vector3(c.x + i * 4f, 0f, c.z), 0f, 1f, Col.None);
                    s.transform.localScale = new Vector3(0.89f, 1.4f, 2f);
                }
            }

            // Shelf aisles in all four corners: 2.5 x 7 x 18 each, four 4.5 m units end to end
            Transform aisles = Group("Shelf_Aisles").transform;
            foreach (float x in new[] { -36f, -30f, 30f, 36f })
            {
                foreach (float zc in new[] { 32f, -32f })
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float z = zc - 6.75f + i * 4.5f;
                        GameObject s = Spawn(Bookshelf((i + (x > 0 ? 1 : 0)) % 2), "Aisle_Shelf", aisles, new Vector3(x, 0f, z), 90f);
                        s.transform.localScale = new Vector3(1f, 1.4f, 2.08f);
                    }
                }
            }

            // Ladder and balcony in the north-west corner
            Transform gallery = Group("Ladder_And_Balcony").transform;
            Prop("Library_Balcony", "Balcony", gallery, new Vector3(-36f, 0f, 42.4f), 0f, pb =>
            {
                pb.Box("WoodDark", new Vector3(0, 5.1f, 0), new Vector3(16f, 0.25f, 3.2f));
                pb.Box("Wood", new Vector3(0, 6.1f, -1.55f), new Vector3(16f, 0.12f, 0.12f));
                for (int i = 0; i <= 10; i++) pb.Box("Wood", new Vector3(-8f + i * 1.6f, 5.6f, -1.55f), new Vector3(0.1f, 1f, 0.1f));
                pb.Box("WoodDark", new Vector3(-7.8f, 2.5f, -1.4f), new Vector3(0.3f, 5f, 0.3f));
                pb.Box("WoodDark", new Vector3(7.8f, 2.5f, -1.4f), new Vector3(0.3f, 5f, 0.3f));
                for (int i = 0; i < 4; i++) pb.Box(BookMats[i], new Vector3(-6f + i * 4f, 5.5f, 0.6f), new Vector3(1.6f, 0.6f, 0.5f));
            }, 1f, Col.None);
            // Ladder leaning 12 degrees against the west aisle shelf (no collider)
            Prop("Library_Ladder", "Ladder", gallery, new Vector3(-37.8f, 0f, 36f), 90f, pb =>
            {
                float t = Mathf.Tan(12f * Mathf.Deg2Rad);
                pb.Box("Wood", new Vector3(-0.32f, 3f, 0), new Vector3(0.1f, 6.1f, 0.1f), new Vector3(12, 0, 0));
                pb.Box("Wood", new Vector3(0.32f, 3f, 0), new Vector3(0.1f, 6.1f, 0.1f), new Vector3(12, 0, 0));
                for (int i = 0; i < 14; i++)
                {
                    float y = 0.35f + i * 0.4f;
                    pb.Box("Wood", new Vector3(0, y, t * (y - 3f)), new Vector3(0.66f, 0.06f, 0.06f));
                }
            }, 1f, Col.None);

            // Great stained-glass window on the north wall (14 x 6.5, centre y 4.25) with the moonlight spot of mood B
            Transform window = Group("Stained_Glass_Window").transform;
            Prop("Library_StainedGlass", "Window", window, new Vector3(0f, 0f, 43.95f), 0f, pb =>
            {
                pb.Box("StoneLight", new Vector3(0, 0.85f, -0.1f), new Vector3(14.4f, 0.4f, 0.6f));
                for (int i = 0; i <= 4; i++) pb.Box("StoneLight", new Vector3(-6.9f + i * 3.45f, 3.9f, -0.1f), new Vector3(i == 0 || i == 4 ? 0.6f : 0.3f, 5.8f, 0.45f));
                string[] glass = { "GlassBlue", "GlassPurple", "GlassRed", "GlassTeal", "GlassGold" };
                for (int l = 0; l < 4; l++)
                {
                    float cx = -5.175f + l * 3.45f;
                    for (int row = 0; row < 4; row++)
                    {
                        pb.Box(glass[(l + row) % 5], new Vector3(cx - 0.8f, 1.65f + row * 1.25f, -0.05f), new Vector3(1.55f, 1.2f, 0.04f));
                        pb.Box(glass[(l + row + 2) % 5], new Vector3(cx + 0.8f, 1.65f + row * 1.25f, -0.05f), new Vector3(1.55f, 1.2f, 0.04f));
                    }
                    pb.Box("GlassGold", new Vector3(cx, 6.6f, -0.05f), new Vector3(1.75f, 1.75f, 0.04f), new Vector3(0, 0, 45));
                    pb.Box("StoneLight", new Vector3(cx - 0.75f, 6.7f, -0.12f), new Vector3(1.9f, 0.25f, 0.3f), new Vector3(0, 0, 45));
                    pb.Box("StoneLight", new Vector3(cx + 0.75f, 6.7f, -0.12f), new Vector3(1.9f, 0.25f, 0.3f), new Vector3(0, 0, -45));
                }
            }, 1f, Col.None, false);
            PointLight("Window_Glow", window, new Vector3(0f, 5f, 40f), new Color(0.7f, 0.6f, 1f), 14f, 1.4f);
            var moonGo = new GameObject("Window_Moon_Spot");
            moonGo.transform.SetParent(window, false);
            moonGo.transform.position = new Vector3(0f, 7.5f, 42f);
            moonGo.transform.rotation = Quaternion.Euler(35f, 180f, 0f);
            Light moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Spot;
            moon.color = Hex("9ab8ff");
            moon.range = 30f;
            moon.spotAngle = 50f;
            moon.intensity = 2f;
            moon.shadows = LightShadows.None;

            // Spell circle under Malakor's spot at the north end of the grid
            Transform circle = Group("Spell_Circle").transform;
            Prop("Library_SpellCircle", "Pentagram", circle, new Vector3(0f, 0.02f, 18f), 0f, pb =>
            {
                pb.Ring("Arcane", Vector3.zero, 5f, 0.22f, 0.02f, 40);
                pb.Ring("Arcane", Vector3.zero, 4.3f, 0.12f, 0.02f, 36);
                var star = new Vector3[5];
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 5f;
                    star[i] = new Vector3(Mathf.Cos(a) * 4.3f, 0f, Mathf.Sin(a) * 4.3f);
                }
                for (int i = 0; i < 5; i++) pb.Line("Arcane", star[i], star[(i + 2) % 5], 0.12f, 0.02f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    pb.Box("Arcane", new Vector3(Mathf.Cos(a) * 4.65f, 0.01f, Mathf.Sin(a) * 4.65f), new Vector3(0.18f, 0.02f, 0.18f), new Vector3(0, a * Mathf.Rad2Deg + 45f, 0));
                }
            }, 1f, Col.None, false);
            Light circleGlow = PointLight("Circle_Glow", circle, new Vector3(0f, 1.2f, 18f), Hex("c070ff"), 10f, 2f);
            var pulse = circleGlow.gameObject.AddComponent<ZoneAmbientMotion>();
            pulse.kind = ZoneAmbientMotion.MotionKind.Pulse;
            pulse.amount = 0.35f;
            pulse.speed = 0.6f;
            Particles("Arcane_Sparks", circle, new Vector3(0f, 2.5f, 18f), new Vector3(9f, 3f, 9f), 40, 3f, 0.3f, 0.05f, 0.12f, Hex("d8a8ff"), 0.4f);

            // Fourteen floating spell books circling 3-7 m out, 2-5 m up
            Transform books = Group("Floating_Books", circle).transform;
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f + Rand(-0.15f, 0.15f);
                float rad = Rand(3f, 7f);
                string mat = BookMats[i % BookMats.Length];
                GameObject b = Prop("FloatingBook_" + mat, "Floating_Book", books,
                    new Vector3(Mathf.Cos(a) * rad, Rand(2f, 5f), 18f + Mathf.Sin(a) * rad), Rand(0f, 360f), pb =>
                    {
                        pb.Box(mat, new Vector3(-0.17f, 0, 0), new Vector3(0.34f, 0.04f, 0.46f), new Vector3(0, 0, 12));
                        pb.Box(mat, new Vector3(0.17f, 0, 0), new Vector3(0.34f, 0.04f, 0.46f), new Vector3(0, 0, -12));
                        pb.Box("Paper", new Vector3(-0.16f, 0.03f, 0), new Vector3(0.3f, 0.03f, 0.42f), new Vector3(0, 0, 12));
                        pb.Box("Paper", new Vector3(0.16f, 0.03f, 0), new Vector3(0.3f, 0.03f, 0.42f), new Vector3(0, 0, -12));
                    }, 1.3f, Col.None, false);
                b.transform.rotation = Quaternion.Euler(Rand(-20f, 20f), Rand(0f, 360f), Rand(-15f, 15f));
                Float(b, Rand(0.2f, 0.4f), Rand(0.8f, 1.4f), Rand(-25f, 25f));
            }

            // Four candle clusters around the circle (emissive only, no lights)
            foreach (Vector3 p in new[] { new Vector3(-6.5f, 0f, 13f), new Vector3(6.5f, 0f, 13f), new Vector3(-6.5f, 0f, 23f), new Vector3(6.5f, 0f, 23f) })
            {
                Prop("CandleCluster", "Candles", circle, p, Rand(0f, 360f), pb =>
                {
                    pb.Prism("StoneDark", Vector3.zero, 0.5f, 0.12f);
                    float[] hs = { 0.8f, 0.55f, 0.65f };
                    Vector3[] os = { new Vector3(0, 0, 0), new Vector3(0.25f, 0, 0.15f), new Vector3(-0.2f, 0, 0.2f) };
                    for (int i = 0; i < 3; i++)
                    {
                        pb.Prism("Candle", os[i] + Vector3.up * 0.12f, 0.07f, hs[i], 8);
                        pb.Cone("Flame", os[i] + Vector3.up * (0.12f + hs[i]), 0.05f, 0.14f);
                    }
                }, 1f, Col.None);
            }

            // Reading table, 14 x 0.9 x 3, with benches on both sides, books, scrolls and candles (south)
            Transform reading = Group("Reading_Table").transform;
            Prop("Library_ReadingTable", "Table", reading, new Vector3(0f, 0f, -34f), 0f, pb =>
            {
                pb.Box("Wood", new Vector3(0, 0.83f, 0), new Vector3(14f, 0.14f, 3f));
                foreach (float x in new[] { -6.5f, 0f, 6.5f })
                {
                    pb.Box("WoodDark", new Vector3(x, 0.4f, 0), new Vector3(0.25f, 0.8f, 2.2f));
                    pb.Box("WoodDark", new Vector3(x, 0.08f, 0), new Vector3(0.5f, 0.16f, 2.6f));
                }
                foreach (float z in new[] { -1.85f, 1.85f })
                {
                    pb.Box("Wood", new Vector3(0, 0.45f, z), new Vector3(13f, 0.1f, 0.55f));
                    foreach (float x in new[] { -6f, 0f, 6f }) pb.Box("WoodDark", new Vector3(x, 0.2f, z), new Vector3(0.18f, 0.4f, 0.4f));
                }
                for (int i = 0; i < 7; i++)
                {
                    float x = -6f + i * 2f;
                    float h = 0.25f + (i % 2) * 0.12f;
                    pb.Prism("Gold", new Vector3(x, 0.9f, 0.9f), 0.1f, 0.04f);
                    pb.Prism("Candle", new Vector3(x, 0.94f, 0.9f), 0.06f, h);
                    pb.Cone("Flame", new Vector3(x, 0.94f + h, 0.9f), 0.05f, 0.13f);
                }
                string[] open = { "BookRed", "BookBlue", "BookGreen", "BookPurple" };
                for (int i = 0; i < 4; i++)
                {
                    float x = -5f + i * 3.3f;
                    pb.Box(open[i], new Vector3(x, 0.905f, -0.6f), new Vector3(0.76f, 0.03f, 0.54f), new Vector3(0, 8 - i * 9, 0));
                    pb.Box("Paper", new Vector3(x, 0.92f, -0.6f), new Vector3(0.7f, 0.03f, 0.5f), new Vector3(0, 8 - i * 9, 0));
                }
                pb.Box("BookTan", new Vector3(1.6f, 0.98f, 0.1f), new Vector3(0.5f, 0.16f, 0.4f), new Vector3(0, 25, 0));
                pb.Box("BookBlue", new Vector3(1.62f, 1.13f, 0.1f), new Vector3(0.45f, 0.14f, 0.36f), new Vector3(0, 40, 0));
                pb.Prism("Paper", new Vector3(-2.4f, 0.96f, 0.2f), 0.06f, 0.55f, 6, new Vector3(0, 0, 90));
            });
            PointLight("Reading_Candles", reading, new Vector3(0f, 1.6f, -34f), Hex("ffbf6a"), 10f, 1.2f, true);
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = new Vector3(Rand(-9f, 9f), 0.05f, Rand(-39f, -29f));
                if (Mathf.Abs(p.x) < 7.6f && p.z > -36.6f && p.z < -31.4f) continue;
                Prop("Scroll", "Scroll", reading, p, Rand(0f, 360f), pb =>
                {
                    pb.Prism("Paper", new Vector3(0, 0, 0), 0.06f, 0.55f, 6, new Vector3(0, 0, 90));
                    pb.Box("Paper", new Vector3(0.1f, -0.045f, 0.32f), new Vector3(0.45f, 0.01f, 0.6f));
                }, 1f, Col.None, false);
            }
        }

        #endregion

        #region Zone 5: Castle Hall

        private static void DressHall()
        {
            SetFloor("CentralHall_Floor", "Hall");
            // Warm safe haven with morning sun through the west windows (spec default)
            Mood(Hex("ffe0a0"), 1.3f, new Vector3(30f, 90f, 0f),
                Hex("5a4a48"), Color.Lerp(Hex("5a4a48"), Hex("3a3034"), 0.5f), Hex("3a3034"),
                new Color(0.12f, 0.10f, 0.10f), 40f, 120f, new Color(0.08f, 0.07f, 0.07f));

            // Nave pillars, scaled like the existing hall pillars
            Transform pillars = Group("Pillar_Rows").transform;
            foreach (float x in new[] { -12f, 12f })
            {
                foreach (float z in new[] { -30f, -20f, -10f, 10f, 20f, 30f })
                {
                    Place("Pillar", pillars, new Vector3(x, 0f, z), 0f, new Vector3(1.2f, 1.6f, 1.2f));
                }
            }

            // Blue banners on the nave side of the end pillars
            Transform banners = Group("Nave_Banners").transform;
            foreach (Vector3 p in new[] { new Vector3(-10.9f, 0f, -30f), new Vector3(10.9f, 0f, -30f), new Vector3(-10.9f, 0f, 30f), new Vector3(10.9f, 0f, 30f) })
            {
                // the banner's front is its local -Z: turn it to face the nave
                HallBanner("Nave_Banner", banners, new Vector3(p.x, 7f, p.z), p.x < 0 ? -90f : 90f, 1.6f, 4f);
            }

            // Red carpets north and south of the altar ring
            Transform carpet = Group("Red_Carpet").transform;
            Carpet("Carpet_North", carpet, new Vector3(0f, 0f, 22.5f), 6f, 31f, "CarpetRed", "CarpetTrim");
            Carpet("Carpet_South", carpet, new Vector3(0f, 0f, -22.5f), 6f, 31f, "CarpetRed", "CarpetTrim");

            // Rune altar: glowing ring, light pillar (r 0.8, 10 m) with rising motes, and the big crystal
            Transform altar = Group("Altar_Light_Pillar").transform;
            Prop("Hall_AltarRing", "Rune_Ring", altar, new Vector3(0f, 0.02f, 0f), 0f, pb =>
            {
                pb.Ring("Cyan", Vector3.zero, 7f, 0.3f, 0.03f, 48);
                pb.Ring("Cyan", Vector3.zero, 6.3f, 0.1f, 0.03f, 44);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    pb.Octa("Cyan", new Vector3(Mathf.Cos(a) * 6.65f, 0.02f, Mathf.Sin(a) * 6.65f), new Vector3(0.45f, 0.04f, 0.45f));
                }
            }, 1f, Col.None, false);
            Beam("Light_Pillar", altar, new Vector3(0f, 0f, 0f), 0.8f, 10f, "CyanBeam");
            Particles("Rising_Motes", altar, new Vector3(0f, 1f, 0f), new Vector3(1.4f, 0.5f, 1.4f), 40, 5f, 0.05f, 0.05f, 0.12f, Hex("aaf4ff"), 1.6f);
            GameObject crystal = Prop("Hall_RuneCrystal", "Rune_Crystal_Large", altar, new Vector3(0f, 3.6f, 0f), 0f, pb =>
            {
                pb.Octa("Cyan", Vector3.zero, new Vector3(1.1f, 2.6f, 1.1f));
            }, 1f, Col.None);
            Float(crystal, 0.2f, 1.2f, 30f);
            PointLight("Crystal_Glow", altar, new Vector3(0f, 4f, 0f), Hex("5ad8ff"), 14f, 2.2f);

            // Standing torches between the pillars, plus unlit wall sconces around the hall
            Transform torches = Group("Torches").transform;
            foreach (Vector3 p in new[] { new Vector3(-12f, 0f, 25f), new Vector3(12f, 0f, 25f), new Vector3(-12f, 0f, -25f), new Vector3(12f, 0f, -25f) })
            {
                StandingTorch("Standing_Torch", torches, p, false);
                PointLight("Torch_Light", torches, p + new Vector3(0f, 2.2f, 0f), Hex("ffb347"), 9f, 1.4f, true);
            }
            foreach (float z in new[] { -30f, -10f, 6f, 34f }) Sconce("Wall_Torch_E", torches, new Vector3(38.95f, 3.2f, z), 90f, "Flame", null);
            foreach (float z in new[] { -22f, -6f, 6f, 22f }) Sconce("Wall_Torch_W", torches, new Vector3(-38.95f, 3.2f, z), -90f, "Flame", null);
            foreach (float x in new[] { -28f, -15f, 15f, 28f })
            {
                Sconce("Wall_Torch_N", torches, new Vector3(x, 3.2f, 38.95f), 0f, "Flame", null);
                Sconce("Wall_Torch_S", torches, new Vector3(x, 3.2f, -38.95f), 180f, "Flame", null);
            }

            // Tall windows on the west wall (3.5 x 6, centre y 4.5) with a shaft of morning light falling east
            Transform windows = Group("West_Windows").transform;
            foreach (float z in new[] { 30f, 14f, -14f, -30f })
            {
                Prop("Hall_Window", "Tall_Window", windows, new Vector3(-38.9f, 0f, z), -90f, pb =>
                {
                    pb.Box("StoneLight", new Vector3(-1.95f, 4.5f, -0.1f), new Vector3(0.4f, 6.4f, 0.4f));
                    pb.Box("StoneLight", new Vector3(1.95f, 4.5f, -0.1f), new Vector3(0.4f, 6.4f, 0.4f));
                    pb.Box("StoneLight", new Vector3(0, 1.4f, -0.15f), new Vector3(4.3f, 0.3f, 0.5f));
                    pb.Box("WindowWarm", new Vector3(0, 4.2f, -0.04f), new Vector3(3.5f, 5.4f, 0.04f));
                    pb.Box("WindowWarm", new Vector3(0, 6.9f, -0.04f), new Vector3(2.47f, 2.47f, 0.04f), new Vector3(0, 0, 45));
                    pb.Box("Iron", new Vector3(0, 4.5f, -0.07f), new Vector3(0.08f, 6f, 0.04f));
                    pb.Box("Iron", new Vector3(0, 3.4f, -0.07f), new Vector3(3.5f, 0.08f, 0.04f));
                    pb.Box("Iron", new Vector3(0, 5.4f, -0.07f), new Vector3(3.5f, 0.08f, 0.04f));
                    // light shaft: a thin translucent slab from the window down to the floor about 7 m east
                    pb.Box("GoldBeam", new Vector3(0, 3.2f, -4.2f), new Vector3(3.2f, 0.05f, 9.6f), new Vector3(-36, 0, 0));
                }, 1f, Col.None, false);
            }
            Particles("Window_Dust", windows, new Vector3(-33f, 3f, 0f), new Vector3(8f, 4f, 64f), 80, 8f, 0.1f, 0.04f, 0.09f, Hex("fff0c8"), 0f);

            // Rest area (north-west): rug, two bedrolls, a barrel, a crate and a low table
            Transform rest = Group("Rest_Area").transform;
            Prop("Hall_RestArea", "Rug_And_Bedrolls", rest, new Vector3(-25f, 0f, 26f), 0f, pb =>
            {
                pb.Box("CarpetDark", new Vector3(0, 0.015f, 0), new Vector3(10f, 0.03f, 6f));
                pb.Box("Gold", new Vector3(0, 0.032f, 2.7f), new Vector3(9.4f, 0.01f, 0.12f));
                pb.Box("Gold", new Vector3(0, 0.032f, -2.7f), new Vector3(9.4f, 0.01f, 0.12f));
                foreach (float x in new[] { -3f, -0.6f })
                {
                    pb.Box("Cloth", new Vector3(x, 0.12f, 0.6f), new Vector3(1.3f, 0.22f, 2.6f));
                    pb.Box("BannerBlue", new Vector3(x, 0.16f, 0.1f), new Vector3(1.32f, 0.24f, 1.6f));
                    pb.Box("Cloth", new Vector3(x, 0.3f, 1.65f), new Vector3(0.9f, 0.22f, 0.5f));
                }
                // low table with a jug and bread
                pb.Box("Wood", new Vector3(2.4f, 0.45f, 0.4f), new Vector3(1.8f, 0.1f, 1.1f));
                foreach (float x in new[] { 1.65f, 3.15f }) foreach (float z in new[] { -0.05f, 0.85f }) pb.Box("WoodDark", new Vector3(x, 0.2f, z), new Vector3(0.1f, 0.4f, 0.1f));
                pb.Prism("Iron", new Vector3(2.1f, 0.5f, 0.4f), 0.13f, 0.32f, 6);
                pb.Box("Hay", new Vector3(2.75f, 0.56f, 0.5f), new Vector3(0.45f, 0.14f, 0.28f), new Vector3(0, 20, 0));
            }, 1f, Col.None, false);
            Place("Barrel", rest, new Vector3(-29f, 0f, 28.2f), 0f, 0.8f);
            Place("Box_2", rest, new Vector3(-21.4f, 0f, 28.3f), 15f, 0.8f);

            // Banquet table (moved west to x -28.5 so it clears the inner column at (-18, -18))
            Transform feast = Group("Banquet_Table").transform;
            Prop("Hall_BanquetTable", "Banquet_Table", feast, new Vector3(-28.5f, 0f, -18f), 0f, pb =>
            {
                pb.Box("Wood", new Vector3(0, 0.85f, 0), new Vector3(13f, 0.12f, 2.2f));
                foreach (float x in new[] { -5.6f, 0f, 5.6f })
                {
                    pb.Box("WoodDark", new Vector3(x, 0.4f, 0), new Vector3(0.25f, 0.8f, 1.5f));
                    pb.Box("WoodDark", new Vector3(x, 0.08f, 0), new Vector3(0.5f, 0.16f, 1.9f));
                }
                foreach (float z in new[] { -1.65f, 1.65f })
                {
                    pb.Box("Wood", new Vector3(0, 0.45f, z), new Vector3(13f, 0.1f, 0.6f));
                    foreach (float x in new[] { -5.6f, 0f, 5.6f }) pb.Box("WoodDark", new Vector3(x, 0.2f, z), new Vector3(0.2f, 0.4f, 0.45f));
                }
                for (int i = 0; i < 10; i++)
                {
                    float x = -5.4f + (i / 2) * 2.7f;
                    float z = i % 2 == 0 ? -0.6f : 0.6f;
                    pb.Prism("Iron", new Vector3(x, 0.91f, z), 0.22f, 0.03f);
                    pb.Prism("Gold", new Vector3(x + 0.4f, 0.91f, z * 0.8f), 0.07f, 0.22f, 6);
                }
                for (int i = 0; i < 4; i++) pb.Box("Hay", new Vector3(-4.2f + i * 2.8f, 0.98f, 0), new Vector3(0.5f, 0.2f, 0.3f), new Vector3(0, i * 37f, 0));
                foreach (float x in new[] { -2.7f, 2.7f })
                {
                    pb.Prism("Iron", new Vector3(x + 0.6f, 0.91f, 0.1f), 0.14f, 0.4f, 6); // jug
                    pb.Prism("Gold", new Vector3(x, 0.91f, 0), 0.12f, 0.5f, 6);
                    pb.Box("Gold", new Vector3(x, 1.38f, 0), new Vector3(0.7f, 0.05f, 0.05f));
                    foreach (float o in new[] { -0.33f, 0f, 0.33f })
                    {
                        pb.Prism("Candle", new Vector3(x + o, 1.4f, 0), 0.04f, 0.22f, 6);
                        pb.Cone("Flame", new Vector3(x + o, 1.62f, 0), 0.04f, 0.1f);
                    }
                }
            });
            PlaceVisual("Village/Village_Pumpkin", feast, new Vector3(-29.9f, 0.91f, -17.9f), 30f, 0.8f);
            PlaceVisual("Village/Village_Cabbage", feast, new Vector3(-25.6f, 0.91f, -18.2f), 0f, 0.7f);
            PointLight("Banquet_Candles", feast, new Vector3(-28.5f, 2.4f, -18f), new Color(1f, 0.78f, 0.48f), 10f, 1.2f, true);

            // Stone fireplace on the east wall: 3 deep, 5 high, 10 long, opening west, with a banner above
            Transform fire = Group("Fireplace").transform;
            Prop("Hall_Fireplace", "Fireplace", fire, new Vector3(39f, 0f, 24f), 90f, pb =>
            {
                pb.Box("Stone", new Vector3(-4.1f, 1.75f, -1.5f), new Vector3(1.8f, 3.5f, 3f));
                pb.Box("Stone", new Vector3(4.1f, 1.75f, -1.5f), new Vector3(1.8f, 3.5f, 3f));
                pb.Box("StoneLight", new Vector3(0, 3.8f, -1.55f), new Vector3(10f, 0.6f, 3.1f));
                pb.Box("StoneDark", new Vector3(0, 4.55f, -1.1f), new Vector3(8f, 0.9f, 2.2f));
                pb.Box("Black", new Vector3(0, 1.75f, -0.35f), new Vector3(6.4f, 3.5f, 0.7f));
                pb.Box("StoneDark", new Vector3(0, 0.08f, -1.6f), new Vector3(9.6f, 0.16f, 3f));
                pb.Box("Wood", new Vector3(0, 0.35f, -1.3f), new Vector3(2.6f, 0.3f, 0.3f), new Vector3(0, 15, 0));
                pb.Box("Wood", new Vector3(0.1f, 0.5f, -1.25f), new Vector3(2.4f, 0.28f, 0.28f), new Vector3(0, -20, 0));
                pb.Prism("Coals", new Vector3(0, 0.16f, -1.3f), 1.1f, 0.12f, 6);
                pb.Cone("Flame", new Vector3(0, 0.4f, -1.3f), 0.65f, 1.6f);
                pb.Cone("Flame", new Vector3(-0.7f, 0.4f, -1.35f), 0.4f, 1.1f, new Vector3(0, 0, 8));
                pb.Cone("Flame", new Vector3(0.75f, 0.4f, -1.2f), 0.42f, 1.2f, new Vector3(0, 0, -8));
            });
            HallBanner("Fireplace_Banner", fire, new Vector3(38.8f, 7.6f, 24f), 90f, 1.8f, 2.6f);
            PointLight("Fireplace_Fire", fire, new Vector3(36f, 1.2f, 24f), Hex("ff9a3a"), 12f, 2f, true);
        }

        /// <summary>Blue hall banner with a gold emblem; origin at the hanging rod, front faces local -Z.</summary>
        private static GameObject HallBanner(string name, Transform parent, Vector3 top, float yaw, float width, float height)
        {
            string mesh = "HallBanner_" + width.ToString("0.#") + "x" + height.ToString("0.#");
            return Prop(mesh, name, parent, top, yaw, pb =>
            {
                pb.Box("Iron", Vector3.zero, new Vector3(width + 0.3f, 0.08f, 0.08f));
                pb.Box("BannerBlue", new Vector3(0, -height * 0.45f, -0.08f), new Vector3(width, height * 0.9f, 0.05f));
                float tip = width * 0.5f / Mathf.Sqrt(2f) * 2f;
                pb.Box("BannerBlue", new Vector3(0, -height * 0.9f, -0.08f), new Vector3(tip, tip, 0.05f), new Vector3(0, 0, 45));
                pb.Box("BannerGold", new Vector3(0, -height * 0.05f, -0.1f), new Vector3(width, 0.12f, 0.03f));
                pb.Octa("BannerGold", new Vector3(0, -height * 0.45f, -0.12f), new Vector3(width * 0.45f, width * 0.6f, 0.06f));
            }, 1f, Col.None);
        }

        #endregion

        #region Zone 6: Tower

        private static void DressTower()
        {
            SetFloor("Tower_Floor", "Tower");
            // Mood B: treasure chamber, dark stone and one warm shaft of light from the roof window
            Mood(new Color(1f, 0.82f, 0.58f), 0.35f, new Vector3(60f, -40f, 0f),
                new Color(0.36f, 0.31f, 0.26f), new Color(0.26f, 0.22f, 0.18f), new Color(0.10f, 0.08f, 0.06f),
                new Color(0.08f, 0.07f, 0.06f), 30f, 80f, new Color(0.07f, 0.06f, 0.05f));
            SetLight("TowerLight_Center", new Color(0.55f, 0.85f, 1f), 1.0f); // cool fill under the warm beam

            // Fix: the four diagonal walls ran radially (corner gaps to the floor's edge); turn them to close the corners
            SetYaw("Wall_NE", -45f);
            SetYaw("Wall_NW", 45f);
            SetYaw("Wall_SE", 45f);
            SetYaw("Wall_SW", -45f);

            // Gold piles along the north wall (one static mesh, no colliders, no pickups)
            Transform hoard = Group("Gold_Hoard").transform;
            Prop("Tower_GoldHoard", "Gold_Piles", hoard, new Vector3(0f, 0f, 11.4f), 0f, pb =>
            {
                var mounds = new[] { new Vector4(-3.8f, 0.1f, 7.6f, 3.4f), new Vector4(3.8f, 0.1f, 7.6f, 3.4f), new Vector4(0f, 0.6f, 5.6f, 3.4f) };
                foreach (Vector4 m in mounds) pb.Sphere("Gold", new Vector3(m.x, -0.3f, m.y), new Vector3(m.z, 2.1f, m.w));
                for (int i = 0; i < 90; i++)
                {
                    Vector4 m = mounds[i % 3];
                    float dx = Rand(-0.45f, 0.45f), dz = Rand(-0.45f, 0.45f);
                    float k = 1f - (dx * dx + dz * dz) / 0.2025f;
                    if (k <= 0.05f) continue;
                    float y = -0.3f + 1.05f * Mathf.Sqrt(k);
                    pb.Prism("GoldGlow", new Vector3(m.x + dx * m.z, y, m.y + dz * m.w), 0.14f, 0.03f, 8, new Vector3(Rand(-25f, 25f), 0, Rand(-25f, 25f)));
                }
                pb.Prism("Gold", new Vector3(-1.3f, 0.55f, -0.4f), 0.12f, 0.35f, 6);
                pb.Prism("Gold", new Vector3(1.8f, 0.55f, -0.2f), 0.12f, 0.35f, 6);
                pb.Ring("Gold", new Vector3(0.3f, 0.7f, 0.6f), 0.32f, 0.08f, 0.18f, 10);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    pb.Cone("GoldGlow", new Vector3(0.3f + Mathf.Cos(a) * 0.3f, 0.88f, 0.6f + Mathf.Sin(a) * 0.3f), 0.07f, 0.22f);
                }
            }, 1f, Col.None);
            PlaceVisual("Chest", hoard, new Vector3(-6.4f, 0f, 9.6f), 150f, 0.7f);
            PlaceVisual("Chest", hoard, new Vector3(6.4f, 0f, 9.6f), 210f, 0.7f);
            PointLight("Gold_Glow", hoard, new Vector3(0f, 1.5f, 11f), Hex("ffcc4a"), 8f, 1.2f);

            // Potion shelves on the west and east walls: 1.2 x 3 x 8, two shelves of six bottles
            Transform potions = Group("Potion_Shelves").transform;
            string[] potionMats = { "PotionRed", "PotionBlue", "PotionGreen", "PotionPurple" };
            foreach (float side in new[] { -1f, 1f })
            {
                int seed = side < 0 ? 11 : 23;
                Prop("Tower_PotionShelf_" + seed, "Potion_Shelf", potions, new Vector3(side * 13.4f, 0f, 2f), side < 0 ? -90f : 90f, pb =>
                {
                    var r = new System.Random(seed);
                    pb.Box("WoodDark", new Vector3(0, 1.5f, 0.55f), new Vector3(8f, 3f, 0.1f));
                    foreach (float x in new[] { -3.95f, 3.95f }) pb.Box("WoodDark", new Vector3(x, 1.5f, 0), new Vector3(0.12f, 3f, 1.2f));
                    pb.Box("WoodDark", new Vector3(0, 2.95f, 0), new Vector3(8f, 0.1f, 1.2f));
                    pb.Box("Wood", new Vector3(0, 0.15f, 0), new Vector3(7.8f, 0.3f, 1.2f));
                    foreach (float y in new[] { 1.0f, 2.0f })
                    {
                        pb.Box("Wood", new Vector3(0, y, 0), new Vector3(7.8f, 0.07f, 1.2f));
                        for (int i = 0; i < 6; i++)
                        {
                            float x = -3.1f + i * 1.24f + (float)(r.NextDouble() - 0.5) * 0.2f;
                            string pm = potionMats[(i + (y > 1.5f ? 2 : 0)) % potionMats.Length];
                            float h = 0.28f + (float)r.NextDouble() * 0.16f;
                            pb.Prism(pm, new Vector3(x, y + 0.035f, -0.1f), 0.15f, h, 6);
                            pb.Prism(pm, new Vector3(x, y + 0.035f + h, -0.1f), 0.05f, 0.13f, 6);
                            pb.Prism("Wood", new Vector3(x, y + 0.165f + h, -0.1f), 0.055f, 0.06f, 6);
                        }
                    }
                });
            }

            // Skylight: a cone of light (r 2.6, 9.5 m) and a spot straight down onto the pedestal, with dust in the beam
            Transform shaft = Group("Skylight").transform;
            Prop("Tower_SkylightBeam", "Skylight_Beam", shaft, new Vector3(0f, 0f, 4f), 0f, pb =>
            {
                pb.Cone("GoldBeam", Vector3.zero, 2.6f, 9.5f);
            }, 1f, Col.None, false).GetComponent<MeshRenderer>().receiveShadows = false;
            var spotGo = new GameObject("Skylight_Spot");
            spotGo.transform.SetParent(shaft, false);
            spotGo.transform.position = new Vector3(0f, 9.5f, 4f);
            spotGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = Hex("fff0c0");
            spot.range = 14f;
            spot.spotAngle = 35f;
            spot.intensity = 3f;
            spot.shadows = LightShadows.None;
            Particles("Beam_Dust", shaft, new Vector3(0f, 4f, 4f), new Vector3(3.2f, 7f, 3.2f), 90, 7f, 0.08f, 0.03f, 0.08f, Hex("fff0c0"), 0.15f);

            // The Giant's Elixir pedestal moves into the beam, and Othelia's signet ring moves from the chest onto it
            GameObject elixir = Find("GiantElixir_Pedestal");
            if (elixir != null) elixir.transform.position = new Vector3(0f, 0.5f, 4f);
            Prop("Tower_Plinth", "Pedestal_Base", shaft, new Vector3(0f, 0f, 4f), 22.5f, pb =>
            {
                pb.Prism("StoneLight", Vector3.zero, 1.35f, 0.12f);
                pb.Ring("Gold", new Vector3(0, 0.12f, 0), 1.2f, 0.08f, 0.02f, 16);
            }, 1f, Col.None);
            PlaceSignetRing(new Vector3(0f, 1.15f, 4.5f));

            // Spiral stairs in the south-west (r 2.2, 6 m, decorative)
            Transform stairs = Group("Spiral_Stairs").transform;
            Prop("Tower_SpiralStairs", "Spiral_Stairs", stairs, new Vector3(-6.5f, 0f, -6.5f), 0f, pb =>
            {
                pb.Prism("Stone", Vector3.zero, 0.35f, 6f);
                for (int i = 0; i < 14; i++)
                {
                    float a = i * 26f * Mathf.Deg2Rad;
                    float y = 0.2f + i * 0.41f;
                    pb.Box(i % 2 == 0 ? "Stone" : "StoneLight", new Vector3(Mathf.Cos(a) * 1.2f, y, Mathf.Sin(a) * 1.2f), new Vector3(1.9f, 0.2f, 0.8f), new Vector3(0, -i * 26f, 0));
                    pb.Box("Iron", new Vector3(Mathf.Cos(a) * 2.1f, y + 0.55f, Mathf.Sin(a) * 2.1f), new Vector3(0.06f, 1.1f, 0.06f));
                }
            });

            // Two wall torches on the diagonal walls by the door
            Transform torches = Group("Torches").transform;
            Color warm = new Color(1f, 0.66f, 0.32f);
            Sconce("Wall_Torch_SW", torches, new Vector3(-9.9f, 3f, -9.9f), -135f, "Flame", warm);
            Sconce("Wall_Torch_SE", torches, new Vector3(9.9f, 3f, -9.9f), 135f, "Flame", warm);
        }

        private static void SetYaw(string name, float yaw)
        {
            GameObject go = Find(name);
            if (go == null) { Debug.LogWarning("[ZoneDressingBuilder] Not found: " + name); return; }
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Othelia's signet ring as a pickup on the pedestal; the tower chest keeps its 50 gold.</summary>
        private static void PlaceSignetRing(Vector3 pos)
        {
            ItemSO ring = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SignetRing.asset");
            GameObject chest = Find("Tower_Reward_Chest");
            if (chest != null)
            {
                ChestRewardInteraction c = chest.GetComponent<ChestRewardInteraction>();
                if (c != null) { c.ItemReward = null; EditorUtility.SetDirty(c); }
            }

            GameObject old = Find("SignetRing_Pickup");
            if (old != null) Object.DestroyImmediate(old);
            GameObject pickup = Prop("Tower_SignetRing", "SignetRing_Pickup", null, pos, 30f, pb =>
            {
                pb.Ring("GoldGlow", Vector3.zero, 0.13f, 0.05f, 0.05f, 12);
                pb.Octa("GemGreen", new Vector3(0f, 0.08f, -0.14f), new Vector3(0.1f, 0.13f, 0.1f));
            }, 1.4f, Col.None);
            pickup.transform.SetParent(null, true);
            ChestRewardInteraction reward = pickup.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = 0;
            reward.ItemReward = ring;
            reward.PromptMessage = "Take Othelia's Signet Ring";
            reward.HideWhenLooted = true;
            reward.EnsureChestCollider();
            PointLight("SignetRing_Glow", pickup.transform, pos + new Vector3(0f, 0.5f, 0f), Hex("8affb0"), 3f, 0.8f);
        }

        #endregion

        #region Zone 7: Throne Room

        private static void DressThroneRoom()
        {
            SetFloor("ThroneRoom_Floor", "Throne");
            // Phase 1 (A): petrified court in cold turquoise light; phase 2 (B) is switched on by LavaCracksReveal
            Mood(Hex("9fc8c0"), 0.8f, new Vector3(50f, -30f, 0f),
                Hex("2a3436"), Color.Lerp(Hex("2a3436"), Hex("1a2224"), 0.5f), Hex("1a2224"),
                Hex("9ac8c0"), 30f, 120f, new Color(0.05f, 0.08f, 0.09f));
            SetLight("CrownLight_Center", new Color(0.45f, 0.92f, 0.86f), 2.4f);
            SetLight("CrownLight_Throne", Hex("6af0d0"), 2.4f);
            Particles("Court_Dust", root, new Vector3(0f, 3f, -5f), new Vector3(40f, 5f, 80f), 120, 9f, 0.1f, 0.04f, 0.09f, Hex("d8fff4"), 0f);

            // Two rows of hall columns
            Transform pillars = Group("Pillar_Rows").transform;
            foreach (float x in new[] { -30f, 30f })
            {
                foreach (float z in new[] { 35f, 20f, 5f, -10f, -25f, -40f })
                {
                    Place("Pillar", pillars, new Vector3(x, 0f, z), 0f, new Vector3(1.6f, 1.8f, 1.6f));
                }
            }

            // Red carpet from the door to the front of the dais (z -48..27)
            Carpet("Carpet", Group("Carpet").transform, new Vector3(0f, 0f, -10.5f), 6f, 75f, "CarpetRed", "CarpetTrim");

            // Petrified knights lining the carpet, facing it; alternating helmet-and-shield and hooded poses
            Transform knights = Group("Petrified_Knights").transform;
            int k = 0;
            foreach (float z in new[] { -38f, -28f, -18f, -8f })
            {
                StoneKnight(knights, new Vector3(-14f, 0f, z), -90f, k++ % 2);
                StoneKnight(knights, new Vector3(14f, 0f, z), 90f, k % 2);
            }

            // High-backed stone throne at the back of the dais, facing south
            // (z 36 rather than 35.6, so it clears the royal chest at z 34 that appears after the fight)
            Transform throne = Group("Throne").transform;
            Prop("Throne_Seat", "Throne", throne, new Vector3(0f, 1.2f, 36f), 0f, pb =>
            {
                pb.Box("StoneStatue", new Vector3(0, 0.45f, 0), new Vector3(3.2f, 0.9f, 2.2f));
                pb.Box("CarpetRed", new Vector3(0, 0.95f, -0.1f), new Vector3(2.3f, 0.12f, 1.6f));
                pb.Box("StoneStatue", new Vector3(0, 2.5f, 0.85f), new Vector3(3.2f, 4.0f, 0.5f));
                pb.Box("CarpetRed", new Vector3(0, 2.3f, 0.58f), new Vector3(2.2f, 2.8f, 0.06f));
                foreach (float x in new[] { -1.8f, 1.8f })
                {
                    pb.Box("StoneStatue", new Vector3(x, 0.8f, 0), new Vector3(0.4f, 1.6f, 2.2f));
                    pb.Octa("Gold", new Vector3(x, 1.75f, -0.95f), new Vector3(0.35f, 0.35f, 0.35f));
                }
                for (int i = -2; i <= 2; i++) pb.Cone("Gold", new Vector3(i * 0.6f, 4.55f, 0.85f), 0.2f, i == 0 ? 0.45f : 0.3f);
                pb.Box("Gold", new Vector3(0, 4.5f, 0.85f), new Vector3(3.3f, 0.15f, 0.55f));
            });

            // Teal wall torches along the side walls
            Transform torches = Group("Torches").transform;
            Color teal = new Color(0.4f, 1f, 0.9f);
            foreach (float z in new[] { -35f, -17.5f, 0f, 17.5f, 35f })
            {
                Sconce("Wall_Torch_W", torches, new Vector3(-48.95f, 4f, z), -90f, "FlameTeal", (z == -17.5f || z == 17.5f) ? teal : (Color?)null);
                Sconce("Wall_Torch_E", torches, new Vector3(48.95f, 4f, z), 90f, "FlameTeal", (z == -17.5f || z == 17.5f) ? teal : (Color?)null);
            }

            // Phase 2: lava cracks, ember sparks, lava light and the red-orange sun, switched on when the golem turns to stone
            var revealGo = Group("Lava_Cracks");
            var cracks = Group("Cracks_Phase2", revealGo.transform);
            var lines = new List<List<Vector3>>
            {
                new List<Vector3> { new Vector3(-6f, 0, 6f), new Vector3(-3f, 0, 9f), new Vector3(-4f, 0, 13f), new Vector3(0f, 0, 16f) },
                new List<Vector3> { new Vector3(5f, 0, 4f), new Vector3(8f, 0, 8f), new Vector3(6f, 0, 12f) },
                new List<Vector3> { new Vector3(-8f, 0, 16f), new Vector3(-4f, 0, 19f), new Vector3(2f, 0, 18f) },
                new List<Vector3> { new Vector3(3f, 0, 14f), new Vector3(7f, 0, 17f), new Vector3(9f, 0, 20f) }
            };
            for (int i = 0; i < lines.Count; i++)
            {
                Ribbon("Crack_Edge_" + i, cracks.transform, lines[i], 0.9f, 0.03f, "Charred", 0.45f);
                Ribbon("Lava_Crack_" + i, cracks.transform, lines[i], 0.4f, 0.04f, "Lava", 0.25f);
            }
            PointLight("Lava_Glow_W", cracks.transform, new Vector3(-4f, 1f, 12f), Hex("ff9a3a"), 8f, 2.2f, true);
            PointLight("Lava_Glow_E", cracks.transform, new Vector3(6.5f, 1f, 11f), Hex("ff9a3a"), 8f, 2.2f, true);
            Particles("Lava_Sparks", cracks.transform, new Vector3(0f, 0.5f, 12f), new Vector3(18f, 0.5f, 16f), 60, 2.5f, 0.4f, 0.05f, 0.12f, Hex("ffa04a"), 1.2f);
            var reveal = revealGo.AddComponent<LavaCracksReveal>();
            reveal.cracks = cracks;
            GameObject center = Find("CrownLight_Center");
            reveal.centerLight = center != null ? center.GetComponent<Light>() : null;
            reveal.centerPhase2Color = Hex("ff5a1a");
            reveal.centerPhase2Intensity = 3f;
            GameObject sun = Find("Directional Light");
            reveal.sun = sun != null ? sun.GetComponent<Light>() : null;
            reveal.sunPhase2Color = new Color(1f, 0.40f, 0.20f);
            reveal.sunPhase2Intensity = 1.3f;
            cracks.SetActive(false);
        }

        private static void StoneKnight(Transform parent, Vector3 pos, float yaw, int variant)
        {
            // Plinth 2 x 0.5 x 2 and a grey stone statue about 1.2 x 3.2 x 1.2 (3.7 m in all)
            Prop("PetrifiedKnight_" + variant, "Petrified_Knight", parent, pos, yaw, pb =>
            {
                pb.Box("StoneDark", new Vector3(0, 0.25f, 0), new Vector3(2f, 0.5f, 2f));
                pb.Box("StoneStatue", new Vector3(-0.2f, 1.05f, 0), new Vector3(0.32f, 1.1f, 0.38f));
                pb.Box("StoneStatue", new Vector3(0.2f, 1.05f, 0), new Vector3(0.32f, 1.1f, 0.38f));
                pb.Box("StoneStatue", new Vector3(0, 2.1f, 0), new Vector3(0.95f, 1.05f, 0.58f));
                pb.Box("StoneDark", new Vector3(0, 1.6f, 0), new Vector3(0.98f, 0.12f, 0.6f));
                pb.Box("StoneStatue", new Vector3(-0.58f, 2.0f, -0.1f), new Vector3(0.24f, 0.85f, 0.28f));
                pb.Box("StoneStatue", new Vector3(0.58f, 2.0f, -0.1f), new Vector3(0.24f, 0.85f, 0.28f));
                if (variant == 0)
                {
                    // helmet, pauldrons, kite shield and a planted sword
                    pb.Box("StoneStatue", new Vector3(-0.58f, 2.55f, 0), new Vector3(0.42f, 0.28f, 0.66f));
                    pb.Box("StoneStatue", new Vector3(0.58f, 2.55f, 0), new Vector3(0.42f, 0.28f, 0.66f));
                    pb.Box("StoneStatue", new Vector3(0, 2.95f, 0), new Vector3(0.52f, 0.58f, 0.54f));
                    pb.Box("Black", new Vector3(0, 2.98f, -0.275f), new Vector3(0.42f, 0.07f, 0.02f));
                    pb.Cone("StoneStatue", new Vector3(0, 3.24f, 0), 0.28f, 0.3f);
                    pb.Box("StoneStatue", new Vector3(-0.8f, 1.85f, -0.32f), new Vector3(0.1f, 1.05f, 0.75f));
                    pb.Box("StoneStatue", new Vector3(0, 1.1f, -0.52f), new Vector3(0.1f, 1.5f, 0.04f));
                    pb.Box("StoneStatue", new Vector3(0, 1.9f, -0.52f), new Vector3(0.45f, 0.08f, 0.08f));
                    pb.Box("StoneDark", new Vector3(0, 2.1f, -0.52f), new Vector3(0.08f, 0.3f, 0.08f));
                }
                else
                {
                    // hooded figure with a cloak and a spear
                    pb.Cone("StoneStatue", new Vector3(0, 2.6f, 0.02f), 0.42f, 1.1f);
                    pb.Box("Black", new Vector3(0, 2.92f, -0.2f), new Vector3(0.28f, 0.3f, 0.02f));
                    pb.Box("StoneStatue", new Vector3(0, 1.6f, 0.22f), new Vector3(1.05f, 2.1f, 0.2f), new Vector3(-6, 0, 0));
                    pb.Box("StoneStatue", new Vector3(0.62f, 1.9f, -0.3f), new Vector3(0.07f, 3.4f, 0.07f));
                    pb.Cone("StoneStatue", new Vector3(0.62f, 3.6f, -0.3f), 0.09f, 0.35f);
                }
            });
        }

        #endregion

        #region Screenshots

        /// <summary>Batch-mode review screenshots: an overview and two gameplay-angle views per zone.</summary>
        public static void BatchScreenshots()
        {
            Directory.CreateDirectory("Screenshots/Zones");
            Shots("Zone_2_ForestPath", "forest", new Vector3(0f, 80f, -82f), Vector3.zero, new[] { new Vector3(0f, 0f, -36f), new Vector3(-13f, 0f, 9f) });
            Shots("Zone_3_CastleCourtyard", "courtyard", new Vector3(0f, 78f, -74f), new Vector3(0f, 0f, 2f), new[] { new Vector3(0f, 0f, -30f), new Vector3(0f, 0f, 30f) });
            Shots("Zone_4_Library", "library", new Vector3(0f, 78f, -70f), new Vector3(0f, 0f, 2f), new[] { new Vector3(0f, 0f, 13f), new Vector3(-30f, 0f, 28f) });
            Shots("Zone_5_CastleHall", "hall", new Vector3(0f, 68f, -62f), new Vector3(0f, 0f, 2f), new[] { new Vector3(0f, 0f, -12f), new Vector3(-24f, 0f, -14f) });
            Shots("Zone_6_Tower", "tower", new Vector3(0f, 30f, -27f), new Vector3(0f, 0f, 1f), new[] { new Vector3(0f, 0f, 5f), new Vector3(-5f, 0f, -3f) });
            Shots("Zone_7_ThroneRoom", "throne", new Vector3(0f, 82f, -80f), new Vector3(0f, 0f, 2f), new[] { new Vector3(0f, 0f, -26f), new Vector3(0f, 0f, 28f) }, true);
        }

        private static void Shots(string sceneName, string tag, Vector3 overviewPos, Vector3 overviewLook, Vector3[] targets, bool showLava = false)
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            var camGo = new GameObject("ScreenshotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.farClipPlane = 600f;
            GameObject main = Find("Main Camera");
            Camera mc = main != null ? main.GetComponent<Camera>() : null;
            if (mc != null) { cam.clearFlags = mc.clearFlags; cam.backgroundColor = mc.backgroundColor; }
            if (main != null) main.SetActive(false);

            // The overview camera sits far beyond the gameplay fog range; leave fog out of that one shot
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            Shot(cam, overviewPos, overviewLook, "Screenshots/Zones/" + tag + "_overview.png", 1920, 1080);
            RenderSettings.fog = fog;
            for (int i = 0; i < targets.Length; i++)
            {
                // Same offset as the in-game CameraFollow (-6, 12, -12)
                Shot(cam, targets[i] + new Vector3(-6f, 12f, -12f), targets[i] + Vector3.up, "Screenshots/Zones/" + tag + "_view" + (i + 1) + ".png", 1600, 900);
            }
            if (showLava)
            {
                GameObject cracks = Find("Cracks_Phase2");
                if (cracks != null)
                {
                    cracks.SetActive(true);
                    Shot(cam, new Vector3(-6f, 16f, -4f), new Vector3(0f, 0f, 14f), "Screenshots/Zones/" + tag + "_phase2_cracks.png", 1600, 900);
                }
            }
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
