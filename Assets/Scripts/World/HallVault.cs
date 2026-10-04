using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The Queen's treasury vault in the Castle Hall (critical review C4). Its door stays shut until the ghost
    /// of Sir Aldric hears the right answer to his riddle; then the door vanishes and the reward chest appears.
    /// Remembered in the save (story flag), so an opened vault stays open.
    /// </summary>
    public class HallVault : MonoBehaviour
    {
        [Tooltip("Blocking door / bars removed when the vault opens.")]
        [SerializeField] private GameObject door;

        [Tooltip("Reward chest shown when the vault opens.")]
        [SerializeField] private GameObject rewardChest;

        public GameObject Door
        {
            get => door;
            set => door = value;
        }

        public GameObject RewardChest
        {
            get => rewardChest;
            set => rewardChest = value;
        }

        /// <summary>Whether the vault is open.</summary>
        public bool IsOpen => StoryFlags.Has(ScriptedNpcFlags.VaultOpened);

        private void Start()
        {
            Apply();
        }

        /// <summary>Opens the vault in the loaded scene (the ghost's riddle action).</summary>
        public static void OpenInScene()
        {
            StoryFlags.Set(ScriptedNpcFlags.VaultOpened);
            HallVault vault = FindAnyObjectByType<HallVault>(FindObjectsInactive.Include);
            if (vault != null) vault.Apply();
        }

        private void Apply()
        {
            bool open = IsOpen;
            if (door != null) door.SetActive(!open);
            if (rewardChest != null) rewardChest.SetActive(open);
        }
    }

    /// <summary>Flag names shared by the hub characters.</summary>
    public static class ScriptedNpcFlags
    {
        public const string VaultOpened = HubDialogues.VaultOpenedFlag;
    }
}
