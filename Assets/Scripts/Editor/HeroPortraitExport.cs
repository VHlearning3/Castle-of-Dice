using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Poses each hero exactly as the game shows them (PlayerHero.prefab with its class model, weapons and the
    /// knight's eye sockets) and writes the posed meshes as OBJ + a small JSON of textures and framing to
    /// BlenderSources/HeroPortraits/. BlenderSources/HeroPortraits_build.py then lights and renders the
    /// Choose Your Hero portraits from them. Works in a new empty scene that is never saved.
    /// Batch: -executeMethod CastleOfTheD20.Editor.HeroPortraitExport.ExportAll
    /// Env HERO_POSES="Warrior=cheer@0.3;Mage=Victory@0.5" overrides the pose (clip name fragment @ share of its length).
    /// </summary>
    public static class HeroPortraitExport
    {
        public const string OutputFolder = "BlenderSources/HeroPortraits";
        private const string HeroPrefabPath = "Assets/PREFABS/Players/PlayerHero.prefab";

        private struct Pose
        {
            public CharacterClassType hero;
            public string clip;
            public float at;
        }

        private static readonly Pose[] Poses =
        {
            new Pose { hero = CharacterClassType.Warrior, clip = "idle", at = 0.25f },
            new Pose { hero = CharacterClassType.Mage, clip = "idle", at = 0.25f },
            new Pose { hero = CharacterClassType.Rogue, clip = "Idle", at = 0.25f },
        };

        [MenuItem("CastleOfDice/Heroes/Export Hero Portrait Poses")]
        public static void ExportFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            ExportAll();
            if (!string.IsNullOrEmpty(before)) EditorSceneManager.OpenScene(before);
        }

        public static void ExportAll()
        {
            Dictionary<string, string> overrides = ParseOverrides(System.Environment.GetEnvironmentVariable("HERO_POSES"));
            foreach (Pose pose in Poses)
            {
                Pose p = pose;
                if (overrides.TryGetValue(p.hero.ToString(), out string o))
                {
                    string[] parts = o.Split('@');
                    p.clip = parts[0];
                    if (parts.Length > 1) p.at = float.Parse(parts[1], CultureInfo.InvariantCulture);
                }
                try { Export(p); }
                catch (System.Exception e) { Debug.LogError($"[HeroPortraits] {p.hero}: {e}"); }
            }
            Debug.Log("[HeroPortraits] export done");
        }

        /// <summary>Imports the rendered portraits (Assets/Resources/HeroPortraits) as UI sprites.</summary>
        [MenuItem("CastleOfDice/Heroes/Import Hero Portraits")]
        public static void ImportPortraits()
        {
            AssetDatabase.Refresh();
            foreach (string hero in new[] { "Warrior", "Mage", "Rogue" })
            {
                TextureImporter imp = AssetImporter.GetAtPath($"Assets/Resources/HeroPortraits/{hero}.png") as TextureImporter;
                if (imp == null) { Debug.LogError($"[HeroPortraits] {hero}.png missing"); continue; }
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.maxTextureSize = 512;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            Debug.Log("[HeroPortraits] imported");
        }

        private static Dictionary<string, string> ParseOverrides(string s)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(s)) return d;
            foreach (string item in s.Split(';'))
            {
                string[] kv = item.Split('=');
                if (kv.Length == 2) d[kv[0].Trim()] = kv[1].Trim();
            }
            return d;
        }

        private static void Export(Pose pose)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            HeroClassModels models = go.GetComponent<HeroClassModels>();
            models.Apply(pose.hero);

            GameObject model = null;
            SerializedObject so = new SerializedObject(models);
            string field = pose.hero == CharacterClassType.Mage ? "mageModel" : pose.hero == CharacterClassType.Rogue ? "rogueModel" : "warriorModel";
            model = so.FindProperty(field).objectReferenceValue as GameObject;
            Animator animator = model.GetComponentInChildren<Animator>(true);

            AnimationClip clip = null;
            List<string> names = new List<string>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                {
                    if (c == null) continue;
                    names.Add(c.name);
                    if (clip == null && c.name.IndexOf(pose.clip, System.StringComparison.OrdinalIgnoreCase) >= 0) clip = c;
                }
            }
            Debug.Log($"[HeroPortraits] {pose.hero} clips: {string.Join(", ", names)}");
            if (clip != null)
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * pose.at);
                AnimationMode.EndSampling();
            }
            else Debug.LogWarning($"[HeroPortraits] {pose.hero}: no clip matching '{pose.clip}', rest pose");

            Transform head = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(":Head")) head = t;
            Vector3 headPos = head != null ? head.position : Vector3.up * 1.6f;
            Vector3 headFwd = head != null ? head.forward : Vector3.forward;

            string dir = Path.Combine(OutputFolder, pose.hero.ToString());
            Directory.CreateDirectory(dir);
            StringBuilder obj = new StringBuilder();
            StringBuilder json = new StringBuilder();
            obj.AppendLine("# Castle of Dice hero pose, exported by HeroPortraitExport (Unity left-handed, x mirrored)");
            json.Append("{\n  \"hero\": \"").Append(pose.hero).Append("\",\n  \"clip\": \"").Append(clip != null ? clip.name : "rest").Append("\",\n");
            json.Append("  \"head\": ").Append(Vec(Obj(headPos))).Append(",\n");
            json.Append("  \"forward\": ").Append(Vec(Obj(go.transform.forward))).Append(",\n");
            json.Append("  \"headForward\": ").Append(Vec(Obj(headFwd))).Append(",\n  \"materials\": [\n");

            int vBase = 1, matIndex = 0;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            bool firstMat = true;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
                Mesh m;
                Matrix4x4 toWorld;
                if (r is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;
                    m = new Mesh();
                    smr.BakeMesh(m, false);
                    toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                }
                else
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    m = mf.sharedMesh;
                    toWorld = r.transform.localToWorldMatrix;
                }

                obj.Append("o ").AppendLine(r.name.Replace(' ', '_'));
                Vector3[] v = m.vertices;
                Vector3[] n = m.normals;
                Vector2[] uv = m.uv;
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 w = toWorld.MultiplyPoint3x4(v[i]);
                    min = Vector3.Min(min, w); max = Vector3.Max(max, w);
                    obj.Append("v ").AppendLine(Vec(Obj(w), " "));
                }
                bool hasUv = uv != null && uv.Length == v.Length;
                bool hasN = n != null && n.Length == v.Length;
                if (hasUv) foreach (Vector2 t in uv) obj.Append("vt ").Append(F(t.x)).Append(' ').AppendLine(F(t.y));
                if (hasN) foreach (Vector3 nn in n) obj.Append("vn ").AppendLine(Vec(Obj(toWorld.MultiplyVector(nn).normalized), " "));

                Material[] mats = r.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    Material mat = s < mats.Length ? mats[s] : null;
                    string matName = $"m{matIndex++}_{(mat != null ? mat.name.Replace(' ', '_') : "none")}";
                    obj.Append("usemtl ").AppendLine(matName);
                    int[] tri = m.GetTriangles(s);
                    for (int i = 0; i < tri.Length; i += 3)
                    {
                        // Mirroring x flips handedness, so the winding flips too (checked against the normals)
                        obj.Append('f');
                        foreach (int k in new[] { tri[i], tri[i + 2], tri[i + 1] })
                        {
                            int idx = k + vBase;
                            obj.Append(' ').Append(idx);
                            if (hasUv || hasN) obj.Append('/').Append(hasUv ? idx.ToString() : "");
                            if (hasN) obj.Append('/').Append(idx);
                        }
                        obj.AppendLine();
                    }

                    Texture tex = mat != null && mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat != null ? mat.mainTexture : null;
                    Color col = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat != null && mat.HasProperty("_Color") ? mat.color : Color.white;
                    float metal = mat != null && mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                    float smooth = mat != null && mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.3f;
                    string texPath = tex != null ? AssetDatabase.GetAssetPath(tex) : "";
                    if (!string.IsNullOrEmpty(texPath) && !texPath.StartsWith("Assets/")) texPath = "";
                    if (!firstMat) json.Append(",\n");
                    firstMat = false;
                    json.Append("    {\"name\": \"").Append(matName).Append("\", \"texture\": \"")
                        .Append(string.IsNullOrEmpty(texPath) ? "" : Path.GetFullPath(texPath).Replace('\\', '/'))
                        .Append("\", \"color\": [").Append(F(col.r)).Append(", ").Append(F(col.g)).Append(", ").Append(F(col.b))
                        .Append("], \"metallic\": ").Append(F(metal)).Append(", \"smoothness\": ").Append(F(smooth)).Append('}');
                }
                vBase += v.Length;
            }
            json.Append("\n  ],\n  \"min\": ").Append(Vec(Obj(min))).Append(",\n  \"max\": ").Append(Vec(Obj(max))).Append("\n}\n");

            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            File.WriteAllText(Path.Combine(dir, "pose.obj"), obj.ToString());
            File.WriteAllText(Path.Combine(dir, "pose.json"), json.ToString());
            Debug.Log($"[HeroPortraits] {pose.hero}: {(clip != null ? clip.name : "rest")} @ {pose.at:F2}, {vBase - 1} verts");
            Object.DestroyImmediate(go);
        }

        private static Vector3 Obj(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        private static string F(float f) => f.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Vec(Vector3 v, string sep = null)
        {
            if (sep != null) return F(v.x) + sep + F(v.y) + sep + F(v.z);
            return "[" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + "]";
        }
    }
}
