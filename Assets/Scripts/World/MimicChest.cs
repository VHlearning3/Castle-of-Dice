using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// What looks like a treasure chest in the Tower (critical review C5). Opening it wakes the mimic: the prop
    /// chest disappears and the guardian's fight starts on the spot. Once the guardian is beaten the chest stays
    /// gone. The fight is optional and has nothing to do with the rune puzzle (Vili 2026-10-10).
    /// </summary>
    public class MimicChest : Interactable
    {
        [SerializeField] private DungeonRoomController guardianRoom;
        [SerializeField] private GameObject propChest;

        public DungeonRoomController GuardianRoom
        {
            get => guardianRoom;
            set => guardianRoom = value;
        }

        public GameObject PropChest
        {
            get => propChest;
            set => propChest = value;
        }

        protected override void Awake()
        {
            base.Awake();
            promptMessage = "Open Chest";
            interactionRadius = Mathf.Max(interactionRadius, 3f);

            // The fight happens where the chest stands, wherever it is moved in the editor. Left behind, the
            // room's walk-in trigger would start the fight on an empty spot of floor.
            if (guardianRoom != null)
            {
                Vector3 room = guardianRoom.transform.position;
                guardianRoom.transform.position = new Vector3(transform.position.x, room.y, transform.position.z);
            }

            // Glows like any other chest, so the disguise holds
            PickupBeacon.Attach(transform, PickupBeacon.TreasureColor, () => isInteractable);

            // Subscribed for the chest's whole life: the prop chest is often this very object, hidden during the fight
            TurnManager.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDestroy()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        private void Start()
        {
            if (StoryFlags.Has(TowerChallenge.GuardianDefeatedFlag))
            {
                if (propChest != null) propChest.SetActive(false);
                isInteractable = false;
            }
        }

        public override void Interact(PlayerUnit player)
        {
            if (guardianRoom == null || guardianRoom.IsCleared) return;

            UI.LockpickMinigameUI.ShowToast("The chest's lid splits into teeth. It's a MIMIC!");
            if (propChest != null) propChest.SetActive(false);
            isInteractable = false;
            guardianRoom.BeginEncounter(player);
        }

        // A lost fight puts the disguise back so the hero can try again
        private void HandleCombatEnded(bool victory)
        {
            if (this == null || isInteractable) return;
            if (victory || guardianRoom == null || guardianRoom.IsCleared) return;
            if (propChest != null) propChest.SetActive(true);
            isInteractable = true;
        }
    }
}
