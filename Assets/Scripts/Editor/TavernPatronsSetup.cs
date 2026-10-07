using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// The four low-poly tavern guests (BlenderSources/Tavern_Patrons_build.py): Humanoid import, one shared
    /// animator (Sit_Idle, Sit_Drink, Stand_Idle, Stand_Drink), a prefab each, and their places in Zone_Tavern.
    /// Called by <see cref="TavernSceneBuilder"/>.
    /// </summary>
    public static class TavernPatronsSetup
    {
        private const string Folder = "Assets/Characters/Tavern_Patrons/";
        private const string PalettePath = Folder + "Tavern_Patrons_Palette.png";
        private const string MaterialPath = Folder + "M_Tavern_Patrons.mat";
        private const string ControllerPath = "Assets/Characters/Animators/NPC_Tavern_Patron_Animator.controller";
        private const string PrefabFolder = "Assets/PREFABS/NPCs/Tavern";
        private const string ClipSource = "Patron_Farmer";
        private static readonly string[] Takes = { TavernPatron.SitIdle, TavernPatron.SitDrink, TavernPatron.StandIdle, TavernPatron.StandDrink };

        private sealed class Guest
        {
            public string Name;
            public float Height;      // game units, crown to sole when standing
            public string State;
            public Vector3 Position;  // a chair centre (seated) or a floor spot (standing)
            public float Yaw;
        }

        // Places from the tavern layout (Tavern_Interior_build.py): chair centres face their table
        private static readonly Guest[] Guests =
        {
            // north chair of Table_1 (-8.2, -6.0), facing the table (south)
            new Guest { Name = "Patron_Farmer", Height = 3.0f, State = TavernPatron.SitDrink, Position = new Vector3(-7.45f, 0f, -4.65f), Yaw = 180f },
            // south-west chair of Table_2 (-2.6, -1.4): a = 45 deg -> chair at +sin/+cos * 1.55, facing 225
            new Guest { Name = "Patron_Elder", Height = 2.9f, State = TavernPatron.SitIdle, Position = new Vector3(-2.6f + 1.096f, 0f, -1.4f + 1.096f), Yaw = 225f },
            // at the bar, back to the counter, looking into the room
            new Guest { Name = "Patron_Lass", Height = 2.8f, State = TavernPatron.StandDrink, Position = new Vector3(-9.6f, 0f, 0.65f), Yaw = 160f },
            // by the foot of the stairs, mug in hand
            new Guest { Name = "Patron_Sellsword", Height = 3.1f, State = TavernPatron.StandIdle, Position = new Vector3(5.0f, 0f, -5.6f), Yaw = 300f },
        };

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

            foreach (Guest g in Guests) ConfigureImporter(Folder + g.Name + ".fbx", mat);
            AnimatorController controller = BuildController();
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/PREFABS/NPCs", "Tavern");
            foreach (Guest g in Guests) BuildPrefab(g, controller);
        }

        private static void ConfigureImporter(string path, Material mat)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogError("[TavernPatronsSetup] Missing " + path + " (run BlenderSources/Tavern_Patrons_build.py)");
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Tavern_Patrons"), mat);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (string take in Takes)
            {
                foreach (ModelImporterClipAnimation src in importer.defaultClipAnimations)
                {
                    if (src.takeName != take && !src.takeName.EndsWith("|" + take)) continue;
                    src.name = take;
                    src.loopTime = true;
                    src.loopPose = take.StartsWith("Sit");
                    src.lockRootRotation = true;
                    src.keepOriginalOrientation = true;
                    src.lockRootHeightY = true;
                    src.keepOriginalPositionY = true;       // seated hips stay at chair height
                    src.lockRootPositionXZ = true;
                    src.keepOriginalPositionXZ = true;      // and over the chair
                    clips.Add(src);
                    break;
                }
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static AnimationClip Clip(string take)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Folder + ClipSource + ".fbx"))
                if (o is AnimationClip c && c.name == take) return c;
            return null;
        }

        private static Avatar AvatarOf(string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Folder + name + ".fbx"))
                if (o is Avatar a) return a;
            return null;
        }

        /// <summary>One looping state per take; <see cref="TavernPatron"/> picks the guest's state.</summary>
        private static AnimatorController BuildController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
            for (int i = 0; i < Takes.Length; i++)
            {
                AnimatorState st = sm.AddState(Takes[i], new Vector3(300f, i * 80f, 0f));
                st.motion = Clip(Takes[i]);
                if (i == 2) sm.defaultState = st;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildPrefab(Guest g, AnimatorController controller)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + g.Name + ".fbx");
            if (fbx == null) return;
            var root = new GameObject(g.Name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                model.name = "Model";
                model.transform.localScale = Vector3.one * g.Height; // the rigs stand 1.0 tall
                Animator anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.avatar = AvatarOf(g.Name);
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Bounds b = smr.localBounds;
                    b.Expand(b.size.magnitude * 0.4f);
                    smr.localBounds = b;
                }
                root.AddComponent<TavernPatron>().State = g.State;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + g.Name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Seats and stands the guests in the tavern scene.</summary>
        public static void Place(Scene scene)
        {
            var group = new GameObject("Tavern_Patrons");
            SceneManager.MoveGameObjectToScene(group, scene);
            foreach (Guest g in Guests)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + g.Name + ".prefab");
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                go.transform.SetPositionAndRotation(g.Position, Quaternion.Euler(0f, g.Yaw, 0f));
                go.GetComponent<TavernPatron>().State = g.State;
                if (g.State.StartsWith("Stand"))
                {
                    // standing guests block the floor so the hero and the waitress walk round them
                    var col = go.AddComponent<CapsuleCollider>();
                    col.radius = 0.45f;
                    col.height = g.Height * 0.9f;
                    col.center = new Vector3(0f, col.height * 0.5f, 0f);
                }
            }
        }
    }
}
