#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CastleOfTheD20.UI;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class QuestHUDTrackerTests
    {
        private GameObject canvasGo;
        private Canvas canvas;
        private GameObject hudGo;
        private QuestHUDUIController questHUD;
        private GameObject invGo;
        private InventoryManager inventoryManager;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("Test_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            hudGo = new GameObject("Quest_Tracker_Card", typeof(RectTransform), typeof(QuestHUDUIController));
            hudGo.transform.SetParent(canvasGo.transform, false);
            questHUD = hudGo.GetComponent<QuestHUDUIController>();
            questHUD.AutoLocateOrBuildHierarchy();

            invGo = new GameObject("Test_InventoryManager", typeof(InventoryManager));
            inventoryManager = invGo.GetComponent<InventoryManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (invGo != null) Object.DestroyImmediate(invGo);
            if (hudGo != null) Object.DestroyImmediate(hudGo);
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void QuestHUD_InitializesExpandedByDefault()
        {
            Assert.IsFalse(questHUD.IsCollapsed, "Quest HUD Tracker must start in expanded state by default.");
        }

        [Test]
        public void QuestHUD_ToggleCollapse_TogglesStateCorrectly()
        {
            questHUD.ToggleCollapse();
            Assert.IsTrue(questHUD.IsCollapsed, "First toggle should collapse the quest card.");

            questHUD.ToggleCollapse();
            Assert.IsFalse(questHUD.IsCollapsed, "Second toggle should re-expand the quest card.");
        }

        [Test]
        public void QuestHUD_SetCollapsed_ExplicitStateUpdatesHeight()
        {
            RectTransform cardRect = questHUD.GetComponent<RectTransform>();

            questHUD.SetCollapsed(true);
            Assert.IsTrue(questHUD.IsCollapsed);
            Assert.AreEqual(36f, cardRect.sizeDelta.y, 0.1f, "Collapsed height should equal 36px header bar.");

            questHUD.SetCollapsed(false);
            Assert.IsFalse(questHUD.IsCollapsed);
            Assert.GreaterOrEqual(cardRect.sizeDelta.y, 140f, "Expanded height should be at least minimum expanded height.");
        }

        [Test]
        public void QuestHUD_RefreshQuestList_ExecutesCleanly()
        {
            Assert.DoesNotThrow(() => questHUD.RefreshQuestList(),
                "RefreshQuestList must execute without null references or exceptions.");
        }
    }
}
#endif
