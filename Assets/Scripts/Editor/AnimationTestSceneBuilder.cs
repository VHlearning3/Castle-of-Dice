using System.Collections.Generic;
using CastleOfTheD20.Core;
using CastleOfTheD20.DevTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Test/AnimationTest.unity: a ground, a light, a camera and an AnimationTestBench
    /// listing every animated character with its real prefab. The animator state names are baked here
    /// because the state machine is only readable in the editor. Re-run after adding a character or
    /// changing a controller. The scene is deliberately left out of Build Settings.
    /// </summary>
    public static class AnimationTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Test/AnimationTest.unity";
        private const string GroundMaterialPath = "Assets/Scenes/Test/AnimationTest_Ground.mat";

        private const string PlayerHero = "Assets/PREFABS/Players/PlayerHero.prefab";
        private const string Npcs = "Assets/PREFABS/NPCs/";
        private const string Visuals = "Assets/PREFABS/Characters/";

        private struct Def
        {
            public string name, group, path, child, note;
            public bool weapons;
            public CharacterClassType heroClass;
        }

        private static readonly Def[] Defs =
        {
            new Def { name = "Sir Roland", group = "Heroes", path = PlayerHero, child = "Knight_Model", heroClass = CharacterClassType.Warrior, note = "Sword and shield are saved on his hand bones in PlayerHero." },
            new Def { name = "Scholar Elira", group = "Heroes", path = PlayerHero, child = "Mage_Model", weapons = true, heroClass = CharacterClassType.Mage, note = "Staff attached from HeroWeaponMounts, as in the game." },
            new Def { name = "Shadow-Corvo", group = "Heroes", path = PlayerHero, child = "Rogue_Model", weapons = true, heroClass = CharacterClassType.Rogue, note = "Daggers attached from HeroWeaponMounts, as in the game." },
            new Def { name = "Baldur the Smith", group = "NPCs", path = Npcs + "NPC_Baldur_Smith.prefab" },
            new Def { name = "Barnaby", group = "NPCs", path = Npcs + "NPC_Barnaby_3dmodel.prefab", note = "Also the model for Pip the peddler." },
            new Def { name = "Mirabel", group = "NPCs", path = Npcs + "NPC_Mirabel_3dmodel.prefab" },
            new Def { name = "Elder Othelia", group = "NPCs", path = Npcs + "NPC_Othelia_3dmodel.prefab" },
            new Def { name = "Giant Cellar Rat", group = "Enemies", path = Visuals + "Visual_Enemy_Rat.prefab" },
            new Def { name = "Rotting Zombie", group = "Enemies", path = Visuals + "Visual_Enemy_Zombie.prefab" },
            new Def { name = "Skeleton Guard", group = "Enemies", path = Visuals + "Visual_Enemy_Skeleton.prefab", note = "Also the ghost of Sir Aldric." },
            new Def { name = "Skeleton Archer", group = "Enemies", path = Visuals + "Visual_Enemy_SkeletonArcher.prefab", note = "The guard's model with a bow; Attack is the bow shot." },
            new Def { name = "Curse Cultist", group = "Enemies", path = Visuals + "Visual_Enemy_Cultist.prefab" },
            new Def { name = "Mimic", group = "Enemies", path = Visuals + "Visual_Enemy_Mimic.prefab" },
            new Def { name = "Grey Wolf", group = "Enemies", path = Visuals + "Visual_Enemy_Wolf.prefab", note = "Enemy_Wolf.prefab is ready; no scene uses it yet." },
            new Def { name = "Cursed Commander", group = "Bosses", path = Visuals + "Visual_Boss_CursedCommander.prefab" },
            new Def { name = "Shadow Mage Malakor", group = "Bosses", path = Visuals + "Visual_Boss_Malakor.prefab", note = "Also his mirror image." },
            new Def { name = "Gargoyle King (stage 1)", group = "Bosses", path = Visuals + "Visual_Boss_Golem_Stage1.prefab" },
            new Def { name = "Gargoyle King (Stone Form)", group = "Bosses", path = Visuals + "Visual_Boss_Golem_Stage2.prefab" },
        };

        private static readonly string[,] Missing =
        {
            { "Village cat", "no rig; VillageCat.cs only breathes and meows" },
        };

        [MenuItem("CastleOfDice/Build Animation Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorUtility.DisplayDialog("Animation Test", "Built " + ScenePath + ".\nPress Play to try the animations.", "OK");
        }

        /// <summary>Batch entry point: -executeMethod CastleOfTheD20.Editor.AnimationTestSceneBuilder.Build</summary>
        public static void Build()
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes/Test");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.44f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.21f, 0.18f);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 3f, -8f), Quaternion.Euler(15f, 0f, 0f));

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, 150f, 0f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, 0f, 12f);
            ground.transform.localScale = new Vector3(8f, 1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            GameObject benchGo = new GameObject("AnimationTestBench");
            AnimationTestBench bench = benchGo.AddComponent<AnimationTestBench>();
            bench.viewCamera = cam;
            bench.entries = BuildEntries();
            bench.missing = new AnimationTestBench.MissingEntry[Missing.GetLength(0)];
            for (int i = 0; i < Missing.GetLength(0); i++)
                bench.missing[i] = new AnimationTestBench.MissingEntry { displayName = Missing[i, 0], reason = Missing[i, 1] };

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationTest] Built {ScenePath} with {bench.entries.Length} characters.");
        }

        private static AnimationTestBench.Entry[] BuildEntries()
        {
            List<AnimationTestBench.Entry> list = new List<AnimationTestBench.Entry>();
            foreach (Def d in Defs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[AnimationTest] Missing prefab {d.path}, skipping {d.name}.");
                    continue;
                }

                AnimationTestBench.Entry e = new AnimationTestBench.Entry
                {
                    displayName = d.name,
                    group = d.group,
                    prefab = prefab,
                    modelChild = d.child,
                    attachHeroWeapons = d.weapons,
                    heroClass = d.heroClass,
                    note = d.note,
                };

                Transform model = string.IsNullOrEmpty(d.child) ? prefab.transform : FindDeep(prefab.transform, d.child);
                Animator animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
                AnimatorController controller = animator != null ? AsController(animator.runtimeAnimatorController) : null;
                if (controller == null)
                    Debug.LogWarning($"[AnimationTest] {d.name} has no AnimatorController.");
                else
                {
                    List<string> names = new List<string>();
                    List<string> paths = new List<string>();
                    AnimatorControllerLayer layer = controller.layers[0];
                    CollectStates(layer.stateMachine, layer.name, names, paths);
                    e.stateNames = names.ToArray();
                    e.statePaths = paths.ToArray();
                }
                list.Add(e);
            }
            return list.ToArray();
        }

        private static AnimatorController AsController(RuntimeAnimatorController rac)
        {
            AnimatorOverrideController over = rac as AnimatorOverrideController;
            return over != null ? over.runtimeAnimatorController as AnimatorController : rac as AnimatorController;
        }

        private static void CollectStates(AnimatorStateMachine sm, string prefix, List<string> names, List<string> paths)
        {
            foreach (ChildAnimatorState cs in sm.states)
            {
                names.Add(cs.state.name);
                paths.Add(prefix + "." + cs.state.name);
            }
            foreach (ChildAnimatorStateMachine sub in sm.stateMachines)
                CollectStates(sub.stateMachine, prefix + "." + sub.stateMachine.name, names, paths);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Material GroundMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            mat.SetColor("_BaseColor", new Color(0.36f, 0.34f, 0.3f));
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, GroundMaterialPath);
            return mat;
        }
    }
}
