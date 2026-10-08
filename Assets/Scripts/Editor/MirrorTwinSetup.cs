using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// The Tower's secret boss (Vili, 2026-10-08): the Mirror Twin, built from BlenderSources/MirrorTwin_scripts
    /// (MirrorTwin.fbx model, MirrorTwin_Anims.fbx takes). Sets up the imports (Humanoid, the takes copy the model's
    /// avatar), materials, the animator, the visual prefab, and in Zone_6_Tower the encounter under the mirror
    /// room: the twin, her room, the cutscene points and the mirror's violet flash. Running it again rebuilds them.
    /// Needs the mirror room (CastleOfDice/Tower Stairs + Mirror Room) first.
    /// </summary>
    public static class MirrorTwinSetup
    {
        public const string Folder = "Assets/Characters/MirrorTwin";
        public const string ModelPath = Folder + "/MirrorTwin.fbx";
        public const string AnimsPath = Folder + "/MirrorTwin_Anims.fbx";
        public const string TexturePath = Folder + "/MirrorTwin_BaseColor.png";
        public const string GrinTexturePath = Folder + "/MirrorTwin_BaseColor_Grin.png";
        public const string SkirtTexturePath = Folder + "/MirrorTwin_Skirt.png";
        public const string MaterialFolder = Folder + "/Materials";
        public const string ControllerPath = "Assets/Characters/Animators/MirrorTwin_Animator.controller";
        public const string PrefabPath = Folder + "/MirrorTwin.prefab";
        public const string LaughPath = "Assets/Audio/MirrorTwin_Laugh.wav";
        public const string EncounterName = "Mirror_Twin_Encounter";
        public const string BossName = "Boss_MirrorTwin";
        public const float ModelScale = 1.9f;       // 1.72 m model to ~3.3 m, like the heroes
        public const float BodyHeight = 3.2f;

        [MenuItem("CastleOfDice/Mirror Twin (Tower secret boss)", false, 18)]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
        }

        public static void BatchSetup()
        {
            SetupAll();
            EditorApplication.Exit(0);
        }

        public static void SetupAll()
        {
            EnsureFolder(MaterialFolder);
            Dictionary<string, Material> mats = BuildMaterials();
            ImportModel(mats);
            ImportAnims();
            AnimatorController controller = BuildController(mats["Grin"]);
            BuildPrefab(controller, mats["Lightning"]);
            PlaceEncounter();
            AssetDatabase.SaveAssets();
            Debug.Log("[MirrorTwinSetup] Mirror Twin prefab and Tower encounter ready.");
        }

        #region Materials

        private static Dictionary<string, Material> BuildMaterials()
        {
            var m = new Dictionary<string, Material>();
            ConfigureTexture(TexturePath);
            ConfigureTexture(GrinTexturePath);
            ConfigureTexture(SkirtTexturePath);
            m["M_MirrorTwin"] = Lit("M_MirrorTwin", Color.white, 0.28f, 0f, AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            m["Grin"] = Lit("M_MirrorTwin_Grin", Color.white, 0.28f, 0f, AssetDatabase.LoadAssetAtPath<Texture2D>(GrinTexturePath));
            m["Twin_Cloth"] = Lit("M_Twin_Cloth", Color.white, 0.3f, 0f, AssetDatabase.LoadAssetAtPath<Texture2D>(SkirtTexturePath));
            m["Twin_Gold"] = Lit("M_Twin_Gold", new Color(0.86f, 0.64f, 0.26f), 0.6f, 0.85f);
            m["Twin_Belt"] = Lit("M_Twin_Belt", new Color(0.05f, 0.05f, 0.065f), 0.45f, 0.2f);
            m["Trident_Shaft"] = Lit("M_Trident_Shaft", new Color(0.06f, 0.045f, 0.08f), 0.5f, 0.6f);
            m["Trident_Metal"] = Lit("M_Trident_Metal", new Color(0.09f, 0.085f, 0.11f), 0.7f, 0.9f);
            m["Trident_Grip"] = Lit("M_Trident_Grip", new Color(0.11f, 0.06f, 0.13f), 0.2f, 0f);
            m["Trident_Glow"] = Lit("M_Trident_Glow", new Color(0.62f, 0.3f, 1f), 0.4f, 0f, null, new Color(1.9f, 0.85f, 3.2f));
            m["Lightning"] = Lightning();
            return m;
        }

        private static void ConfigureTexture(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic, Texture2D tex = null, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
            }
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Lightning()
        {
            string path = MaterialFolder + "/M_Twin_Lightning.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            // Additive, bright violet-white
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            var c = new Color(0.85f, 0.6f, 1f, 1f);
            m.SetColor("_BaseColor", c);
            m.color = c;
            EditorUtility.SetDirty(m);
            return m;
        }

        #endregion

        #region Imports

        private static void ImportModel(Dictionary<string, Material> mats)
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[MirrorTwinSetup] No model at " + ModelPath);
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in mats)
            {
                if (pair.Key == "Grin" || pair.Key == "Lightning") continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
            importer.SaveAndReimport();
        }

        private static readonly string[] LoopingTakes = { "Idle", "Walk" };

        private static void ImportAnims()
        {
            var importer = AssetImporter.GetAtPath(AnimsPath) as ModelImporter;
            Avatar avatar = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (o is Avatar a) avatar = a;
            }
            if (importer == null || avatar == null)
            {
                Debug.LogError("[MirrorTwinSetup] Takes or avatar missing.");
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                bool loop = System.Array.IndexOf(LoopingTakes, clips[i].name) >= 0;
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                clips[i].lockRootRotation = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].lockRootHeightY = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimationClip Clip(string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AnimsPath))
            {
                if (o is AnimationClip c && c.name == name && !c.name.StartsWith("__preview__")) return c;
            }
            Debug.LogWarning("[MirrorTwinSetup] Missing take " + name);
            return null;
        }

        #endregion

        #region Animator

        private static AnimatorController BuildController(Material grin)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            foreach (string t in new[] { "Attack", "Cast", "Laugh", "TakeHit", "Die" })
            {
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);
            }
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState idle = sm.AddState("Idle", new Vector3(300, 0, 0));
            idle.motion = Clip("Idle");
            sm.defaultState = idle;
            AnimatorState walk = sm.AddState("Walk", new Vector3(300, 120, 0));
            walk.motion = Clip("Walk");

            AnimatorStateTransition toWalk = idle.AddTransition(walk);
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            AnimatorStateTransition toIdle = walk.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;

            OneShot(sm, idle, "Attack", "Attack", 0.1f, new Vector3(560, -60, 0));
            OneShot(sm, idle, "Cast", "Cast", 0.12f, new Vector3(560, 30, 0));
            AnimatorState laugh = OneShot(sm, idle, "Laugh", "Laugh", 0.2f, new Vector3(560, 120, 0));
            OneShot(sm, idle, "Hit", "TakeHit", 0.06f, new Vector3(560, 210, 0));
            var swap = laugh.AddStateMachineBehaviour<StateMaterialSwap>();
            swap.stateMaterial = grin;
            swap.rendererName = "Twin_Body";

            AnimatorState die = sm.AddState("Die", new Vector3(300, 300, 0));
            die.motion = Clip("Die");
            AnimatorStateTransition toDie = sm.AddAnyStateTransition(die);
            toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            toDie.hasExitTime = false;
            toDie.duration = 0.12f;
            toDie.canTransitionToSelf = false;
            EditorUtility.SetDirty(ac);
            return ac;
        }

        private static AnimatorState OneShot(AnimatorStateMachine sm, AnimatorState idle, string take, string trigger, float blend, Vector3 pos)
        {
            AnimatorState s = sm.AddState(take, pos);
            s.motion = Clip(take);
            AnimatorStateTransition into = sm.AddAnyStateTransition(s);
            into.AddCondition(AnimatorConditionMode.If, 0, trigger);
            into.hasExitTime = false;
            into.duration = blend;
            into.canTransitionToSelf = false;
            AnimatorStateTransition back = s.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.2f;
            return s;
        }

        #endregion

        #region Prefab

        private static void BuildPrefab(AnimatorController controller, Material lightningMat)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject("MirrorTwin");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.name = "Model";
            inst.transform.localScale = Vector3.one * ModelScale;
            // The model's front (Blender -Y) must face the root's +Z
            inst.transform.localRotation = Quaternion.identity;
            Animator anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            // The long skirt swings on its own bones after the Animator (SkirtSway)
            root.AddComponent<SkirtSway>().Animator = anim;

            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Twin_Trident") r.shadowCastingMode = ShadowCastingMode.On;
            }

            // Lightning on the trident: a flickering light at the head and crackling arcs
            Transform head = Find(inst.transform, "Trident_Head");
            var fx = new GameObject("Trident_Lightning");
            fx.transform.SetParent(root.transform, false);
            TridentLightning lightning = fx.AddComponent<TridentLightning>();
            lightning.Head = head != null ? head : inst.transform;
            var lightGo = new GameObject("Trident_Light");
            lightGo.transform.SetParent(head != null ? head : inst.transform, false);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.7f, 0.42f, 1f);
            light.range = 3.5f;
            light.intensity = 1.0f;
            light.shadows = LightShadows.None;
            lightning.Glow = light;
            var arcs = new LineRenderer[3];
            for (int i = 0; i < arcs.Length; i++) arcs[i] = Line(fx.transform, "Arc_" + (i + 1), lightningMat, 0.03f);
            lightning.Arcs = arcs;
            lightning.Bolt = Line(fx.transform, "Bolt", lightningMat, 0.09f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static LineRenderer Line(Transform parent, string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 2;
            return lr;
        }

        #endregion

        #region Tower encounter

        private static void PlaceEncounter()
        {
            Scene scene = EditorSceneManager.OpenScene(TowerMirrorSetup.ScenePath, OpenSceneMode.Single);
            GameObject mirrorRoot = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == TowerMirrorSetup.RoomRootName) mirrorRoot = r;
            }
            Transform room = mirrorRoot != null ? mirrorRoot.transform.Find("Room") : null;
            if (room == null)
            {
                Debug.LogError("[MirrorTwinSetup] Build the mirror room first (CastleOfDice/Tower Stairs + Mirror Room).");
                return;
            }
            Transform old;
            while ((old = room.Find(EncounterName)) != null) Object.DestroyImmediate(old.gameObject);

            PlanarMirror mirror = room.GetComponentInChildren<PlanarMirror>(true);
            var enc = new GameObject(EncounterName);
            enc.transform.SetParent(room, false);

            float mirrorZ = TowerMirrorSetup.RoomNorth;
            float half = BodyHeight * 0.5f;
            Vector3 stand = new Vector3(0f, half, mirrorZ - 3.8f);

            // The twin: a capsule body at mid-height (units stand on tiles by their collider), the model at her feet
            var boss = new GameObject(BossName);
            boss.tag = "Enemy";
            boss.transform.SetParent(enc.transform, false);
            boss.transform.SetPositionAndRotation(stand, Quaternion.Euler(0f, 180f, 0f));
            CapsuleCollider col = boss.AddComponent<CapsuleCollider>();
            col.height = BodyHeight;
            col.radius = 0.6f;
            col.center = Vector3.zero;
            boss.AddComponent<StatusEffectController>();
            MirrorTwinBoss unit = boss.AddComponent<MirrorTwinBoss>();
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), boss.transform);
            visual.name = "Visuals";
            visual.transform.localPosition = new Vector3(0f, -half, 0f);
            visual.transform.localRotation = Quaternion.identity;
            boss.SetActive(false);

            // Her room: no walk-in trigger (the mirror's cutscene starts it); the grid covers the room and the doorway
            var roomGo = new GameObject("Mirror_Twin_Room");
            roomGo.transform.SetParent(enc.transform, false);
            roomGo.transform.position = new Vector3(0f, 1f, 24f);
            DungeonRoomController drc = roomGo.AddComponent<DungeonRoomController>();
            // The room insists on a trigger box; this one sits under the floor where no one walks (the mirror starts the fight)
            BoxCollider unreachable = roomGo.GetComponent<BoxCollider>();
            if (unreachable == null) unreachable = roomGo.AddComponent<BoxCollider>();
            unreachable.isTrigger = true;
            unreachable.center = new Vector3(0f, -12f, 0f);
            unreachable.size = new Vector3(0.05f, 0.05f, 0.05f);
            drc.roomLocation = "Tower";
            drc.bossIdentifier = "MirrorTwin";
            drc.roomKey = "MirrorTwin";
            drc.roomEnemies = new List<GameObject> { boss };
            drc.gridWidth = 12;
            drc.gridHeight = 12;
            drc.gridAreaXZ = new Rect(-12.6f, 12.4f, 25.2f, 21.2f);

            Transform emerge = Point(enc.transform, "Twin_Emerge", new Vector3(0f, 0.75f + half, mirrorZ - 0.15f));
            Transform stepDown = Point(enc.transform, "Twin_StepDown", new Vector3(0f, 0.75f + half, mirrorZ - 0.95f));
            Transform standPt = Point(enc.transform, "Twin_Stand", stand);

            var flashGo = new GameObject("Mirror_Flash");
            flashGo.transform.SetParent(enc.transform, false);
            flashGo.transform.position = new Vector3(0f, 3.4f, mirrorZ - 1.4f);
            Light flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(0.68f, 0.38f, 1f);
            flash.range = 11f;
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;

            MirrorTwinEncounter encounter = enc.AddComponent<MirrorTwinEncounter>();
            encounter.Mirror = mirror;
            encounter.Twin = unit;
            encounter.Room = drc;
            encounter.EmergePoint = emerge;
            encounter.StepDownPoint = stepDown;
            encounter.StandPoint = standPt;
            encounter.MirrorFlash = flash;
            encounter.Laugh = AssetDatabase.LoadAssetAtPath<AudioClip>(LaughPath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Transform Point(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        #endregion

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
