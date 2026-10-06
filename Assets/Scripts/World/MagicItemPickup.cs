using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A worn magic item lying in the world (Vili, 2026-10-06): the Oathkeeper Shield in the Courtyard, the Shadow
    /// Cloak in the mage's secret room, the Boots of Striding in the Tower. Taking it puts it in the bag, worn,
    /// and tells the hero what it does. Built in code (low-poly primitives) by <see cref="MagicItemSpawner"/>.
    /// </summary>
    public class MagicItemPickup : Interactable
    {
        [SerializeField] private string itemId;

        private bool taken;

        /// <summary>Story flag set once an item has been found, so it never appears twice.</summary>
        public static string FoundFlag(string itemId) => "MagicItemFound:" + itemId;

        /// <summary>The item this pickup gives.</summary>
        public string ItemId => itemId;

        protected override void Awake()
        {
            base.Awake();
            interactionRadius = Mathf.Max(interactionRadius, 3f);
        }

        private void Start()
        {
            PickupBeacon.Attach(transform, PickupBeacon.TreasureColor, () => !taken);
        }

        private void Update()
        {
            // A slow turn so the item catches the eye
            if (!taken) transform.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
        }

        public override void Interact(PlayerUnit player)
        {
            if (taken) return;
            if (!Give(itemId)) return;
            taken = true;

            ItemSO item = InventoryManager.Instance != null ? InventoryManager.Instance.FindItemByID(itemId) : null;
            string name = item != null ? item.ItemName : "a magic item";
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.CriticalSuccess, transform.position);
            StoryPanelUI.Show(name, (item != null ? item.Description : string.Empty) +
                "\n\nYou are wearing it now. Open the bag (I) to take it off or put it back on.");
            Destroy(gameObject);
        }

        /// <summary>Puts the item in the bag (worn) and records it as found. False if it was already found.</summary>
        public static bool Give(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || StoryFlags.Has(FoundFlag(itemId))) return false;
            InventoryManager inventory = InventoryManager.Instance;
            ItemSO item = inventory != null ? inventory.FindItemByID(itemId) : null;
            if (item == null)
            {
                Debug.LogWarning($"[MagicItemPickup] Item '{itemId}' not found in Resources/Items.");
                return false;
            }

            StoryFlags.Set(FoundFlag(itemId));
            if (!inventory.HasItem(item, 1)) inventory.AddItem(item, 1);
            MagicItems.SetWorn(itemId, true);
            Debug.Log($"[MagicItemPickup] Found {item.ItemName}.");
            return true;
        }

        #region Creation

        private static Material litTemplate;

        /// <summary>Builds the pickup for <paramref name="itemId"/> standing at <paramref name="position"/>.</summary>
        public static MagicItemPickup Create(string itemId, Vector3 position, float yaw)
        {
            GameObject root = new GameObject("MagicItem_" + itemId);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            switch (itemId)
            {
                case MagicItems.OathkeeperShieldId: BuildShield(root.transform); break;
                case MagicItems.ShadowCloakId: BuildCloak(root.transform); break;
                default: BuildBoots(root.transform); break;
            }

            BoxCollider click = root.AddComponent<BoxCollider>();
            click.center = new Vector3(0f, 0.7f, 0f);
            click.size = new Vector3(1.2f, 1.4f, 1.2f);

            GameObject glowObj = new GameObject("Item_Glow");
            glowObj.transform.SetParent(root.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.5f);
            glow.range = 3.5f;
            glow.intensity = 2f;

            MagicItemPickup pickup = root.AddComponent<MagicItemPickup>();
            pickup.itemId = itemId;
            ItemSO item = InventoryManager.Instance != null ? InventoryManager.Instance.FindItemByID(itemId) : null;
            pickup.promptMessage = "Take: " + (item != null ? item.ItemName : "magic item");
            return pickup;
        }

        private static void BuildShield(Transform root)
        {
            Material steel = Mat("Oathkeeper_Steel", new Color(0.34f, 0.42f, 0.58f));
            Material gold = Mat("Oathkeeper_Gold", new Color(0.86f, 0.68f, 0.24f));
            // A round shield standing on its rim, leaning back a little, its face toward +Z
            Quaternion lean = Quaternion.Euler(78f, 0f, 0f);
            Part(root, PrimitiveType.Cylinder, "Shield_Rim", new Vector3(0f, 0.6f, -0.02f), new Vector3(1.04f, 0.035f, 1.04f), lean, gold);
            Part(root, PrimitiveType.Cylinder, "Shield_Face", new Vector3(0f, 0.6f, 0f), new Vector3(0.95f, 0.05f, 0.95f), lean, steel);
            Part(root, PrimitiveType.Cube, "Shield_Band", new Vector3(0f, 0.6f, 0.035f), new Vector3(0.12f, 0.9f, 0.03f), Quaternion.Euler(-12f, 0f, 0f), gold);
            Part(root, PrimitiveType.Sphere, "Shield_Boss", new Vector3(0f, 0.61f, 0.07f), new Vector3(0.26f, 0.26f, 0.14f), Quaternion.Euler(-12f, 0f, 0f), gold);
        }

        private static void BuildCloak(Transform root)
        {
            Material cloth = Mat("ShadowCloak_Cloth", new Color(0.16f, 0.10f, 0.26f));
            Material lining = Mat("ShadowCloak_Lining", new Color(0.32f, 0.18f, 0.45f));
            Material silver = Mat("ShadowCloak_Clasp", new Color(0.78f, 0.80f, 0.86f));
            Material wood = Mat("ShadowCloak_Stand", new Color(0.30f, 0.20f, 0.12f));
            Part(root, PrimitiveType.Cylinder, "Stand_Pole", new Vector3(0f, 0.8f, -0.1f), new Vector3(0.07f, 0.8f, 0.07f), Quaternion.identity, wood);
            Part(root, PrimitiveType.Cylinder, "Stand_Foot", new Vector3(0f, 0.03f, -0.1f), new Vector3(0.5f, 0.03f, 0.5f), Quaternion.identity, wood);
            Part(root, PrimitiveType.Cube, "Cloak_Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.78f, 1.15f, 0.1f), Quaternion.Euler(4f, 0f, 0f), cloth);
            Part(root, PrimitiveType.Cube, "Cloak_Lining", new Vector3(0f, 0.85f, -0.06f), new Vector3(0.7f, 1.05f, 0.04f), Quaternion.Euler(4f, 0f, 0f), lining);
            Part(root, PrimitiveType.Sphere, "Cloak_Hood", new Vector3(0f, 1.52f, -0.04f), new Vector3(0.42f, 0.36f, 0.34f), Quaternion.identity, cloth);
            Part(root, PrimitiveType.Sphere, "Cloak_Clasp", new Vector3(0f, 1.33f, 0.07f), new Vector3(0.1f, 0.1f, 0.06f), Quaternion.identity, silver);
        }

        private static void BuildBoots(Transform root)
        {
            Material leather = Mat("Boots_Leather", new Color(0.38f, 0.24f, 0.13f));
            Material gold = Mat("Boots_Gold", new Color(0.86f, 0.68f, 0.24f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.18f;
                Part(root, PrimitiveType.Cube, "Boot_Shaft", new Vector3(x, 0.3f, 0f), new Vector3(0.2f, 0.46f, 0.22f), Quaternion.identity, leather);
                Part(root, PrimitiveType.Cube, "Boot_Foot", new Vector3(x, 0.07f, 0.12f), new Vector3(0.22f, 0.14f, 0.42f), Quaternion.identity, leather);
                Part(root, PrimitiveType.Cube, "Boot_Band", new Vector3(x, 0.5f, 0f), new Vector3(0.23f, 0.05f, 0.25f), Quaternion.identity, gold);
                Part(root, PrimitiveType.Cube, "Boot_Wing", new Vector3(x + side * 0.13f, 0.42f, -0.02f), new Vector3(0.04f, 0.16f, 0.2f), Quaternion.Euler(0f, 0f, side * 25f), gold);
            }
        }

        private static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Quaternion rotation, Material mat)
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
            return part.transform;
        }

        private static Material Mat(string name, Color color)
        {
            if (litTemplate == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) return null;
                litTemplate = new Material(shader);
            }
            Material mat = new Material(litTemplate) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
            return mat;
        }

        #endregion
    }
}
