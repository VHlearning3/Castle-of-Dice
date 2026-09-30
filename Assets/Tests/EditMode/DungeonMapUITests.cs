#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CastleOfTheD20.UI;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class DungeonMapUITests
    {
        private GameObject canvasGo;
        private Canvas canvas;
        private GameObject mapGo;
        private DungeonMapUIController mapController;
        private GameObject gameManagerGo;
        private GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("Test_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            gameManagerGo = new GameObject("Test_GameManager", typeof(GameManager));
            gameManager = gameManagerGo.GetComponent<GameManager>();

            mapGo = new GameObject("Test_DungeonMapUIController", typeof(DungeonMapUIController));
            mapGo.transform.SetParent(canvasGo.transform, false);
            mapController = mapGo.GetComponent<DungeonMapUIController>();
            mapController.EnsureUIHierarchy();
        }

        [TearDown]
        public void TearDown()
        {
            if (mapGo != null) Object.DestroyImmediate(mapGo);
            if (gameManagerGo != null) Object.DestroyImmediate(gameManagerGo);
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
            GameInput.SetExplorationInputEnabled(true);
        }

        [Test]
        public void DungeonMap_InitializesClosedByDefault()
        {
            mapController.CloseMap();
            Assert.IsFalse(mapController.IsMapOpen, "Expedition Map modal must be closed by default.");
            Assert.IsTrue(GameInput.IsExplorationInputEnabled, "Exploration input must remain active while map is closed.");
        }

        [Test]
        public void DungeonMap_ShowMap_OpensModalAndSuspendsExploration()
        {
            mapController.ShowMap();
            Assert.IsTrue(mapController.IsMapOpen, "ShowMap() must activate the map modal panel.");
            Assert.IsFalse(GameInput.IsExplorationInputEnabled, "Exploration input must be suspended while map is open.");
        }

        [Test]
        public void DungeonMap_CloseMap_HidesModalAndRestoresExploration()
        {
            mapController.ShowMap();
            Assert.IsTrue(mapController.IsMapOpen);

            mapController.CloseMap();
            Assert.IsFalse(mapController.IsMapOpen, "CloseMap() must deactivate the map modal panel.");
            Assert.IsTrue(GameInput.IsExplorationInputEnabled, "Exploration input must be restored upon closing the map.");
        }

        [Test]
        public void DungeonMap_ToggleMap_AlternatesOpenState()
        {
            mapController.CloseMap();
            Assert.IsFalse(mapController.IsMapOpen);

            mapController.ToggleMap();
            Assert.IsTrue(mapController.IsMapOpen, "ToggleMap() should open the closed map.");

            mapController.ToggleMap();
            Assert.IsFalse(mapController.IsMapOpen, "ToggleMap() should close the open map.");
        }

        [Test]
        public void DungeonMap_RefreshMapNodes_ExecutesSuccessfully()
        {
            mapController.ShowMap();

            gameManager.SetLocation(GameLocation.Courtyard);
            mapController.RefreshMapNodes();

            gameManager.SetLocation(GameLocation.Library);
            mapController.RefreshMapNodes();

            gameManager.SetLocation(GameLocation.CrownHall);
            mapController.RefreshMapNodes();

            Assert.IsTrue(mapController.IsMapOpen, "Map should remain open and refreshed across location changes.");
        }

        [Test]
        public void DungeonMap_BuildsOneCardPerWorldZone()
        {
            Assert.AreEqual(7, mapController.ZoneNodeCount, "The map needs one card for each of the seven zones.");
        }

        [Test]
        public void DungeonMap_EveryLocation_MarksItsZoneAsCurrent()
        {
            mapController.ShowMap();

            foreach (GameLocation loc in System.Enum.GetValues(typeof(GameLocation)))
            {
                gameManager.SetLocation(loc);
                mapController.RefreshMapNodes();

                Assert.AreEqual(DungeonMapUIController.GetNodeKeyForLocation(loc), mapController.CurrentNodeKey,
                    $"{loc} must highlight its own zone card.");
                StringAssert.Contains(DungeonMapUIController.GetZoneDisplayName(loc), mapController.CurrentLocationHeader,
                    $"The map header must name the zone the player is in ({loc}).");
            }
        }

        [Test]
        public void DungeonMap_CellarAndThroneRoom_UseTheirParentCards()
        {
            Assert.AreEqual(DungeonMapUIController.KeyVillage, DungeonMapUIController.GetNodeKeyForLocation(GameLocation.Cellar));
            Assert.AreEqual(DungeonMapUIController.KeyCrownHall, DungeonMapUIController.GetNodeKeyForLocation(GameLocation.ThroneRoom));
            Assert.AreEqual(DungeonMapUIController.KeyHall, DungeonMapUIController.GetNodeKeyForLocation(GameLocation.CastleHall));
            Assert.AreEqual(DungeonMapUIController.KeyTower, DungeonMapUIController.GetNodeKeyForLocation(GameLocation.Tower));
        }

        [Test]
        public void DungeonMap_ReplacesModalBakedIntoTheScene()
        {
            GameObject otherCanvasGo = new GameObject("Legacy_Canvas", typeof(Canvas));
            GameObject legacyModal = new GameObject(DungeonMapUIController.ModalName, typeof(RectTransform));
            legacyModal.transform.SetParent(otherCanvasGo.transform, false);
            new GameObject("Node_Village", typeof(RectTransform)).transform.SetParent(legacyModal.transform, false);

            GameObject otherMapGo = new GameObject("Legacy_Map", typeof(DungeonMapUIController));
            otherMapGo.transform.SetParent(otherCanvasGo.transform, false);

            try
            {
                otherMapGo.GetComponent<DungeonMapUIController>().EnsureUIHierarchy();

                Assert.IsTrue(legacyModal == null, "The old baked map modal must be removed.");
                int modalCount = 0;
                foreach (Transform child in otherCanvasGo.transform)
                {
                    if (child.name == DungeonMapUIController.ModalName) modalCount++;
                }
                Assert.AreEqual(1, modalCount, "Exactly one fresh map modal should remain on the canvas.");
            }
            finally
            {
                Object.DestroyImmediate(otherCanvasGo);
            }
        }
    }
}
#endif
