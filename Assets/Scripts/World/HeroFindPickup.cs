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
    /// Something only one hero notices (the tavern finds, Vili 2026-10-10): taking it puts an item in the bag, tells the
    /// little story of where it was, and sets <see cref="FoundFlag"/> so it never comes back. Made by <see cref="TavernHeroFinds"/>.
    /// </summary>
    public class HeroFindPickup : Interactable
    {
        [SerializeField] private string findId;
        [SerializeField] private string itemId;
        [SerializeField] private CharacterClassType heroClass;
        [SerializeField] private string title;
        [TextArea]
        [SerializeField] private string body;

        private bool taken;

        public string FindId => findId;
        public string ItemId => itemId;
        public CharacterClassType HeroClass => heroClass;

        /// <summary>Story flag set once the find has been taken.</summary>
        public static string FoundFlag(string findId) => "HeroFind:" + findId;

        public void Configure(string id, string item, CharacterClassType cls, string prompt, string storyTitle, string storyBody)
        {
            findId = id;
            itemId = item;
            heroClass = cls;
            promptMessage = prompt;
            title = storyTitle;
            body = storyBody;
            interactionRadius = 2.8f;
        }

        private void Start()
        {
            PickupBeacon.Attach(transform, PickupBeacon.TreasureColor, () => !taken, 0.8f);
        }

        public override bool CanInteract(PlayerUnit player)
        {
            return !taken && IsFor(player) && base.CanInteract(player);
        }

        private bool IsFor(PlayerUnit player)
        {
            return player != null && player.CharacterClass != null && player.CharacterClass.ClassType == heroClass;
        }

        public override void Interact(PlayerUnit player)
        {
            if (taken || !IsFor(player) || StoryFlags.Has(FoundFlag(findId))) return;
            InventoryManager inventory = InventoryManager.Instance;
            ItemSO item = inventory != null ? inventory.FindItemByID(itemId) : null;
            if (item == null)
            {
                Debug.LogWarning($"[HeroFindPickup] Item '{itemId}' not found; '{findId}' stays where it is.");
                return;
            }

            taken = true;
            StoryFlags.Set(FoundFlag(findId));
            inventory.AddItem(item, 1);
            Debug.Log($"[HeroFindPickup] {player.UnitName} found {item.ItemName} ({findId}).");
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.CriticalSuccess, transform.position);
            StoryPanelUI.Show(title, body + "\n\n" + item.ItemName + " added to your bag.");
            Destroy(gameObject);
        }
    }
}
