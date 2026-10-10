using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The expedition map names the current bosses and zones and only locks what the game locks, its
    /// "YOU ARE HERE" tag fits its text, and save shrines carry a name label that says "Game Saved" after a save.
    /// </summary>
    [TestFixture]
    public class MapAndSavePointLabelTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            gameManager = Track(new GameObject("MapTest_GameManager")).AddComponent<GameManager>();
            SetGameManager(gameManager);
            StoryFlags.Remove(TowerChallenge.CompletedFlag);
        }

        [TearDown]
        public void TearDown()
        {
            StoryFlags.Remove(TowerChallenge.CompletedFlag);
            SetGameManager(null);
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        [Test]
        public void Map_UsesTheCurrentBossAndZoneNames()
        {
            GameObject canvasGo = BuildMap(out DungeonMapUIController map);
            gameManager.SetLocation(GameLocation.Village);
            map.RefreshMapNodes();

            string all = AllText(canvasGo);
            StringAssert.DoesNotContain("MALAKOR", all.ToUpperInvariant());
            StringAssert.Contains("BOSS: SHADOW WITCH", Status(canvasGo, DungeonMapUIController.KeyLibrary));
            StringAssert.Contains("HIDDEN TRAIL", all);
            StringAssert.Contains(TavernEntrance.DisplayName, all);
        }

        [Test]
        public void Map_LocksOnlyTheThroneRoom()
        {
            GameObject canvasGo = BuildMap(out DungeonMapUIController map);
            gameManager.SetLocation(GameLocation.Forest);
            map.RefreshMapNodes();

            StringAssert.DoesNotContain("LOCKED", Status(canvasGo, DungeonMapUIController.KeyHall));
            StringAssert.DoesNotContain("LOCKED", Status(canvasGo, DungeonMapUIController.KeyTower));
            StringAssert.Contains("LOCKED - DEFEAT BOTH BOSSES", Status(canvasGo, DungeonMapUIController.KeyCrownHall));
        }

        [Test]
        public void Map_TowerShowsTheChallengeDone()
        {
            GameObject canvasGo = BuildMap(out DungeonMapUIController map);
            map.RefreshMapNodes();
            StringAssert.Contains("TOWER CHALLENGE", Status(canvasGo, DungeonMapUIController.KeyTower));

            StoryFlags.Set(TowerChallenge.CompletedFlag);
            map.RefreshMapNodes();
            StringAssert.Contains("CHALLENGE COMPLETE", Status(canvasGo, DungeonMapUIController.KeyTower));
        }

        [Test]
        public void Map_TavernTagIsWideEnoughForItsText()
        {
            GameObject canvasGo = BuildMap(out DungeonMapUIController map);
            gameManager.SetLocation(GameLocation.Tavern);
            map.RefreshMapNodes();

            TMP_Text pinLabel = null;
            foreach (TMP_Text text in canvasGo.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Label" && text.transform.parent.name == "Pin"
                    && text.transform.parent.parent.name == "Node_" + DungeonMapUIController.KeyVillage)
                {
                    pinLabel = text;
                }
            }
            Assert.IsNotNull(pinLabel);
            Assert.AreEqual("YOU ARE HERE: TAVERN", pinLabel.text);
            float needed = pinLabel.GetPreferredValues(pinLabel.text, 1000f, 34f).x;
            float width = ((RectTransform)pinLabel.transform.parent).sizeDelta.x;
            Assert.GreaterOrEqual(width, needed, "The tag must be wide enough for one line of text.");
            StringAssert.Contains(TavernEntrance.DisplayName, map.CurrentLocationHeader);
        }

        [Test]
        public void SavePoint_GetsANameLabelThatSaysGameSavedAfterASave()
        {
            GameObject shrineGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            SavePoint shrine = shrineGo.AddComponent<SavePoint>();

            SavePointLabel label = SavePointLabel.Attach(shrine);
            Assert.IsNotNull(label);
            Assert.AreSame(label, SavePointLabel.Attach(shrine), "Only one label per shrine.");
            Assert.AreEqual(shrine.ShrineName, label.Title.text);
            Assert.AreEqual(2, label.gameObject.layer, "The label must not take clicks.");
            Assert.Greater(label.transform.position.y, shrineGo.transform.position.y + 1f, "The label floats above the shrine.");
            Assert.IsFalse(label.IsShowingSaved);

            label.ShowSaved();
            Assert.IsTrue(label.IsShowingSaved);
            bool savedShown = false;
            foreach (TextMeshPro tmp in label.GetComponentsInChildren<TextMeshPro>(false))
            {
                if (tmp.text == SavePointLabel.SavedText) savedShown = true;
            }
            Assert.IsTrue(savedShown, "\"Game Saved\" shows after a save.");
            Assert.IsFalse(label.Caption.gameObject.activeSelf);
        }

        private GameObject BuildMap(out DungeonMapUIController map)
        {
            GameObject canvasGo = Track(new GameObject("Canvas", typeof(Canvas)));
            map = new GameObject("Map").AddComponent<DungeonMapUIController>();
            map.transform.SetParent(canvasGo.transform, false);
            map.EnsureUIHierarchy();
            return canvasGo;
        }

        private static string Status(GameObject canvasGo, string key)
        {
            foreach (TMP_Text text in canvasGo.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "StatusBadge" && text.transform.parent.name == "Node_" + key) return text.text;
            }
            return string.Empty;
        }

        private static string AllText(GameObject root)
        {
            var sb = new System.Text.StringBuilder();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) sb.AppendLine(text.text);
            return sb.ToString();
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private static void SetGameManager(GameManager value)
        {
            FieldInfo field = typeof(GameManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, value);
        }
    }
}
