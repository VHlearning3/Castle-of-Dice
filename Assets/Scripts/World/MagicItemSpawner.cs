using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Places the three worn magic items (Vili, 2026-10-06) at runtime, so no zone scene is edited:
    /// - Oathkeeper Shield: Castle Courtyard, beside the Commander's reward chest once he is overcome;
    /// - Shadow Cloak: the mage's secret room in the Library, beside her chest once the shelves slide open;
    /// - Boots of Striding: the Treasure Tower, beside the Giant's Elixir once the Tower challenge is done.
    /// An item appears as soon as its condition is met (also mid-visit) and never again after it was taken.
    /// </summary>
    public static class MagicItemSpawner
    {
        public const string CourtyardScene = "Zone_3_CastleCourtyard";
        public const string LibraryScene = "Zone_4_Library";
        public const string TowerScene = "Zone_6_Tower";

        public const string CommanderId = "CursedCommander";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            StoryFlags.OnFlagSet -= HandleFlagSet;
            StoryFlags.OnFlagSet += HandleFlagSet;
            GameManager.OnBossDefeated -= HandleBossDefeated;
            GameManager.OnBossDefeated += HandleBossDefeated;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SpawnFor(scene.name);
        }

        private static void HandleFlagSet(string flag)
        {
            if (string.Equals(flag, TomeShelfPuzzle.OpenedFlag, StringComparison.OrdinalIgnoreCase)
                || string.Equals(flag, TowerChallenge.CompletedFlag, StringComparison.OrdinalIgnoreCase))
            {
                SpawnFor(SceneManager.GetActiveScene().name);
            }
        }

        private static void HandleBossDefeated(string bossId)
        {
            if (string.Equals(bossId, CommanderId, StringComparison.OrdinalIgnoreCase)) SpawnFor(SceneManager.GetActiveScene().name);
        }

        /// <summary>Places the items whose time has come in <paramref name="sceneName"/>. Returns how many.</summary>
        public static int SpawnFor(string sceneName)
        {
            if (!Application.isPlaying) return 0;
            int placed = 0;

            if (sceneName == CourtyardScene && GameManager.Instance != null && GameManager.Instance.IsBossDefeated(CommanderId))
            {
                if (TrySpawn(MagicItems.OathkeeperShieldId, FindCommanderSpot())) placed++;
            }
            else if (sceneName == LibraryScene && StoryFlags.Has(TomeShelfPuzzle.OpenedFlag))
            {
                if (TrySpawn(MagicItems.ShadowCloakId, NearAnchor("Mage_Secret_Chest", new Vector3(-1.8f, 0f, -0.6f)))) placed++;
            }
            else if (sceneName == TowerScene && StoryFlags.Has(TowerChallenge.CompletedFlag))
            {
                if (TrySpawn(MagicItems.BootsOfStridingId, NearAnchor("GiantElixir_Pedestal", new Vector3(-2.2f, 0f, 0f)))) placed++;
            }
            return placed;
        }

        private static bool TrySpawn(string itemId, Vector3? spot)
        {
            if (spot == null || StoryFlags.Has(MagicItemPickup.FoundFlag(itemId))) return false;
            if (GameObject.Find("MagicItem_" + itemId) != null) return false;
            MagicItemPickup.Create(itemId, FindFreeSpot(spot.Value, 0.6f), 180f);
            return true;
        }

        private static Vector3? NearAnchor(string anchorName, Vector3 offset)
        {
            GameObject anchor = GameObject.Find(anchorName);
            if (anchor == null) return null;
            return anchor.transform.position + anchor.transform.rotation * offset;
        }

        /// <summary>Beside the Commander's reward chest, else where the Commander waited.</summary>
        private static Vector3? FindCommanderSpot()
        {
            DungeonRoomController[] rooms = UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rooms.Length; i++)
            {
                DungeonRoomController room = rooms[i];
                if (room == null || !string.Equals(room.bossIdentifier, CommanderId, StringComparison.OrdinalIgnoreCase)) continue;
                if (room.secretPassageOrChest != null) return room.secretPassageOrChest.transform.position + new Vector3(2.2f, 0f, 0f);
                if (room.roomEnemies != null)
                {
                    for (int e = 0; e < room.roomEnemies.Count; e++)
                    {
                        GameObject enemy = room.roomEnemies[e];
                        if (enemy != null && enemy.name.StartsWith("Boss_", StringComparison.OrdinalIgnoreCase)) return enemy.transform.position;
                    }
                }
                return room.transform.position;
            }
            return null;
        }

        /// <summary>The nearest spot to <paramref name="preferred"/> with nothing solid in the way, on the floor.</summary>
        public static Vector3 FindFreeSpot(Vector3 preferred, float radius, float maxSearch = 6f)
        {
            Physics.SyncTransforms();
            for (float r = 0f; r <= maxSearch; r += 0.75f)
            {
                int steps = r < 0.5f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r);
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Vector3 p = preferred + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float floorY = FloorY(p, preferred.y);
                    Vector3 center = new Vector3(p.x, floorY + 1.1f, p.z);
                    if (!Physics.CheckBox(center, new Vector3(radius, 0.8f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return new Vector3(p.x, floorY, p.z);
                    }
                }
            }
            return new Vector3(preferred.x, FloorY(preferred, preferred.y), preferred.z);
        }

        private static float FloorY(Vector3 p, float near)
        {
            // The floor is the highest surface below knee height around the anchor (roofs and canopies ignored)
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, near + 4f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                float y = hits[i].point.y;
                if (y < near + 0.8f && y > best) best = y;
            }
            return float.IsNegativeInfinity(best) ? near : best;
        }
    }
}
