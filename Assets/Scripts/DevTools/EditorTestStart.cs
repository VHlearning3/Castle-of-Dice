#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.DevTools
{
    /// <summary>
    /// One Editor Test Start: the zone, arrival spot, hero, level with its level-up picks, gear and story progress
    /// picked in the CastleOfDice/Test Start window (Vili, 2026-10-08). Editor only.
    /// </summary>
    [Serializable]
    public class EditorTestStartSetup
    {
        /// <summary>Level-up pick: Hero's Resilience (+5 Max HP).</summary>
        public const int PickResilience = -1;

        /// <summary>Level-up pick: +1 primary attribute.</summary>
        public const int PickAttribute = -2;

        /// <summary>Arrival spot token: in front of the Tower's mirror (the room opens with the Tower challenge).</summary>
        public const string MirrorRoomSpawn = "@MirrorRoom";

        public string scene = "Zone_1_VillageAndCellar";
        /// <summary>Spawn point object name in the zone, <see cref="MirrorRoomSpawn"/>, or empty for the zone's default.</summary>
        public string spawn = "";
        public CharacterClassType hero = CharacterClassType.Warrior;
        public int level = 1;
        /// <summary>The pick of levels 2..5: <see cref="PickResilience"/>, <see cref="PickAttribute"/> or an ability slot 0-3 (Rank 2).</summary>
        public int[] levelPicks = { PickResilience, PickResilience, PickResilience, PickResilience };
        public DifficultyLevel difficulty = DifficultyLevel.Normal;

        public int gold = InventoryManager.StartingGold;
        public int scrap = 0;
        public int runesOfFate = 1;
        /// <summary>Baldur's Sharpened Blade tiers (0-3), +1 damage each.</summary>
        public int bladeTier = 0;
        /// <summary>Baldur's Runic Armor tiers (0-3), +1 AC each.</summary>
        public int armorTier = 0;
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();

        public bool firstBattleWon;
        public bool seasoned;
        public bool commanderDefeated;
        public bool witchDefeated;
        public bool kingDefeated;
        public bool heroQuestDone;
        public bool towerChallengeDone;
        public bool commanderHint;
        public bool twinWarned;
        public bool twinDefeated;
        /// <summary>More story flags, separated by commas or new lines.</summary>
        public string extraFlags = "";

        /// <summary>Gives a level-up pick, or <see cref="PickResilience"/> when missing.</summary>
        public int PickForLevel(int lvl)
        {
            int i = lvl - 2;
            return levelPicks != null && i >= 0 && i < levelPicks.Length ? levelPicks[i] : PickResilience;
        }

        /// <summary>
        /// The save this setup starts from: a normal Continue save, so the game restores it exactly as it would
        /// a player's own.
        /// </summary>
        public PlayerSaveData BuildSave()
        {
            var save = new PlayerSaveData();
            save.characterClass = (int)hero;
            save.currentLevel = Mathf.Clamp(level, 1, PlayerUnit.MaxLevel);
            save.sceneName = scene;
            save.hasPosition = false;
            save.currentHP = -1;
            save.difficulty = (int)difficulty;

            for (int lvl = 2; lvl <= save.currentLevel; lvl++)
            {
                int pick = PickForLevel(lvl);
                if (pick == PickResilience) save.maxHPBonus += 5;
                else if (pick == PickAttribute) save.attributeBonusModifier += 1;
                else if (pick >= 0 && pick < 4 && !save.upgradedAbilityIndices.Contains(pick)) save.upgradedAbilityIndices.Add(pick);
            }

            int blade = Mathf.Clamp(bladeTier, 0, ShopManager.MAX_UPGRADE_LEVEL);
            int armor = Mathf.Clamp(armorTier, 0, ShopManager.MAX_UPGRADE_LEVEL);
            save.permanentWeaponDamageBonus = blade + (heroQuestDone ? 1 : 0);
            save.permanentArmorClassBonus = armor;
            for (int t = 1; t <= blade; t++) save.storyFlags.Add(ShopManager.UpgradeFlagPrefix + "Weapon:" + t);
            for (int t = 1; t <= armor; t++) save.storyFlags.Add(ShopManager.UpgradeFlagPrefix + "Armor:" + t);

            save.gold = Mathf.Max(0, gold);
            save.scrapMetal = Mathf.Max(0, scrap);
            save.rerollScrolls = Mathf.Max(0, runesOfFate);
            int n = Mathf.Min(itemIds.Count, itemCounts.Count);
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(itemIds[i]) || itemCounts[i] <= 0) continue;
                save.itemIds.Add(itemIds[i]);
                save.itemCounts.Add(itemCounts[i]);
            }

            if (firstBattleWon) save.storyFlags.Add(PlayerProgressionManager.FirstVictoryFlag);
            if (seasoned) save.storyFlags.Add(PlayerProgressionManager.SeasonedFlag);
            if (commanderDefeated)
            {
                save.defeatedBosses.Add("CursedCommander");
                save.clearedWings.Add((int)GameLocation.Courtyard);
            }
            if (witchDefeated)
            {
                save.defeatedBosses.Add("ShadowMageMalakor");
                save.clearedWings.Add((int)GameLocation.Library);
            }
            if (kingDefeated)
            {
                save.defeatedBosses.Add("GargoyleKing");
                save.clearedWings.Add((int)GameLocation.CrownHall);
            }
            if (heroQuestDone)
            {
                ClassQuests.Definition quest = ClassQuests.For(hero);
                if (quest != null) save.storyFlags.Add(quest.RewardFlag);
            }
            if (towerChallengeDone || spawn == MirrorRoomSpawn)
            {
                save.storyFlags.Add(TowerChallenge.RunesSolvedFlag);
                save.storyFlags.Add(TowerChallenge.GuardianDefeatedFlag);
                save.storyFlags.Add(TowerChallenge.CompletedFlag);
            }
            if (commanderHint) save.storyFlags.Add("CommanderArmorWeakened");
            if (twinWarned) save.storyFlags.Add(MirrorTwinQuest.WarnedFlag);
            if (twinDefeated) save.storyFlags.Add(MirrorTwinQuest.DefeatedFlag);

            if (!string.IsNullOrEmpty(extraFlags))
            {
                foreach (string raw in extraFlags.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string flag = raw.Trim();
                    if (flag.Length > 0 && !save.storyFlags.Contains(flag)) save.storyFlags.Add(flag);
                }
            }

            return save;
        }
    }

    /// <summary>
    /// Runs an Editor Test Start in Play Mode: the game boots in the village as usual, then continues a test save
    /// (kept in its own PlayerPrefs slot, so the player's real save is never read or written) into the chosen
    /// zone and spot. The CastleOfDice/Test Start window arms it through <see cref="UnityEditor.SessionState"/>.
    /// Editor only: none of this exists in a build.
    /// </summary>
    public static class EditorTestStart
    {
        /// <summary>PlayerPrefs slot of test runs (the real one is CastleOfDice_SaveData).</summary>
        public const string TestSaveKey = "CastleOfDice_SaveData_EditorTest";

        public const string PendingKey = "CastleOfDice.TestStart.Pending";
        public const string SaveJsonKey = "CastleOfDice.TestStart.SaveJson";
        public const string SpawnKey = "CastleOfDice.TestStart.Spawn";

        /// <summary>True while this Play Mode session is a test start.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>True once the hero stands in the chosen zone with the chosen setup.</summary>
        public static bool IsReady { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            IsActive = false;
            IsReady = false;
            SaveSystem.EditorSaveKeyOverride = null;

            if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
            UnityEditor.SessionState.EraseBool(PendingKey);
            string json = UnityEditor.SessionState.GetString(SaveJsonKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;

            Begin(json, UnityEditor.SessionState.GetString(SpawnKey, string.Empty));
        }

        /// <summary>Starts a test run from a save (JSON) once the village and its main menu are up.</summary>
        public static void Begin(string saveJson, string spawn)
        {
            IsActive = true;
            IsReady = false;
            SaveSystem.EditorSaveKeyOverride = TestSaveKey;
            PlayerPrefs.SetString(TestSaveKey, saveJson);
            PlayerPrefs.Save();
            if (!Application.isPlaying) return;

            var go = new GameObject("[Editor Test Start]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<EditorTestStartRunner>().Spawn = spawn;
        }

        /// <summary>Back to the real save slot (Play Mode ended).</summary>
        public static void End()
        {
            IsActive = false;
            IsReady = false;
            SaveSystem.EditorSaveKeyOverride = null;
            PlayerPrefs.DeleteKey(TestSaveKey);
            PlayerPrefs.Save();
        }

        internal static void MarkReady() => IsReady = true;
    }

    /// <summary>Waits for the village's main menu, then continues the test save into the chosen zone.</summary>
    internal class EditorTestStartRunner : MonoBehaviour
    {
        public string Spawn;

        private IEnumerator Start()
        {
            // The village's managers and main menu run their own Start first
            float until = Time.realtimeSinceStartup + 20f;
            while (MainMenuController.Instance == null && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            yield return null;

            MainMenuController menu = MainMenuController.Instance;
            if (menu == null)
            {
                Debug.LogError("[EditorTestStart] No main menu found; the test start needs the village scene.");
                Destroy(gameObject);
                yield break;
            }

            PlayerSaveData save = SaveSystem.PeekSave();
            bool mirrorRoom = Spawn == EditorTestStartSetup.MirrorRoomSpawn;
            menu.ContinueGameAt(mirrorRoom ? string.Empty : Spawn);
            StoryPanelUI.CloseIfOpen();

            // Wait for the zone to load (Continue starts the scene change on its own)
            yield return null;
            until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until &&
                   ((SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) ||
                    (save != null && !string.IsNullOrEmpty(save.sceneName) && SceneManager.GetActiveScene().name != save.sceneName)))
            {
                yield return null;
            }
            yield return null;

            if (mirrorRoom) PlaceInMirrorRoom();

            PlayerUnit hero = FindAnyObjectByType<PlayerUnit>();
            Debug.Log($"<color=#facc15><b>[EditorTestStart] {(hero != null && hero.CharacterClass != null ? hero.CharacterClass.CharacterName : "Hero")} " +
                      $"level {(hero != null ? hero.Level : 0)} in '{SceneManager.GetActiveScene().name}'" +
                      $"{(string.IsNullOrEmpty(Spawn) ? "" : " at " + Spawn)}. Saves go to the test slot.</b></color>");
            EditorTestStart.MarkReady();
            Destroy(gameObject);
        }

        /// <summary>Just inside the mirror room, facing the mirror from beyond the Mirror Twin's trigger range.</summary>
        private static void PlaceInMirrorRoom()
        {
            TowerMirrorRoom room = FindAnyObjectByType<TowerMirrorRoom>();
            MirrorTwinEncounter encounter = FindAnyObjectByType<MirrorTwinEncounter>(FindObjectsInactive.Include);
            PlayerExplorationMovement movement = FindAnyObjectByType<PlayerExplorationMovement>();
            if (room == null || room.BlockingWall == null || encounter == null || encounter.Mirror == null || movement == null)
            {
                Debug.LogWarning("[EditorTestStart] The Tower's mirror room was not found; the hero stays at the zone's entrance.");
                return;
            }

            // The twin steps out once the hero is within TriggerDistance of the point 2 m in front of the glass:
            // arrive on the mirror's axis a little beyond that, but not past the room's (sunk) north wall
            Vector3 mirror = encounter.Mirror.transform.position;
            Vector3 normal = encounter.Mirror.WorldNormal;
            normal.y = 0f;
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
            float toWall = Vector3.Dot(room.BlockingWall.position - mirror, normal);
            float along = 2f + encounter.TriggerDistance + 1.5f;
            if (toWall > 1f && toWall - 0.5f < along)
            {
                // A short room: stand in its doorway if that is still out of range, else just outside it
                along = toWall - 0.5f > 2f + encounter.TriggerDistance + 0.3f ? toWall - 0.5f : toWall + 1.5f;
            }
            Debug.Log($"[EditorTestStart] Mirror room: wall {toWall:F1} m from the mirror, hero placed {along:F1} m out.");
            Vector3 spot = mirror + normal * along;
            Vector3 dir = -normal;

            // The room's floor continues the Tower's: keep the height the hero arrived at
            spot.y = movement.transform.position.y;

            movement.TeleportTo(spot, Quaternion.LookRotation(dir));
            CameraFollow cam = FindAnyObjectByType<CameraFollow>();
            if (cam != null) cam.SnapToTarget();
        }
    }
}
#endif
