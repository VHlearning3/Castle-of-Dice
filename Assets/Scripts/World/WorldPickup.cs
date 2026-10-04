using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A gold coin, healing potion or scrap pile that drops from a defeated enemy or an opened chest (see <see cref="LootDrops"/>)
    /// or lies in the field.
    /// It pops out in a short arc, spins and bobs, drifts to the hero once they are close and is collected on
    /// touch. Pickups placed by hand in a scene are remembered as taken (GameManager rewards); dropped ones are not.
    /// </summary>
    [SelectionBase]
    public class WorldPickup : Interactable
    {
        public enum PickupKind { Gold, Item, Scrap }

        [Header("Reward")]
        [SerializeField] private PickupKind kind = PickupKind.Gold;
        [Min(0)]
        [SerializeField] private int goldAmount = 10;
        [SerializeField] private ItemSO item;
        [Tooltip("Scrap metal a scrap pile is worth.")]
        [Min(0)]
        [SerializeField] private int scrapAmount = 3;

        [Header("Presentation")]
        [Tooltip("Child that spins and bobs (the model).")]
        [SerializeField] private Transform visual;
        [SerializeField] private float spinDegreesPerSecond = 90f;
        [SerializeField] private float bobHeight = 0.12f;

        [Header("Collection")]
        [Tooltip("The hero collects the pickup on reaching this distance.")]
        [SerializeField] private float autoCollectRadius = 1.3f;
        [Tooltip("Within this distance the pickup drifts toward the hero.")]
        [SerializeField] private float magnetRadius = 3.5f;
        [SerializeField] private float magnetSpeed = 7f;

        private const float LaunchDuration = 0.55f;
        private const float LaunchHeight = 1.4f;

        private static PlayerUnit s_player;
        private static AudioClip s_pickupClip;

        private Vector3 visualBasePosition;
        private float bobPhase;
        private bool collected;
        private bool persistent = true;
        private float launchTimer = -1f;
        private Vector3 launchFrom;
        private Vector3 launchTo;

        public PickupKind Kind => kind;
        public int GoldAmount => goldAmount;
        public int ScrapAmount => scrapAmount;

        protected override void Awake()
        {
            base.Awake();
            if (visual != null) visualBasePosition = visual.localPosition;
            bobPhase = Random.value * Mathf.PI * 2f;

            if (persistent && GameManager.Instance != null && GameManager.Instance.IsRewardClaimed(GameManager.RewardKey(this)))
            {
                collected = true;
                gameObject.SetActive(false);
            }
        }

        /// <summary>Sets up a dropped pickup: its reward, and an arc from <paramref name="from"/> to where it lands.</summary>
        public void InitializeDrop(int gold, ItemSO droppedItem, Vector3 from, Vector3 landing)
        {
            persistent = false;
            goldAmount = gold;
            if (droppedItem != null) item = droppedItem;
            launchFrom = from;
            launchTo = landing;
            launchTimer = 0f;
            transform.position = from;
        }

        private void Update()
        {
            if (collected) return;

            if (visual != null)
            {
                visual.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
                Vector3 p = visualBasePosition;
                p.y += (Mathf.Sin(Time.time * 2.2f + bobPhase) + 1f) * 0.5f * bobHeight;
                visual.localPosition = p;
            }

            if (launchTimer >= 0f)
            {
                launchTimer += Time.deltaTime;
                float t = Mathf.Clamp01(launchTimer / LaunchDuration);
                Vector3 pos = Vector3.Lerp(launchFrom, launchTo, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * LaunchHeight;
                transform.position = pos;
                if (t < 1f) return;
                launchTimer = -1f;
            }

            if (s_player == null) s_player = FindAnyObjectByType<PlayerUnit>();
            if (s_player == null || !s_player.IsAlive) return;

            Vector3 toPlayer = s_player.transform.position - transform.position;
            toPlayer.y = 0f;
            float sqrDistance = toPlayer.sqrMagnitude;
            if (sqrDistance <= autoCollectRadius * autoCollectRadius)
            {
                TriggerInteraction(s_player);
            }
            else if (sqrDistance <= magnetRadius * magnetRadius)
            {
                transform.position += toPlayer.normalized * (magnetSpeed * Time.deltaTime);
            }
        }

        public override void Interact(PlayerUnit player)
        {
            if (collected) return;
            collected = true;
            if (persistent) GameManager.Instance?.MarkRewardClaimed(GameManager.RewardKey(this));

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                if (kind == PickupKind.Gold && goldAmount > 0) inventory.AddGold(goldAmount);
                if (kind == PickupKind.Item && item != null) inventory.AddItem(item, 1);
                if (kind == PickupKind.Scrap && scrapAmount > 0) inventory.AddScrapMetal(scrapAmount);
            }
            else
            {
                Debug.LogWarning($"[WorldPickup] No InventoryManager: '{name}' reward lost.");
            }

            if (SFXManager.Instance != null)
            {
                if (s_pickupClip == null) s_pickupClip = SynthesizePickupChime();
                SFXManager.Instance.PlaySFX(s_pickupClip, transform.position, kind == PickupKind.Gold ? 0.8f : 0.7f, randomizePitch: true);
            }

            Debug.Log($"[WorldPickup] Picked up {(kind == PickupKind.Gold ? goldAmount + " Gold" : kind == PickupKind.Scrap ? scrapAmount + " Scrap Metal" : item != null ? item.ItemName : "item")}.");
            if (persistent) gameObject.SetActive(false);
            else Destroy(gameObject);
        }

        /// <summary>Two bright rising bell tones, synthesized once (no audio file to ship).</summary>
        private static AudioClip SynthesizePickupChime()
        {
            const int sampleRate = 22050;
            const float duration = 0.35f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float note = t < 0.09f ? 1318.5f : 1975.5f; // E6 then B6
                float local = t < 0.09f ? t : t - 0.09f;
                float env = Mathf.Min(1f, local * 200f) * Mathf.Exp(-local * 11f);
                data[i] = env * 0.45f * (Mathf.Sin(2f * Mathf.PI * note * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * note * t));
            }
            AudioClip clip = AudioClip.Create("SFX_PickupChime_Synth", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
