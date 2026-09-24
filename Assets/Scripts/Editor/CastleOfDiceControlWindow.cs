using UnityEngine;
using UnityEditor;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Interactive editor window providing visual controls to trigger Castle of Dice
    /// builders and display live status of village and dungeon wing components.
    /// </summary>
    public class CastleOfDiceControlWindow : EditorWindow
    {
        [MenuItem("CastleOfDice/Open Control Panel", false, 1)]
        [MenuItem("Tools/Castle of Dice/Open Control Panel", false, 1)]
        [MenuItem("Window/Castle of Dice Control Panel", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<CastleOfDiceControlWindow>("Castle of Dice");
            window.minSize = new Vector2(380, 420);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Castle of Dice - Setup & Build Controls", EditorStyles.boldLabel);
            GUILayout.Label("One-click builders for Village, Dungeon Wings & Game Assets.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(15);

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("★ Run Complete Game Setup & Build All ★", GUILayout.Height(45)))
            {
                AutoSetupGameEditor.RunCompleteGameSetup();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);
            GUILayout.Label("Individual Builders:", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Generate Game Assets & Dialogues", GUILayout.Height(30)))
            {
                GenerateGameDataEditor.GenerateAllGameAssets();
            }

            if (GUILayout.Button("2. Build Kivenkolo Village & NPCs", GUILayout.Height(30)))
            {
                BuildVillageEditor.BuildCompleteVillage();
            }

            if (GUILayout.Button("3. Build 3 Castle Dungeon Wings", GUILayout.Height(30)))
            {
                BuildDungeonWingsEditor.BuildAllDungeonWings();
            }

            GUILayout.Space(20);
            GUILayout.Label("Scene Status & Info:", EditorStyles.boldLabel);
            bool hasVillage = GameObject.Find("Village_Layout") != null;
            bool hasWings = GameObject.Find("Castle_Wings") != null;
            bool hasOthelia = GameObject.Find("NPC_Othelia") != null;
            bool hasMirabel = GameObject.Find("NPC_Mirabel") != null;

            EditorGUILayout.LabelField("Village Layout:", hasVillage ? "✓ Present" : "✗ Missing");
            EditorGUILayout.LabelField("Kylänvanhin Othelia:", hasOthelia ? "✓ Present" : "✗ Missing");
            EditorGUILayout.LabelField("Yrttiparantaja Mirabel:", hasMirabel ? "✓ Present" : "✗ Missing");
            EditorGUILayout.LabelField("Castle Wings (1, 2, 3):", hasWings ? "✓ Present" : "✗ Missing");

            GUILayout.Space(10);
            if (GUILayout.Button("Refresh Scene Status"))
            {
                Repaint();
            }
        }
    }
}
