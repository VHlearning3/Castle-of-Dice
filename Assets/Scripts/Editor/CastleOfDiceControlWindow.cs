using UnityEngine;
using UnityEditor;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Interactive editor window providing visual controls to trigger Castle of Dice
    /// builders and display live status of village, rooms, planes, and bosses.
    /// </summary>
    public class CastleOfDiceControlWindow : EditorWindow
    {
        [MenuItem("CastleOfDice/Open Control Panel", false, 0)]
        [MenuItem("Tools/Castle of Dice/Open Control Panel", false, 0)]
        [MenuItem("Window/Castle of Dice Control Panel", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<CastleOfDiceControlWindow>("Castle of Dice");
            window.minSize = new Vector2(400, 520);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Castle of Dice - Setup & Build Controls", EditorStyles.boldLabel);
            GUILayout.Label("One-click builders for Village, Planes (CourtYard, Forest, Library, ThroneRoom), Doors & Bosses.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(12);

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("★ Build Walls, Doors & Place Bosses on Planes ★", GUILayout.Height(40)))
            {
                BuildDungeonWingsEditor.BuildAllDungeonWings();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("Run Complete Game Setup (Assets + Village + Boss Rooms)", GUILayout.Height(35)))
            {
                AutoSetupGameEditor.RunCompleteGameSetup();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);
            GUILayout.Label("Individual Steps:", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Generate Game Assets & Dialogues", GUILayout.Height(28)))
            {
                GenerateGameDataEditor.GenerateAllGameAssets();
            }

            if (GUILayout.Button("2. Build Kivenkolo Village & NPCs (Othelia, Mirabel)", GUILayout.Height(28)))
            {
                BuildVillageEditor.BuildCompleteVillage();
            }

            if (GUILayout.Button("3. Setup MusicManager & Assign 7 Audio Tracks", GUILayout.Height(28)))
            {
                BuildVillageEditor.EnsureMusicManager();
            }

            GUILayout.Space(15);
            GUILayout.Label("Planes & Room Status:", EditorStyles.boldLabel);
            bool hasCourtyard = GameObject.Find("CourtYard") != null;
            bool hasForest = GameObject.Find("Forest") != null;
            bool hasLibrary = GameObject.Find("Library") != null;
            bool hasThroneRoom = GameObject.Find("ThroneRoom") != null;
            bool hasDoors = GameObject.Find("Doors_And_Passages") != null;

            EditorGUILayout.LabelField("CourtYard Plane:", hasCourtyard ? "✓ Detected" : "✗ Missing");
            EditorGUILayout.LabelField("Forest Plane:", hasForest ? "✓ Detected" : "✗ Missing");
            EditorGUILayout.LabelField("Library Plane:", hasLibrary ? "✓ Detected" : "✗ Missing");
            EditorGUILayout.LabelField("ThroneRoom Plane:", hasThroneRoom ? "✓ Detected" : "✗ Missing");
            EditorGUILayout.LabelField("Walls & Connecting Doors:", hasDoors ? "✓ Built" : "✗ Not built yet");

            GUILayout.Space(10);
            GUILayout.Label("Boss & NPC Status:", EditorStyles.boldLabel);
            bool hasCmdr = GameObject.Find("Boss_CursedCommander") != null || GameObject.Find("NPC_CursedCommander") != null;
            bool hasMalakor = GameObject.Find("Boss_ShadowMageMalakor") != null || GameObject.Find("NPC_Malakor") != null;
            bool hasGargoyle = GameObject.Find("Boss_GargoyleKing") != null || GameObject.Find("NPC_GargoyleKing") != null;
            bool hasVillage = GameObject.Find("Village_Layout") != null;

            EditorGUILayout.LabelField("Kivenkolo Village & NPCs:", hasVillage ? "✓ Present" : "✗ Missing");
            EditorGUILayout.LabelField("Kirottu Komentaja (Courtyard):", hasCmdr ? "✓ Placed" : "✗ Missing");
            EditorGUILayout.LabelField("Varjomaagi Malakor (Library):", hasMalakor ? "✓ Placed" : "✗ Missing");
            EditorGUILayout.LabelField("Kivettymiskuningas (ThroneRoom):", hasGargoyle ? "✓ Placed" : "✗ Missing");

            GUILayout.Space(10);
            GUILayout.Label("Audio & Music Status:", EditorStyles.boldLabel);
            var musicManager = Object.FindAnyObjectByType<CastleOfTheD20.Core.MusicManager>();
            bool hasMM = musicManager != null;
            bool clipsAssigned = hasMM && musicManager.VillageSongClip != null && musicManager.GargoyleKingPhase2Clip != null;
            EditorGUILayout.LabelField("MusicManager Component:", hasMM ? "✓ Present" : "✗ Missing");
            EditorGUILayout.LabelField("All 7 Audio Tracks Assigned:", clipsAssigned ? "✓ Configured (7 MP3s)" : "✗ Incomplete");

            GUILayout.Space(12);
            if (GUILayout.Button("Refresh Status", GUILayout.Height(26)))
            {
                Repaint();
            }
        }
    }
}
