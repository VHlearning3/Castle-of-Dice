#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Every zone scene carries exactly one copy of each singleton manager. A stray second copy makes the
    /// duplicate check in Awake destroy whichever wakes up second, which can take the whole "Managers"
    /// object (quests, inventory, sounds) with it depending on load order.
    /// </summary>
    [TestFixture]
    public class ZoneSceneManagerTests
    {
        private static readonly string[] ZoneScenes =
        {
            "Assets/Scenes/Zone_1_VillageAndCellar.unity",
            "Assets/Scenes/Zone_2_ForestPath.unity",
            "Assets/Scenes/Zone_3_CastleCourtyard.unity",
            "Assets/Scenes/Zone_4_Library.unity",
            "Assets/Scenes/Zone_5_CastleHall.unity",
            "Assets/Scenes/Zone_6_Tower.unity",
            "Assets/Scenes/Zone_7_ThroneRoom.unity"
        };

        private static readonly Type[] SingletonManagers =
        {
            typeof(GameManager),
            typeof(SceneLoader),
            typeof(MusicManager),
            typeof(SFXManager),
            typeof(PlayerProgressionManager),
            typeof(InventoryManager),
            typeof(QuestManager),
            typeof(DialogueActionTrigger)
        };

        [Test]
        public void ZoneScenes_HoldAtMostOneOfEachSingletonManager()
        {
            foreach (string scenePath in ZoneScenes)
            {
                Assert.IsTrue(File.Exists(scenePath), $"Missing zone scene: {scenePath}");
                string yaml = File.ReadAllText(scenePath);

                foreach (Type managerType in SingletonManagers)
                {
                    int copies = CountComponents(yaml, managerType);
                    Assert.LessOrEqual(copies, 1, $"{scenePath} has {copies} {managerType.Name} components; keep only the one on 'Managers'.");
                }
            }
        }

        [Test]
        public void VillageScene_KeepsItsQuestManager()
        {
            string yaml = File.ReadAllText(ZoneScenes[0]);
            Assert.AreEqual(1, CountComponents(yaml, typeof(QuestManager)), "The village needs its QuestManager for the side quests.");
            Assert.AreEqual(1, CountComponents(yaml, typeof(InventoryManager)));
        }

        private static int CountComponents(string sceneYaml, Type componentType)
        {
            string guid = FindScriptGuid(componentType);
            Assert.IsFalse(string.IsNullOrEmpty(guid), $"No script asset found for {componentType.Name}.");
            return Regex.Matches(sceneYaml, $@"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}").Count;
        }

        private static string FindScriptGuid(Type componentType)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{componentType.Name} t:MonoScript"))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == componentType) return guid;
            }
            return null;
        }
    }
}
#endif
