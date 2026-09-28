using UnityEngine;
using UnityEditor;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor window with the Castle of Dice build controls. The 7 zone scenes are built by
    /// <see cref="ZoneSceneBuilder"/>; the individual steps operate on the currently open scene
    /// and only mark it dirty, so nothing is saved without the user's say-so.
    /// </summary>
    public class CastleOfDiceControlWindow : EditorWindow
    {
        [MenuItem("CastleOfDice/Open Control Panel", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<CastleOfDiceControlWindow>("Castle of Dice");
            window.minSize = new Vector2(400, 420);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Castle of Dice - Build Controls", EditorStyles.boldLabel);
            GUILayout.Label("Builds and registers the 7 zone scenes (Zone_1 ... Zone_7).", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(12);

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("Build All 7 Zone Scenes", GUILayout.Height(40)))
            {
                ZoneSceneBuilder.BuildAll7ZoneScenes();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);
            GUILayout.Label("Game Data:", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Missing Game Assets", GUILayout.Height(28)))
            {
                GenerateGameDataEditor.GenerateAllGameAssets(true);
            }

            if (GUILayout.Button("Reset All Game Assets To Defaults", GUILayout.Height(28)))
            {
                GenerateGameDataEditor.ResetAllGameAssetsMenu();
            }

            if (GUILayout.Button("Generate / Refresh UI Theme Asset", GUILayout.Height(28)))
            {
                GenerateUIThemeEditor.GenerateUITheme();
            }

            GUILayout.Space(15);
            GUILayout.Label("Active Scene Steps (marks scene dirty, save manually):", EditorStyles.boldLabel);

            if (GUILayout.Button("Build Oakhaven Village & NPCs", GUILayout.Height(28)))
            {
                BuildVillageEditor.BuildCompleteVillage();
            }

            if (GUILayout.Button("Setup MusicManager & Assign 7 Audio Tracks", GUILayout.Height(28)))
            {
                BuildVillageEditor.EnsureMusicManager();
            }

            if (GUILayout.Button("Validate and Repair Cellar Setup", GUILayout.Height(28)))
            {
                BuildCellarEditor.ValidateAndRepairCellar();
            }

            if (GUILayout.Button("Rebuild & Style D&D HUD", GUILayout.Height(28)))
            {
                BuildHUDEditor.RebuildAndStyleHUD();
            }

            if (GUILayout.Button("Rebuild & Style Dialogue & Baldur Shop Panels", GUILayout.Height(28)))
            {
                BuildDialogueAndShopEditor.RebuildAndStyleDialogueAndShop();
            }

            GUILayout.Space(15);
            GUILayout.Label("Active Scene Status:", EditorStyles.boldLabel);

            var musicManager = Object.FindAnyObjectByType<CastleOfTheD20.Core.MusicManager>();
            bool hasMM = musicManager != null;
            bool clipsAssigned = hasMM && musicManager.VillageSongClip != null && musicManager.GargoyleKingPhase2Clip != null;
            EditorGUILayout.LabelField("MusicManager Component:", hasMM ? "[OK] Present" : "[MISSING]");
            EditorGUILayout.LabelField("All 7 Audio Tracks Assigned:", clipsAssigned ? "[OK] Configured" : "[MISSING] Incomplete");

            var hud = Object.FindAnyObjectByType<CastleOfTheD20.UI.PlayerHUD>(FindObjectsInactive.Include);
            bool hasHUD = hud != null;
            bool hasHeroCard = hasHUD && hud.transform.Find("Hero_Status_Card") != null;
            EditorGUILayout.LabelField("PlayerHUD Component:", hasHUD ? "[OK] Present" : "[MISSING]");
            EditorGUILayout.LabelField("Tabletop D&D Styling:", hasHeroCard ? "[OK] Configured & Styled" : "[MISSING] Needs Rebuilding");

            var theme = Resources.Load<CastleOfTheD20.UI.UITheme>("UITheme");
            bool hasTheme = theme != null && theme.panelDark != null;
            EditorGUILayout.LabelField("UITheme Resource Asset:", hasTheme ? "[OK] Generated & Configured" : "[MISSING] Run Generate UI Theme");

            GUILayout.Space(12);
            if (GUILayout.Button("Refresh Status", GUILayout.Height(26)))
            {
                Repaint();
            }
        }
    }
}
