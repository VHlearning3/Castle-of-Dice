using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Walking paths for click-to-move (Vili, 2026-10-07). No zone has a baked NavMesh and the zones are built
    /// in code, so a NavMesh is built at runtime from the physics colliders around the hero and the clicked spot,
    /// the first time a click needs it and again when a click lands outside the built area.
    /// People, doors and other interactables (layer 8) and units (layer 9) are left out: the path runs through
    /// them and the hero's CharacterController handles the bump.
    /// </summary>
    public static class RuntimeNavMesh
    {
        private const float MinBuildSize = 90f;
        private const float MaxBuildSize = 200f;
        private const float BuildHeight = 60f;
        private const float EdgeMargin = 8f;
        private const float RebuildAfterSeconds = 20f;

        private static NavMeshData data;
        private static NavMeshDataInstance instance;
        private static Bounds builtBounds;
        private static float builtTime = -1000f;
        private static int builtSceneHandle = -1;
        private static readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        private static readonly List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
        private static NavMeshPath path;
        private static AsyncOperation refreshing;

        /// <summary>
        /// Makes sure the NavMesh covers both points, building it if needed. Returns false when nothing walkable
        /// was found (the caller then walks straight).
        /// </summary>
        public static bool EnsureCovers(Vector3 a, Vector3 b, Transform hero)
        {
            int sceneHandle = SceneManager.GetActiveScene().handle;
            if (sceneHandle != builtSceneHandle) Clear();

            if (data != null && Time.unscaledTime - builtTime < RebuildAfterSeconds && Covers(a) && Covers(b)) return true;
            // A background refresh is still working on the NavMesh: keep walking on the current one meanwhile
            if (data != null && refreshing != null && !refreshing.isDone) return true;

            Vector3 center = (a + b) * 0.5f;
            float span = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z)) + EdgeMargin * 4f;
            float size = Mathf.Clamp(span, MinBuildSize, MaxBuildSize);
            Bounds bounds = new Bounds(center, new Vector3(size, BuildHeight, size));

            if (!CollectFor(bounds, hero)) return false;
            NavMeshBuildSettings settings = BuildSettings();

            if (data == null)
            {
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                if (data == null) return false;
                instance = NavMesh.AddNavMeshData(data);
            }
            else if (!NavMeshBuilder.UpdateNavMeshData(data, settings, sources, bounds))
            {
                return false;
            }

            builtBounds = bounds;
            builtTime = Time.unscaledTime;
            builtSceneHandle = sceneHandle;
            return true;
        }

        /// <summary>
        /// Fills <paramref name="corners"/> with a walking path. Returns the corner count, or 0 when there is no
        /// usable path (walk straight instead).
        /// </summary>
        public static int FindPath(Vector3 from, Vector3 to, Vector3[] corners)
        {
            if (data == null) return 0;

            if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, 2.5f, NavMesh.AllAreas)) return 0;
            if (!NavMesh.SamplePosition(to, out NavMeshHit toHit, 3f, NavMesh.AllAreas)) return 0;

            if (path == null) path = new NavMeshPath();
            if (!NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path)) return 0;
            if (path.status == NavMeshPathStatus.PathInvalid) return 0;

            return path.GetCornersNonAlloc(corners);
        }

        /// <summary>
        /// Updates the current NavMesh in the background after walls move (the Tower's sinking wall). Only the
        /// changed tiles are redone, over a few frames, and the old paths stay usable until it is ready, so there
        /// is no one-frame full rebuild on the next click.
        /// </summary>
        public static void RefreshAsync(Transform hero)
        {
            if (data == null || builtSceneHandle != SceneManager.GetActiveScene().handle)
            {
                Clear();
                return;
            }
            if (!CollectFor(builtBounds, hero)) return;
            refreshing = NavMeshBuilder.UpdateNavMeshDataAsync(data, BuildSettings(), new List<NavMeshBuildSource>(sources), builtBounds);
            builtTime = Time.unscaledTime;
        }

        /// <summary>Forgets the NavMesh (scene change).</summary>
        public static void Clear()
        {
            if (instance.valid) instance.Remove();
            instance = default;
            data = null;
            refreshing = null;
            builtTime = -1000f;
            builtSceneHandle = -1;
        }

        private static bool CollectFor(Bounds bounds, Transform hero)
        {
            int mask = ~((1 << 2) | (1 << 8) | (1 << 9));
            sources.Clear();
            markups.Clear();
            NavMeshBuilder.CollectSources(bounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            FilterSources(hero);
            return sources.Count > 0;
        }

        private static NavMeshBuildSettings BuildSettings()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.9f;
            settings.agentClimb = 0.35f;
            settings.agentSlope = 45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.2f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            return settings;
        }

        private static bool Covers(Vector3 p)
        {
            Vector3 min = builtBounds.min, max = builtBounds.max;
            return p.x > min.x + EdgeMargin && p.x < max.x - EdgeMargin
                && p.z > min.z + EdgeMargin && p.z < max.z - EdgeMargin
                && p.y > min.y && p.y < max.y;
        }

        private static void FilterSources(Transform hero)
        {
            int keep = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                NavMeshBuildSource source = sources[i];
                Component component = source.component;
                if (component != null)
                {
                    if (component is Collider col && (col.isTrigger || col is CharacterController)) continue;
                    if (hero != null && component.transform.IsChildOf(hero)) continue;
                    if (component.GetComponentInParent<CombatUnit>() != null) continue;
                }
                sources[keep++] = source;
            }
            sources.RemoveRange(keep, sources.Count - keep);
        }
    }
}
