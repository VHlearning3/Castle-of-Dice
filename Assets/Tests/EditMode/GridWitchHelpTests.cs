using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Vili, 2026-10-07: the How to Play page fits its panel, the combat grid is see-through like Baldur's Gate 3,
    /// and the Shadow Witch moves only by teleporting, at most 4 tiles.
    /// </summary>
    [TestFixture]
    public class GridWitchHelpTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private GridManager grid;

        [SetUp]
        public void SetUp()
        {
            grid = Track(new GameObject("GWH_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);
        }

        [TearDown]
        public void TearDown()
        {
            StoryPanelUI.CloseIfOpen();
            foreach (StoryPanelUI panel in Object.FindObjectsByType<StoryPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(panel.gameObject);
            }
            GameInput.SetExplorationInputEnabled(true);

            foreach (EnemyUnit enemy in Object.FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
            GridManager.Instance = null;
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        #region How to Play

        [Test]
        public void HowToPlay_PanelGrowsToHoldTheWholeText()
        {
            StoryPanelUI.Show("How to Play", PauseMenuUI.HelpText);
            Assert.IsTrue(StoryPanelUI.IsOpen);

            TMPro.TextMeshProUGUI body = StoryPanelUI.CurrentBody;
            Assert.IsNotNull(body);
            float needed = body.GetPreferredValues(body.text, ((RectTransform)body.transform).rect.width > 0f ? ((RectTransform)body.transform).rect.width : 760f, 0f).y;
            float bodyRoom = ((RectTransform)body.transform).rect.height;

            Assert.Greater(StoryPanelUI.CurrentPanelHeight, 600f, "The help page is longer than a letter, so the panel grows.");
            Assert.LessOrEqual(needed, bodyRoom + 1f, "Every paragraph, Saving included, sits above the Close button.");
        }

        [Test]
        public void HowToPlay_PanelStaysWithinTheScreen()
        {
            Assert.AreEqual(600f, StoryPanelUI.PanelHeightFor(100f, 1080f), 0.01f, "Short pages keep the standard size.");
            Assert.AreEqual(960f, StoryPanelUI.PanelHeightFor(5000f, 1080f), 0.01f, "Never taller than the canvas minus a margin.");
        }

        #endregion

        #region See-through grid

        [Test]
        public void GridOverlay_IsTransparent_WithStrongEdgesAndFaintInside()
        {
            Material overlay = GridTile.OverlayMaterial;
            Assert.IsNotNull(overlay);
            Assert.GreaterOrEqual(overlay.renderQueue, 3000, "Drawn in the transparent queue so the floor shows through.");
            Assert.IsNotNull(overlay.mainTexture);

            Assert.AreEqual(1f, GridTile.OverlayAlpha(0, 30), 0.001f, "The edge is drawn fully.");
            Assert.AreEqual(1f, GridTile.OverlayAlpha(30, 63), 0.001f);
            Assert.Less(GridTile.OverlayAlpha(32, 32), 0.3f, "The inside is see-through.");
        }

        #endregion

        #region Shadow Witch teleports

        [Test]
        public void Witch_TeleportsToTheTileHerAiPicks_WhenWithinFour()
        {
            ShadowMageMalakorBoss witch = CreateWitch(new Vector2Int(1, 1));
            witch.WalkToTile(grid.GetTileAt(new Vector2Int(3, 5)));

            Assert.AreEqual(new Vector2Int(3, 5), witch.GridPosition);
            Assert.IsFalse(witch.IsWalking, "She appears there, she does not walk.");
        }

        [Test]
        public void Witch_TeleportsAtMostFourTiles_TowardAFartherTile()
        {
            ShadowMageMalakorBoss witch = CreateWitch(new Vector2Int(1, 1));
            witch.WalkToTile(grid.GetTileAt(new Vector2Int(10, 10)));

            Assert.AreEqual(ShadowMageMalakorBoss.TeleportRange, grid.GetDistance(new Vector2Int(1, 1), witch.GridPosition));
            Assert.AreEqual(new Vector2Int(5, 5), witch.GridPosition, "The closest tile to her goal within reach.");
        }

        [Test]
        public void Witch_EscapeFromAHit_StaysWithinFourTiles()
        {
            ShadowMageMalakorBoss witch = CreateWitch(new Vector2Int(6, 6));
            for (int i = 0; i < 25; i++)
            {
                Vector2Int before = witch.GridPosition;
                witch.TeleportToRandomTile();
                int moved = grid.GetDistance(before, witch.GridPosition);
                Assert.That(moved, Is.InRange(2, ShadowMageMalakorBoss.TeleportRange));
            }
        }

        #endregion

        private ShadowMageMalakorBoss CreateWitch(Vector2Int at)
        {
            GameObject go = Track(new GameObject("GWH_Witch"));
            ShadowMageMalakorBoss witch = go.AddComponent<ShadowMageMalakorBoss>();
            StatusEffectController effects = go.GetComponent<StatusEffectController>();
            if (effects != null) Invoke(effects, "Awake");
            Invoke(witch, "Awake");
            witch.InitializeUnit();
            witch.MoveToTile(grid.GetTileAt(at));
            return witch;
        }

        private static void Invoke(object target, string method)
        {
            for (System.Type t = target.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (m != null)
                {
                    m.Invoke(target, null);
                    return;
                }
            }
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }
    }
}
