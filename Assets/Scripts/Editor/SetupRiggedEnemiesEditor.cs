using System.Collections.Generic;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Wires the rigged Tripo models into the game: the three bosses (the golem in two stages), the
    /// Courtyard skeleton guard (and the one the Commander raises), the Forest Path zombie, the cellar rats, and Elira / Corvo on the hero prefab.
    /// Each model gets a material, an Idle / Walk / Attack / Die animator driven by IsMoving / Attack / Die,
    /// and a feet-pivot "visual" prefab that replaces the capsule placeholders in the zone scenes.
    /// Clips a file lacks are borrowed from another model with the same skeleton.
    /// </summary>
    public static class SetupRiggedEnemiesEditor
    {
        private const string Idle = "Idle";
        private const string Walk = "Walk";
        private const string Attack = "Attack";
        private const string Die = "Die";
        private const string Victory = "Victory";
        // Hero extras: Run plays while exploring, Walk on the combat grid (CombatUnit sets IsWalking)
        private const string Run = "Run";
        private const string TakeHit = "TakeHit";
        private const string DrinkPotion = "DrinkPotion";
        // Clip-only role: the archer's bow shot, imported with the skeleton's clips and borrowed as his Attack
        private const string Shoot = "Shoot";

        private const string TripoHips = "Hips";
        private const string MixamoHips = "mixamorig:Hips";

        private const string VisualFolder = "Assets/PREFABS/Characters";
        private const string EnemyFolder = "Assets/PREFABS/Enemies";
        private const string ZombiePrefabPath = EnemyFolder + "/Enemy_Zombie.prefab";
        private const string SkeletonGuardPrefabPath = EnemyFolder + "/Enemy_SkeletonGuard.prefab";
        public const string SkeletonArcherPrefabPath = EnemyFolder + "/Enemy_SkeletonArcher.prefab";
        public const string WolfPrefabPath = EnemyFolder + "/Enemy_Wolf.prefab";
        public const string MimicVisualPath = VisualFolder + "/Visual_Enemy_Mimic.prefab";
        public const string CultistVisualPath = VisualFolder + "/Visual_Enemy_Cultist.prefab";
        private const string ChestMaterialPath = "Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat";
        private const string Zone2 = "Assets/Scenes/Zone_2_ForestPath.unity";
        private const string PlayerHeroPrefabPath = "Assets/PREFABS/Players/PlayerHero.prefab";

        private const string Zone1 = "Assets/Scenes/Zone_1_VillageAndCellar.unity";
        private const string Zone3 = "Assets/Scenes/Zone_3_CastleCourtyard.unity";
        private const string Zone4 = "Assets/Scenes/Zone_4_Library.unity";
        private const string Zone7 = "Assets/Scenes/Zone_7_ThroneRoom.unity";

        private sealed class ModelSpec
        {
            public string Key;
            public string FbxPath;
            public string TexturePath;
            public string MotionNode = TripoHips;
            public float TargetHeight;
            public bool HeroParameters;
            // Turns models whose file faces sideways so they look along +Z like the rest
            public float Yaw;
            // role -> take name inside this FBX
            public Dictionary<string, string> Takes = new Dictionary<string, string>();
            // Optional animation-only FBX on the same skeleton (role -> take name in it)
            public string AnimFbxPath;
            public Dictionary<string, string> AnimTakes = new Dictionary<string, string>();
            // role -> (first, last) frame when only part of a take is used
            public Dictionary<string, (int first, int last)> Frames = new Dictionary<string, (int, int)>();
            // role -> (other model key, role in that model)
            public Dictionary<string, (string key, string role)> Borrowed = new Dictionary<string, (string, string)>();
            // role -> playback speed (1 when missing)
            public Dictionary<string, float> Speeds = new Dictionary<string, float>();
            // One-shot roles played by a trigger of the same name (e.g. an ability ID like mage_fireball)
            public List<string> TriggeredRoles = new List<string>();
            // Props hung on a bone of the visual prefab: FBX placed in the model's rest-pose space (from Blender)
            public List<(string fbx, string bone)> Attachments = new List<(string, string)>();
            // Models built in Blender keep their own material slots (mapped by name, see PropMaterial)
            public bool OwnMaterials;

            public string MaterialPath => $"Assets/Characters/Materials/M_{Key}.mat";
            public string ControllerPath => $"Assets/Characters/Animators/{Key}_Animator.controller";
            public string VisualPrefabPath => $"{VisualFolder}/Visual_{Key}.prefab";
        }

        private static readonly ModelSpec[] Specs =
        {
            new ModelSpec
            {
                Key = "Enemy_Skeleton",
                FbxPath = "Assets/Characters/Enemy_Normal_skeleton_3d_model/tripo_convert_5bd365a6-e8a7-40ea-9d29-0f8a1ba6d8b8.fbx",
                TexturePath = "Assets/Characters/Enemy_Normal_skeleton_3d_model/tripo_convert_5bd365a6-e8a7-40ea-9d29-0f8a1ba6d8b8.fbm/Normal_skeleton_3d_model_basecolor.JPEG",
                TargetHeight = 3.0f,
                Takes = { [Idle] = "idle", [Walk] = "walk", [Die] = "defeat_03" },
                // BlenderSources/Enemy_Anims.blend: the Scary Zombie Pack's two-handed swipe retargeted onto the
                // skeleton (it used to punch with the Commander's box_01), and the archer's bow shot
                AnimFbxPath = "Assets/Characters/Enemy_Normal_skeleton_3d_model/Skeleton_Anims.fbx",
                AnimTakes = { [Attack] = "Attack", [Shoot] = "Shoot" },
            },
            new ModelSpec
            {
                // The Forest ambush archer: the skeleton guard's model with a bow in the left hand
                Key = "Enemy_SkeletonArcher",
                FbxPath = "Assets/Characters/Enemy_Normal_skeleton_3d_model/tripo_convert_5bd365a6-e8a7-40ea-9d29-0f8a1ba6d8b8.fbx",
                TexturePath = "Assets/Characters/Enemy_Normal_skeleton_3d_model/tripo_convert_5bd365a6-e8a7-40ea-9d29-0f8a1ba6d8b8.fbm/Normal_skeleton_3d_model_basecolor.JPEG",
                TargetHeight = 3.0f,
                Borrowed =
                {
                    [Idle] = ("Enemy_Skeleton", Idle), [Walk] = ("Enemy_Skeleton", Walk),
                    [Attack] = ("Enemy_Skeleton", Shoot), [Die] = ("Enemy_Skeleton", Die),
                },
                Attachments = { ("Assets/Models/Items/Skeleton_Bow.fbx", "Left_Hand") },
            },
            new ModelSpec
            {
                Key = "Enemy_Zombie",
                FbxPath = "Assets/Characters/FIxed_zombie_3dmodel/FIxed_zombie_3dmodel.fbx",
                TexturePath = "Assets/Characters/FIxed_zombie_3dmodel/tripo_convert_c1b79a5c-9761-428b-98e6-f77fc470776b.fbm/FIxed_zombie_3dmodel_basecolor.JPEG",
                TargetHeight = 3.0f,
                // Scary Zombie Pack (Mixamo) idle, walk, attack and death retargeted onto the zombie's Tripo skeleton
                AnimFbxPath = "Assets/Characters/FIxed_zombie_3dmodel/Zombie_Anims.fbx",
                AnimTakes = { [Idle] = "Idle", [Walk] = "Walk", [Attack] = "Attack", [Die] = "Die" },
            },
            new ModelSpec
            {
                Key = "Boss_CursedCommander",
                FbxPath = "Assets/Characters/1_BOSS_skeletal_warrior_3d_model/tripo_convert_10a8093e-f398-40af-83c7-e7fd03dc711c.fbx",
                TexturePath = "Assets/Characters/1_BOSS_skeletal_warrior_3d_model/tripo_convert_10a8093e-f398-40af-83c7-e7fd03dc711c.fbm/1_BOSS_skeletal_warrior_3d_model_basecolor.JPEG",
                TargetHeight = 3.6f,
                Takes = { [Walk] = "walk", [Attack] = "box_01", [Die] = "defeat_03" },
                Borrowed = { [Idle] = ("Enemy_Skeleton", Idle) },
            },
            new ModelSpec
            {
                Key = "Boss_Malakor",
                FbxPath = "Assets/Characters/2_Boss_fantasy_silhouette_3d_model/tripo_convert_18f25e08-0ec5-4a79-9364-4aeeb5202816.fbx",
                TexturePath = "Assets/Characters/2_Boss_fantasy_silhouette_3d_model/tripo_convert_18f25e08-0ec5-4a79-9364-4aeeb5202816.fbm/Boss_2_fantasy_silhouette_3d_model_basecolor.JPEG",
                TargetHeight = 3.4f,
                Takes = { [Attack] = "cast_a_spell", [Die] = "defeat_03" },
                Borrowed = { [Idle] = ("Enemy_Skeleton", Idle), [Walk] = ("Enemy_Skeleton", Walk) },
            },
            new ModelSpec
            {
                Key = "Boss_Golem_Stage2",
                FbxPath = "Assets/Characters/3_Boss+2nd+stage+stone+golem+3d+model_Clone1/tripo_convert_88af72af-f931-4552-992d-56645f758afc.fbx",
                TexturePath = "Assets/Characters/3_Boss+2nd+stage+stone+golem+3d+model_Clone1/tripo_convert_88af72af-f931-4552-992d-56645f758afc.fbm/tripo_rgb_29e807b8-afd4-41cb-b089-fa1a8510c1c3.jpg",
                TargetHeight = 4.2f,
                Takes = { [Idle] = "idle.001", [Walk] = "walk.001", [Attack] = "box_01.001", [Die] = "fall.001" },
            },
            new ModelSpec
            {
                Key = "Boss_Golem_Stage1",
                FbxPath = "Assets/Characters/3_Boss_1st stage_stone+golem+3d+model(1)/tripo_convert_013987b6-8a6c-453f-b599-cc964fbc38ee.fbx",
                TexturePath = "Assets/Characters/3_Boss_1st stage_stone+golem+3d+model(1)/tripo_convert_013987b6-8a6c-453f-b599-cc964fbc38ee.fbm/tripo_rgb_04390c22-4323-4fa6-8201-841da5c5e04c.jpg",
                TargetHeight = 4.2f,
                Takes = { [Idle] = "idle.001", [Walk] = "walk.001", [Attack] = "box_01.001" },
                // Stage 1 never dies (it turns to stone at half HP); fall like stage 2 just in case
                Borrowed = { [Die] = ("Boss_Golem_Stage2", Die) },
            },
            new ModelSpec
            {
                Key = "Enemy_Rat",
                FbxPath = "Assets/Characters/low-poly+rat+3d+model(1)/tripo_convert_7efcb97a-f610-452a-92cc-f77e43bb2a34.fbx",
                TexturePath = "Assets/Characters/low-poly+rat+3d+model(1)/tripo_convert_7efcb97a-f610-452a-92cc-f77e43bb2a34.fbm/tripo_rgb_2929b0c3-a7dc-4eec-89d8-4974e19c3885.jpg",
                MotionNode = "tripo::Root",
                TargetHeight = 1.0f,
                Yaw = 90f,
                // Only a walk cycle ships with the rat: idle (sniffing), a lunging bite and a death (rolls onto its
                // side) are keyed on its own skeleton in BlenderSources/Enemy_Anims.blend
                Takes = { [Walk] = "preset:quadruped:walk.001" },
                AnimFbxPath = "Assets/Characters/low-poly+rat+3d+model(1)/Rat_Anims.fbx",
                AnimTakes = { [Idle] = "Idle", [Attack] = "Attack", [Die] = "Die" },
            },
            // Low-poly models built in Blender (BlenderSources/Mimic.blend, Wolf.blend, Cultist.blend)
            new ModelSpec
            {
                // The Tower's mimic: the village chest and its lid with teeth, a tongue and eyes; the lid bites
                Key = "Enemy_Mimic",
                FbxPath = "Assets/Characters/Mimic/Mimic.fbx",
                MotionNode = "Root",
                TargetHeight = 1.5f,
                Yaw = 180f,
                OwnMaterials = true,
                Takes = { [Idle] = "Idle", [Walk] = "Walk", [Attack] = "Attack", [Die] = "Die" },
            },
            new ModelSpec
            {
                Key = "Enemy_Wolf",
                FbxPath = "Assets/Characters/Wolf/Wolf.fbx",
                MotionNode = "Root",
                TargetHeight = 1.8f,
                OwnMaterials = true,
                Takes = { [Idle] = "Idle", [Walk] = "Walk", [Attack] = "Attack", [Die] = "Die" },
            },
            new ModelSpec
            {
                // The Forest ambush's curse cultist: a hooded robe on Mirabel's skeleton, with her idle, walk and
                // fall and Elira's Mana Shield arm-raise retargeted as the curse / heal cast
                Key = "Enemy_Cultist",
                FbxPath = "Assets/Characters/Cultist/Cultist.fbx",
                MotionNode = MixamoHips,
                TargetHeight = 2.6f,
                OwnMaterials = true,
                Takes = { [Idle] = "Idle", [Walk] = "Walk", [Attack] = "Attack", [Die] = "Die" },
            },
            new ModelSpec
            {
                Key = "Hero_Mage_Elira",
                FbxPath = "Assets/Characters/Player_mage_new/Mage_Unity.fbx",
                TexturePath = "Assets/Characters/Player_mage_new/tripo_rgb_70139358-154f-4f40-b28c-91fc93740886.png",
                MotionNode = MixamoHips,
                TargetHeight = 3.0f,
                HeroParameters = true,
                // Vili's own Blender export: one take per ability. Unused takes: fall, standing_idle.
                Takes =
                {
                    [Idle] = "Armature|idle",
                    [Attack] = "Armature|FrostRay", [Die] = "Armature|death",
                    [TakeHit] = "Armature|afraid", [DrinkPotion] = "Armature|drink potion",
                    ["mage_fireball"] = "Armature|Fireball", ["mage_frostbite"] = "Armature|FrostRay",
                    ["mage_mana_shield"] = "Armature|ManaShield", ["mage_blink"] = "Armature|Blink",
                },
                // BlenderSources/Elira_Victory.blend: the knight's cheer (raise and hold, ~3 s) and cast_a_spell,
                // retargeted onto her skeleton, and her own walk / run with the staff arm held still (her
                // idle's right arm) and the run, which sat 0.6 m ahead of her, moved back under her
                AnimFbxPath = "Assets/Characters/Player_mage_new/Elira_Anims.fbx",
                AnimTakes =
                {
                    [Walk] = "Walk", [Run] = "Run", [Victory] = "Victory", ["mage_arcane_chains"] = "ArcaneChains",
                },
                Frames = { [Victory] = (0, 100), ["mage_arcane_chains"] = (0, 120) },
                TriggeredRoles =
                {
                    TakeHit, DrinkPotion, "mage_fireball", "mage_frostbite", "mage_mana_shield", "mage_blink",
                    "mage_arcane_chains",
                },
            },
            new ModelSpec
            {
                Key = "Hero_Rogue_Corvo",
                FbxPath = "Assets/Characters/Rogue+3d+model/tripo_convert_e816ce67-bf17-4244-a3c4-00e5e2b78833.fbx",
                TexturePath = "Assets/Characters/Rogue+3d+model/tripo_convert_e816ce67-bf17-4244-a3c4-00e5e2b78833.fbm/tripo_rgb_bd678014-8a25-43cf-8b96-3e96ec492a96.jpg",
                MotionNode = MixamoHips,
                TargetHeight = 3.0f,
                HeroParameters = true,
                Takes =
                {
                    [Attack] = "Character does dnd small sword attack.001", [Die] = "dnd character dies.001",
                    ["rogue_backstab"] = "Character does dnd small sword attack.001",
                },
                // BlenderSources/Corvo_Rogue.blend: Mixamo Action Adventure idle / walk / run, his own unused
                // takes, and Elira's and the knight's, retargeted onto Corvo's skeleton in metres and in place.
                // Mixamo and Elira's rigs are in centimetres, in T-pose and roll the right arm differently from
                // his A-pose Tripo rig, so their clips are retargeted in world space, not copied bone for bone.
                AnimFbxPath = "Assets/Characters/Rogue+3d+model/Corvo_Anims.fbx",
                AnimTakes =
                {
                    [Idle] = "Idle", [Walk] = "Walk", [Run] = "Run", [TakeHit] = "TakeHit", [Victory] = "Victory",
                    [DrinkPotion] = "DrinkPotion", ["rogue_poison_dagger"] = "PoisonDagger",
                    ["rogue_smoke_bomb"] = "ThrowBomb", ["rogue_poison_cloud"] = "ThrowBomb",
                    ["rogue_shadow_step"] = "ShadowStep",
                },
                // The flinch at the start of his "afraid" take, and the cheer's raise and hold (~3 s)
                Frames = { [TakeHit] = (0, 36), [Victory] = (0, 80) },
                TriggeredRoles =
                {
                    TakeHit, DrinkPotion, "rogue_backstab", "rogue_poison_dagger", "rogue_smoke_bomb",
                    "rogue_shadow_step", "rogue_poison_cloud",
                },
            },
        };

        [MenuItem("CastleOfDice/Setup Rigged Enemies, Bosses & Heroes")]
        public static void SetupAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupRiggedEnemiesEditor] Exit Play Mode first.");
                return;
            }

            BuildAssets();
            DressZone1Rats();
            DressZone2();
            DressZone3();
            DressZone4();
            DressZone7();
            Debug.Log("[SetupRiggedEnemiesEditor] Rigged enemies, bosses and heroes set up.");
        }

        [MenuItem("CastleOfDice/Setup Rigged Enemies (assets only)")]
        public static void BuildAssets()
        {
            EnsureFolder(VisualFolder);
            EnsureFolder(EnemyFolder);

            foreach (ModelSpec spec in Specs) ConfigureImporter(spec);
            AssetDatabase.Refresh();

            foreach (ModelSpec spec in Specs)
            {
                Material material = SetupMaterial(spec);
                AnimatorController controller = BuildController(spec);
                if (!spec.HeroParameters) BuildVisualPrefab(spec, material, controller);
            }

            BuildZombiePrefab();
            BuildSkeletonGuardPrefab();
            SetupHeroPrefab();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Re-imports the skeleton's, zombie's and rat's clips and rebuilds their animators in place, and builds the
        /// skeleton archer (with the bow), mimic, wolf and cultist visual prefabs plus Enemy_SkeletonArcher and
        /// Enemy_Wolf. Existing visual prefabs, the hero prefab and the zone scenes are left alone (the Forest
        /// ambush and the Tower mimic pick the new models up through ReviewContentBuilder).
        /// </summary>
        [MenuItem("CastleOfDice/Setup Enemy Animations (skeleton, archer, zombie, rat, mimic, wolf, cultist)")]
        public static void SetupEnemyAnimations()
        {
            EnsureFolder(VisualFolder);
            EnsureFolder(EnemyFolder);
            string[] keys = { "Enemy_Skeleton", "Enemy_Zombie", "Enemy_Rat", "Enemy_SkeletonArcher", "Enemy_Mimic", "Enemy_Wolf", "Enemy_Cultist" };
            var newVisuals = new HashSet<string> { "Enemy_SkeletonArcher", "Enemy_Mimic", "Enemy_Wolf", "Enemy_Cultist" };
            foreach (string key in keys) ConfigureImporter(Find(key));
            AssetDatabase.Refresh();
            foreach (string key in keys)
            {
                ModelSpec spec = Find(key);
                Material material = SetupMaterial(spec);
                AnimatorController controller = BuildController(spec);
                if (newVisuals.Contains(key)) BuildVisualPrefab(spec, material, controller);
            }
            BuildSkeletonArcherPrefab();
            BuildWolfPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupRiggedEnemiesEditor] Skeleton, archer, zombie and rat animations set up.");
        }

        /// <summary>Re-imports only Elira's model and puts her on the hero prefab; enemies and zone scenes are left alone.</summary>
        [MenuItem("CastleOfDice/Setup Hero Mage Model")]
        public static void SetupMage()
        {
            ModelSpec mage = Find("Hero_Mage_Elira");
            ConfigureImporter(mage);
            AssetDatabase.Refresh();
            SetupMaterial(mage);
            BuildController(mage);
            SetupHeroPrefab(rogueToo: false);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupRiggedEnemiesEditor] Elira's model set up on the hero prefab.");
        }

        /// <summary>Re-imports Elira's clips and rebuilds her animator in place; the hero prefab is left alone.</summary>
        [MenuItem("CastleOfDice/Setup Hero Mage Animations")]
        public static void SetupMageAnimations()
        {
            ModelSpec mage = Find("Hero_Mage_Elira");
            ConfigureImporter(mage);
            AssetDatabase.Refresh();
            BuildController(mage);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupRiggedEnemiesEditor] Elira's animations set up.");
        }

        /// <summary>
        /// Re-imports Corvo's clips and rebuilds his animator in place (the hero prefab keeps its Rogue_Model),
        /// then re-aims his daggers, which are fitted to whatever his Idle clip is.
        /// </summary>
        [MenuItem("CastleOfDice/Setup Hero Rogue Animations")]
        public static void SetupRogue()
        {
            ModelSpec rogue = Find("Hero_Rogue_Corvo");
            ConfigureImporter(rogue);
            AssetDatabase.Refresh();
            BuildController(rogue);
            AssetDatabase.SaveAssets();
            SetupLowPolyItemsEditor.BuildHeroWeaponMounts();
            Debug.Log("[SetupRiggedEnemiesEditor] Corvo's animations and daggers set up.");
        }

        #region Import

        private static ModelSpec Find(string key)
        {
            foreach (ModelSpec s in Specs) if (s.Key == key) return s;
            return null;
        }

        private static void ConfigureImporter(ModelSpec spec)
        {
            // A model whose clips all come from elsewhere keeps its own import untouched
            if (spec.Takes.Count > 0) ConfigureImporter(spec, spec.FbxPath, spec.Takes);
            if (spec.AnimFbxPath != null) ConfigureImporter(spec, spec.AnimFbxPath, spec.AnimTakes);
        }

        private static void ConfigureImporter(ModelSpec spec, string fbxPath, Dictionary<string, string> takes)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupRiggedEnemiesEditor] FBX not found: {fbxPath}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // Hips as root node: travel becomes root motion (discarded), so clips play in place
            importer.motionNodeName = spec.MotionNode;
            importer.importAnimation = true;
            // Models built in Blender keep their material slots so CreateModel can map them by name
            importer.materialImportMode = spec.OwnMaterials ? ModelImporterMaterialImportMode.ImportViaMaterialDescription : ModelImporterMaterialImportMode.None;

            var clips = new List<ModelImporterClipAnimation>();
            // One clip per role; two roles may share a take (the mage's FrostRay is also her plain cast)
            foreach (KeyValuePair<string, string> kv in takes)
            {
                ModelImporterClipAnimation source = null;
                foreach (ModelImporterClipAnimation take in importer.defaultClipAnimations)
                {
                    if (take.takeName == kv.Value || take.takeName.EndsWith("|" + kv.Value)) { source = take; break; }
                }
                if (source == null) continue;

                string role = kv.Key;
                source.name = role;
                if (spec.Frames.TryGetValue(role, out (int first, int last) range))
                {
                    source.firstFrame = range.first;
                    source.lastFrame = range.last;
                }
                bool loops = role == Idle || role == Walk || role == Run;
                source.loopTime = loops;
                source.loopPose = loops;
                source.lockRootRotation = true;
                source.keepOriginalOrientation = true;
                source.lockRootHeightY = true;
                source.keepOriginalPositionY = true;
                source.lockRootPositionXZ = false;
                clips.Add(source);
            }

            if (clips.Count != takes.Count)
            {
                Debug.LogWarning($"[SetupRiggedEnemiesEditor] {spec.Key}: found {clips.Count}/{takes.Count} takes in {fbxPath}.");
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string fbxPath, string clipName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName && !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        private static AnimationClip ClipFor(ModelSpec spec, string role)
        {
            if (spec.Takes.ContainsKey(role)) return LoadClip(spec.FbxPath, role);
            if (spec.AnimTakes.ContainsKey(role)) return LoadClip(spec.AnimFbxPath, role);
            if (spec.Borrowed.TryGetValue(role, out (string key, string role) src))
            {
                ModelSpec other = Find(src.key);
                return other != null ? ClipFor(other, src.role) : null;
            }
            return null;
        }

        private static Avatar LoadAvatar(string fbxPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is Avatar avatar) return avatar;
            }
            return null;
        }

        #endregion

        #region Material & Animator

        private static Material SetupMaterial(ModelSpec spec)
        {
            if (spec.OwnMaterials) return null;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, spec.MaterialPath);
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            }
            else
            {
                Debug.LogWarning($"[SetupRiggedEnemiesEditor] Texture missing: {spec.TexturePath}");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimatorController BuildController(ModelSpec spec)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(spec.ControllerPath);

            foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter(Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(Die, AnimatorControllerParameterType.Trigger);
            // Set by CombatUnit on every hit; no state, it only keeps the console quiet
            controller.AddParameter("TakeHit", AnimatorControllerParameterType.Trigger);
            if (spec.HeroParameters)
            {
                controller.AddParameter("CastSpell", AnimatorControllerParameterType.Trigger);
                controller.AddParameter(Victory, AnimatorControllerParameterType.Trigger);
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);

            AnimationClip idle = ClipFor(spec, Idle);
            AnimationClip walk = ClipFor(spec, Walk) ?? idle;
            AnimationClip attack = ClipFor(spec, Attack);
            AnimationClip die = ClipFor(spec, Die);
            AnimationClip victory = spec.HeroParameters ? ClipFor(spec, Victory) : null;

            AnimatorState idleState = AddState(sm, spec, Idle, idle, new Vector3(300f, 0f, 0f));
            AnimatorState walkState = AddState(sm, spec, Walk, walk, new Vector3(300f, 120f, 0f));
            sm.defaultState = idleState;

            AnimationClip run = ClipFor(spec, Run);
            if (run == null)
            {
                AddLocomotion(idleState, walkState, null);
                AddLocomotion(walkState, idleState, null, moving: false);
            }
            else
            {
                // Free exploration runs; tile-by-tile combat movement also sets IsWalking and walks
                controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
                AnimatorState runState = AddState(sm, spec, Run, run, new Vector3(60f, 120f, 0f));
                AddLocomotion(idleState, walkState, true);
                AddLocomotion(idleState, runState, false);
                AddLocomotion(walkState, idleState, null, moving: false);
                AddLocomotion(runState, idleState, null, moving: false);
                AddLocomotion(walkState, runState, false);
                AddLocomotion(runState, walkState, true);
            }

            float slot = 0f;
            foreach (string role in spec.TriggeredRoles)
            {
                AnimationClip clip = ClipFor(spec, role);
                if (clip == null) continue;
                bool hasParameter = false;
                foreach (AnimatorControllerParameter p in controller.parameters) hasParameter |= p.name == role;
                if (!hasParameter) controller.AddParameter(role, AnimatorControllerParameterType.Trigger);
                AnimatorState state = AddState(sm, spec, role, clip, new Vector3(820f, slot, 0f));
                slot += 60f;
                AddAnyTransition(sm, state, role);
                AnimatorStateTransition back = state.AddTransition(idleState);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.15f;
            }

            if (attack != null)
            {
                AnimatorState attackState = AddState(sm, spec, Attack, attack, new Vector3(560f, 0f, 0f));
                AddAnyTransition(sm, attackState, Attack);
                if (spec.HeroParameters) AddAnyTransition(sm, attackState, "CastSpell");

                AnimatorStateTransition back = attackState.AddTransition(idleState);
                back.hasExitTime = true;
                back.exitTime = spec.Key == "Enemy_Rat" ? 0.9f : 0.95f;
                back.duration = 0.15f;
            }

            if (die != null)
            {
                AnimatorState dieState = AddState(sm, spec, Die, die, new Vector3(560f, 120f, 0f));
                AddAnyTransition(sm, dieState, Die);
            }

            if (victory != null)
            {
                AnimatorState victoryState = AddState(sm, spec, Victory, victory, new Vector3(560f, 240f, 0f));
                AddAnyTransition(sm, victoryState, Victory);
                AddVictoryExits(victoryState, idleState);
            }

            EditorUtility.SetDirty(controller);
            if (idle == null || attack == null)
            {
                Debug.LogWarning($"[SetupRiggedEnemiesEditor] {spec.Key}: missing clip(s) idle={idle != null} attack={attack != null} die={die != null}");
            }
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, ModelSpec spec, string role, AnimationClip clip, Vector3 pos)
        {
            AnimatorState state = sm.AddState(role, pos);
            state.motion = clip;
            if (spec.Speeds.TryGetValue(role, out float speed)) state.speed = speed;
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

        /// <summary>
        /// The cheer plays once and settles back into Idle; walking off right away cuts it short, so the hero
        /// never stays stuck in the Victory pose while exploring.
        /// </summary>
        public static void AddVictoryExits(AnimatorState victory, AnimatorState idle)
        {
            AnimatorStateTransition done = victory.AddTransition(idle);
            done.hasExitTime = true;
            done.exitTime = 0.9f;
            done.duration = 0.4f;

            AnimatorStateTransition move = victory.AddTransition(idle);
            move.hasExitTime = false;
            move.duration = 0.15f;
            move.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
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

        #region Prefabs

        /// <summary>Instantiates the FBX as "Model": scaled to height, textured, animated. Pivot stays at the feet.</summary>
        private static GameObject CreateModel(ModelSpec spec, Material material, AnimatorController controller, Transform parent, string name)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, spec.Yaw, 0f);
            model.transform.localScale = Vector3.one;

            float scale = spec.TargetHeight / Mathf.Max(0.01f, MeasureHeight(model));
            model.transform.localScale = Vector3.one * scale;

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (spec.OwnMaterials)
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = PropMaterial(mats[i] != null ? mats[i].name : "Prop");
                    r.sharedMaterials = mats;
                }
                else
                {
                    r.sharedMaterial = material;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = false;
                // Generous local bounds so swinging or falling limbs are not culled
                Bounds b = smr.localBounds;
                b.Expand(b.size.magnitude * 0.5f);
                smr.localBounds = b;
            }

            foreach ((string fbx, string bone) a in spec.Attachments) Attach(model, spec, a.fbx, a.bone);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = LoadAvatar(spec.FbxPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return model;
        }

        /// <summary>
        /// Hangs a prop (exported from Blender in the model's rest-pose space) on <paramref name="boneName"/>,
        /// keeping the offset it has from that bone in the bind pose. Its materials become M_{prop}_{material}.
        /// </summary>
        private static void Attach(GameObject model, ModelSpec spec, string fbxPath, string boneName)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (importer == null || asset == null)
            {
                Debug.LogError($"[SetupRiggedEnemiesEditor] Attachment not found: {fbxPath}");
                return;
            }
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();

            Transform bone = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) if (t.name == boneName) bone = t;
            if (bone == null)
            {
                Debug.LogError($"[SetupRiggedEnemiesEditor] {spec.Key} has no bone {boneName}.");
                return;
            }

            // Bind pose of the bone in the model root's space (rotation and position, no scale)
            Matrix4x4 boneBind = Matrix4x4.identity;
            bool found = false;
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Matrix4x4[] bindposes = skin.sharedMesh != null ? skin.sharedMesh.bindposes : null;
                if (bindposes == null) continue;
                Matrix4x4 toModel = model.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                for (int i = 0; i < skin.bones.Length && i < bindposes.Length; i++)
                {
                    if (skin.bones[i] != bone) continue;
                    Matrix4x4 m = toModel * bindposes[i].inverse;
                    boneBind = Matrix4x4.TRS(m.GetPosition(), m.rotation, Vector3.one);
                    found = true;
                }
            }
            if (!found) Debug.LogWarning($"[SetupRiggedEnemiesEditor] {boneName} is not skinned; {fbxPath} uses its rest transform.");

            GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(prop, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform mesh = prop.transform.childCount > 0 ? prop.transform.GetChild(0) : prop.transform;
            // The prop was exported in the model's rest-pose space and is instantiated at the origin
            Matrix4x4 local = boneBind.inverse * mesh.localToWorldMatrix;
            mesh.SetParent(bone, false);
            mesh.localPosition = local.GetPosition();
            mesh.localRotation = local.rotation;
            mesh.localScale = local.lossyScale;
            if (mesh != prop.transform) Object.DestroyImmediate(prop);

            foreach (MeshRenderer r in mesh.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = PropMaterial(mats[i] != null ? mats[i].name : "Prop");
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        // Blender material name -> colour, for the props built in BlenderSources/Enemy_Anims.blend
        private static readonly Dictionary<string, Color> PropColors = new Dictionary<string, Color>
        {
            ["M_Bow_Wood"] = new Color(0.36f, 0.2f, 0.08f),
            ["M_Bow_Grip"] = new Color(0.15f, 0.09f, 0.05f),
            ["M_Bow_String"] = new Color(0.85f, 0.82f, 0.72f),
            ["M_Mimic_Teeth"] = new Color(0.92f, 0.88f, 0.75f),
            ["M_Mimic_Tongue"] = new Color(0.7f, 0.12f, 0.2f),
            ["M_Mimic_Eyes"] = new Color(1f, 0.85f, 0.1f),
            ["M_Mimic_Mouth"] = new Color(0.25f, 0.02f, 0.05f),
            ["M_Wolf_Fur"] = new Color(0.42f, 0.42f, 0.44f),
            ["M_Wolf_Belly"] = new Color(0.72f, 0.7f, 0.66f),
            ["M_Wolf_Dark"] = new Color(0.12f, 0.11f, 0.11f),
            ["M_Wolf_Eyes"] = new Color(0.95f, 0.75f, 0.15f),
            ["M_Cultist_Robe"] = new Color(0.38f, 0.05f, 0.07f),
            ["M_Cultist_Trim"] = new Color(0.1f, 0.06f, 0.05f),
            ["M_Cultist_Skin"] = new Color(0.62f, 0.55f, 0.5f),
            ["M_Cultist_Shadow"] = new Color(0.02f, 0.01f, 0.01f),
            ["M_Cultist_Eyes"] = new Color(1f, 0.35f, 0.05f),
        };

        // Glowing eyes
        private static readonly HashSet<string> EmissiveProps = new HashSet<string> { "M_Mimic_Eyes", "M_Wolf_Eyes", "M_Cultist_Eyes" };

        private static Material PropMaterial(string sourceName)
        {
            string name = sourceName.Replace(" (Instance)", "");
            // The mimic's box and lid keep the village chest's own textured material
            if (name.StartsWith("Chest")) return AssetDatabase.LoadAssetAtPath<Material>(ChestMaterialPath);
            string path = $"Assets/Characters/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            Color c = PropColors.TryGetValue(name, out Color col) ? col : Color.gray;
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (EmissiveProps.Contains(name) && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 2f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Standing height in the model's own units: feet to crown. Tripo rigs have no head-top bone,
        /// so the crown is estimated from the head bone; the rat (no humanoid bones) uses its mesh bounds.
        /// </summary>
        private static float MeasureHeight(GameObject model)
        {
            Transform head = null, headTop = null;
            float lowest = float.MaxValue;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head" || t.name == "mixamorig:Head") head = t;
                if (t.name == "mixamorig:HeadTop_End") headTop = t;
                if (t.name.Contains("Toe") || t.name.Contains("Foot")) lowest = Mathf.Min(lowest, t.position.y);
            }

            float feet = lowest < float.MaxValue ? lowest : model.transform.position.y;
            if (headTop != null) return (headTop.position.y - feet) * 1.04f;
            if (head != null) return (head.position.y - feet) * 1.14f;

            Bounds b = new Bounds();
            bool has = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? b.size.y : 1f;
        }

        private static void BuildVisualPrefab(ModelSpec spec, Material material, AnimatorController controller)
        {
            GameObject root = new GameObject($"Visual_{spec.Key}");
            try
            {
                CreateModel(spec, material, controller, root.transform, "Model");
                PrefabUtility.SaveAsPrefabAsset(root, spec.VisualPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject LoadVisual(string key)
        {
            ModelSpec spec = Find(key);
            return spec != null ? AssetDatabase.LoadAssetAtPath<GameObject>(spec.VisualPrefabPath) : null;
        }

        /// <summary>The Forest Path's zombie.</summary>
        private static void BuildZombiePrefab() =>
            BuildEnemyPrefab(ZombiePrefabPath, "Enemy_Zombie", "Rotting Zombie", hp: 20, ac: 11, damage: 4);

        /// <summary>A grey wolf (WolfUnit sets its own stats). No scene uses it yet.</summary>
        private static void BuildWolfPrefab() =>
            BuildEnemyPrefab(WolfPrefabPath, "Enemy_Wolf", "Grey Wolf", hp: 14, ac: 12, damage: 4, height: 1.8f, unitType: typeof(WolfUnit));

        /// <summary>The Forest ambush's skeleton archer: the guard's model with a bow and a bow shot.</summary>
        private static void BuildSkeletonArcherPrefab() =>
            BuildEnemyPrefab(SkeletonArcherPrefabPath, "Enemy_SkeletonArcher", "Skeleton Archer", hp: 16, ac: 12, damage: 4);

        /// <summary>The skeleton the Cursed Commander raises at half HP (same profile as the Courtyard guard).</summary>
        private static void BuildSkeletonGuardPrefab() =>
            BuildEnemyPrefab(SkeletonGuardPrefabPath, "Enemy_Skeleton", "Armored Skeleton Guard", hp: 20, ac: 12, damage: 4);

        private static void BuildEnemyPrefab(string path, string visualKey, string displayName, int hp, int ac, int damage,
            float height = 3.0f, System.Type unitType = null)
        {
            GameObject root = new GameObject(displayName);
            try
            {
                root.tag = "Enemy";
                EnemyUnit enemy = (EnemyUnit)root.AddComponent(unitType ?? typeof(EnemyUnit));
                if (unitType == null) enemy.ConfigureStats(displayName, hp: hp, ac: ac, damage: damage, bonus: 2);

                CapsuleCollider col = root.AddComponent<CapsuleCollider>();
                col.height = height;
                col.radius = 0.6f;
                col.center = Vector3.zero;

                AttachVisual(root, LoadVisual(visualKey), height);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetupHeroPrefab(bool rogueToo = true)
        {
            ModelSpec mage = Find("Hero_Mage_Elira");
            ModelSpec rogue = Find("Hero_Rogue_Corvo");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerHeroPrefabPath);
            try
            {
                Transform visuals = root.transform.Find("Visuals");
                Transform knight = visuals != null ? visuals.Find("Knight_Model") : null;
                if (visuals == null || knight == null)
                {
                    Debug.LogError("[SetupRiggedEnemiesEditor] PlayerHero has no Visuals/Knight_Model.");
                    return;
                }

                GameObject mageModel = ReplaceChild(visuals, "Mage_Model", mage);
                Transform oldRogue = visuals.Find("Rogue_Model");
                GameObject rogueModel = rogueToo || oldRogue == null ? ReplaceChild(visuals, "Rogue_Model", rogue) : oldRogue.gameObject;
                mageModel.SetActive(false);
                rogueModel.SetActive(false);

                HeroClassModels models = root.GetComponent<HeroClassModels>() ?? root.AddComponent<HeroClassModels>();
                SerializedObject so = new SerializedObject(models);
                so.FindProperty("warriorModel").objectReferenceValue = knight.gameObject;
                so.FindProperty("mageModel").objectReferenceValue = mageModel;
                so.FindProperty("rogueModel").objectReferenceValue = rogueModel;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerHeroPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject ReplaceChild(Transform parent, string name, ModelSpec spec)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.ControllerPath);
            return CreateModel(spec, mat, controller, parent, name);
        }

        #endregion

        #region Scenes

        private static void DressZone1Rats()
        {
            Scene scene = OpenScene(Zone1);
            GameObject rat = LoadVisual("Enemy_Rat");
            foreach (EnemyUnit enemy in Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!enemy.name.StartsWith("Cellar_Pest")) continue;
                Transform visual = enemy.transform.Find("Rat_Visual");
                if (visual == null) continue;

                // Rat_Visual already sits at the feet (lowered by the collider half-height): swap its contents
                for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(rat, visual);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            SaveScene(scene);
        }

        /// <summary>
        /// Forest Path encounter: a zombie waits on the path north of the village gate. Walking into the
        /// trigger lays out the 12x12 grid and starts the fight (DungeonRoomController, like the Courtyard).
        /// </summary>
        private static void DressZone2()
        {
            Scene scene = OpenScene(Zone2);
            const float encounterZ = -24f;

            GameObject old = FindInSceneQuiet("Forest_Zombie_Encounter");
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new GameObject("Forest_Zombie_Encounter");
            SceneManager.MoveGameObjectToScene(root, scene);

            GameObject gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, root.transform);
                grid.name = "CombatGrid_Forest";
                grid.transform.position = new Vector3(0f, 0.05f, encounterZ);
            }

            GameObject zombie = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath), root.transform);
            zombie.name = "Forest_Zombie";
            zombie.transform.position = new Vector3(0f, 1.5f, encounterZ + 6f);
            zombie.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // faces the village gate
            StandOnGround(zombie, 3.0f);
            zombie.SetActive(false);

            GameObject trigger = new GameObject("Forest_Zombie_Trigger");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.position = new Vector3(0f, 2.5f, encounterZ);
            BoxCollider col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(20f, 6f, 14f);

            DungeonRoomController room = trigger.AddComponent<DungeonRoomController>();
            room.roomLocation = "Forest";
            room.bossIdentifier = "";
            room.roomEnemies = new List<GameObject> { zombie };

            SaveScene(scene);
        }

        private static GameObject FindInSceneQuiet(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name && go.scene.IsValid()) return go;
            }
            return null;
        }

        private static void DressZone3()
        {
            Scene scene = OpenScene(Zone3);
            GameObject skeleton = LoadVisual("Enemy_Skeleton");
            GameObject commander = LoadVisual("Boss_CursedCommander");

            DressUnit(FindInScene("Courtyard_Skeleton"), skeleton, 3.0f, 0.6f);
            GameObject boss = FindInScene("Boss_CursedCommander");
            DressUnit(boss, commander, 3.6f, 0.8f);
            DressDialogueNpc(FindInScene("NPC_CursedCommander"), commander, 3.6f);

            if (boss != null)
            {
                CursedCommanderBoss commanderBoss = boss.GetComponent<CursedCommanderBoss>();
                if (commanderBoss != null)
                {
                    SerializedObject so = new SerializedObject(commanderBoss);
                    so.FindProperty("skeletonAddPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonGuardPrefabPath);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            SaveScene(scene);
        }

        private static void DressZone4()
        {
            Scene scene = OpenScene(Zone4);
            GameObject malakor = LoadVisual("Boss_Malakor");
            DressUnit(FindInScene("Boss_ShadowMageMalakor"), malakor, 3.4f, 0.8f);
            DressDialogueNpc(FindInScene("NPC_Malakor"), malakor, 3.4f);
            SaveScene(scene);
        }

        private static void DressZone7()
        {
            Scene scene = OpenScene(Zone7);
            GameObject stage1 = LoadVisual("Boss_Golem_Stage1");
            GameObject stage2 = LoadVisual("Boss_Golem_Stage2");

            GameObject boss = FindInScene("Boss_GargoyleKing");
            if (boss != null)
            {
                GameObject stage1Visual = DressUnit(boss, stage1, 4.2f, 1.0f);
                GameObject stage2Visual = AttachVisual(boss, stage2, 4.2f, "Visuals_Stage2");
                stage2Visual.SetActive(false);

                GargoyleKingStageModels swap = boss.GetComponent<GargoyleKingStageModels>() ?? boss.AddComponent<GargoyleKingStageModels>();
                SerializedObject so = new SerializedObject(swap);
                so.FindProperty("stage1Model").objectReferenceValue = stage1Visual;
                so.FindProperty("stage2Model").objectReferenceValue = stage2Visual;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            DressDialogueNpc(FindInScene("NPC_GargoyleKing"), stage1, 4.2f);
            SaveScene(scene);
        }

        /// <summary>
        /// Replaces a unit's capsule placeholder with the rigged visual. The collider is centred on the root
        /// (CombatUnit.MoveToTile lifts units by its half-height), so the visual hangs half a height below it.
        /// </summary>
        private static GameObject DressUnit(GameObject unit, GameObject visualPrefab, float height, float radius)
        {
            if (unit == null || visualPrefab == null) return null;

            RemoveChildren(unit.transform, "Body", "BossBody", "BossCrown", "Visuals", "Visuals_Stage2");

            CapsuleCollider col = unit.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.height = height;
                col.radius = radius;
                col.center = Vector3.zero;
            }

            GameObject visual = AttachVisual(unit, visualPrefab, height);
            StandOnGround(unit, height);
            return visual;
        }

        private static GameObject AttachVisual(GameObject unit, GameObject visualPrefab, float height, string name = "Visuals")
        {
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, unit.transform);
            visual.name = name;
            visual.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            return visual;
        }

        /// <summary>Boss "talk first" NPCs were plain scaled capsules: hide the capsule and stand the boss model there.</summary>
        private static void DressDialogueNpc(GameObject npc, GameObject visualPrefab, float height)
        {
            if (npc == null || visualPrefab == null) return;

            RemoveChildren(npc.transform, "Visuals");
            MeshRenderer capsule = npc.GetComponent<MeshRenderer>();
            if (capsule != null) capsule.enabled = false;

            npc.transform.localScale = Vector3.one;
            CapsuleCollider col = npc.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.height = height;
                col.radius = 0.8f;
                col.center = Vector3.zero;
            }

            AttachVisual(npc, visualPrefab, height);
            StandOnGround(npc, height);
        }

        private static void RemoveChildren(Transform parent, params string[] names)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (System.Array.IndexOf(names, child.name) >= 0) Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>Puts the feet (half a height below the root) on the first floor below the unit.</summary>
        private static void StandOnGround(GameObject unit, float height)
        {
            Collider own = unit.GetComponent<Collider>();
            bool wasEnabled = own != null && own.enabled;
            if (own != null) own.enabled = false;

            Vector3 origin = unit.transform.position + Vector3.up * 4f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(unit.transform)) continue;
                if (hit.collider.GetComponentInParent<CombatUnit>() != null) continue;
                if (hit.point.y > unit.transform.position.y + 1f) continue; // ceilings, arches
                best = Mathf.Max(best, hit.point.y);
            }
            if (best > float.MinValue)
            {
                Vector3 p = unit.transform.position;
                unit.transform.position = new Vector3(p.x, best + height * 0.5f, p.z);
            }
            else
            {
                Debug.LogWarning($"[SetupRiggedEnemiesEditor] No floor found under {unit.name}.");
            }

            if (own != null) own.enabled = wasEnabled;
        }

        private static GameObject FindInScene(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name && go.scene.IsValid()) return go;
            }
            Debug.LogWarning($"[SetupRiggedEnemiesEditor] '{name}' not found in {SceneManager.GetActiveScene().name}.");
            return null;
        }

        private static Scene OpenScene(string path)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == path) return active;
            if (active.isDirty && !string.IsNullOrEmpty(active.path)) EditorSceneManager.SaveScene(active);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        #endregion
    }
}
