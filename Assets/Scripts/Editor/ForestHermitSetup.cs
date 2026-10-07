using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// The old hermit of the Whispering Woods (BlenderSources/Forest_OldMan_build.py): Humanoid import, an
    /// animator with Sit_Idle (loops) and Sit_Speak (plays once, back to Sit_Idle), and the prefab. The scene copy
    /// sits on the log by the cottage in Zone_2_ForestPath (placed by hand; this only rebuilds the prefab).
    /// </summary>
    public static class ForestHermitSetup
    {
        private const string Folder = "Assets/Characters/Forest_OldMan/";
        private const string ModelPath = Folder + "Forest_OldMan.fbx";
        private const string PalettePath = Folder + "Forest_OldMan_Palette.png";
        private const string MaterialPath = Folder + "M_Forest_OldMan.mat";
        private const string ControllerPath = "Assets/Characters/Animators/NPC_Forest_Hermit_Animator.controller";
        public const string PrefabPath = "Assets/PREFABS/NPCs/NPC_Forest_Hermit.prefab";

        /// <summary>Standing height in game units; the build script sizes his seat for this (log top 0.8).</summary>
        public const float Height = 2.85f;

        [MenuItem("CastleOfDice/Setup Forest Hermit (prefab only)")]
        public static void SetupAssets()
        {
            if (AssetImporter.GetAtPath(PalettePath) is TextureImporter pal)
            {
                pal.filterMode = FilterMode.Point;
                pal.mipmapEnabled = false;
                pal.textureCompression = TextureImporterCompression.Uncompressed;
                pal.maxTextureSize = 32;
                pal.SaveAndReimport();
            }
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);

            if (!ConfigureImporter(mat)) return;
            AnimatorController controller = BuildController();
            BuildPrefab(controller);
            AssetDatabase.SaveAssets();
        }

        private static bool ConfigureImporter(Material mat)
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
            {
                Debug.LogError("[ForestHermitSetup] Missing " + ModelPath + " (run BlenderSources/Forest_OldMan_build.py)");
                return false;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Forest_OldMan"), mat);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (string take in new[] { ForestHermit.IdleState, ForestHermit.SpeakState })
            {
                foreach (ModelImporterClipAnimation src in importer.defaultClipAnimations)
                {
                    if (src.takeName != take && !src.takeName.EndsWith("|" + take)) continue;
                    src.name = take;
                    src.loopTime = take == ForestHermit.IdleState;
                    src.loopPose = take == ForestHermit.IdleState;
                    src.lockRootRotation = true;
                    src.keepOriginalOrientation = true;
                    src.lockRootHeightY = true;
                    src.keepOriginalPositionY = true;       // seated hips stay at log height
                    src.lockRootPositionXZ = true;
                    src.keepOriginalPositionXZ = true;
                    clips.Add(src);
                    break;
                }
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
            return true;
        }

        private static AnimationClip Clip(string take)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (o is AnimationClip c && c.name == take) return c;
            return null;
        }

        private static AnimatorController BuildController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
            AnimatorState idle = sm.AddState(ForestHermit.IdleState, new Vector3(300f, 0f, 0f));
            idle.motion = Clip(ForestHermit.IdleState);
            AnimatorState speak = sm.AddState(ForestHermit.SpeakState, new Vector3(300f, 90f, 0f));
            speak.motion = Clip(ForestHermit.SpeakState);
            sm.defaultState = idle;
            AnimatorStateTransition back = speak.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.94f;
            back.duration = 0.25f;
            back.hasFixedDuration = true;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildPrefab(AnimatorController controller)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (fbx == null) return;
            var root = new GameObject("NPC_Forest_Hermit");
            try
            {
                root.layer = 8;   // people: left out of the runtime NavMesh, still clickable
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = "Model";
                model.transform.localScale = Vector3.one * Height; // the rig stands 1.0 tall
                Animator anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                    if (o is Avatar a) anim.avatar = a;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Bounds b = smr.localBounds;
                    b.Expand(b.size.magnitude * 0.4f);
                    smr.localBounds = b;
                }
                // Seated on the log: a capsule over his body for clicks (the log itself blocks walking)
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.15f, 0.15f);
                col.radius = 0.55f;
                col.height = 2.3f;
                var hermit = root.AddComponent<ForestHermit>();
                hermit.PromptMessage = "Talk to the Old Hermit";
                hermit.InteractionRadius = 2.5f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
