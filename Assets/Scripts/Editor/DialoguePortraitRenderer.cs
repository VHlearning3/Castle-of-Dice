using System.Collections.Generic;
using System.IO;
using CastleOfTheD20.Dialogue;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Renders the dialogue box's face icons (Assets/Resources/Portraits/&lt;key&gt;.png) from each speaker's own
    /// prefab: head and shoulders, a slight three-quarter view, warm key light, dark backdrop. Works in a new
    /// empty scene that is never saved, so no zone scene is touched.
    /// Batch: -executeMethod CastleOfTheD20.Editor.DialoguePortraitRenderer.RenderAll
    /// </summary>
    public static class DialoguePortraitRenderer
    {
        public const string OutputFolder = "Assets/Resources/Portraits";
        private const int Size = 256;
        private const string Npcs = "Assets/PREFABS/NPCs/";
        private const string Visuals = "Assets/PREFABS/Characters/";

        private struct Def
        {
            public string key, path;
            public float yaw;        // degrees turned from the model's front (three-quarter view)
            public float frame;      // framed height as a share of the model's height
            public float lift;       // moves the framing up (+) or down (-), as a share of the model's height
            public float faceYaw;    // extra turn when a model's front is not +Z
        }

        private static readonly Def[] Defs =
        {
            new Def { key = "Baldur", path = Npcs + "NPC_Baldur_Smith.prefab", yaw = -18f },
            new Def { key = "Othelia", path = Npcs + "NPC_Othelia_3dmodel.prefab" },
            new Def { key = "Barnaby", path = Npcs + "NPC_Barnaby_3dmodel.prefab" },
            new Def { key = "Mirabel", path = Npcs + "NPC_Mirabel_3dmodel.prefab" },
            new Def { key = "Pip", path = Visuals + "Visual_NPC_Pip_Ghost.prefab", frame = 0.58f, lift = 0.05f },
            new Def { key = "GhostAldric", path = Visuals + "Visual_NPC_Aldric_Ghost.prefab", frame = 0.5f },
            new Def { key = "BlondeElf", path = "Assets/Characters/BlondeElf/BlondeElf.prefab" },
            new Def { key = "Cultist", path = Visuals + "Visual_Enemy_Cultist.prefab", frame = 0.6f, lift = 0.04f },
            new Def { key = "CursedCommander", path = Visuals + "Visual_Boss_CursedCommander.prefab" },
            new Def { key = "ShadowWitch", path = Visuals + "Visual_Boss_Malakor.prefab" },
            new Def { key = "GargoyleKing", path = Visuals + "Visual_Boss_Golem_Stage1.prefab", lift = 0.04f },
            new Def { key = "MirrorTwin", path = "Assets/Characters/MirrorTwin/MirrorTwin.prefab", yaw = -34f },
        };

        private static readonly Color BackdropCentre = new Color(0.25f, 0.18f, 0.12f);
        private static readonly Color BackdropEdge = new Color(0.06f, 0.045f, 0.035f);

        [MenuItem("CastleOfDice/Dialogue/Render Speaker Portraits")]
        public static void RenderFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string before = SceneManager.GetActiveScene().path;
            RenderAll();
            if (!string.IsNullOrEmpty(before)) EditorSceneManager.OpenScene(before);
        }

        public static void RenderAll()
        {
            Directory.CreateDirectory(OutputFolder);
            string only = System.Environment.GetEnvironmentVariable("PORTRAIT_ONLY");
            foreach (Def def in Defs)
            {
                if (!string.IsNullOrEmpty(only) && !only.Contains(def.key)) continue;
                try { Render(def); }
                catch (System.Exception e) { Debug.LogError($"[Portraits] {def.key}: {e}"); }
            }
            AssetDatabase.Refresh();
            foreach (Def def in Defs) ConfigureImporter($"{OutputFolder}/{def.key}.png");
            AssetDatabase.SaveAssets();
            Debug.Log("[Portraits] done");
        }

        private static void Render(Def def)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.path);
            if (prefab == null) { Debug.LogError($"[Portraits] missing {def.path}"); return; }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.4f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.26f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.11f, 0.09f);
            RenderSettings.fog = false;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, def.faceYaw, 0f));

            Animator animator = go.GetComponentInChildren<Animator>(true);
            Transform head = FindHead(go, animator);
            PoseIdle(animator);
            List<Renderer> renderers = BakeVisibleMeshes(go);
            if (renderers.Count == 0) { Debug.LogError($"[Portraits] {def.key}: no meshes"); Object.DestroyImmediate(go); return; }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            // Body height from the feet to the top of the head, so a raised weapon or a hat brim doesn't count
            float height = head != null ? Mathf.Max(0.2f, (head.position.y - b.min.y) * 1.12f) : Mathf.Max(0.2f, b.size.y);

            float headY = head != null ? head.position.y + 0.045f * height : b.max.y - 0.08f * height;
            Vector3 headPos = head != null ? new Vector3(head.position.x, headY, head.position.z) : new Vector3(b.center.x, headY, b.center.z);
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();

            float frame = (def.frame > 0f ? def.frame : 0.42f) * height;
            Vector3 centre = headPos + Vector3.up * (def.lift * height - 0.14f * frame);

            Vector3 front = Quaternion.Euler(0f, def.faceYaw, 0f) * Vector3.forward;
            Vector3 dir = Quaternion.AngleAxis(def.yaw == 0f ? 22f : def.yaw, Vector3.up) * front;
            dir = (dir + Vector3.up * 0.1f).normalized;

            const float fov = 20f;
            float distance = frame * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            GameObject camGo = new GameObject("PortraitCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = Mathf.Max(0.01f, distance * 0.05f);
            cam.farClipPlane = distance * 10f + height * 4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.transform.position = centre + dir * distance;
            cam.transform.LookAt(centre);

            AddLight("Key", new Color(1f, 0.83f, 0.62f), 1.6f, Quaternion.Euler(28f, 0f, 0f) * Quaternion.LookRotation(Quaternion.AngleAxis(-40f, Vector3.up) * -dir));
            AddLight("Fill", new Color(0.55f, 0.6f, 0.75f), 0.45f, Quaternion.LookRotation(Quaternion.AngleAxis(60f, Vector3.up) * -dir));
            AddLight("Rim", new Color(1f, 0.75f, 0.45f), 1.1f, Quaternion.Euler(-10f, 0f, 0f) * Quaternion.LookRotation(dir));

            RenderTexture rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);

            Texture2D final = Composite(shot);
            File.WriteAllBytes($"{OutputFolder}/{def.key}.png", final.EncodeToPNG());
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(final);
            Debug.Log($"[Portraits] {def.key}: height {height:F2}, head {(head != null ? head.name : "bounds")}");
        }

        private static void AddLight(string name, Color color, float intensity, Quaternion rotation)
        {
            Light l = new GameObject(name).AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.transform.rotation = rotation;
        }

        private static Transform FindHead(GameObject go, Animator animator)
        {
            if (animator != null && animator.isHuman && animator.avatar != null)
            {
                Transform h = animator.GetBoneTransform(HumanBodyBones.Head);
                if (h != null) return h;
            }
            Transform best = null;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("head") || n.Contains("end") || n.Contains("top") || n.Contains("nub")) continue;
                if (best == null || t.name.Length < best.name.Length) best = t;
            }
            return best;
        }

        // The idle pose of the animator's default state, so arms hang as they do in the game
        private static void PoseIdle(Animator animator)
        {
            if (animator == null) return;
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null && animator.runtimeAnimatorController is AnimatorOverrideController ov)
                controller = ov.runtimeAnimatorController as AnimatorController;
            if (controller == null || controller.layers.Length == 0) return;
            AnimatorState state = controller.layers[0].stateMachine.defaultState;
            AnimationClip clip = state != null ? state.motion as AnimationClip : null;
            if (clip == null && state != null && state.motion is BlendTree tree && tree.children.Length > 0)
                clip = tree.children[0].motion as AnimationClip;
            if (clip == null) return;
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * 0.25f);
            AnimationMode.EndSampling();
        }

        // Edit mode never re-skins skinned meshes for Camera.Render: bake each into a plain mesh
        private static List<Renderer> BakeVisibleMeshes(GameObject go)
        {
            List<Renderer> result = new List<Renderer>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (r is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;
                    Mesh baked = new Mesh();
                    // Without the scale flag the vertices carry the renderer's world scale, so the copy needs only position and rotation
                    smr.BakeMesh(baked, false);
                    GameObject copy = new GameObject(smr.name + "_Baked");
                    copy.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    copy.AddComponent<MeshFilter>().sharedMesh = baked;
                    MeshRenderer mr = copy.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = smr.sharedMaterials;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    result.Add(mr);
                }
                else if (r is MeshRenderer)
                {
                    result.Add(r);
                }
                else
                {
                    r.enabled = false;   // particles, lines and text stay out of the face
                }
            }
            foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.enabled = false;
            foreach (Canvas c in go.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
            return result;
        }

        // The model over a warm radial backdrop with a soft dark rim
        private static Texture2D Composite(Texture2D shot)
        {
            Color32[] src = shot.GetPixels32();
            bool hasAlpha = false;
            for (int i = 0; i < src.Length; i += 7) { if (src[i].a < 250) { hasAlpha = true; break; } }

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            Color[] dst = new Color[src.Length];
            float half = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float dx = (x - half) / half, dy = (y - half * 1.15f) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.25f);
                    Color bg = Color.Lerp(BackdropCentre, BackdropEdge, d * d * (3f - 2f * d));
                    Color c = src[i];
                    float a = hasAlpha ? c.a : 1f;
                    Color o = hasAlpha ? new Color(c.r, c.g, c.b) * 1f : c;
                    Color px = Color.Lerp(bg, o, a);
                    // Outer vignette so the face sits inside the gold frame
                    float edge = Mathf.Max(Mathf.Abs(x - half), Mathf.Abs(y - half)) / half;
                    px *= Mathf.Lerp(1f, 0.7f, Mathf.Clamp01((edge - 0.82f) / 0.18f));
                    px.a = 1f;
                    dst[i] = px;
                }
            }
            if (!hasAlpha) Debug.LogWarning("[Portraits] render had no alpha; backdrop is the clear colour");
            tex.SetPixels(dst);
            tex.Apply();
            return tex;
        }

        private static void ConfigureImporter(string path)
        {
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = Size;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
    }
}
