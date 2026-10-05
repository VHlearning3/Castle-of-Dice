using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Gives Sir Roland (PlayerHero/Visuals/Knight_Model) his sword and shield and the Mixamo
    /// "Sword and Shield" animations. Knight_Weapons.fbx comes from BlenderSources/Knight_Weapons.blend:
    /// both weapons sit where the knight's hands hold them in his rest pose, so they are dropped onto the
    /// model at the same spot and re-parented to the hand / forearm bones. Each warrior ability plays its
    /// own clip through a trigger named after the ability ID (see CombatUnit.TrySetAnimatorTrigger).
    /// </summary>
    public static class SetupHeroKnightEditor
    {
        private const string KnightFbx = "Assets/Characters/PlayerKnightModel.fbx";
        private const string WeaponsFbx = "Assets/Characters/Player_knight_weapons/Knight_Weapons.fbx";
        private const string ClipFolder = "Assets/Characters/Player_knight_anims";
        private const string MaterialFolder = "Assets/Characters/Materials";
        private const string ControllerPath = "Assets/Characters/Animators/HeroKnightAnimator.controller";
        private const string PlayerHeroPrefabPath = "Assets/PREFABS/Players/PlayerHero.prefab";

        private const string SwordBone = "mixamorig:RightHand";
        private const string ShieldBone = "mixamorig:LeftForeArm";

        private const string Idle = "Idle";
        private const string Walk = "Walk";
        private const string Run = "Run";
        private const string Attack = "Attack";
        private const string TakeHit = "TakeHit";
        private const string Die = "Die";
        private const string Victory = "Victory";
        private const string DrinkPotion = "DrinkPotion";

        // role -> Mixamo file in ClipFolder (each holds one "mixamo.com" take)
        private static readonly Dictionary<string, string> MixamoClips = new Dictionary<string, string>
        {
            [Idle] = "sword and shield idle",
            [Run] = "sword and shield run",
            [Attack] = "sword and shield attack (4)",
            [TakeHit] = "sword and shield block (2)",
            [Die] = "sword and shield death",
            ["warrior_sword_slash"] = "sword and shield attack (2)",
            ["warrior_shield_block"] = "sword and shield block",
            ["warrior_war_cry"] = "sword and shield attack",
            ["warrior_iron_will"] = "draw sword 1",
            ["warrior_retaliation"] = "sword and shield attack (3)",
            // Elira's potion drink (BlenderSources/Elira_Victory.blend, her T-pose skeleton), retargeted by the avatar
            [DrinkPotion] = "elira drink potion",
        };

        // The pack has no walk or cheer: keep the knight's own takes for those
        private static readonly Dictionary<string, string> KnightClips = new Dictionary<string, string>
        {
            [Walk] = "walk",
            [Victory] = "cheer.001",
        };

        private static readonly string[] AbilityRoles =
        {
            "warrior_sword_slash", "warrior_shield_block", "warrior_war_cry", "warrior_iron_will", "warrior_retaliation",
        };

        // The knight's cheer is 12 s long: keep the raise and hold (frames at 24 fps)
        private const int VictoryLastFrame = 80;

        // FBX material name -> (asset name, colour, metallic, smoothness)
        private static readonly (string source, string asset, Color color, float metallic, float smoothness)[] WeaponMaterials =
        {
            ("M_Weapon_Steel", "M_Knight_Steel", new Color(0.80f, 0.82f, 0.86f), 0.6f, 0.55f),
            ("M_Weapon_Gold", "M_Knight_Gold", new Color(0.86f, 0.64f, 0.24f), 0.6f, 0.5f),
            ("M_Weapon_Leather", "M_Knight_Leather", new Color(0.36f, 0.22f, 0.13f), 0f, 0.2f),
            ("M_Shield_Blue", "M_Knight_ShieldBlue", new Color(0.17f, 0.25f, 0.52f), 0f, 0.3f),
        };

        [MenuItem("CastleOfDice/Setup Hero Knight Sword & Shield")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupHeroKnightEditor] Exit Play Mode first.");
                return;
            }

            ExposeWeaponBones();
            foreach (KeyValuePair<string, string> kv in MixamoClips) ConfigureMixamoClip(kv.Key, kv.Value);
            TrimVictory();
            ConfigureWeaponsImporter();
            AssetDatabase.Refresh();

            AnimatorController controller = BuildController();
            AttachWeapons(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupHeroKnightEditor] Sir Roland has his sword, shield and sword-and-shield animations.");
        }

        /// <summary>Re-imports Sir Roland's clips and rebuilds his animator in place; prefab and weapons are left alone.</summary>
        [MenuItem("CastleOfDice/Setup Hero Knight Animations")]
        public static void SetupAnimations()
        {
            foreach (KeyValuePair<string, string> kv in MixamoClips) ConfigureMixamoClip(kv.Key, kv.Value);
            TrimVictory();
            AssetDatabase.Refresh();
            BuildController();
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupHeroKnightEditor] Sir Roland's animations set up.");
        }

        #region Import

        /// <summary>
        /// The knight is imported with Optimize Game Objects, which hides his bones. Expose just the two
        /// the weapons hang from; they show up as direct children of Knight_Model, animated as usual.
        /// </summary>
        private static void ExposeWeaponBones()
        {
            ModelImporter importer = AssetImporter.GetAtPath(KnightFbx) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupHeroKnightEditor] Knight FBX not found: {KnightFbx}");
                return;
            }

            var exposed = new List<string>(importer.extraExposedTransformPaths);
            bool changed = false;
            foreach (string bone in new[] { SwordBone, ShieldBone })
            {
                foreach (string path in importer.transformPaths)
                {
                    if (!path.EndsWith("/" + bone) || exposed.Contains(path)) continue;
                    exposed.Add(path);
                    changed = true;
                }
            }
            if (!changed) return;

            importer.extraExposedTransformPaths = exposed.ToArray();
            importer.SaveAndReimport();
        }

        private static string ClipPath(string file) => $"{ClipFolder}/{file}.fbx";

        private static void ConfigureMixamoClip(string role, string file)
        {
            string path = ClipPath(file);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupHeroKnightEditor] Clip FBX not found: {path}");
                return;
            }

            // Humanoid like the knight, so the Mixamo motion retargets onto his avatar
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes.Length == 0)
            {
                Debug.LogError($"[SetupHeroKnightEditor] No take in {path}");
                return;
            }

            ModelImporterClipAnimation clip = takes[0];
            clip.name = role;
            bool loops = role == Idle || role == Run;
            clip.loopTime = loops;
            clip.loopPose = loops;
            // Keep facing and height in the pose; forward travel is dropped so he stays on his tile
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        /// <summary>Shortens the knight FBX's cheer take, which the Victory state plays once.</summary>
        private static void TrimVictory()
        {
            ModelImporter importer = AssetImporter.GetAtPath(KnightFbx) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip.name != KnightClips[Victory]) continue;
                if (clip.lastFrame == VictoryLastFrame && !clip.loopTime) break;
                clip.firstFrame = 0;
                clip.lastFrame = VictoryLastFrame;
                clip.loopTime = false;
                changed = true;
            }
            if (!changed) return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void ConfigureWeaponsImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(WeaponsFbx) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupHeroKnightEditor] Weapons FBX not found: {WeaponsFbx}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var m in WeaponMaterials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.source), WeaponMaterial(m));
            }
            importer.SaveAndReimport();
        }

        private static Material WeaponMaterial((string source, string asset, Color color, float metallic, float smoothness) spec)
        {
            string path = $"{MaterialFolder}/{spec.asset}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = spec.color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", spec.color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", spec.metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", spec.smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimationClip LoadClip(string path, string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && clip.name == name) return clip;
            }
            Debug.LogWarning($"[SetupHeroKnightEditor] Clip '{name}' not found in {path}");
            return null;
        }

        private static AnimationClip ClipFor(string role)
        {
            if (MixamoClips.TryGetValue(role, out string file)) return LoadClip(ClipPath(file), role);
            if (KnightClips.TryGetValue(role, out string take)) return LoadClip(KnightFbx, take);
            return null;
        }

        #endregion

        #region Animator

        /// <summary>Rebuilds HeroKnightAnimator in place, so PlayerHero keeps its reference.</summary>
        private static AnimatorController BuildController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            foreach (string trigger in new[] { Attack, "CastSpell", TakeHit, Die, Victory })
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);

            AnimatorState idle = AddState(sm, Idle, new Vector3(300f, 0f, 0f));
            AnimatorState walk = AddState(sm, Walk, new Vector3(300f, 120f, 0f));
            AnimatorState run = AddState(sm, Run, new Vector3(60f, 120f, 0f));
            sm.defaultState = idle;

            // Free exploration runs; tile-by-tile combat movement also sets IsWalking and walks
            AddLocomotion(idle, walk, true);
            AddLocomotion(idle, run, false);
            AddLocomotion(walk, idle, null, moving: false);
            AddLocomotion(run, idle, null, moving: false);
            AddLocomotion(walk, run, false);
            AddLocomotion(run, walk, true);

            AnimatorState attack = AddOneShot(sm, Attack, idle, new Vector3(560f, 0f, 0f));
            AddAnyTransition(sm, attack, "CastSpell");
            AddOneShot(sm, TakeHit, idle, new Vector3(560f, 60f, 0f));
            controller.AddParameter(DrinkPotion, AnimatorControllerParameterType.Trigger);
            AddOneShot(sm, DrinkPotion, idle, new Vector3(560f, 120f, 0f));

            float slot = 0f;
            foreach (string role in AbilityRoles)
            {
                controller.AddParameter(role, AnimatorControllerParameterType.Trigger);
                AddOneShot(sm, role, idle, new Vector3(820f, slot, 0f));
                slot += 60f;
            }

            AnimatorState die = AddState(sm, Die, new Vector3(560f, 180f, 0f));
            AddAnyTransition(sm, die, Die);
            AnimatorState victory = AddState(sm, Victory, new Vector3(560f, 240f, 0f));
            AddAnyTransition(sm, victory, Victory);
            SetupRiggedEnemiesEditor.AddVictoryExits(victory, idle);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string role, Vector3 pos)
        {
            AnimatorState state = sm.AddState(role, pos);
            state.motion = ClipFor(role);
            return state;
        }

        /// <summary>A clip played once by the trigger of the same name, then back to idle.</summary>
        private static AnimatorState AddOneShot(AnimatorStateMachine sm, string role, AnimatorState idle, Vector3 pos)
        {
            AnimatorState state = AddState(sm, role, pos);
            AddAnyTransition(sm, state, role);
            AnimatorStateTransition back = state.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.15f;
            return state;
        }

        /// <summary>Locomotion edge on IsMoving (<paramref name="moving"/>), optionally also on IsWalking (<paramref name="walking"/>).</summary>
        private static void AddLocomotion(AnimatorState from, AnimatorState to, bool? walking, bool moving = true)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsMoving");
            if (walking.HasValue) t.AddCondition(walking.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "IsWalking");
        }

        private static void AddAnyTransition(AnimatorStateMachine sm, AnimatorState target, string trigger)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        #endregion

        #region Weapons

        private static void AttachWeapons(AnimatorController controller)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerHeroPrefabPath);
            try
            {
                Transform knight = root.transform.Find("Visuals/Knight_Model");
                if (knight == null)
                {
                    Debug.LogError("[SetupHeroKnightEditor] PlayerHero has no Visuals/Knight_Model.");
                    return;
                }

                Animator animator = knight.GetComponent<Animator>();
                if (animator != null) animator.runtimeAnimatorController = controller;

                Transform swordBone = FindDeep(knight, SwordBone);
                Transform shieldBone = FindDeep(knight, ShieldBone);
                if (swordBone == null || shieldBone == null)
                {
                    Debug.LogError("[SetupHeroKnightEditor] Knight_Model is missing its hand / forearm bones.");
                    return;
                }

                // Re-running replaces the weapons instead of stacking them
                Transform oldSword = swordBone.Find("Sword");
                if (oldSword != null) Object.DestroyImmediate(oldSword.gameObject);
                Transform oldShield = shieldBone.Find("Shield");
                if (oldShield != null) Object.DestroyImmediate(oldShield.gameObject);

                // The weapons were placed against the knight's rest (bind) pose in Blender; the model's
                // default pose is not that pose, so the offsets come from the bind pose instead
                GameObject weapons = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponsFbx));
                PrefabUtility.UnpackPrefabInstance(weapons, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Transform sword = weapons.transform.Find("Sword");
                Transform shield = weapons.transform.Find("Shield");
                Dictionary<string, Matrix4x4> bind = BindPose(SwordBone, ShieldBone);
                HangFrom(sword, swordBone, bind[SwordBone]);
                HangFrom(shield, shieldBone, bind[ShieldBone]);
                Object.DestroyImmediate(weapons);

                foreach (Transform weapon in new[] { sword, shield })
                {
                    MeshRenderer r = weapon.GetComponent<MeshRenderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerHeroPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Bind-pose matrices of the given bones in the knight model's own space (rotation and position
        /// only, the bones carry no scale), read from the skin's bind poses on a de-optimised copy.
        /// </summary>
        private static Dictionary<string, Matrix4x4> BindPose(params string[] bones)
        {
            var result = new Dictionary<string, Matrix4x4>();
            GameObject copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(KnightFbx));
            try
            {
                AnimatorUtility.DeoptimizeTransformHierarchy(copy);
                SkinnedMeshRenderer skin = copy.GetComponentInChildren<SkinnedMeshRenderer>();
                Matrix4x4[] bindposes = skin.sharedMesh.bindposes;
                Matrix4x4 toModel = copy.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                for (int i = 0; i < skin.bones.Length; i++)
                {
                    if (skin.bones[i] == null || System.Array.IndexOf(bones, skin.bones[i].name) < 0) continue;
                    Matrix4x4 m = toModel * bindposes[i].inverse;
                    result[skin.bones[i].name] = Matrix4x4.TRS(m.GetPosition(), m.rotation, Vector3.one);
                }
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
            return result;
        }

        /// <summary>
        /// Parents <paramref name="weapon"/> (placed in the model's rest-pose space, as exported from
        /// Blender) to <paramref name="bone"/> with the offset it has from that bone in the bind pose.
        /// </summary>
        private static void HangFrom(Transform weapon, Transform bone, Matrix4x4 boneBind)
        {
            Matrix4x4 local = boneBind.inverse * Matrix4x4.TRS(weapon.localPosition, weapon.localRotation, weapon.localScale);
            weapon.SetParent(bone, false);
            weapon.localPosition = local.GetPosition();
            weapon.localRotation = local.rotation;
            weapon.localScale = local.lossyScale;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        #endregion
    }
}
