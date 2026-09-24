using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Master editor controller that runs all setup steps: generating game data assets,
    /// constructing Kivenkolo Village layout (Baldur, Barnaby, Othelia, Mirabel, Castle Gate),
    /// building the 3 Castle Wings (Courtyard, Library, Crown Hall), and saving the scene.
    /// </summary>
    public static class AutoSetupGameEditor
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += CheckAndAutoSetup;
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                EditorApplication.delayCall += CheckAndAutoSetup;
            };
        }

        private static void CheckAndAutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name == "StartVillage")
            {
                bool missingVillage = GameObject.Find("Village_Layout") == null || GameObject.Find("NPC_Othelia") == null || GameObject.Find("NPC_Mirabel") == null;
                bool missingWings = GameObject.Find("Castle_Wings") == null;

                if (missingVillage || missingWings)
                {
                    Debug.Log("[AutoSetupGameEditor] StartVillage active and missing layout pieces detected. Running auto-setup...");
                    RunCompleteGameSetup();
                }
            }
        }

        [MenuItem("CastleOfDice/Run Complete Game Setup & Build All", false, 0)]
        [MenuItem("Tools/Castle of Dice/Run Complete Game Setup & Build All", false, 0)]
        public static void RunCompleteGameSetup()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "StartVillage")
            {
                activeScene = EditorSceneManager.OpenScene("Assets/Scenes/StartVillage.unity", OpenSceneMode.Single);
            }

            Debug.Log("[AutoSetupGameEditor] --- STEP 1: Generating All ScriptableObject Assets ---");
            GenerateGameDataEditor.GenerateAllGameAssets();

            Debug.Log("[AutoSetupGameEditor] --- STEP 2: Building Village Layout & NPCs (Baldur, Barnaby, Othelia, Mirabel) ---");
            BuildVillageEditor.BuildCompleteVillage();

            Debug.Log("[AutoSetupGameEditor] --- STEP 3: Building Castle Wings (Courtyard, Library, Crown Hall) ---");
            BuildDungeonWingsEditor.BuildAllDungeonWings();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[AutoSetupGameEditor] === Complete Game Setup Successfully Finished! ===");
        }
    }
}
