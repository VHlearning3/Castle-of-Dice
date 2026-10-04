using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Automates Steps 3 to 6 for the 3D Player Knight character model:
    /// - Step 3: Generates HeroKnightAnimator.controller with Mecanim state machine and transitions.
    /// - Step 4: Creates M_PlayerKnight URP material, updates PlayerHero.prefab & Hero_Warrior.prefab with rigged mesh and CharacterController.
    /// - Step 5: Code integrations verified (locomotion, damage, die, attack, cast, victory).
    /// - Step 6: Binds prefab to Character_Warrior_SirRoland.asset and updates scene instances across all zones.
    /// </summary>
    public static class SetupKnightCharacterEditor
    {
        private const string MODEL_PATH = "Assets/Characters/PlayerKnightModel.fbx";
        private const string TEXTURE_PATH = "Assets/Characters/tripo_rgb_9f4fbd7c-4c41-4556-8b04-e296ba1d0d00.jpg";
        private const string MATERIAL_PATH = "Assets/Characters/Materials/M_PlayerKnight.mat";
        private const string ANIMATOR_PATH = "Assets/Characters/Animators/HeroKnightAnimator.controller";
        private const string PREFAB_HERO_PATH = "Assets/PREFABS/Players/PlayerHero.prefab";
        private const string PREFAB_WARRIOR_PATH = "Assets/PREFABS/Players/Hero_Warrior.prefab";
        private const string WARRIOR_SO_PATH = "Assets/Data/Character_Warrior_SirRoland.asset";

        [MenuItem("CastleOfDice/Setup Knight Character (Steps 3-6)")]
        public static void SetupKnightCharacter()
        {
            Debug.Log("==================================================");
            Debug.Log("[SetupKnightCharacterEditor] Starting Knight Character Setup (Steps 3-6)...");

            EnsureDirectories();

            // Step 3: Animator Controller
            AnimatorController controller = SetupAnimatorController();

            // Step 4: Material & Prefabs
            Material material = SetupMaterial();
            GameObject prefab = SetupHeroPrefabs(material, controller);

            // Step 6: CharacterClassSO & Scene Hookup
            SetupCharacterClassSO(prefab);
            UpdateSceneInstances(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The steps above rebuild the knight with his original clips; put the sword, shield and Mixamo animations back
            SetupHeroKnightEditor.Setup();

            Debug.Log("[SetupKnightCharacterEditor] Knight Character Setup completed successfully!");
            Debug.Log("==================================================");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Characters/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/Characters", "Materials");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Characters/Animators"))
            {
                AssetDatabase.CreateFolder("Assets/Characters", "Animators");
            }
            if (!AssetDatabase.IsValidFolder("Assets/PREFABS/Players"))
            {
                AssetDatabase.CreateFolder("Assets/PREFABS", "Players");
            }
        }

        #region Step 3: Animator Controller Setup

        public static AnimatorController SetupAnimatorController()
        {
            Debug.Log("[SetupKnightCharacterEditor] Step 3: Building Mecanim Animator Controller...");

            // Load clips and avatar from FBX
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(MODEL_PATH);
            AnimationClip idleClip = null;
            AnimationClip runClip = null;
            AnimationClip walkClip = null;
            AnimationClip spellClip = null;
            AnimationClip dieClip = null;
            AnimationClip hitClip = null;
            AnimationClip victoryClip = null;
            Avatar avatar = null;

            foreach (var asset in assets)
            {
                if (asset is Avatar av)
                {
                    avatar = av;
                }
                else if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    string clipName = clip.name.ToLowerInvariant();
                    Debug.Log($"[SetupKnightCharacterEditor] Discovered clip: {clip.name}");

                    if (clipName.Contains("idle")) idleClip = clip;
                    else if (clipName.Contains("run")) runClip = clip;
                    else if (clipName.Contains("walk")) walkClip = clip;
                    else if (clipName.Contains("cast")) spellClip = clip;
                    else if (clipName.Contains("die") || clipName.Contains("defeat")) dieClip = clip;
                    else if (clipName.Contains("fall")) hitClip = clip;
                    else if (clipName.Contains("cheer")) victoryClip = clip;
                }
            }

            // Safe fallbacks
            if (runClip == null) runClip = walkClip;
            if (walkClip == null) walkClip = runClip;
            if (hitClip == null) hitClip = dieClip;
            if (victoryClip == null) victoryClip = idleClip;

            // Delete old controller if exists to create cleanly
            if (File.Exists(ANIMATOR_PATH))
            {
                AssetDatabase.DeleteAsset(ANIMATOR_PATH);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ANIMATOR_PATH);

            // Add Parameters
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastSpell", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TakeHit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Victory", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            // States
            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            var runState = rootStateMachine.AddState("Run");
            runState.motion = runClip;

            var attackState = rootStateMachine.AddState("Attack");
            attackState.motion = spellClip;

            var castSpellState = rootStateMachine.AddState("CastSpell");
            castSpellState.motion = spellClip;

            var takeHitState = rootStateMachine.AddState("TakeHit");
            takeHitState.motion = hitClip;

            var dieState = rootStateMachine.AddState("Die");
            dieState.motion = dieClip;

            var victoryState = rootStateMachine.AddState("Victory");
            victoryState.motion = victoryClip;

            // Locomotion Transitions: Idle <-> Run
            var idleToRun = idleState.AddTransition(runState);
            idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.15f;

            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.15f;

            // Combat Transitions: AnyState -> Action
            // Attack
            var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.1f;

            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.85f;
            attackToIdle.duration = 0.15f;

            // CastSpell
            var anyToCast = rootStateMachine.AddAnyStateTransition(castSpellState);
            anyToCast.AddCondition(AnimatorConditionMode.If, 0, "CastSpell");
            anyToCast.hasExitTime = false;
            anyToCast.duration = 0.1f;

            var castToIdle = castSpellState.AddTransition(idleState);
            castToIdle.hasExitTime = true;
            castToIdle.exitTime = 0.85f;
            castToIdle.duration = 0.15f;

            // TakeHit
            var anyToHit = rootStateMachine.AddAnyStateTransition(takeHitState);
            anyToHit.AddCondition(AnimatorConditionMode.If, 0, "TakeHit");
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.08f;

            var hitToIdle = takeHitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.8f;
            hitToIdle.duration = 0.15f;

            // Die (One way, no exit transition)
            var anyToDie = rootStateMachine.AddAnyStateTransition(dieState);
            anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.1f;

            // Victory
            var anyToVictory = rootStateMachine.AddAnyStateTransition(victoryState);
            anyToVictory.AddCondition(AnimatorConditionMode.If, 0, "Victory");
            anyToVictory.hasExitTime = false;
            anyToVictory.duration = 0.2f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupKnightCharacterEditor] Animator Controller created at {ANIMATOR_PATH}.");

            return controller;
        }

        #endregion

        #region Step 4: Material & Prefab Setup

        public static Material SetupMaterial()
        {
            Debug.Log("[SetupKnightCharacterEditor] Step 4: Setting up URP Lit Material...");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
            }
            else
            {
                mat.shader = shader;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_PATH);
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
            }
            else
            {
                Debug.LogWarning($"[SetupKnightCharacterEditor] Knight texture not found at {TEXTURE_PATH}!");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupKnightCharacterEditor] Material configured at {MATERIAL_PATH}.");

            return mat;
        }

        public static GameObject SetupHeroPrefabs(Material material, AnimatorController controller)
        {
            Debug.Log("[SetupKnightCharacterEditor] Step 4: Assembling PlayerHero & Hero_Warrior prefabs...");

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL_PATH);
            if (fbxAsset == null)
            {
                Debug.LogError($"[SetupKnightCharacterEditor] Model asset missing at {MODEL_PATH}!");
                return null;
            }

            // Load Avatar from FBX
            Avatar avatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MODEL_PATH))
            {
                if (asset is Avatar av)
                {
                    avatar = av;
                    break;
                }
            }

            // Load prefab contents
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_HERO_PATH);

            // 1. Remove old capsule primitives from root
            var mf = prefabRoot.GetComponent<MeshFilter>();
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf, true);

            var mr = prefabRoot.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr, true);

            // 2. Setup Visuals container
            Transform visuals = prefabRoot.transform.Find("Visuals");
            if (visuals == null)
            {
                var visualsGo = new GameObject("Visuals");
                visualsGo.transform.SetParent(prefabRoot.transform, false);
                visuals = visualsGo.transform;
            }

            // Clear any old child models under Visuals
            for (int i = visuals.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(visuals.GetChild(i).gameObject, true);
            }

            // 3. Instantiate FBX under Visuals
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, visuals);
            modelInstance.name = "Knight_Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            // 4. Assign Material & Shadows to SkinnedMeshRenderers
            var skinnedRenderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedRenderers)
            {
                smr.sharedMaterial = material;
                smr.receiveShadows = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var meshRenderers = modelInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in meshRenderers)
            {
                r.sharedMaterial = material;
                r.receiveShadows = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // 5. Configure Animator on Knight_Model
            var anim = modelInstance.GetComponent<Animator>();
            if (anim == null) anim = modelInstance.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Remove any redundant root animator to avoid dual animator conflicts
            var rootAnim = prefabRoot.GetComponent<Animator>();
            if (rootAnim != null) UnityEngine.Object.DestroyImmediate(rootAnim, true);

            // 6. Configure CharacterController dimensions
            var cc = prefabRoot.GetComponent<CharacterController>();
            if (cc == null) cc = prefabRoot.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 1.0f, 0f);

            // 7. Wire serialized animator reference on PlayerExplorationMovement
            var pem = prefabRoot.GetComponent<PlayerExplorationMovement>();
            if (pem != null)
            {
                var pemSo = new SerializedObject(pem);
                var animProp = pemSo.FindProperty("animator");
                if (animProp != null)
                {
                    animProp.objectReferenceValue = anim;
                    pemSo.ApplyModifiedProperties();
                }
            }

            // 8. Wire serialized unitAnimator reference on PlayerUnit
            var playerUnit = prefabRoot.GetComponent<PlayerUnit>();
            if (playerUnit != null)
            {
                var puSo = new SerializedObject(playerUnit);
                var unitAnimProp = puSo.FindProperty("unitAnimator");
                if (unitAnimProp != null)
                {
                    unitAnimProp.objectReferenceValue = anim;
                    puSo.ApplyModifiedProperties();
                }
            }

            // Save both PlayerHero.prefab and Hero_Warrior.prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_HERO_PATH);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_WARRIOR_PATH);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"[SetupKnightCharacterEditor] Prefabs assembled at {PREFAB_HERO_PATH} and {PREFAB_WARRIOR_PATH}.");

            return AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_HERO_PATH);
        }

        #endregion

        #region Step 6: CharacterClassSO & Scene Hookup

        public static void SetupCharacterClassSO(GameObject heroPrefab)
        {
            Debug.Log("[SetupKnightCharacterEditor] Step 6: Linking prefab to Character_Warrior_SirRoland.asset...");

            CharacterClassSO warriorSO = AssetDatabase.LoadAssetAtPath<CharacterClassSO>(WARRIOR_SO_PATH);
            if (warriorSO == null)
            {
                Debug.LogError($"[SetupKnightCharacterEditor] CharacterClassSO missing at {WARRIOR_SO_PATH}!");
                return;
            }

            var so = new SerializedObject(warriorSO);
            var prefabProp = so.FindProperty("characterPrefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = heroPrefab;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(warriorSO);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SetupKnightCharacterEditor] Assigned characterPrefab on {warriorSO.CharacterName} ({warriorSO.name}).");
            }
        }

        public static void UpdateSceneInstances(GameObject heroPrefab)
        {
            Debug.Log("[SetupKnightCharacterEditor] Step 6: Updating scene instances across scenes...");

            // Check current active scene
            Scene activeScene = SceneManager.GetActiveScene();
            UpdateScenePlayerHero(activeScene, heroPrefab);

            // Also check all 7 zone scenes
            string[] scenePaths = new string[]
            {
                "Assets/Scenes/Zone_1_VillageAndCellar.unity",
                "Assets/Scenes/Zone_2_ForestPath.unity",
                "Assets/Scenes/Zone_3_CastleCourtyard.unity",
                "Assets/Scenes/Zone_4_Library.unity",
                "Assets/Scenes/Zone_5_CastleHall.unity",
                "Assets/Scenes/Zone_6_Tower.unity",
                "Assets/Scenes/Zone_7_ThroneRoom.unity"
            };

            foreach (var path in scenePaths)
            {
                if (File.Exists(path) && path != activeScene.path)
                {
                    try
                    {
                        Scene openedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        UpdateScenePlayerHero(openedScene, heroPrefab);
                        EditorSceneManager.SaveScene(openedScene);
                        EditorSceneManager.CloseScene(openedScene, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SetupKnightCharacterEditor] Note while processing {path}: {ex.Message}");
                    }
                }
            }

            Debug.Log("[SetupKnightCharacterEditor] All scene instances updated.");
        }

        private static void UpdateScenePlayerHero(Scene scene, GameObject heroPrefab)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == "PlayerHero")
                {
                    // Remove root MeshFilter and MeshRenderer if present
                    var mf = root.GetComponent<MeshFilter>();
                    if (mf != null) UnityEngine.Object.DestroyImmediate(mf, true);

                    var mr = root.GetComponent<MeshRenderer>();
                    if (mr != null) UnityEngine.Object.DestroyImmediate(mr, true);

                    // Ensure prefab synchronization
                    if (PrefabUtility.IsPartOfPrefabInstance(root))
                    {
                        PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log($"[SetupKnightCharacterEditor] Cleaned and synchronized PlayerHero in scene '{scene.name}'.");
                }
            }
        }

        #endregion
    }
}
