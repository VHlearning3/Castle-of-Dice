using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Imports the elf tavern waitress (Tripo model, re-UV'd and rigged in BlenderSources/Elf_Waitress.blend)
    /// as a Humanoid with five clips (Idle and Walk carrying both trays, Serve, Attack, Die), builds her
    /// animator and saves Assets/PREFABS/NPCs/NPC_Elf_Waitress.prefab. No scene is touched: placing her is
    /// left to the editor. The trays are MeshRenderers on her hand bones ("Tray_Bread", "Tray_Beer").
    /// </summary>
    public static class SetupElfWaitressEditor
    {
        private const string Folder = "Assets/Characters/NPC_Elf_Waitress/";
        private const string FbxPath = Folder + "Elf_Waitress.fbx";
        private const string BodyTexturePath = Folder + "Elf_Waitress_BaseColor.png";
        private const string TrayTexturePath = Folder + "Elf_Trays_Palette.png";
        private const string BodyMaterialPath = Folder + "M_Elf_Waitress.mat";
        private const string TrayMaterialPath = Folder + "M_Elf_Trays.mat";
        private const string ControllerPath = "Assets/Characters/Animators/NPC_Elf_Waitress_Animator.controller";
        public const string PrefabPath = "Assets/PREFABS/NPCs/NPC_Elf_Waitress.prefab";
        private const string HeadTopBone = "mixamorig:HeadTop_End";

        // Same height as Mirabel (village NPCs are ~2.8-3.0 units tall at the game's scale)
        private const float TargetHeight = 2.9f;

        public const string IdleClip = "Idle";
        public const string WalkClip = "Walk";
        public const string ServeClip = "Serve";
        public const string AttackClip = "Attack";
        public const string DieClip = "Die";
        private static readonly string[] Clips = { IdleClip, WalkClip, ServeClip, AttackClip, DieClip };

        [MenuItem("CastleOfDice/Setup Elf Waitress (prefab only)")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupElfWaitressEditor] Exit Play Mode first.");
                return;
            }

            ConfigureTextures();
            Material body = MakeMaterial(BodyMaterialPath, BodyTexturePath, 0.15f);
            Material trays = MakeMaterial(TrayMaterialPath, TrayTexturePath, 0.2f);
            ConfigureModel(body, trays);
            AnimatorController controller = BuildController();
            BuildPrefab(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupElfWaitressEditor] NPC_Elf_Waitress prefab ready: " + PrefabPath);
        }

        private static void ConfigureTextures()
        {
            if (AssetImporter.GetAtPath(BodyTexturePath) is TextureImporter bodyTex)
            {
                bodyTex.maxTextureSize = 1024;
                bodyTex.sRGBTexture = true;
                bodyTex.mipmapEnabled = true;
                bodyTex.textureCompression = TextureImporterCompression.Compressed;
                bodyTex.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(TrayTexturePath) is TextureImporter trayTex)
            {
                // 8x8 colour cells: no filtering, no mips, no compression bleeding between cells
                trayTex.maxTextureSize = 32;
                trayTex.filterMode = FilterMode.Point;
                trayTex.mipmapEnabled = false;
                trayTex.textureCompression = TextureImporterCompression.Uncompressed;
                trayTex.SaveAndReimport();
            }
        }

        private static Material MakeMaterial(string path, string texturePath, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureModel(Material body, Material trays)
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[SetupElfWaitressEditor] FBX not found: " + FbxPath);
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Elf_Waitress"), body);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Elf_Trays"), trays);

            // Mixamo bone names and a clean T-pose rest: Unity's humanoid auto-mapping takes it as is
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false; // keep the hand bones so the trays stay addressable
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            var clips = new List<ModelImporterClipAnimation>();
            foreach (string role in Clips)
            {
                ModelImporterClipAnimation source = null;
                foreach (ModelImporterClipAnimation take in importer.defaultClipAnimations)
                {
                    if (take.takeName == role || take.takeName.EndsWith("|" + role)) { source = take; break; }
                }
                if (source == null)
                {
                    Debug.LogWarning("[SetupElfWaitressEditor] Take '" + role + "' not found in " + FbxPath);
                    continue;
                }

                bool loops = role == IdleClip || role == WalkClip;
                source.name = role;
                source.loopTime = loops;
                source.loopPose = loops;
                source.lockRootRotation = true;
                source.keepOriginalOrientation = false;
                source.lockRootHeightY = true;
                source.keepOriginalPositionY = true;
                // Horizontal travel is root motion, which her Animator ignores: every clip plays in place,
                // so Die drops her on her own grid tile instead of a body length behind it
                source.lockRootPositionXZ = false;
                clips.Add(source);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (asset is AnimationClip clip && clip.name == name) return clip;
            }
            return null;
        }

        private static Avatar LoadAvatar()
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (asset is Avatar avatar) return avatar;
            }
            return null;
        }

        /// <summary>Same parameters as the village NPCs (IsMoving, Attack, Die) plus a Serve trigger.</summary>
        private static AnimatorController BuildController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Serve", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);

            AnimatorState idle = sm.AddState(IdleClip, new Vector3(300f, 0f, 0f));
            idle.motion = LoadClip(IdleClip);
            AnimatorState walk = sm.AddState(WalkClip, new Vector3(300f, 120f, 0f));
            walk.motion = LoadClip(WalkClip);
            AnimatorState serve = sm.AddState(ServeClip, new Vector3(560f, -120f, 0f));
            serve.motion = LoadClip(ServeClip);
            AnimatorState attack = sm.AddState(AttackClip, new Vector3(560f, 0f, 0f));
            attack.motion = LoadClip(AttackClip);
            AnimatorState die = sm.AddState(DieClip, new Vector3(560f, 120f, 0f));
            die.motion = LoadClip(DieClip);
            sm.defaultState = idle;

            AnimatorStateTransition toWalk = idle.AddTransition(walk);
            toWalk.hasExitTime = false; toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            AnimatorStateTransition toIdle = walk.AddTransition(idle);
            toIdle.hasExitTime = false; toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            AnimatorStateTransition toServe = idle.AddTransition(serve);
            toServe.hasExitTime = false; toServe.duration = 0.15f;
            toServe.AddCondition(AnimatorConditionMode.If, 0f, "Serve");
            AnimatorStateTransition serveBack = serve.AddTransition(idle);
            serveBack.hasExitTime = true; serveBack.exitTime = 0.95f; serveBack.duration = 0.15f;

            AnimatorStateTransition toAttack = sm.AddAnyStateTransition(attack);
            toAttack.hasExitTime = false; toAttack.duration = 0.1f; toAttack.canTransitionToSelf = false;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            AnimatorStateTransition attackBack = attack.AddTransition(idle);
            attackBack.hasExitTime = true; attackBack.exitTime = 0.95f; attackBack.duration = 0.15f;

            AnimatorStateTransition toDie = sm.AddAnyStateTransition(die);
            toDie.hasExitTime = false; toDie.duration = 0.1f; toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildPrefab(AnimatorController controller)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) return;

            var root = new GameObject("NPC_Elf_Waitress");
            try
            {
                CapsuleCollider col = root.AddComponent<CapsuleCollider>();
                col.radius = 0.6f;
                col.height = TargetHeight;
                col.center = new Vector3(0f, TargetHeight * 0.5f, 0f);

                var visuals = new GameObject("Visuals").transform;
                visuals.SetParent(root.transform, false);
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, visuals);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * FitScale(model);

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = false;
                    Bounds b = smr.localBounds;
                    b.Expand(b.size.magnitude * 0.5f); // not culled while swinging a tray or falling
                    smr.localBounds = b;
                }

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = LoadAvatar();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Uniform scale that makes feet-to-crown equal the target height (same rule as the village NPCs).</summary>
        private static float FitScale(GameObject model)
        {
            Transform headTop = null;
            float lowest = float.MaxValue;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == HeadTopBone) headTop = t;
                if (t.name.Contains("Toe") || t.name.Contains("Foot")) lowest = Mathf.Min(lowest, t.position.y);
            }
            float height = headTop != null && lowest < float.MaxValue ? headTop.position.y - lowest : 1f;
            return TargetHeight / Mathf.Max(0.01f, height * 1.04f);
        }
    }
}
