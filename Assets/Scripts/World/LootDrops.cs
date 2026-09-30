using UnityEngine;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Spawns collectible coin and potion pickups from defeated enemies and opened chests,
    /// using the amounts in Resources/LootDropTable.
    /// </summary>
    public static class LootDrops
    {
        private const string TablePath = "LootDropTable";
        private static LootDropTableSO s_table;
        private static bool s_tableLoaded;

        public static LootDropTableSO Table
        {
            get
            {
                if (!s_tableLoaded)
                {
                    s_table = Resources.Load<LootDropTableSO>(TablePath);
                    s_tableLoaded = true;
                }
                return s_table;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            s_tableLoaded = false;
            EnemyUnit.OnLootDropped -= DropForEnemy;
            EnemyUnit.OnLootDropped += DropForEnemy;
        }

        /// <summary>Drops gold (and maybe a potion) where <paramref name="enemy"/> fell.</summary>
        public static void DropForEnemy(EnemyUnit enemy)
        {
            LootDropTableSO table = Table;
            // Village brawls are friendly bouts: nobody drops coins
            if (table == null || enemy == null || enemy is NpcBrawlerUnit) return;

            bool boss = enemy is CursedCommanderBoss || enemy is ShadowMageMalakorBoss || enemy is GargoyleKingBoss;
            int gold = boss ? table.bossGold : Random.Range(table.enemyGoldMin, table.enemyGoldMax + 1);
            float potionChance = boss ? table.bossPotionChance : table.enemyPotionChance;

            Vector3 origin = GroundBelow(enemy.transform.position);
            if (gold > 0) SpawnCoin(origin, gold, 0);
            if (Random.value < potionChance) SpawnPotion(origin, 1);
        }

        /// <summary>
        /// Pops the chest's gold out as a coin (and maybe a potion). Returns false when no loot table is set up,
        /// so the chest can pay out directly instead.
        /// </summary>
        public static bool DropForChest(ChestRewardInteraction chest, int gold)
        {
            LootDropTableSO table = Table;
            if (table == null || table.coinPrefab == null || chest == null) return false;

            // Pop out on the hero's side of the chest, clear of its lid
            Vector3 chestPos = chest.transform.position;
            PlayerUnit player = Object.FindAnyObjectByType<PlayerUnit>();
            Vector3 toPlayer = player != null ? player.transform.position - chestPos : chest.transform.forward;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
            Vector3 origin = GroundBelow(chestPos + toPlayer.normalized * 1.0f);

            if (gold > 0) SpawnCoin(origin, gold, 0);
            if (Random.value < table.chestPotionChance) SpawnPotion(origin, 1);
            return true;
        }

        public static WorldPickup SpawnCoin(Vector3 origin, int gold, int slot)
        {
            LootDropTableSO table = Table;
            return table != null ? Spawn(table.coinPrefab, origin, gold, null, slot) : null;
        }

        public static WorldPickup SpawnPotion(Vector3 origin, int slot)
        {
            LootDropTableSO table = Table;
            return table != null ? Spawn(table.potionPrefab, origin, 0, table.potionItem, slot) : null;
        }

        private static WorldPickup Spawn(GameObject prefab, Vector3 origin, int gold, ItemSO item, int slot)
        {
            if (prefab == null) return null;

            // Land a short hop away, coins and potions on different sides so they do not overlap
            float angle = (slot * 140f + Random.Range(-35f, 35f)) * Mathf.Deg2Rad;
            Vector3 landing = GroundBelow(origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 1.1f);

            GameObject go = Object.Instantiate(prefab, origin, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            WorldPickup pickup = go.GetComponent<WorldPickup>();
            if (pickup != null) pickup.InitializeDrop(gold, item, origin + Vector3.up * 0.8f, landing);
            return pickup;
        }

        private static Vector3 GroundBelow(Vector3 point)
        {
            Vector3 from = point + Vector3.up * 2f;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.GetComponentInParent<CombatUnit>() != null) continue;
                if (hits[i].point.y > best) best = hits[i].point.y;
            }
            return best > float.MinValue ? new Vector3(point.x, best, point.z) : point;
        }
    }
}
