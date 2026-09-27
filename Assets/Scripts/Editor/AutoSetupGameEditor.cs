using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Master editor controller that runs all setup steps: generating game data assets,
    /// constructing Oakhaven Village layout (Baldur, Barnaby, Othelia, Mirabel, Castle Gate),
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
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!System.IO.File.Exists("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png"))
            {
                GenerateFantasyUISpritesEditor.GenerateAllSprites();
            }

            if (activeScene.IsValid() && activeScene.name == "StartVillage")
            {
                bool missingVillage = GameObject.Find("Village_Layout") == null || GameObject.Find("NPC_Othelia") == null || GameObject.Find("NPC_Mirabel") == null;
                bool missingWings = GameObject.Find("Castle_Wings") == null;
                var hud = Object.FindAnyObjectByType<CastleOfTheD20.UI.PlayerHUD>();
                bool missingHUDStyling = hud == null || hud.transform.Find("Hero_Status_Card") == null;
                bool missingDialogueShop = GameObject.Find("Portrait_Slot_Frame") == null;

                if (missingVillage || missingWings || missingHUDStyling || missingDialogueShop)
                {
                    Debug.Log("[AutoSetupGameEditor] StartVillage active and missing layout pieces or UI styling detected. Running auto-setup...");
                    RunCompleteGameSetup();
                }
            }
        }

        [MenuItem("CastleOfDice/Run Complete Game Setup & Build All", false, 0)]
        [MenuItem("Tools/Castle of Dice/Run Complete Game Setup & Build All", false, 0)]
        public static void RunCompleteGameSetup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[AutoSetupGameEditor] Cannot run setup during play mode.");
                return;
            }

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

            Debug.Log("[AutoSetupGameEditor] --- STEP 4: Styling & Rebuilding Professional Tabletop D&D HUD ---");
            BuildHUDEditor.RebuildAndStyleHUD();

            Debug.Log("[AutoSetupGameEditor] --- STEP 5: Styling & Rebuilding Dialogue & Baldur Shop Panels ---");
            BuildDialogueAndShopEditor.RebuildAndStyleDialogueAndShop();

            Debug.Log("[AutoSetupGameEditor] --- STEP 6: Ensuring EventSystem, GraphicRaycaster & Core GameManager ---");
            MainMenuController.EnsureEventSystem();
            Canvas mainCanvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (mainCanvas != null)
            {
                MainMenuController.EnsureGraphicRaycaster(mainCanvas);
            }

            // Ensure GameManager exists on Managers object
            GameObject managersObj = GameObject.Find("Managers") ?? new GameObject("Managers");
            if (managersObj.GetComponent<CastleOfTheD20.Core.GameManager>() == null)
            {
                managersObj.AddComponent<CastleOfTheD20.Core.GameManager>();
                Debug.Log("[AutoSetupGameEditor] Attached GameManager to 'Managers' object.");
            }

            // Ensure PlayerHero in scene has no conflicting CapsuleCollider and valid CharacterController center
            GameObject playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("PlayerHero");
            if (playerObj != null)
            {
                CapsuleCollider col = playerObj.GetComponent<CapsuleCollider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                    Debug.Log("[AutoSetupGameEditor] Removed conflicting CapsuleCollider from player in scene.");
                }

                CharacterController cc = playerObj.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[AutoSetupGameEditor] === Complete Game Setup Successfully Finished! ===");
        }
    }
}
