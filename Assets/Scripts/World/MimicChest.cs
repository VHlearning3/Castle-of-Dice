using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// What looks like a treasure chest in the Tower corner (critical review C5). Opening it wakes the mimic:
    /// the prop chest disappears and the guardian's fight starts on the spot. Once the guardian is beaten
    /// the chest stays gone.
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

        private void OnEnable()
        {
            TurnManager.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDisable()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        // A lost fight puts the disguise back so the hero can try again
        private void HandleCombatEnded(bool victory)
        {
            if (victory || guardianRoom == null || guardianRoom.IsCleared) return;
            if (propChest != null) propChest.SetActive(true);
            isInteractable = true;
        }
    }
}
