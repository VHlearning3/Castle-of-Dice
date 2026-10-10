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

        [MenuItem("CastleOfDice/Heroes/Fix Knight Eye Sockets")]
        public static void Run()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModelPath);
            if (model == null) { Debug.LogError("[EyeSockets] knight model not found"); return; }

            // The model's bones are optimised away; a de-optimised copy gives the head bone and its bind pose
            GameObject copy = Object.Instantiate(model);
            string headPath = null;
            Mesh plug = null;
            try
            {
                AnimatorUtility.DeoptimizeTransformHierarchy(copy);
                SkinnedMeshRenderer source = copy.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Transform head = source != null ? FindHead(source) : null;
                if (head == null) { Debug.LogError("[EyeSockets] no head bone"); return; }
                headPath = PathFrom(copy.transform, head);
                plug = BuildPlug(source, head);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
            if (plug == null) return;

            ExposeHead(headPath);

            Directory.CreateDirectory(Folder);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) { EditorUtility.CopySerialized(plug, existing); plug = existing; }
            else AssetDatabase.CreateAsset(plug, MeshPath);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetColor("_BaseColor", new Color(0.018f, 0.016f, 0.02f));
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            AttachToHero(plug, mat);
            Debug.Log("[EyeSockets] done");
        }

        /// <summary>Exposes the head bone on the optimised knight model so the sockets can ride on it.</summary>
        private static void ExposeHead(string headPath)
        {
            ModelImporter imp = (ModelImporter)AssetImporter.GetAtPath(KnightModelPath);
            List<string> paths = new List<string>(imp.extraExposedTransformPaths ?? new string[0]);
            if (paths.Contains(headPath)) return;
            paths.Add(headPath);
            imp.extraExposedTransformPaths = paths.ToArray();
            imp.SaveAndReimport();
        }

        private static string PathFrom(Transform root, Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static Transform FindHead(SkinnedMeshRenderer smr)
        {
            foreach (Transform b in smr.bones)
                if (b != null && b.name.EndsWith(":Head")) return b;
            return null;
        }

        /// <summary>Adds (or refreshes) the sockets under the knight's exposed head bone in PlayerHero.prefab.</summary>
        private static void AttachToHero(Mesh plug, Material mat)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HeroPrefabPath);
            try
            {
                HeroClassModels models = root.GetComponent<HeroClassModels>();
                SerializedProperty warrior = models != null ? new SerializedObject(models).FindProperty("warriorModel") : null;
                GameObject knight = warrior != null ? warrior.objectReferenceValue as GameObject : null;
                Transform head = null;
                if (knight != null)
                    foreach (Transform t in knight.GetComponentsInChildren<Transform>(true))
                        if (t.name.EndsWith(":Head")) head = t;
                if (head == null) { Debug.LogError("[EyeSockets] PlayerHero's knight has no exposed head"); return; }

                Transform existing = head.Find(SocketObjectName);
                GameObject go = existing != null ? existing.gameObject : new GameObject(SocketObjectName);
                go.transform.SetParent(head, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = knight.layer;

                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf == null) mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = plug;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr == null) mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root, HeroPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
