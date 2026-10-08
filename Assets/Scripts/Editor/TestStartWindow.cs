using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.DevTools;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// CastleOfDice/Test Start (Vili, 2026-10-08): start Play Mode in any zone with any hero, level, level-up
    /// picks, gear and story progress. The game boots in the village as always and continues a test save into the
    /// chosen zone; the test save has its own PlayerPrefs slot, so the real save is never touched. The normal game
    /// start (pressing Play) is unchanged, and none of this is in a build.
    /// </summary>
    public class TestStartWindow : EditorWindow
    {
        private const string SetupPrefsKey = "CastleOfDice.TestStart.Setup";
        private const string RestoreKey = "CastleOfDice.TestStart.RestoreStartScene";
        private const string PreviousStartSceneKey = "CastleOfDice.TestStart.PreviousStartScene";
        private const string VillageScenePath = "Assets/Scenes/Zone_1_VillageAndCellar.unity";

        private static readonly Dictionary<string, string> ZoneNames = new Dictionary<string, string>
        {
            { "Zone_1_VillageAndCellar", "Oakhaven Village & Wine Cellar" },
            { "Zone_Tavern", "The Foaming Tankard (tavern)" },
            { "Zone_2_ForestPath", "Whispering Woods" },
            { "Zone_2.5_SecretForestPath", "Hidden Trail" },
            { "Zone_3_CastleCourtyard", "Castle Courtyard (Cursed Commander)" },
            { "Zone_4_Library", "Grand Archives (Shadow Witch)" },
            { "Zone_5_CastleHall", "The Great Hall" },
            { "Zone_6_Tower", "Treasure Tower (+ mirror room)" },
            { "Zone_7_ThroneRoom", "The Throne Room (Gargoyle King)" },
        };

        private static readonly string[] HeroLabels = { "Sir Roland (Warrior)", "Elisa Star-Eye (Mage)", "Sylvaine (Rogue)" };
        private static readonly CharacterClassType[] HeroTypes = { CharacterClassType.Warrior, CharacterClassType.Mage, CharacterClassType.Rogue };
        private static readonly string[] HeroAssets = { "Character_Warrior_SirRoland", "Character_Mage_Elira", "Character_Rogue_Corvo" };
        private static readonly string[] AttributeNames = { "STR", "INT", "AGI" };
        private static readonly string[] TierLabels = { "None", "+1", "+2", "+3" };

        private EditorTestStartSetup setup;
        private Vector2 scroll;
        private bool showItems = true;
        private bool showStory = true;

        private readonly List<string> scenes = new List<string>();
        private string[] sceneLabels = new string[0];
        private string spawnsScene;
        private readonly List<string> spawns = new List<string>();
        private string[] spawnLabels = new string[0];
        private ItemSO[] items = new ItemSO[0];
        private readonly CharacterClassSO[] classes = new CharacterClassSO[3];

        [MenuItem("CastleOfDice/Test Start...", false, -100)]
        public static void Open()
        {
            var window = GetWindow<TestStartWindow>("Test Start");
            window.minSize = new Vector2(420, 560);
            window.Show();
        }

        [MenuItem("CastleOfDice/Test Start (last setup)", false, -99)]
        public static void StartLastSetup()
        {
            Launch(LoadSetup());
        }

        [MenuItem("CastleOfDice/Test Start (last setup)", true)]
        private static bool CanStartLastSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private void OnEnable()
        {
            setup = LoadSetup();
            RefreshLists();
        }

        private void OnDisable() => SaveSetup(setup);

        private void OnFocus() => RefreshLists();

        private void RefreshLists()
        {
            scenes.Clear();
            var labels = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled || string.IsNullOrEmpty(s.path)) continue;
                string name = Path.GetFileNameWithoutExtension(s.path);
                if (!name.StartsWith("Zone_")) continue;
                scenes.Add(name);
                labels.Add(ZoneNames.TryGetValue(name, out string nice) ? $"{nice}   [{name}]" : name);
            }
            sceneLabels = labels.ToArray();
            spawnsScene = null;

            var found = new List<ItemSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemSO"))
            {
                ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || string.IsNullOrEmpty(item.ItemID)) continue;
                if (item.ItemType == ItemType.WeaponUpgrade || item.ItemType == ItemType.ArmorUpgrade || item.ItemType == ItemType.ScrapMetal) continue;
                if (item.ItemID.IndexOf("reroll", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                found.Add(item);
            }
            found.Sort((a, b) => string.Compare(a.ItemName, b.ItemName, System.StringComparison.OrdinalIgnoreCase));
            items = found.ToArray();

            for (int i = 0; i < HeroAssets.Length; i++)
            {
                classes[i] = Resources.Load<CharacterClassSO>("Data/" + HeroAssets[i]);
                if (classes[i] == null)
                {
                    string[] guids = AssetDatabase.FindAssets(HeroAssets[i] + " t:CharacterClassSO");
                    if (guids.Length > 0) classes[i] = AssetDatabase.LoadAssetAtPath<CharacterClassSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
        }

        /// <summary>The zone's arrival spots, read from the scene file (the scene is not opened).</summary>
        private void RefreshSpawns()
        {
            if (spawnsScene == setup.scene) return;
            spawnsScene = setup.scene;
            spawns.Clear();
            var labels = new List<string>();
            spawns.Add(string.Empty);
            labels.Add("Zone default");

            string path = null;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (Path.GetFileNameWithoutExtension(s.path) == setup.scene) path = s.path;
            }
            if (path != null && File.Exists(path))
            {
                var names = new SortedSet<string>();
                foreach (Match m in Regex.Matches(File.ReadAllText(path), @"m_Name: ((?:Spawn_\w+)|StartSpawn|Cellar_PlayerSpawnPoint)\s*$", RegexOptions.Multiline))
                {
                    names.Add(m.Groups[1].Value);
                }
                foreach (string n in names)
                {
                    spawns.Add(n);
                    labels.Add(n.Replace("Spawn_From_", "From ").Replace("_", " "));
                }
            }

            // Spots made at runtime
            if (setup.scene == World.TavernEntrance.VillageSceneName)
            {
                spawns.Add(World.TavernEntrance.SpawnInVillage);
                labels.Add("From Tavern (outside its door)");
            }
            if (setup.scene == "Zone_6_Tower")
            {
                spawns.Add(EditorTestStartSetup.MirrorRoomSpawn);
                labels.Add("Mirror room (opens the Tower challenge)");
            }

            spawnLabels = labels.ToArray();
            if (!spawns.Contains(setup.spawn)) setup.spawn = string.Empty;
        }

        private void OnGUI()
        {
            if (setup == null) setup = LoadSetup();
            RefreshSpawns();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Castle of Dice - Test Start", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Starts Play Mode in the chosen zone with this hero and gear. Uses its own test save slot: " +
                                    "your real save is never read or overwritten. Pressing Play normally is unchanged.", MessageType.None);

            EditorGUI.BeginChangeCheck();

            // --- Where
            Section("Where");
            int sceneIndex = Mathf.Max(0, scenes.IndexOf(setup.scene));
            if (scenes.Count > 0)
            {
                sceneIndex = EditorGUILayout.Popup("Zone", sceneIndex, sceneLabels);
                if (scenes[sceneIndex] != setup.scene)
                {
                    setup.scene = scenes[sceneIndex];
                    RefreshSpawns();
                }
            }
            int spawnIndex = Mathf.Max(0, spawns.IndexOf(setup.spawn));
            spawnIndex = EditorGUILayout.Popup("Arrive at", spawnIndex, spawnLabels);
            setup.spawn = spawns[spawnIndex];

            // --- Hero
            Section("Hero");
            int heroIndex = Mathf.Max(0, System.Array.IndexOf(HeroTypes, setup.hero));
            heroIndex = GUILayout.Toolbar(heroIndex, HeroLabels);
            setup.hero = HeroTypes[heroIndex];
            setup.difficulty = (DifficultyLevel)EditorGUILayout.EnumPopup("Difficulty", setup.difficulty);
            setup.level = EditorGUILayout.IntSlider("Level", setup.level, 1, Combat.PlayerUnit.MaxLevel);
            DrawLevelPicks(heroIndex);

            // --- Gear
            Section("Gear");
            setup.gold = Mathf.Max(0, EditorGUILayout.IntField("Gold", setup.gold));
            setup.scrap = Mathf.Max(0, EditorGUILayout.IntField("Scrap metal", setup.scrap));
            setup.runesOfFate = Mathf.Max(0, EditorGUILayout.IntField("Rune of Fate", setup.runesOfFate));
            setup.bladeTier = EditorGUILayout.Popup("Sharpened Blade (Baldur)", setup.bladeTier, TierLabels);
            setup.armorTier = EditorGUILayout.Popup("Runic Armor (Baldur)", setup.armorTier, TierLabels);
            showItems = EditorGUILayout.Foldout(showItems, "Bag items", true);
            if (showItems) DrawItems();

            // --- Story
            Section("Story progress");
            showStory = EditorGUILayout.Foldout(showStory, "Flags and bosses", true);
            if (showStory) DrawStory();

            if (EditorGUI.EndChangeCheck()) SaveSetup(setup);

            EditorGUILayout.Space(6);
            EditorGUILayout.EndScrollView();

            // Always in view under the scrolling settings
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", GUILayout.Height(34), GUILayout.Width(90)))
            {
                setup = new EditorTestStartSetup();
                SaveSetup(setup);
                spawnsScene = null;
                GUI.FocusControl(null);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
                if (GUILayout.Button("▶  Start", GUILayout.Height(34)))
                {
                    Launch(setup);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            if (EditorApplication.isPlaying && EditorTestStart.IsActive)
            {
                EditorGUILayout.HelpBox("Test start running. Saves go to the test slot and are thrown away when Play Mode ends.", MessageType.Info);
            }
            EditorGUILayout.Space(6);
        }

        private void DrawLevelPicks(int heroIndex)
        {
            if (setup.levelPicks == null || setup.levelPicks.Length < 4)
            {
                var picks = new int[4];
                for (int i = 0; i < 4; i++) picks[i] = setup.levelPicks != null && i < setup.levelPicks.Length ? setup.levelPicks[i] : EditorTestStartSetup.PickResilience;
                setup.levelPicks = picks;
            }

            CharacterClassSO cls = classes[heroIndex];
            var labels = new List<string> { "Hero's Resilience (+5 Max HP)", $"+1 {AttributeNames[heroIndex]}" };
            for (int slot = 0; slot < 4; slot++)
            {
                string ability = cls != null && slot < cls.StartingAbilities.Count && cls.StartingAbilities[slot] != null
                    ? cls.StartingAbilities[slot].AbilityName : $"Ability {slot + 1}";
                labels.Add($"Rank 2: {ability}");
            }
            string[] options = labels.ToArray();

            var ranked = new HashSet<int>();
            bool duplicate = false;
            EditorGUI.indentLevel++;
            for (int lvl = 2; lvl <= setup.level; lvl++)
            {
                int pick = setup.levelPicks[lvl - 2];
                int index = pick == EditorTestStartSetup.PickResilience ? 0 : pick == EditorTestStartSetup.PickAttribute ? 1 : Mathf.Clamp(pick + 2, 0, options.Length - 1);
                index = EditorGUILayout.Popup($"Level {lvl} pick", index, options);
                pick = index == 0 ? EditorTestStartSetup.PickResilience : index == 1 ? EditorTestStartSetup.PickAttribute : index - 2;
                setup.levelPicks[lvl - 2] = pick;
                if (pick >= 0 && !ranked.Add(pick)) duplicate = true;
            }
            EditorGUI.indentLevel--;

            if (duplicate)
            {
                EditorGUILayout.HelpBox("The same ability is picked for Rank 2 twice; the second pick does nothing.", MessageType.Warning);
            }
            if (setup.level >= Combat.PlayerUnit.FifthAbilityLevel && cls != null && cls.Level4Ability != null)
            {
                EditorGUILayout.LabelField(" ", $"Level {Combat.PlayerUnit.FifthAbilityLevel} adds {cls.Level4Ability.AbilityName}.", EditorStyles.miniLabel);
            }
        }

        private void DrawItems()
        {
            EditorGUI.indentLevel++;
            foreach (ItemSO item in items)
            {
                int i = setup.itemIds.IndexOf(item.ItemID);
                int count = i >= 0 && i < setup.itemCounts.Count ? setup.itemCounts[i] : 0;
                int next = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent(item.ItemName, item.ItemID), count), 0, 99);
                if (next == count) continue;
                if (i < 0)
                {
                    setup.itemIds.Add(item.ItemID);
                    setup.itemCounts.Add(next);
                }
                else
                {
                    while (setup.itemCounts.Count <= i) setup.itemCounts.Add(0);
                    setup.itemCounts[i] = next;
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawStory()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Level milestones (each gives +1 level once)", EditorStyles.miniBoldLabel);
            setup.firstBattleWon = EditorGUILayout.ToggleLeft("First battle won", setup.firstBattleWon);
            setup.seasoned = EditorGUILayout.ToggleLeft("3 battles won / Commander or Witch dealt with", setup.seasoned);
            setup.heroQuestDone = EditorGUILayout.ToggleLeft("Hero's own quest rewarded (+1 weapon damage)", setup.heroQuestDone);
            setup.towerChallengeDone = EditorGUILayout.ToggleLeft("Tower challenge complete (mirror room open)", setup.towerChallengeDone);

            int used = (setup.firstBattleWon ? 1 : 0) + (setup.seasoned ? 1 : 0) + (setup.heroQuestDone ? 1 : 0) + (setup.towerChallengeDone ? 1 : 0);
            if (used + 1 != setup.level)
            {
                EditorGUILayout.HelpBox($"{used} milestone(s) used = level {used + 1} in a normal game. With level {setup.level} the " +
                                        "remaining milestones still give levels (up to 5).", MessageType.None);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Bosses", EditorStyles.miniBoldLabel);
            setup.commanderDefeated = EditorGUILayout.ToggleLeft("Cursed Commander defeated", setup.commanderDefeated);
            setup.witchDefeated = EditorGUILayout.ToggleLeft("Shadow Witch defeated", setup.witchDefeated);
            setup.kingDefeated = EditorGUILayout.ToggleLeft("The Gargoyle King defeated", setup.kingDefeated);
            setup.twinDefeated = EditorGUILayout.ToggleLeft("Mirror Twin defeated", setup.twinDefeated);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Story", EditorStyles.miniBoldLabel);
            setup.commanderHint = EditorGUILayout.ToggleLeft("Baldur's greaves hint (Commander -2 AC)", setup.commanderHint);
            setup.twinWarned = EditorGUILayout.ToggleLeft("Blonde elf's Mirror Twin warning heard", setup.twinWarned);
            EditorGUILayout.LabelField("More story flags (comma separated)");
            setup.extraFlags = EditorGUILayout.TextField(setup.extraFlags);
            EditorGUI.indentLevel--;
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static EditorTestStartSetup LoadSetup()
        {
            string json = EditorPrefs.GetString(SetupPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<EditorTestStartSetup>(json);
                    if (loaded != null) return loaded;
                }
                catch (System.ArgumentException) { }
            }
            return new EditorTestStartSetup();
        }

        private static void SaveSetup(EditorTestStartSetup s)
        {
            if (s != null) EditorPrefs.SetString(SetupPrefsKey, JsonUtility.ToJson(s));
        }

        /// <summary>Arms the test start and enters Play Mode from the village (where the game always boots).</summary>
        public static void Launch(EditorTestStartSetup s)
        {
            if (s == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SceneAsset village = AssetDatabase.LoadAssetAtPath<SceneAsset>(VillageScenePath);
            if (village == null)
            {
                Debug.LogError($"[TestStart] {VillageScenePath} not found.");
                return;
            }

            SaveSetup(s);
            SessionState.SetString(EditorTestStart.SaveJsonKey, JsonUtility.ToJson(s.BuildSave()));
            SessionState.SetString(EditorTestStart.SpawnKey, s.spawn ?? string.Empty);
            SessionState.SetBool(EditorTestStart.PendingKey, true);

            if (!SessionState.GetBool(RestoreKey, false))
            {
                SessionState.SetString(PreviousStartSceneKey, EditorSceneManager.playModeStartScene != null
                    ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : string.Empty);
                SessionState.SetBool(RestoreKey, true);
            }
            EditorSceneManager.playModeStartScene = village;
            Debug.Log($"[TestStart] {s.hero} level {s.level} -> {s.scene}{(string.IsNullOrEmpty(s.spawn) ? "" : " / " + s.spawn)}");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>After Play Mode: the normal Play start scene comes back and the test save is thrown away.</summary>
        [InitializeOnLoadMethod]
        private static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;

            // A start that never reached Play Mode (compile errors) must not hijack the next normal Play
            SessionState.EraseBool(EditorTestStart.PendingKey);
            if (!SessionState.GetBool(RestoreKey, false)) return;

            string previous = SessionState.GetString(PreviousStartSceneKey, string.Empty);
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.EraseBool(RestoreKey);
            SessionState.EraseString(PreviousStartSceneKey);
            EditorTestStart.End();
        }
    }
}
