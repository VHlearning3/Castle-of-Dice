using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Encounter grids of any size up to 32x32: the grid slides to keep the hero on it,
    /// stays inside the room's floor area, and coordinates hold at the far corners.
    /// </summary>
    [TestFixture]
    public class EncounterGridTests
    {
        private const float Tile = 1.6f;

        // Courtyard floor inside the walls (x -44..44, z -6..44)
        private static readonly Rect CourtyardArea = new Rect(-44f, -6f, 88f, 50f);

        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GridManager.Instance = null;
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        [Test]
        public void GridCenter_StaysAuthored_WhenHeroAlreadyOnGrid()
        {
            Vector3 desired = new Vector3(0f, 0f, 19f);
            Vector3 center = DungeonRoomController.ComputeGridCenter(desired, new Vector3(3f, 0f, 15f), 32, 32, Tile, CourtyardArea);

            Assert.AreEqual(desired.x, center.x, 0.001f);
            Assert.AreEqual(desired.z, center.z, 0.001f);
        }

        [Test]
        public void GridCenter_SlidesToHero_AtTheFarSideOfTheCourtyard()
        {
            Vector3 hero = new Vector3(42f, 0f, 30f);
            Vector3 center = DungeonRoomController.ComputeGridCenter(new Vector3(0f, 0f, 19f), hero, 32, 32, Tile, CourtyardArea);

            float half = 31 * 0.5f * Tile;
            Assert.LessOrEqual(hero.x, center.x + half, "Hero must stand on the grid instead of being teleported.");
            Assert.LessOrEqual(center.x + half + Tile * 0.5f, CourtyardArea.xMax + 0.001f, "Grid must not poke through the east wall.");
            Assert.GreaterOrEqual(center.x - half - Tile * 0.5f, CourtyardArea.xMin - 0.001f);
            Assert.AreEqual(19f, center.z, 0.001f, "The grid already spans the courtyard depth, so Z keeps its authored center.");
        }

        [Test]
        public void GridCenter_KeepsTwoTileMargin_WhenAreaAllowsIt()
        {
            Rect wideArea = new Rect(-100f, -100f, 200f, 200f);
            Vector3 hero = new Vector3(30f, 0f, 0f);
            Vector3 center = DungeonRoomController.ComputeGridCenter(Vector3.zero, hero, 12, 12, Tile, wideArea);

            float eastTile = center.x + 11 * 0.5f * Tile;
            Assert.AreEqual(2f * Tile, eastTile - hero.x, 0.001f);
        }

        [Test]
        public void GridArea_ComesFromTriggerBox_WhenNotAuthored()
        {
            GameObject go = Track(new GameObject("Room"));
            go.transform.position = new Vector3(0f, 2.5f, 10f);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(88f, 6f, 50f);
            box.center = new Vector3(0f, 0f, 9f);
            DungeonRoomController room = go.AddComponent<DungeonRoomController>();

            Rect area = room.GetGridArea();

            Assert.AreEqual(-44f, area.xMin, 0.001f);
            Assert.AreEqual(44f, area.xMax, 0.001f);
            Assert.AreEqual(-6f, area.yMin, 0.001f);
            Assert.AreEqual(44f, area.yMax, 0.001f);
        }

        [Test]
        public void Grid32x32_BuildsEveryTile_AndMapsCornersBackToCoordinates()
        {
            GridManager grid = Track(new GameObject("Grid32")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(new Vector3(0f, 0f, 19f), 32, 32, Tile);

            Assert.AreEqual(32 * 32, grid.Tiles.Count);

            Vector2Int[] corners = { new Vector2Int(0, 0), new Vector2Int(31, 0), new Vector2Int(0, 31), new Vector2Int(31, 31) };
            foreach (Vector2Int c in corners)
            {
                Vector3 world = grid.GetWorldPosition(c);
                Assert.AreEqual(c, grid.GetGridPosition(world));
                Assert.IsNotNull(grid.GetTileAt(c));
            }

            List<GridTile> reach = grid.GetReachableTiles(new Vector2Int(0, 0), 5);
            Assert.IsNotEmpty(reach);
            Assert.IsNotNull(grid.FindPath(new Vector2Int(0, 0), new Vector2Int(31, 31)), "A path must cross the whole 32x32 grid.");
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }
    }
}
