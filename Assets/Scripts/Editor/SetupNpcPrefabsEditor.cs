using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CastleOfTheD20.World;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Creates and configures NPC prefabs for:
    /// - NPC_Barnaby_3dmodel
    /// - NPC_Othelia_3dmodel
    /// - NPC_Mirabel_3dmodel
    /// - NPC_Baldur_Smith
    /// Configures Animator Controllers containing strictly: walk, idle, and attack animations.
    /// Updates scene instances in Zone_1_VillageAndCellar.
    /// </summary>
    public static class SetupNpcPrefabsEditor
    {
        private const string BALDUR_FBX = "Assets/Characters/NPC_Baldur_Smith.fbx";
        private const string BALDUR_TEX = "Assets/Characters/NPC_Baldur_Smith_Texture.JPEG";
        private const string BALDUR_MAT = "Assets/Characters/Materials/M_NPC_Baldur_Smith.mat";
        private const string BALDUR_ANIMATOR = "Assets/Characters/Animators/NPC_Baldur_Smith_Animator.controller";

        private const string BARNABY_FBX = "Assets/Characters/NPC_Barnaby_3dmodel.fbx";
        private const string BARNABY_TEX = "Assets/Characters/NPC_Barnaby_3d_Texture.jpg";
        private const string BARNABY_MAT = "Assets/Characters/Materials/M_NPC_Barnaby.mat";
        private const string BARNABY_ANIMATOR = "Assets/Characters/Animators/NPC_Barnaby_Animator.controller";

        private const string OTHELIA_FBX = "Assets/Characters/NPC_Othelia_3dmodel.fbx";
        private const string OTHELIA_TEX = "Assets/Characters/Old_noble_women_traditional+dress+3d+model_basecolor.jpg";
        private const string OTHELIA_MAT = "Assets/Characters/Materials/M_NPC_Othelia.mat";
        private const string OTHELIA_ANIMATOR = "Assets/Characters/Animators/NPC_Othelia_Animator.controller";

        private const string MIRABEL_FBX = "Assets/Characters/NPC_Mirabel_3dmodel.fbx";
        private const string MIRABEL_TEX = "Assets/Characters/NPC_Mirabel_Texture.jpg";
        private const string MIRABEL_MAT = "Assets/Characters/Materials/M_NPC_Mirabel.mat";
        private const string MIRABEL_ANIMATOR = "Assets/Characters/Animators/NPC_Mirabel_Animator.controller";

        private const string GENERIC_IDLE_CLIP = "Assets/Characters/Animations/NPC_Generic_Idle.anim";
        private const string GENERIC_WALK_CLIP = "Assets/Characters/Animations/NPC_Generic_Walk.anim";
        private const string GENERIC_ATTACK_CLIP = "Assets/Characters/Animations/NPC_Generic_Attack.anim";

        private const float NPC_MODEL_SCALE = 1.8f;

        [MenuItem("CastleOfDice/Setup All NPC Prefabs and Animations")]
        public static void SetupAllNpcs()
        {
            Debug.Log("==================================================");
            Debug.Log("[SetupNpcPrefabsEditor] Starting NPC Setup...");

            EnsureDirectories();

            // 1. Materials
            Material baldurMat = SetupMaterial(BALDUR_MAT, BALDUR_TEX);
            Material barnabyMat = SetupMaterial(BARNABY_MAT, BARNABY_TEX);
            Material otheliaMat = SetupMaterial(OTHELIA_MAT, OTHELIA_TEX);
            Material mirabelMat = SetupMaterial(MIRABEL_MAT, MIRABEL_TEX);

            // 2. Animations & Controllers
            SetupGenericAnimationClips();
            AnimatorController baldurController = SetupBaldurAnimatorController();
            AnimatorController barnabyController = SetupStaticNpcAnimatorController(BARNABY_ANIMATOR, "Barnaby");
            AnimatorController otheliaController = SetupStaticNpcAnimatorController(OTHELIA_ANIMATOR, "Othelia");
            AnimatorController mirabelController = SetupStaticNpcAnimatorController(MIRABEL_ANIMATOR, "Mirabel");

            // 3. Dialogue Nodes
            DialogueNodeSO baldurDialogue = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Baldur_StartNode.asset")
                ?? AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Baldur_Intro.asset");
            DialogueNodeSO barnabyDialogue = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Barnaby_Intro.asset");
            DialogueNodeSO otheliaDialogue = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Othelia_Intro.asset");
            DialogueNodeSO mirabelDialogue = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Mirabel_Intro.asset");

            // 4. Build Prefabs
            GameObject baldurPrefab = CreateNpcPrefab("NPC_Baldur_Smith", BALDUR_FBX, baldurMat, baldurController,
                "Baldur", isBlacksmith: true, baldurDialogue, "Talk to Blacksmith Baldur", isSkeletal: true);

            GameObject barnabyPrefab = CreateNpcPrefab("NPC_Barnaby_3dmodel", BARNABY_FBX, barnabyMat, barnabyController,
                "Barnaby", isBlacksmith: false, barnabyDialogue, "Talk to Barnaby", isSkeletal: false);

            GameObject otheliaPrefab = CreateNpcPrefab("NPC_Othelia_3dmodel", OTHELIA_FBX, otheliaMat, otheliaController,
                "Lady Othelia", isBlacksmith: false, otheliaDialogue, "Talk to Lady Othelia", isSkeletal: false);

            GameObject mirabelPrefab = CreateNpcPrefab("NPC_Mirabel_3dmodel", MIRABEL_FBX, mirabelMat, mirabelController,
                "Mirabel", isBlacksmith: false, mirabelDialogue, "Talk to Mirabel the Herbalist", isSkeletal: false);

            // 4b. Barnaby, Mirabel and Othelia use rigged FBX models with their own animations now
            SetupRiggedNpcsEditor.SetupPrefabsOnly();

            // 5. Update Scene Instances in Zone 1
            UpdateSceneInstances(baldurPrefab, barnabyPrefab, otheliaPrefab, mirabelPrefab);
            SetupRiggedNpcsEditor.PlaceRiggedNpcsInZone1();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupNpcPrefabsEditor] NPC Prefabs and Animations Setup completed successfully!");
            Debug.Log("==================================================");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Characters/Materials"))
                AssetDatabase.CreateFolder("Assets/Characters", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Characters/Animators"))
                AssetDatabase.CreateFolder("Assets/Characters", "Animators");
            if (!AssetDatabase.IsValidFolder("Assets/Characters/Animations"))
                AssetDatabase.CreateFolder("Assets/Characters", "Animations");
            if (!AssetDatabase.IsValidFolder("Assets/PREFABS"))
                AssetDatabase.CreateFolder("Assets", "PREFABS");
            if (!AssetDatabase.IsValidFolder("Assets/PREFABS/NPCs"))
                AssetDatabase.CreateFolder("Assets/PREFABS", "NPCs");
        }

        #region Materials

        private static Material SetupMaterial(string matPath, string texPath)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            }
            else
            {
                Debug.LogWarning($"[SetupNpcPrefabsEditor] Texture missing at {texPath}");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        #endregion

        #region Animation Clips & Controllers

        private static void SetupGenericAnimationClips()
        {
            // 1. Idle Clip (gentle breathing bob + subtle yaw sway)
            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_IDLE_CLIP);
            if (idleClip == null)
            {
                idleClip = new AnimationClip { name = "NPC_Generic_Idle" };
                AssetDatabase.CreateAsset(idleClip, GENERIC_IDLE_CLIP);
            }
            idleClip.ClearCurves();

            AnimationCurve idleY = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.035f);
            idleY.AddKey(new Keyframe(2f, 0f, 0f, 0f));
            idleY.postWrapMode = WrapMode.Loop;
            idleClip.SetCurve("Visuals", typeof(Transform), "localPosition.y", idleY);

            AnimationCurve idleRotY = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, -1.5f),
                new Keyframe(1.5f, 1.5f),
                new Keyframe(2.0f, 0f)
            );
            idleRotY.postWrapMode = WrapMode.Loop;
            idleClip.SetCurve("Visuals", typeof(Transform), "localEulerAnglesRaw.y", idleRotY);

            AnimationCurve idleRotX = AnimationCurve.EaseInOut(0f, 0f, 1f, 1.2f);
            idleRotX.AddKey(new Keyframe(2f, 0f, 0f, 0f));
            idleRotX.postWrapMode = WrapMode.Loop;
            idleClip.SetCurve("Visuals", typeof(Transform), "localEulerAnglesRaw.x", idleRotX);

            AnimationClipSettings idleSettings = AnimationUtility.GetAnimationClipSettings(idleClip);
            idleSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(idleClip, idleSettings);
            EditorUtility.SetDirty(idleClip);

            // 2. Walk Clip (low-poly step bounce + roll roll sway)
            AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_WALK_CLIP);
            if (walkClip == null)
            {
                walkClip = new AnimationClip { name = "NPC_Generic_Walk" };
                AssetDatabase.CreateAsset(walkClip, GENERIC_WALK_CLIP);
            }
            walkClip.ClearCurves();

            AnimationCurve walkY = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 0.08f),
                new Keyframe(0.4f, 0f),
                new Keyframe(0.6f, 0.08f),
                new Keyframe(0.8f, 0f)
            );
            walkY.postWrapMode = WrapMode.Loop;
            walkClip.SetCurve("Visuals", typeof(Transform), "localPosition.y", walkY);

            AnimationCurve walkRotZ = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, -3.5f),
                new Keyframe(0.4f, 0f),
                new Keyframe(0.6f, 3.5f),
                new Keyframe(0.8f, 0f)
            );
            walkRotZ.postWrapMode = WrapMode.Loop;
            walkClip.SetCurve("Visuals", typeof(Transform), "localEulerAnglesRaw.z", walkRotZ);

            AnimationCurve walkRotX = new AnimationCurve(
                new Keyframe(0f, 2.5f),
                new Keyframe(0.4f, 4.5f),
                new Keyframe(0.8f, 2.5f)
            );
            walkRotX.postWrapMode = WrapMode.Loop;
            walkClip.SetCurve("Visuals", typeof(Transform), "localEulerAnglesRaw.x", walkRotX);

            AnimationClipSettings walkSettings = AnimationUtility.GetAnimationClipSettings(walkClip);
            walkSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(walkClip, walkSettings);
            EditorUtility.SetDirty(walkClip);

            // 3. Attack Clip (windup, lunge thrust, recovery)
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_ATTACK_CLIP);
            if (attackClip == null)
            {
                attackClip = new AnimationClip { name = "NPC_Generic_Attack" };
                AssetDatabase.CreateAsset(attackClip, GENERIC_ATTACK_CLIP);
            }
            attackClip.ClearCurves();

            AnimationCurve atkZ = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, -0.12f),
                new Keyframe(0.35f, 0.45f),
                new Keyframe(0.55f, 0.1f),
                new Keyframe(0.7f, 0f)
            );
            attackClip.SetCurve("Visuals", typeof(Transform), "localPosition.z", atkZ);

            AnimationCurve atkRotX = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, -6f),
                new Keyframe(0.35f, 15f),
                new Keyframe(0.55f, 4f),
                new Keyframe(0.7f, 0f)
            );
            attackClip.SetCurve("Visuals", typeof(Transform), "localEulerAnglesRaw.x", atkRotX);

            AnimationCurve atkY = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 0.02f),
                new Keyframe(0.35f, 0.08f),
                new Keyframe(0.7f, 0f)
            );
            attackClip.SetCurve("Visuals", typeof(Transform), "localPosition.y", atkY);

            AnimationClipSettings attackSettings = AnimationUtility.GetAnimationClipSettings(attackClip);
            attackSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(attackClip, attackSettings);
            EditorUtility.SetDirty(attackClip);

            AssetDatabase.SaveAssets();
            Debug.Log("[SetupNpcPrefabsEditor] Generic animation clips (idle, walk, attack) created.");
        }

        private static AnimatorController SetupStaticNpcAnimatorController(string controllerPath, string npcName)
        {
            if (File.Exists(controllerPath))
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_IDLE_CLIP);
            AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_WALK_CLIP);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_ATTACK_CLIP);

            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            var walkState = rootStateMachine.AddState("walk");
            walkState.motion = walkClip;

            var attackState = rootStateMachine.AddState("attack");
            attackState.motion = attackClip;

            // idle <-> walk
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;

            // AnyState -> attack
            var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.1f;

            // attack -> idle
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.85f;
            attackToIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[SetupNpcPrefabsEditor] Controller created: {controllerPath}");
            return controller;
        }

        private static AnimatorController SetupBaldurAnimatorController()
        {
            // Configure FBX clip loop settings on Baldur's model
            ModelImporter importer = AssetImporter.GetAtPath(BALDUR_FBX) as ModelImporter;
            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

                var defaultClips = importer.defaultClipAnimations;
                if (defaultClips != null && defaultClips.Length > 0)
                {
                    foreach (var c in defaultClips)
                    {
                        if (c.name == "walk" || c.name == "agree")
                        {
                            c.loopTime = true;
                        }
                        else if (c.name == "box_01")
                        {
                            c.loopTime = false;
                        }
                    }
                    importer.clipAnimations = defaultClips;
                }
                importer.SaveAndReimport();
            }

            if (File.Exists(BALDUR_ANIMATOR))
            {
                AssetDatabase.DeleteAsset(BALDUR_ANIMATOR);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(BALDUR_ANIMATOR);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            AnimationClip walkClip = null;
            AnimationClip idleClip = null;
            AnimationClip attackClip = null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(BALDUR_FBX))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    if (clip.name == "walk") walkClip = clip;
                    else if (clip.name == "agree") idleClip = clip;
                    else if (clip.name == "box_01") attackClip = clip;
                }
            }

            // Fallbacks if not found
            if (idleClip == null) idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_IDLE_CLIP);
            if (walkClip == null) walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_WALK_CLIP);
            if (attackClip == null) attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GENERIC_ATTACK_CLIP);

            var rootStateMachine = controller.layers[0].stateMachine;

            var idleState = rootStateMachine.AddState("idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            var walkState = rootStateMachine.AddState("walk");
            walkState.motion = walkClip;

            var attackState = rootStateMachine.AddState("attack");
            attackState.motion = attackClip;

            // idle <-> walk
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;

            // AnyState -> attack
            var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.1f;

            // attack -> idle
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.85f;
            attackToIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[SetupNpcPrefabsEditor] Baldur Animator Controller created at {BALDUR_ANIMATOR}.");
            return controller;
        }

        #endregion

        #region Prefab Creation

        private static GameObject CreateNpcPrefab(
            string prefabName,
            string fbxPath,
            Material material,
            AnimatorController controller,
            string npcDisplayName,
            bool isBlacksmith,
            DialogueNodeSO dialogueNode,
            string prompt,
            bool isSkeletal)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[SetupNpcPrefabsEditor] FBX missing at {fbxPath}!");
                return null;
            }

            Avatar avatar = null;
            if (isSkeletal)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                {
                    if (asset is Avatar av)
                    {
                        avatar = av;
                        break;
                    }
                }
            }

            // Create temporary instance to save as prefab
            GameObject root = new GameObject(prefabName);
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0) root.layer = interactableLayer;

            // Collider
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.0f, 0f);
            collider.height = 2.0f;
            collider.radius = 0.5f;

            // VillageNPC
            var vNpc = root.AddComponent<VillageNPC>();
            vNpc.PromptMessage = prompt;
            vNpc.InteractionRadius = 4.0f;
            vNpc.IsInteractable = true;
            vNpc.IsBlacksmith = isBlacksmith;
            vNpc.StartingDialogueNode = dialogueNode;

            var so = new SerializedObject(vNpc);
            so.FindProperty("npcName").stringValue = npcDisplayName;
            so.FindProperty("isBlacksmith").boolValue = isBlacksmith;
            so.FindProperty("openShopDirectlyOnInteract").boolValue = false;
            so.FindProperty("startingDialogueNode").objectReferenceValue = dialogueNode;
            so.ApplyModifiedProperties();

            // Visuals
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localRotation = Quaternion.identity;
            visuals.transform.localScale = Vector3.one;

            // Model Instance
            GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, visuals.transform);
            modelInst.name = "Model";
            modelInst.transform.localPosition = Vector3.zero;
            modelInst.transform.localRotation = Quaternion.identity;
            modelInst.transform.localScale = Vector3.one * NPC_MODEL_SCALE;

            // Apply Materials & Shadows
            foreach (var smr in modelInst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.sharedMaterial = material;
                smr.receiveShadows = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var mr in modelInst.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.sharedMaterial = material;
                mr.receiveShadows = true;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // Animator
            // Attach Animator to root or model
            var anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            if (isSkeletal && avatar != null)
            {
                anim.avatar = avatar;
            }
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Save Prefab in Assets/PREFABS/NPCs/ (the copies in Assets/PREFABS/ were unused duplicates, removed 2026-10-04)
            string npcsFolderPrefab = $"Assets/PREFABS/NPCs/{prefabName}.prefab";

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, npcsFolderPrefab);

            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[SetupNpcPrefabsEditor] Successfully created prefab: {npcsFolderPrefab}");
            return savedPrefab;
        }

        #endregion

        #region Scene Updates

        private static void UpdateSceneInstances(
            GameObject baldurPrefab,
            GameObject barnabyPrefab,
            GameObject otheliaPrefab,
            GameObject mirabelPrefab)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "Zone_1_VillageAndCellar")
            {
                string path = "Assets/Scenes/Zone_1_VillageAndCellar.unity";
                if (File.Exists(path))
                {
                    EditorSceneManager.OpenScene(path);
                }
            }

            // 1. Baldur
            ReplaceOrUpdateSceneNpc("NPC_Baldur", baldurPrefab, new Vector3(16.8f, 0.05f, -6.2f), Quaternion.Euler(0f, 220f, 0f));

            // 2. Barnaby
            ReplaceOrUpdateSceneNpc("NPC_Barnaby", barnabyPrefab, new Vector3(0.2f, 0.05f, -5.2f), Quaternion.Euler(0f, 160f, 0f));

            // 3. Othelia
            ReplaceOrUpdateSceneNpc("NPC_Othelia", otheliaPrefab, new Vector3(11.5f, 0.05f, 12.5f), Quaternion.Euler(0f, 230f, 0f));

            // 4. Mirabel
            ReplaceOrUpdateSceneNpc("NPC_Mirabel", mirabelPrefab, new Vector3(-18.5f, 0.05f, 8.5f), Quaternion.Euler(0f, 130f, 0f));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[SetupNpcPrefabsEditor] Zone_1_VillageAndCellar NPCs updated and scene saved.");
        }

        private static void ReplaceOrUpdateSceneNpc(string objectName, GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return;

            GameObject existing = GameObject.Find(objectName);
            Transform parent = null;
            if (existing != null)
            {
                pos = existing.transform.position;
                rot = existing.transform.rotation;
                parent = existing.transform.parent;
                UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = objectName;
            instance.transform.position = pos;
            instance.transform.rotation = rot;
            Debug.Log($"[SetupNpcPrefabsEditor] Instantiated {objectName} in scene from {prefab.name}.");
        }

        #endregion
    }
}
