using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Castle Hall walls (2026-10-07): 16 m tall so the camera cannot see over them, with an even, upright
    /// stone-block texture instead of the stretched Ruined_walls model atlas.
    /// <list type="bullet">
    /// <item>Generates a tileable ashlar texture and normal map (4 m per repeat, 0.8 m courses) and the
    /// M_Hall_Wall_Stone material.</item>
    /// <item>Raises the hall walls and every gate's wall fill to <see cref="WallHeight"/>; above each wooden
    /// door a ledge for the braziers and a wall piece up to the top close the gap, and the exit sign hangs
    /// on the wall in front of it (Sign_Anchor).</item>
    /// <item>Gives every wall renderer a mesh with world-space UVs, so the stone runs on at the same size and
    /// upright across all wall pieces.</item>
    /// </list>
    /// Run after <see cref="HallDoorsSetup"/> (it rebuilds the Tower stairs, whose wall piece gets new UVs here).
    /// </summary>
    public static class HallWallsSetup
    {
        public const float WallHeight = 16f;
        public const float TileMetres = 4f;
        public const string TextureFolder = "Assets/Textures/Walls";
        public const string AlbedoPath = TextureFolder + "/Hall_Wall_Stone.png";
        public const string NormalPath = TextureFolder + "/Hall_Wall_Stone_Normal.png";
        public const string MaterialPath = "Assets/Materials/Walls/M_Hall_Wall_Stone.mat";
        public const string MeshFolder = "Assets/Models/Walls/Hall";
        public const string LedgeName = "Wall_Ledge";
        public const string TopFillName = "Wall_Top";

        private const string OldWallMaterial = "M_Ruined_walls";
        private const float LedgeTop = 10.4f;
        private const float SignHeight = 11.4f;

        [MenuItem("CastleOfDice/Castle Hall Walls (16 m, even stone)", false, 17)]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
        }

        public static void BatchSetup()
        {
            BuildMaterial();
            HallDoorsSetup.SetupAll();
            SetupAll();
            EditorApplication.Exit(0);
        }

        public static void SetupAll()
        {
            Material stone = BuildMaterial();
            Scene scene = EditorSceneManager.OpenScene(HallDoorsSetup.ScenePath, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Walls") RaiseWalls(root.transform);
                else if (root.name.StartsWith("Door_")) RaiseGate(root.transform, stone);
            }

            // Every wall renderer: the new stone with world-space UVs. Sources are read before the old copies go.
            EnsureFolder(MeshFolder);
            var walls = new List<(MeshRenderer renderer, Mesh source, string name)>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!UsesWallMaterial(r, stone)) continue;
                    Mesh source = SourceMesh(r.GetComponent<MeshFilter>());
                    if (source != null) walls.Add((r, source, $"{root.name}_{r.transform.parent.name}_{r.name}"));
                }
            }
            foreach (string old in Directory.GetFiles(MeshFolder, "*.asset")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));

            var used = new HashSet<string>();
            foreach (var (r, source, baseName) in walls)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (IsWall(mats[i], stone)) mats[i] = stone;
                }
                r.sharedMaterials = mats;
                string name = baseName;
                for (int i = 2; !used.Add(name); i++) name = baseName + "_" + i;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                mf.sharedMesh = WorldUvMesh(source, r.transform, name);
                if (PrefabUtility.IsPartOfPrefabInstance(r))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                }
            }
            int count = walls.Count;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HallWallsSetup] Castle Hall walls at {WallHeight} m, {count} wall meshes re-UV'd.");
        }

        #region Walls and gates

        private static void RaiseWalls(Transform walls)
        {
            foreach (Transform t in walls)
            {
                if (!t.name.StartsWith("Hall_Wall_")) continue;
                Vector3 s = t.localScale;
                Vector3 p = t.localPosition;
                t.localScale = new Vector3(s.x, WallHeight, s.z);
                t.localPosition = new Vector3(p.x, WallHeight * 0.5f, p.z);
            }
        }

        private static void RaiseGate(Transform gate, Material stone)
        {
            Transform fill = gate.Find(CastleDoorSetup.WallFillName);
            if (fill == null) return;
            Transform fillL = fill.Find("Fill_L");
            foreach (Transform cube in fill)
            {
                if (cube.name != "Fill_L" && cube.name != "Fill_R") continue;
                Vector3 s = cube.localScale;
                Vector3 p = cube.localPosition;
                cube.localScale = new Vector3(s.x, WallHeight, s.z);
                cube.localPosition = new Vector3(p.x, WallHeight * 0.5f, p.z);
            }
            foreach (string old in new[] { LedgeName, TopFillName })
            {
                Transform t;
                while ((t = fill.Find(old)) != null) Object.DestroyImmediate(t.gameObject);
            }

            // Only the wooden doors leave a gap above them; the Tower stairs' own wall piece is full height
            Transform door = gate.Find(CastleDoorSetup.DoorModelName);
            if (door == null || door.GetComponent<SwingDoor>() == null || fillL == null) return;

            Bounds frame = LocalBounds(gate, door);
            float inner = Mathf.Abs(fillL.localPosition.x) - fillL.localScale.x * 0.5f;
            float width = inner * 2f + 0.3f;
            float wallFront = fillL.localPosition.z - fillL.localScale.z * 0.5f;
            float wallBack = fillL.localPosition.z + fillL.localScale.z * 0.5f;

            // A ledge set back behind the braziers on the door's pillars, then the wall flush to the top
            const float ledgeFront = 0.55f;
            Cube(fill, LedgeName, stone, new Vector3(0f, (frame.max.y - 0.2f + LedgeTop) * 0.5f, (ledgeFront + wallBack) * 0.5f),
                new Vector3(width, LedgeTop - frame.max.y + 0.2f, wallBack - ledgeFront));
            Cube(fill, TopFillName, stone, new Vector3(0f, (LedgeTop + WallHeight) * 0.5f, (wallFront + wallBack) * 0.5f),
                new Vector3(width, WallHeight - LedgeTop, wallBack - wallFront));

            // The exit sign hangs on the wall above the door instead of floating over its top
            Transform anchor = gate.Find(ZoneExitSign.SignAnchorName);
            if (anchor == null)
            {
                anchor = new GameObject(ZoneExitSign.SignAnchorName).transform;
                anchor.SetParent(gate, false);
            }
            anchor.localPosition = new Vector3(0f, SignHeight, wallFront - 0.35f);
            anchor.localRotation = Quaternion.identity;
        }

        private static void Cube(Transform parent, string name, Material mat, Vector3 localPos, Vector3 size)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.layer = 7; // Obstacle
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Bounds LocalBounds(Transform space, Transform root)
        {
            bool found = false;
            Bounds b = default;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.EndsWith("_Passage")) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!found) { b = new Bounds(p, Vector3.zero); found = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        #endregion

        #region World-space UVs

        private static bool IsWall(Material m, Material stone)
        {
            return m != null && (m == stone || m.name == OldWallMaterial);
        }

        private static bool UsesWallMaterial(Renderer r, Material stone)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (IsWall(m, stone)) return true;
            }
            return false;
        }

        /// <summary>The mesh to re-UV: the original (FBX or built-in cube), not one of our earlier copies.</summary>
        private static Mesh SourceMesh(MeshFilter mf)
        {
            if (mf == null || mf.sharedMesh == null) return null;
            Mesh m = mf.sharedMesh;
            string path = AssetDatabase.GetAssetPath(m);
            if (path.StartsWith(MeshFolder))
            {
                // An earlier copy: the stair wall's collider still holds its FBX mesh; plain pieces are cubes
                MeshCollider mc = mf.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh != null && !AssetDatabase.GetAssetPath(mc.sharedMesh).StartsWith(MeshFolder)) return mc.sharedMesh;
                return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            }
            return m;
        }

        /// <summary>
        /// Copies the mesh with UVs from world position: walls take (along the face, height), tops take (x, z),
        /// one texture repeat per <see cref="TileMetres"/>. Upright on every face, continuous across pieces.
        /// </summary>
        private static Mesh WorldUvMesh(Mesh source, Transform t, string name)
        {
            Mesh mesh = Object.Instantiate(source);
            mesh.name = name;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            var uv = new Vector2[v.Length];
            Matrix4x4 l2w = t.localToWorldMatrix;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = l2w.MultiplyPoint3x4(v[i]);
                Vector3 wn = l2w.MultiplyVector(n.Length > i ? n[i] : Vector3.up).normalized;
                Vector2 c;
                Vector3 flat = new Vector3(wn.x, 0f, wn.z);
                if (Mathf.Abs(wn.y) >= 0.7f || flat.sqrMagnitude < 1e-6f) c = new Vector2(p.x, p.z);
                else
                {
                    // Along the face horizontally (left to right seen from outside), straight up for v
                    Vector3 along = Vector3.Cross(Vector3.up, flat.normalized);
                    c = new Vector2(Vector3.Dot(p, along), p.y);
                }
                uv[i] = c / TileMetres;
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, MeshFolder + "/" + Sanitize(name) + ".asset");
            return mesh;
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        #endregion

        #region Stone texture

        public static Material BuildMaterial()
        {
            EnsureFolder(TextureFolder);
            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            GenerateTextures(1024);

            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            m.SetTexture("_BaseMap", albedo);
            m.mainTexture = albedo;
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", 0.08f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Paints a tileable wall of cut stone blocks in the hall's olive-grey: 4 courses of 1 m per 4 m
        /// repeat, blocks 1.2 to 2.4 m long with rounded corners in running bond, dark mortar, a pale chipped rim inside each block
        /// edge and soft blotches, plus a matching normal map from the block bevels.
        /// </summary>
        public static void GenerateTextures(int size)
        {
            const int rows = 4;
            const float corner = 0.12f;
            const float mortar = 0.035f;   // half the joint width, metres
            const float bevel = 0.07f;
            var rng = new System.Random(20261007);

            // Block edges per course (in metres along the 4 m repeat), random lengths that close the repeat
            var courses = new List<float>[rows];
            var tints = new List<float>[rows];
            for (int r = 0; r < rows; r++)
            {
                var edges = new List<float>();
                var tint = new List<float>();
                float start = (float)rng.NextDouble() * 1.6f;
                float x = start;
                while (true)
                {
                    edges.Add(x);
                    tint.Add(0.9f + (float)rng.NextDouble() * 0.2f);
                    float w = 1.2f + (float)rng.NextDouble() * 1.2f;
                    if (x + w > start + TileMetres - 1.2f) break;
                    x += w;
                }
                edges.Add(start + TileMetres);
                courses[r] = edges;
                tints[r] = tint;
            }

            Color stoneA = new Color(0.47f, 0.45f, 0.35f);
            Color stoneB = new Color(0.42f, 0.41f, 0.33f);
            Color joint = new Color(0.25f, 0.24f, 0.17f);
            Color rim = new Color(0.66f, 0.64f, 0.53f);

            var height = new float[size * size];
            var albedo = new Color[size * size];
            float rowH = TileMetres / rows;
            for (int py = 0; py < size; py++)
            {
                float y = (py + 0.5f) / size * TileMetres;
                int r = Mathf.Min(rows - 1, (int)(y / rowH));
                float dy = Mathf.Min(y - r * rowH, (r + 1) * rowH - y);
                List<float> edges = courses[r];
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * TileMetres;
                    float xs = x < edges[0] ? x + TileMetres : x;
                    int k = 0;
                    while (k < edges.Count - 2 && xs >= edges[k + 1]) k++;
                    float dx = Mathf.Min(xs - edges[k], edges[k + 1] - xs);
                    // Distance to the block outline, rounded at the corners
                    float edgeDist = dx < corner && dy < corner
                        ? corner - new Vector2(corner - dx, corner - dy).magnitude
                        : Mathf.Min(dx, dy);
                    float d = edgeDist - mortar;

                    float n1 = TileNoise(x, y, 1f, 11) * 0.5f + TileNoise(x, y, 0.5f, 23) * 0.3f + TileNoise(x, y, 0.25f, 5) * 0.2f;
                    float n2 = TileNoise(x, y, 0.4f, 37) * 0.6f + TileNoise(x, y, 0.2f, 41) * 0.25f + TileNoise(x, y, 0.1f, 43) * 0.15f;
                    int idx = py * size + px;
                    if (d <= 0f)
                    {
                        height[idx] = 0f;
                        albedo[idx] = joint * (0.85f + 0.3f * n2);
                        continue;
                    }

                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / bevel));
                    // Chipped corners: noise lowers the block near its edge
                    height[idx] = edge * (0.85f + 0.15f * n1) - (d < bevel ? 0.25f * n2 * (1f - edge) : 0f);
                    float tint = tints[r][Mathf.Min(k, tints[r].Count - 1)];
                    Color c = Color.Lerp(stoneB, stoneA, n1) * tint;
                    c = Color.Lerp(c, c * 0.86f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n2 - 0.52f) * 4f))); // soft darker blotches
                    c *= 0.96f + 0.08f * (y - r * rowH) / rowH;                                                // a touch lighter at the top of each block
                    // The painted pale rim just inside each block edge
                    float rimT = Mathf.Clamp01(1f - Mathf.Abs(d - 0.035f) / 0.022f);
                    c = Color.Lerp(c, rim, rimT * 0.75f);
                    c = Color.Lerp(joint, c, 0.35f + 0.65f * edge);
                    c.a = 1f;
                    albedo[idx] = c;
                }
            }

            var normals = new Color[size * size];
            const float strength = 6f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float l = height[py * size + (px + size - 1) % size];
                    float rr = height[py * size + (px + 1) % size];
                    float dn = height[((py + size - 1) % size) * size + px];
                    float up = height[((py + 1) % size) * size + px];
                    Vector3 nrm = new Vector3((l - rr) * strength, (dn - up) * strength, 1f).normalized;
                    normals[py * size + px] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
                }
            }

            Save(AlbedoPath, albedo, size, false);
            Save(NormalPath, normals, size, true);
        }

        private static void Save(string path, Color[] pixels, int size, bool normalMap)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        /// <summary>Smooth value noise that repeats every <see cref="TileMetres"/> (cell size in metres).</summary>
        private static float TileNoise(float x, float y, float cell, int seed)
        {
            int period = Mathf.Max(1, Mathf.RoundToInt(TileMetres / cell));
            float fx = x / TileMetres * period, fy = y / TileMetres * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
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
