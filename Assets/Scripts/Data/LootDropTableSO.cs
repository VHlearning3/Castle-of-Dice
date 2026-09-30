using UnityEngine;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// What defeated enemies and opened chests drop as collectible coins and potions, and how much.
    /// Loaded from Resources/LootDropTable; tune the numbers here.
    /// </summary>
    [CreateAssetMenu(fileName = "LootDropTable", menuName = "CastleOfDice/Data/Loot Drop Table", order = 20)]
    public class LootDropTableSO : ScriptableObject
    {
        [Header("Pickup Prefabs")]
        public GameObject coinPrefab;
        public GameObject potionPrefab;
        public ItemSO potionItem;

        [Header("Regular Enemies")]
        [Min(0)] public int enemyGoldMin = 3;
        [Min(0)] public int enemyGoldMax = 8;
        [Range(0f, 1f)] public float enemyPotionChance = 0.25f;

        [Header("Bosses")]
        [Min(0)] public int bossGold = 25;
        [Range(0f, 1f)] public float bossPotionChance = 1f;

        [Header("Chests")]
        [Tooltip("Chest gold comes out as a coin worth the chest's own gold reward.")]
        [Range(0f, 1f)] public float chestPotionChance = 0.5f;
    }
}
