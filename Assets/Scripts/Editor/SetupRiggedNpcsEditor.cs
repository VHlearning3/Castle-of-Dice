using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Replaces the boneless Barnaby / Mirabel / Othelia village NPC models with the rigged (Mixamo skeleton)
    /// FBX versions and wires the animations shipped inside those files:
    /// Idle, Walk, Attack (kick / punch) and Die (fall).
    /// The existing prefabs are updated in place, so their GUIDs and every scene instance are kept.
    /// </summary>
    public static class SetupRiggedNpcsEditor
    {
        private sealed class RiggedNpc
        {
            public string PrefabPath;
            public string FbxPath;
            public string TexturePath;
            public string MaterialPath;
            public string ControllerPath;
            public string SceneObjectName;
            public float TargetHeight;
            // Take names inside the FBX for each role (Walk may be null when the file has no walk take)
            public string IdleTake;
            public string WalkTake;
            public string AttackTake;
            public string DieTake;
        }

        private const string IdleClip = "Idle";
        private const string WalkClip = "Walk";
        private const string AttackClip = "Attack";
        private const string DieClip = "Die";
        private const string MotionNode = "mixamorig:Hips";
        private const string HeadTopBone = "mixamorig:HeadTop_End";
        private const string Zone1ScenePath = "Assets/Scenes/Zone_1_VillageAndCellar.unity";

        // Village NPCs in Zone 1 are ~3.0 units tall (matches Baldur and the hero at the scene's scale)
        private static readonly RiggedNpc[] Npcs =
        {
            new RiggedNpc
            {
                PrefabPath = "Assets/PREFABS/NPCs/NPC_Barnaby_3dmodel.prefab",
                FbxPath = "Assets/Characters/NPC_BarnabyFIXED/NPC_BarnabyFIXED.fbx",
                TexturePath = "Assets/Characters/NPC_BarnabyFIXED/tripo_convert_317f365c-d18a-45f3-b64c-732fde1b6a29.fbm/chef_character_3d_model_basecolor.JPEG",
                MaterialPath = "Assets/Characters/Materials/M_NPC_Barnaby.mat",
                ControllerPath = "Assets/Characters/Animators/NPC_Barnaby_Animator.controller",
                SceneObjectName = "NPC_Barnaby",
                TargetHeight = 3.0f,
                IdleTake = "idle", WalkTake = "walk", AttackTake = "front_kick_01", DieTake = "fall",
            },
            new RiggedNpc
            {
                PrefabPath = "Assets/PREFABS/NPCs/NPC_Mirabel_3dmodel.prefab",
                FbxPath = "Assets/Characters/NPC_Mirabel_3dmodelFIXED/NPC_Mirabel_3dmodel.fbx",
                TexturePath = "Assets/Characters/NPC_Mirabel_3dmodelFIXED/tripo_convert_fc421eec-b85d-4daf-b0e7-5a03742e6e77.fbm/farmer_girl_3d_model_basecolor.JPEG",
                MaterialPath = "Assets/Characters/Materials/M_NPC_Mirabel.mat",
                ControllerPath = "Assets/Characters/Animators/NPC_Mirabel_Animator.controller",
                SceneObjectName = "NPC_Mirabel",
                TargetHeight = 2.9f,
                IdleTake = "idle", WalkTake = "walk", AttackTake = "front_kick_02", DieTake = "fall",
            },
            new RiggedNpc
            {
                PrefabPath = "Assets/PREFABS/NPCs/NPC_Othelia_3dmodel.prefab",
                FbxPath = "Assets/Characters/NPC_Othelia_3d_model_FixedSpecialanimations/NPC_Old_Women_FixedSpecialanimations.fbx",
                TexturePath = "Assets/Characters/NPC_Othelia_3d_model_FixedSpecialanimations/tripo_convert_2a8e4ba4-d61b-4c4d-8f5f-4b510d3849ef.fbm/NPC_Old_Women_FixedSpecialanimations_basecolor.JPEG",
                MaterialPath = "Assets/Characters/Materials/M_NPC_Othelia.mat",
                ControllerPath = "Assets/Characters/Animators/NPC_Othelia_Animator.controller",
                SceneObjectName = "NPC_Othelia",
                TargetHeight = 2.8f,
                // This file ships no walk take: the Walk state reuses the idle loop
                IdleTake = "Old women idle", WalkTake = null, AttackTake = "Old women punch", DieTake = "Old women falls down",
            },
        };

        [MenuItem("CastleOfDice/Setup Rigged NPCs (Barnaby, Mirabel, Othelia)")]
        public static void SetupAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupRiggedNpcsEditor] Exit Play Mode first.");
                return;
            }

            foreach (RiggedNpc npc in Npcs)
            {
                ConfigureImporter(npc);
            }
            AssetDatabase.Refresh();

            foreach (RiggedNpc npc in Npcs)
            {
                Material material = SetupMaterial(npc);
                AnimatorController controller = BuildController(npc);
                UpdatePrefab(npc, material, controller);
            }

            AssetDatabase.SaveAssets();
            PlaceInZone1();
            Debug.Log("[SetupRiggedNpcsEditor] Rigged Barnaby, Mirabel and Othelia prefabs updated and placed in Zone 1.");
        }

        /// <summary>Entry point used by SetupNpcPrefabsEditor so re-running it keeps the rigged models.</summary>
        public static void SetupPrefabsOnly()
        {
            foreach (RiggedNpc npc in Npcs) ConfigureImporter(npc);
            AssetDatabase.Refresh();
            foreach (RiggedNpc npc in Npcs) UpdatePrefab(npc, SetupMaterial(npc), BuildController(npc));
            AssetDatabase.SaveAssets();
        }

        #region Import

        private static void ConfigureImporter(RiggedNpc npc)
        {
            ModelImporter importer = AssetImporter.GetAtPath(npc.FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupRiggedNpcsEditor] FBX not found: {npc.FbxPath}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // Hips as root node: horizontal travel becomes root motion (discarded, applyRootMotion = false),
            // so Walk and Idle play in place instead of drifting off and snapping back each loop
            importer.motionNodeName = MotionNode;
            importer.importAnimation = true;

            var clips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation source in importer.defaultClipAnimations)
            {
                string role = RoleForTake(npc, source.takeName);
                if (role == null) continue;

                source.name = role;
                bool loops = role == IdleClip || role == WalkClip;
                source.loopTime = loops;
                source.loopPose = loops;
                source.lockRootRotation = true;      // bake rotation into pose
                source.keepOriginalOrientation = true;
                source.lockRootHeightY = true;       // bake height (hip bob / falling to the floor) into pose
                source.keepOriginalPositionY = true;
                source.lockRootPositionXZ = false;   // XZ travel -> root motion (ignored)
                clips.Add(source);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static string RoleForTake(RiggedNpc npc, string take)
        {
            if (take == npc.IdleTake) return IdleClip;
            if (npc.WalkTake != null && take == npc.WalkTake) return WalkClip;
            if (take == npc.AttackTake) return AttackClip;
            if (take == npc.DieTake) return DieClip;
            return null;
        }

        private static AnimationClip LoadClip(RiggedNpc npc, string clipName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(npc.FbxPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName) return clip;
            }
            return null;
        }

        private static Avatar LoadAvatar(RiggedNpc npc)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(npc.FbxPath))
            {
                if (asset is Avatar avatar) return avatar;
            }
            return null;
        }

        #endregion

        #region Material & Animator

        private static Material SetupMaterial(RiggedNpc npc)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(npc.MaterialPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, npc.MaterialPath);
            }

            // The rigged models have new UV layouts: use the base colour texture exported with each FBX
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(npc.TexturePath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                Debug.LogWarning($"[SetupRiggedNpcsEditor] Texture missing: {npc.TexturePath}");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimatorController BuildController(RiggedNpc npc)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(npc.ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(npc.ControllerPath);

            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                controller.RemoveParameter(p);
            }
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);

            AnimationClip idle = LoadClip(npc, IdleClip);
            AnimationClip walk = LoadClip(npc, WalkClip) ?? idle;
            AnimationClip attack = LoadClip(npc, AttackClip);
            AnimationClip die = LoadClip(npc, DieClip);

            AnimatorState idleState = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
            idleState.motion = idle;
            AnimatorState walkState = sm.AddState("Walk", new Vector3(300f, 120f, 0f));
            walkState.motion = walk;
            AnimatorState attackState = sm.AddState("Attack", new Vector3(560f, 0f, 0f));
            attackState.motion = attack;
            AnimatorState dieState = sm.AddState("Die", new Vector3(560f, 120f, 0f));
            dieState.motion = die;
            sm.defaultState = idleState;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attackState);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.1f;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.95f;
            attackToIdle.duration = 0.15f;

            AnimatorStateTransition anyToDie = sm.AddAnyStateTransition(dieState);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.1f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            if (idle == null || attack == null || die == null)
            {
                Debug.LogWarning($"[SetupRiggedNpcsEditor] {npc.SceneObjectName}: missing clip(s) idle={idle != null} attack={attack != null} die={die != null}");
            }
            return controller;
        }

        #endregion

        #region Prefab

        private static void UpdatePrefab(RiggedNpc npc, Material material, AnimatorController controller)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(npc.FbxPath);
            if (fbx == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(npc.PrefabPath);
            try
            {
                // Generic clips animate bone paths relative to the FBX root, so the Animator must live on the model
                Animator rootAnimator = root.GetComponent<Animator>();
                if (rootAnimator != null) Object.DestroyImmediate(rootAnimator);

                Transform visuals = root.transform.Find("Visuals");
                if (visuals == null)
                {
                    visuals = new GameObject("Visuals").transform;
                    visuals.SetParent(root.transform, false);
                }
                for (int i = visuals.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(visuals.GetChild(i).gameObject);
                }

                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, visuals);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                float scale = FitScale(model, npc.TargetHeight);
                model.transform.localScale = Vector3.one * scale;

                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
                foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = false;
                    // Generous local bounds so the animated mesh is not culled while kicking or falling
                    Bounds b = smr.localBounds;
                    b.Expand(b.size.magnitude * 0.5f);
                    smr.localBounds = b;
                }

                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = LoadAvatar(npc);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // Interaction collider sized to the new model
                CapsuleCollider col = root.GetComponent<CapsuleCollider>();
                if (col != null)
                {
                    col.height = npc.TargetHeight;
                    col.center = new Vector3(0f, npc.TargetHeight * 0.5f, 0f);
                    col.radius = 0.6f;
                }

                PrefabUtility.SaveAsPrefabAsset(root, npc.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Uniform scale that makes the model's feet-to-head-top distance equal the target height.</summary>
        private static float FitScale(GameObject model, float targetHeight)
        {
            Transform headTop = null;
            float lowest = float.MaxValue;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == HeadTopBone) headTop = t;
                if (t.name.Contains("Toe") || t.name.Contains("Foot")) lowest = Mathf.Min(lowest, t.position.y);
            }

            float height = headTop != null && lowest < float.MaxValue ? headTop.position.y - lowest : 0f;
            if (height < 0.01f)
            {
                Bounds b = new Bounds();
                bool has = false;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
                height = has ? b.size.y : 1f;
            }

            // Head-top bone sits at the crown; feet bones sit slightly above the sole
            return targetHeight / Mathf.Max(0.01f, height * 1.04f);
        }

        #endregion

        #region Scene

        /// <summary>Stands the rigged NPC instances upright on the ground in Zone 1 and saves the scene.</summary>
        public static void PlaceRiggedNpcsInZone1() => PlaceInZone1();

        private static void PlaceInZone1()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Zone1ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(Zone1ScenePath, OpenSceneMode.Single);
            }

            foreach (RiggedNpc npc in Npcs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(npc.PrefabPath);
                GameObject instance = FindSceneObject(npc.SceneObjectName);
                if (instance == null && prefab != null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = npc.SceneObjectName;
                }
                if (instance == null) continue;

                // Keep the authored heading but stand the (now skeletal) character upright
                float yaw = instance.transform.eulerAngles.y;
                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                SnapToGround(instance);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name && go.scene.IsValid()) return go;
            }
            return null;
        }

        /// <summary>Rigged models pivot at the feet: stand the NPC on the first surface below it.</summary>
        private static void SnapToGround(GameObject npc)
        {
            Collider own = npc.GetComponent<Collider>();
            bool wasEnabled = own != null && own.enabled;
            if (own != null) own.enabled = false;

            Vector3 origin = npc.transform.position + Vector3.up * 3f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = npc.transform.position;
                npc.transform.position = new Vector3(p.x, hit.point.y, p.z);
            }

            if (own != null) own.enabled = wasEnabled;
        }

        #endregion
    }
}
