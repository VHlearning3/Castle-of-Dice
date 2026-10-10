using System.Collections.Generic;
using System.IO;
using CastleOfTheD20.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Sir Roland's helmet has two open eye slits with nothing behind them, so the background showed through
    /// his eyes. This builds a dark, slightly recessed socket that plugs each slit (fitted to the slit's own
    /// rim) and hangs it on the knight's head bone in PlayerHero.prefab.
    /// Batch: -executeMethod CastleOfTheD20.Editor.KnightEyeSocketsSetup.Run
    /// </summary>
    public static class KnightEyeSocketsSetup
    {
        public const string KnightModelPath = "Assets/Characters/PlayerKnightModel.fbx";
        public const string Folder = "Assets/Characters/Player_knight_eyes";
        public const string MeshPath = Folder + "/Knight_EyeSockets.asset";
        public const string MaterialPath = Folder + "/M_Knight_EyeSocket.mat";
        public const string HeroPrefabPath = "Assets/PREFABS/Players/PlayerHero.prefab";
        public const string SocketObjectName = "Knight_EyeSockets";
        public const string NeckMeshPath = Folder + "/Knight_NeckFill.asset";
        public const string NeckMaterialPath = Folder + "/M_Knight_Neck.mat";
        public const string NeckObjectName = "Knight_NeckFill";
        public const string InteriorMaterialPath = Folder + "/M_Knight_Interior.mat";

        [MenuItem("CastleOfDice/Heroes/Fix Knight Eye Sockets")]
        public static void Run()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModelPath);
            if (model == null) { Debug.LogError("[EyeSockets] knight model not found"); return; }

            // The model's bones are optimised away; a de-optimised copy gives the bones and their bind poses
            GameObject copy = Object.Instantiate(model);
            string headPath = null, neckPath = null;
            Mesh plug = null, neckFill = null;
            try
            {
                AnimatorUtility.DeoptimizeTransformHierarchy(copy);
                SkinnedMeshRenderer source = copy.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Transform head = source != null ? FindBone(source, ":Head") : null;
                Transform neck = source != null ? FindBone(source, ":Neck") : null;
                if (head == null || neck == null) { Debug.LogError("[EyeSockets] no head or neck bone"); return; }
                headPath = PathFrom(copy.transform, head);
                neckPath = PathFrom(copy.transform, neck);
                plug = BuildPlug(source, head);
                neckFill = BuildNeckFill(source, neck, head);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
            if (plug == null || neckFill == null) return;

            Expose(headPath, neckPath);

            Directory.CreateDirectory(Folder);
            plug = SaveMesh(plug, MeshPath);
            neckFill = SaveMesh(neckFill, NeckMeshPath);
            Material mat = SaveMaterial(MaterialPath, new Color(0.018f, 0.016f, 0.02f), 0.35f, 0f);
            // Shadowed mail under the helmet rim: dark, a little metallic
            Material neckMat = SaveMaterial(NeckMaterialPath, new Color(0.09f, 0.095f, 0.105f), 0.3f, 0.4f);
            AssetDatabase.SaveAssets();

            AttachToHero(":Head", SocketObjectName, plug, mat, ":Neck", NeckObjectName, neckFill, neckMat);
            Debug.Log("[EyeSockets] done");
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            return existing;
        }

        private static Material SaveMaterial(string path, Color color, float smoothness, float metallic)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Exposes bones on the optimised knight model so the added meshes can ride on them.</summary>
        private static void Expose(params string[] bonePaths)
        {
            ModelImporter imp = (ModelImporter)AssetImporter.GetAtPath(KnightModelPath);
            List<string> paths = new List<string>(imp.extraExposedTransformPaths ?? new string[0]);
            bool changed = false;
            foreach (string p in bonePaths)
                if (!paths.Contains(p)) { paths.Add(p); changed = true; }
            if (!changed) return;
            imp.extraExposedTransformPaths = paths.ToArray();
            imp.SaveAndReimport();
        }

        private static string PathFrom(Transform root, Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static Transform FindBone(SkinnedMeshRenderer smr, string suffix)
        {
            foreach (Transform b in smr.bones)
                if (b != null && b.name.EndsWith(suffix)) return b;
            return null;
        }

        /// <summary>Adds (or refreshes) the sockets and the neck fill on the knight's exposed bones in PlayerHero.prefab.</summary>
        private static void AttachToHero(string headSuffix, string socketName, Mesh plug, Material mat,
            string neckSuffix, string neckName, Mesh neckFill, Material neckMat)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HeroPrefabPath);
            try
            {
                HeroClassModels models = root.GetComponent<HeroClassModels>();
                SerializedProperty warrior = models != null ? new SerializedObject(models).FindProperty("warriorModel") : null;
                GameObject knight = warrior != null ? warrior.objectReferenceValue as GameObject : null;
                if (knight == null) { Debug.LogError("[EyeSockets] PlayerHero has no knight"); return; }
                if (!Hang(knight, headSuffix, socketName, plug, mat) || !Hang(knight, neckSuffix, neckName, neckFill, neckMat)) return;
                AddInteriorPass(knight);
                PrefabUtility.SaveAsPrefabAsset(root, HeroPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The knight's armour is a hollow shell drawn from the outside only, so every opening (eye slits,
        /// the neck, the gaps by the shoulder plates) showed whatever was behind him. A second material on the
        /// same mesh draws only its inside faces, unlit and dark, so an opening shows shadow instead.
        /// </summary>
        private static void AddInteriorPass(GameObject knight)
        {
            Material interior = AssetDatabase.LoadAssetAtPath<Material>(InteriorMaterialPath);
            if (interior == null)
            {
                interior = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(interior, InteriorMaterialPath);
            }
            interior.SetColor("_BaseColor", new Color(0.035f, 0.035f, 0.04f));
            interior.SetFloat("_Cull", (float)CullMode.Front);   // back faces only
            EditorUtility.SetDirty(interior);
            AssetDatabase.SaveAssets();

            SkinnedMeshRenderer body = knight.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (body == null) { Debug.LogError("[EyeSockets] knight has no skinned mesh"); return; }
            List<Material> mats = new List<Material>(body.sharedMaterials);
            if (mats.Contains(interior)) return;
            // One submesh: an extra material slot draws that submesh again with it
            mats.Add(interior);
            body.sharedMaterials = mats.ToArray();
        }

        private static bool Hang(GameObject knight, string boneSuffix, string name, Mesh mesh, Material mat)
        {
            Transform bone = null;
            foreach (Transform t in knight.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(boneSuffix)) bone = t;
            if (bone == null) { Debug.LogError($"[EyeSockets] PlayerHero's knight has no exposed {boneSuffix}"); return false; }

            Transform existing = bone.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(bone, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = knight.layer;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return true;
        }

        /// <summary>
        /// Welds UV-seam duplicates and returns the mesh's open (boundary) edges as pairs of welded vertex ids.
        /// </summary>
        private static List<Vector2Int> OpenEdges(Mesh mesh, float unit, out List<Vector3> welded)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            float q = unit * 1e-3f;
            Dictionary<Vector3Int, int> weldIndex = new Dictionary<Vector3Int, int>();
            welded = new List<Vector3>();
            int[] map = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(verts[i].x / q), Mathf.RoundToInt(verts[i].y / q), Mathf.RoundToInt(verts[i].z / q));
                if (!weldIndex.TryGetValue(key, out int w)) { w = welded.Count; welded.Add(verts[i]); weldIndex[key] = w; }
                map[i] = w;
            }
            Dictionary<long, int> edgeUse = new Dictionary<long, int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = map[tris[i + k]], b = map[tris[i + (k + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edgeUse.TryGetValue(key, out int n);
                    edgeUse[key] = n + 1;
                }
            }
            List<Vector2Int> open = new List<Vector2Int>();
            foreach (KeyValuePair<long, int> e in edgeUse)
                if (e.Value == 1) open.Add(new Vector2Int((int)(e.Key >> 32), (int)(e.Key & 0xffffffff)));
            return open;
        }

        /// <summary>
        /// The knight has no neck: between the helmet's bottom rim and the collar's top rim there is an open
        /// gap, and from the front (or the game camera above) the ground showed through it. This finds both
        /// rims around the neck joint and builds a closed tube just inside them, from inside the collar up into
        /// the helmet. Vertices are in the neck bone's own space.
        /// </summary>
        public static Mesh BuildNeckFill(SkinnedMeshRenderer source, Transform neckBone, Transform headBone)
        {
            Mesh mesh = source.sharedMesh;
            int neck = System.Array.IndexOf(source.bones, neckBone), head = System.Array.IndexOf(source.bones, headBone);
            if (neck < 0 || head < 0) { Debug.LogError("[EyeSockets] neck or head is not a skin bone"); return null; }
            Vector3 neckPos = mesh.bindposes[neck].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 headPos = mesh.bindposes[head].inverse.MultiplyPoint3x4(Vector3.zero);
            float unit = Vector3.Distance(neckPos, headPos);
            Vector3 up = (headPos - neckPos).normalized;

            List<Vector2Int> open = OpenEdges(mesh, unit, out List<Vector3> welded);

            // Group the open edges into rims
            Dictionary<int, int> parent = new Dictionary<int, int>();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            foreach (Vector2Int e in open)
            {
                if (!parent.ContainsKey(e.x)) parent[e.x] = e.x;
                if (!parent.ContainsKey(e.y)) parent[e.y] = e.y;
                parent[Find(e.x)] = Find(e.y);
            }
            Dictionary<int, List<int>> rims = new Dictionary<int, List<int>>();
            foreach (int v in new List<int>(parent.Keys))
            {
                int r = Find(v);
                if (!rims.TryGetValue(r, out List<int> list)) rims[r] = list = new List<int>();
                list.Add(v);
            }

            // Rings around the neck axis: centred on it, at neck height, wide enough to go round the neck
            Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(up, side);
            List<List<Vector3>> rings = new List<List<Vector3>>();
            float hMin = float.MaxValue, hMax = float.MinValue;
            Vector3 axisCentre = Vector3.zero;
            foreach (List<int> rim in rims.Values)
            {
                if (rim.Count < 8) continue;
                Vector3 c = Vector3.zero;
                foreach (int v in rim) c += welded[v];
                c /= rim.Count;
                float h = Vector3.Dot(c - neckPos, up) / unit;
                float off = Vector3.ProjectOnPlane(c - neckPos, up).magnitude / unit;
                float radius = 0f;
                foreach (int v in rim) radius += Vector3.ProjectOnPlane(welded[v] - c, up).magnitude;
                radius /= rim.Count * unit;
                bool ring = h > -0.8f && h < 1.0f && off < 0.4f && radius > 0.35f;
                Debug.Log($"[EyeSockets] rim {rim.Count} verts, height {h:F2}, off-axis {off:F2}, radius {radius:F2} (neck units){(ring ? " <- neck ring" : "")}");
                if (!ring) continue;
                List<Vector3> pts = new List<Vector3>();
                foreach (int v in rim) pts.Add(welded[v]);
                rings.Add(pts);
                hMin = Mathf.Min(hMin, h);
                hMax = Mathf.Max(hMax, h);
                axisCentre += Vector3.ProjectOnPlane(c - neckPos, up);
            }
            if (rings.Count < 2) { Debug.LogError($"[EyeSockets] expected the helmet and collar rims, found {rings.Count}"); return null; }
            axisCentre = neckPos + axisCentre / rings.Count;

            // Each tube point sits just inside the tighter of the rims in its direction
            const int segments = 20;
            float[] radii = new float[segments];
            for (int s = 0; s < segments; s++)
            {
                float ang = s * Mathf.PI * 2f / segments;
                Vector3 dir = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                float r = float.MaxValue;
                foreach (List<Vector3> ring in rings) r = Mathf.Min(r, RadiusToward(ring, axisCentre, up, dir));
                radii[s] = r * 0.94f;
            }

            float bottom = (hMin - 0.45f) * unit, top = (hMax + 0.55f) * unit;
            List<Vector3> outV = new List<Vector3>();
            List<int> outT = new List<int>();
            for (int s = 0; s < segments; s++)
            {
                float ang = s * Mathf.PI * 2f / segments;
                Vector3 dir = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                Vector3 baseP = axisCentre + dir * radii[s];
                outV.Add(baseP + up * bottom);
                outV.Add(baseP + up * top);
            }
            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                int a = s * 2, b = s * 2 + 1, c = n * 2, d = n * 2 + 1;
                float ang = (s + 0.5f) * Mathf.PI * 2f / segments;
                Vector3 outward = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                Tri(outT, outV, a, b, d, outward, false);
                Tri(outT, outV, a, d, c, outward, false);
            }
            Debug.Log($"[EyeSockets] neck fill: {rings.Count} rims, from {hMin - 0.45f:F2} to {hMax + 0.55f:F2} neck units");

            Matrix4x4 toNeck = mesh.bindposes[neck];
            for (int i = 0; i < outV.Count; i++) outV[i] = toNeck.MultiplyPoint3x4(outV[i]);
            Mesh fill = new Mesh { name = "Knight_NeckFill" };
            fill.SetVertices(outV);
            fill.SetTriangles(outT, 0);
            fill.RecalculateNormals();
            fill.RecalculateBounds();
            return fill;
        }

        // Distance from the axis to the rim in one direction (the rim point closest to that direction)
        private static float RadiusToward(List<Vector3> ring, Vector3 axis, Vector3 up, Vector3 dir)
        {
            float best = -2f, r = 0f;
            foreach (Vector3 p in ring)
            {
                Vector3 flat = Vector3.ProjectOnPlane(p - axis, up);
                float cos = Vector3.Dot(flat.normalized, dir);
                if (cos > best) { best = cos; r = flat.magnitude; }
            }
            return r;
        }

        /// <summary>
        /// Finds the open rims above the head joint (the eye slits) and closes each with a short inward tube
        /// and a cap, both faces drawn so the socket reads dark from any angle. Vertices are in the head
        /// bone's own space.
        /// </summary>
        public static Mesh BuildPlug(SkinnedMeshRenderer source, Transform headBone)
        {
            Mesh mesh = source.sharedMesh;
            int head = System.Array.IndexOf(source.bones, headBone);
            if (head < 0) { Debug.LogError("[EyeSockets] head is not a skin bone"); return null; }

            Vector3 headPos = mesh.bindposes[head].inverse.MultiplyPoint3x4(Vector3.zero);
            Transform top = headBone.childCount > 0 ? headBone.GetChild(0) : null;
            if (top == null) { Debug.LogError("[EyeSockets] no head-top joint"); return null; }
            // The head-top joint, carried into mesh space by the head's bind pose (its posed transform may differ)
            Vector3 topPos = mesh.bindposes[head].inverse.MultiplyPoint3x4(top.localPosition);
            float unit = Vector3.Distance(headPos, topPos);
            Vector3 up = (topPos - headPos).normalized;

            // Weld split (UV seam) vertices so only real openings count as boundary
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            float q = unit * 1e-3f;
            Dictionary<Vector3Int, int> weldIndex = new Dictionary<Vector3Int, int>();
            List<Vector3> welded = new List<Vector3>();
            int[] map = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(verts[i].x / q), Mathf.RoundToInt(verts[i].y / q), Mathf.RoundToInt(verts[i].z / q));
                if (!weldIndex.TryGetValue(key, out int w)) { w = welded.Count; welded.Add(verts[i]); weldIndex[key] = w; }
                map[i] = w;
            }

            Dictionary<long, int> edgeUse = new Dictionary<long, int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = map[tris[i + k]], b = map[tris[i + (k + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edgeUse.TryGetValue(key, out int n);
                    edgeUse[key] = n + 1;
                }
            }

            // Open edges a little above the head joint and close to it: the two eye slits (each slit
            // also runs down behind the nose guard to an open seam, which nothing can see through)
            HashSet<int> rimVerts = new HashSet<int>();
            foreach (KeyValuePair<long, int> e in edgeUse)
            {
                if (e.Value != 1) continue;
                int a = (int)(e.Key >> 32), b = (int)(e.Key & 0xffffffff);
                Vector3 mid = (welded[a] + welded[b]) * 0.5f;
                float rise = Vector3.Dot(mid - headPos, up);
                if (rise < 0.4f * unit || rise > 0.8f * unit || Vector3.Distance(mid, headPos) > 1.1f * unit) continue;
                rimVerts.Add(a);
                rimVerts.Add(b);
            }
            if (rimVerts.Count == 0) { Debug.LogError("[EyeSockets] no eye slits found"); return null; }

            // Split the rim vertices into left and right eye by their side of the head
            Vector3 centre = Vector3.zero;
            foreach (int v in rimVerts) centre += welded[v];
            centre /= rimVerts.Count;
            Vector3 forward = Vector3.ProjectOnPlane(centre - headPos, up).normalized;
            Vector3 side = Vector3.Cross(up, forward).normalized;

            List<Vector3> outV = new List<Vector3>();
            List<int> outT = new List<int>();
            for (int s = -1; s <= 1; s += 2)
            {
                List<Vector3> rim = new List<Vector3>();
                foreach (int v in rimVerts)
                    if (Mathf.Sign(Vector3.Dot(welded[v] - centre, side)) == s) rim.Add(welded[v]);
                if (rim.Count < 3) continue;
                Vector3 c = Vector3.zero;
                foreach (Vector3 p in rim) c += p;
                c /= rim.Count;
                Vector3 axisX = side, axisY = Vector3.Cross(forward, side).normalized;
                rim.Sort((p1, p2) => Mathf.Atan2(Vector3.Dot(p1 - c, axisY), Vector3.Dot(p1 - c, axisX))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(p2 - c, axisY), Vector3.Dot(p2 - c, axisX))));
                AddSocket(rim, c, forward, unit, outV, outT);
                Debug.Log($"[EyeSockets] eye {(s < 0 ? "one" : "two")}: {rim.Count} rim points");
            }

            // Into the head bone's own space (its bind pose), so the sockets ride on the head as a plain mesh
            Matrix4x4 toHead = mesh.bindposes[head];
            for (int i = 0; i < outV.Count; i++) outV[i] = toHead.MultiplyPoint3x4(outV[i]);
            Mesh plug = new Mesh { name = "Knight_EyeSockets" };
            plug.SetVertices(outV);
            plug.SetTriangles(outT, 0);
            plug.RecalculateNormals();
            plug.RecalculateBounds();
            return plug;
        }

        private static void AddSocket(List<Vector3> rim, Vector3 c, Vector3 forward, float unit, List<Vector3> v, List<int> t)
        {
            float depth = 0.14f * unit;
            int n = rim.Count;
            Vector3 back = c - forward * depth * 1.15f;
            // Two copies (one per facing) so normals stay clean on both sides
            for (int side = 0; side < 2; side++)
            {
                int start = v.Count;
                for (int i = 0; i < n; i++) v.Add(rim[i] - forward * unit * 0.004f);
                for (int i = 0; i < n; i++) v.Add(c + (rim[i] - c) * 0.7f - forward * depth);
                v.Add(back);
                int capIdx = start + 2 * n;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    int a = start + i, b = start + j, a2 = start + n + i, b2 = start + n + j;
                    Tri(t, v, a, b, b2, forward, side == 1);
                    Tri(t, v, a, b2, a2, forward, side == 1);
                    Tri(t, v, a2, b2, capIdx, forward, side == 1);
                }
            }
        }

        // Unity's front face is clockwise: Cross(b - a, c - a) points at the viewer
        private static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 forward, bool flip)
        {
            bool facesForward = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), forward) >= 0f;
            if (facesForward == flip) { int x = b; b = c; c = x; }
            t.Add(a); t.Add(b); t.Add(c);
        }
    }
}
