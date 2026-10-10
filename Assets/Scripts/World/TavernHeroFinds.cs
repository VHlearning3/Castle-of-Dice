using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Every hero has one thing of their own to find in The Foaming Tankard (Vili, 2026-10-10), placed at runtime so
    /// the hand-edited Zone_Tavern scene stays untouched:
    /// - a locked chest by the north wall under the gallery that only the rogue can pick (30 gold);
    /// - a Scroll of Embers beside the bookshelf that only the mage notices;
    /// - a Greater Health Potion on the bar counter that only the knight notices.
    /// Each is taken once per adventure. An empty named like <see cref="RogueChestAnchor"/> in the scene moves its find.
    /// </summary>
    public static class TavernHeroFinds
    {
        public const string RogueChestName = "Tavern_Rogue_Chest";
        public const string MageScrollId = "tavern_mage_scroll";
        public const string WarriorPotionId = "tavern_warrior_potion";

        public const string RogueChestAnchor = "TavernFind_RogueChest";
        public const string MageScrollAnchor = "TavernFind_MageScroll";
        public const string WarriorPotionAnchor = "TavernFind_WarriorPotion";

        public const string ScrollItemId = "scroll_embers";
        public const string PotionItemId = "item_greater_potion";
        public const int RogueChestGold = 30;
        public const string ChestPrefabPath = "Tavern/Tavern_Chest";

        // Default spots in the tavern (Unity coordinates of Tavern_Interior)
        public static readonly Vector3 RogueChestSpot = new Vector3(0.6f, 0f, 8.0f);   // north wall, between the crates and the gallery post
        public const float RogueChestYaw = 0f;                                         // keyhole towards the room (the model faces -z)
        public static readonly Vector3 MageScrollSpot = new Vector3(11.05f, 0f, 6.35f); // floor at the north end of the bookshelf
        public static readonly Vector3 WarriorPotionSpot = new Vector3(-7.05f, 1.72f, 1.65f); // bar counter, between the bottle and the mug

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != TavernEntrance.SceneName) return;
            Spawn(CurrentHeroClass());
        }

        /// <summary>The hero of this adventure (null before one is chosen).</summary>
        public static CharacterClassType? CurrentHeroClass()
        {
            CharacterClassSO cls = PlayerDataSO.Session != null ? PlayerDataSO.Session.SelectedClass : null;
            return cls != null ? cls.ClassType : (CharacterClassType?)null;
        }

        /// <summary>Places the rogue's chest and this hero's own find. Returns how many objects were placed.</summary>
        public static int Spawn(CharacterClassType? hero)
        {
            if (!Application.isPlaying) return 0;
            int placed = 0;
            if (SpawnRogueChest() != null) placed++;
            if (hero == CharacterClassType.Mage && SpawnMageScroll() != null) placed++;
            if (hero == CharacterClassType.Warrior && SpawnWarriorPotion() != null) placed++;
            return placed;
        }

        /// <summary>The locked chest: seen by everyone, picked only by the rogue; stays open once picked.</summary>
        public static LockpickInteraction SpawnRogueChest()
        {
            GameObject existing = GameObject.Find(RogueChestName);
            if (existing != null) return existing.GetComponent<LockpickInteraction>();
            GameObject prefab = Resources.Load<GameObject>(ChestPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[TavernHeroFinds] Chest prefab missing: Resources/" + ChestPrefabPath);
                return null;
            }

            Vector3 spot;
            Quaternion rot;
            if (!Anchor(RogueChestAnchor, out spot, out rot))
            {
                spot = MagicItemSpawner.FindFreeSpot(RogueChestSpot, 0.55f, 2.5f);
                rot = Quaternion.Euler(0f, RogueChestYaw, 0f);
            }

            // Built while inactive, so the lock's Awake sees the final name (its saved-reward key) and the lid
            var holder = new GameObject("TavernFinds_Build");
            holder.SetActive(false);
            GameObject chest = Object.Instantiate(prefab, spot, rot, holder.transform);
            chest.name = RogueChestName;
            Transform lid = chest.transform.Find("Visual/Chest_Lid");
            LockpickInteraction lockpick = chest.AddComponent<LockpickInteraction>();
            lockpick.ConfigureChest(13, RogueChestGold, lid, new Vector3(70f, 0f, 0f), false);
            chest.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(chest, SceneManager.GetActiveScene());
            Object.Destroy(holder);
            chest.SetActive(true);
            return lockpick;
        }

        public static HeroFindPickup SpawnMageScroll()
        {
            if (StoryFlags.Has(HeroFindPickup.FoundFlag(MageScrollId)) || GameObject.Find("HeroFind_" + MageScrollId) != null) return null;
            Vector3 spot;
            Quaternion rot;
            if (!Anchor(MageScrollAnchor, out spot, out rot)) { spot = MageScrollSpot; rot = Quaternion.Euler(0f, 20f, 0f); }

            GameObject root = Root(MageScrollId, spot, rot);
            Material parchment = Mat("TavernFind_Parchment", new Color(0.93f, 0.85f, 0.66f));
            Material ribbon = Mat("TavernFind_EmberRibbon", new Color(0.85f, 0.22f, 0.08f), new Color(1f, 0.35f, 0.08f) * 1.6f);
            Material seal = Mat("TavernFind_Seal", new Color(0.62f, 0.08f, 0.06f));
            // A rolled scroll lying on the floorboards, tied with a glowing ember-red ribbon
            Quaternion lying = Quaternion.Euler(0f, 0f, 90f);
            Part(root.transform, PrimitiveType.Cylinder, "Scroll_Roll", new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.42f, 0.3f), lying, parchment);
            Part(root.transform, PrimitiveType.Cylinder, "Scroll_Ribbon", new Vector3(0f, 0.16f, 0f), new Vector3(0.32f, 0.05f, 0.32f), lying, ribbon);
            Part(root.transform, PrimitiveType.Sphere, "Scroll_Seal", new Vector3(0f, 0.16f, 0.16f), new Vector3(0.12f, 0.12f, 0.06f), Quaternion.identity, seal);
            AddGlow(root.transform, new Vector3(0f, 0.6f, 0f), new Color(1f, 0.45f, 0.15f), 1.6f, 2.5f);
            Collider(root, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.7f));

            HeroFindPickup pickup = root.AddComponent<HeroFindPickup>();
            pickup.Configure(MageScrollId, ScrollItemId, CharacterClassType.Mage, "Take: Scroll of Embers",
                "A Scroll Among the Almanacs",
                "Wedged behind the bookshelf, between a farmer's almanac and a cookbook, lies a scroll that is warm to the touch. " +
                "Its seal is written in a fire-mage's shorthand. To anyone else it would look like a recipe for soup.");
            return pickup;
        }

        public static HeroFindPickup SpawnWarriorPotion()
        {
            if (StoryFlags.Has(HeroFindPickup.FoundFlag(WarriorPotionId)) || GameObject.Find("HeroFind_" + WarriorPotionId) != null) return null;
            Vector3 spot;
            Quaternion rot;
            if (!Anchor(WarriorPotionAnchor, out spot, out rot)) { spot = WarriorPotionSpot; rot = Quaternion.identity; }

            GameObject root = Root(WarriorPotionId, spot, rot);
            Material glass = Mat("TavernFind_PotionRed", new Color(0.78f, 0.06f, 0.08f), new Color(0.9f, 0.08f, 0.1f) * 0.9f);
            Material cork = Mat("TavernFind_Cork", new Color(0.55f, 0.38f, 0.22f));
            Material tag = Mat("TavernFind_Tag", new Color(0.92f, 0.86f, 0.7f));
            // A big round-bellied flask, larger than the mugs beside it
            Part(root.transform, PrimitiveType.Sphere, "Potion_Belly", new Vector3(0f, 0.26f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, glass);
            Part(root.transform, PrimitiveType.Cylinder, "Potion_Neck", new Vector3(0f, 0.6f, 0f), new Vector3(0.16f, 0.14f, 0.16f), Quaternion.identity, glass);
            Part(root.transform, PrimitiveType.Cylinder, "Potion_Cork", new Vector3(0f, 0.77f, 0f), new Vector3(0.18f, 0.06f, 0.18f), Quaternion.identity, cork);
            Part(root.transform, PrimitiveType.Cube, "Potion_Tag", new Vector3(0.12f, 0.5f, -0.2f), new Vector3(0.16f, 0.1f, 0.01f), Quaternion.Euler(0f, 30f, -15f), tag);
            AddGlow(root.transform, new Vector3(0f, 0.9f, -0.3f), new Color(1f, 0.3f, 0.3f), 1.4f, 2.5f);
            Collider(root, new Vector3(0f, 0.4f, 0f), new Vector3(0.7f, 0.8f, 0.7f));

            HeroFindPickup pickup = root.AddComponent<HeroFindPickup>();
            pickup.Configure(WarriorPotionId, PotionItemId, CharacterClassType.Warrior, "Take: Greater Health Potion",
                "For the Brave",
                "Behind the mugs stands a stout red flask with a tag tied to its neck: \"For whoever carries the shield next. " +
                "Drink it before you need it, not after. An old soldier.\"");
            return pickup;
        }

        private static bool Anchor(string name, out Vector3 position, out Quaternion rotation)
        {
            GameObject anchor = GameObject.Find(name);
            position = anchor != null ? anchor.transform.position : Vector3.zero;
            rotation = anchor != null ? Quaternion.Euler(0f, anchor.transform.eulerAngles.y, 0f) : Quaternion.identity;
            return anchor != null;
        }

        private static GameObject Root(string id, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("HeroFind_" + id);
            root.transform.SetPositionAndRotation(position, rotation);
            return root;
        }

        private static void Collider(GameObject root, Vector3 center, Vector3 size)
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static void AddGlow(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("Find_Glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private static void Part(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Quaternion rotation, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Collider col = part.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            Renderer r = part.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        private static Material litTemplate;

        private static Material Mat(string name, Color color, Color? emission = null)
        {
            if (litTemplate == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) return null;
                litTemplate = new Material(shader);
            }
            var mat = new Material(litTemplate) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            return mat;
        }
    }
}
