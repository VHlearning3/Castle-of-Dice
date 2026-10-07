using System.Collections;
using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The Queen's treasury vault in the Castle Hall (critical review C4). Its door stays shut until the ghost
    /// of Sir Aldric hears the right answer to his riddle; then the iron door swings open (or, with no leaf set,
    /// the blocker simply vanishes) and the reward chest appears.
    /// Remembered in the save (story flag), so an opened vault stays open.
    /// </summary>
    public class HallVault : MonoBehaviour
    {
        [Tooltip("Blocking door / bars removed when the vault opens.")]
        [SerializeField] private GameObject door;

        [Tooltip("Reward chest shown when the vault opens.")]
        [SerializeField] private GameObject rewardChest;

        [Tooltip("Iron door leaf, pivoting on its hinge; swings by Open Angle about Y when the vault opens.")]
        [SerializeField] private Transform doorLeaf;

        [Tooltip("Degrees the leaf turns about its hinge to stand open (sign picks the side it swings to).")]
        [SerializeField] private float openAngle = 100f;

        [Tooltip("Seconds the door takes to swing open.")]
        [SerializeField] private float swingTime = 1.6f;

        private Quaternion closedRotation;
        private bool closedCaptured;
        private Coroutine swing;

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

        public Transform DoorLeaf
        {
            get => doorLeaf;
            set => doorLeaf = value;
        }

        public float OpenAngle
        {
            get => openAngle;
            set => openAngle = value;
        }

        /// <summary>Whether the vault is open.</summary>
        public bool IsOpen => StoryFlags.Has(ScriptedNpcFlags.VaultOpened);

        private void Start()
        {
            Apply(false);
        }

        /// <summary>Opens the vault in the loaded scene (the ghost's riddle action).</summary>
        public static void OpenInScene()
        {
            StoryFlags.Set(ScriptedNpcFlags.VaultOpened);
            HallVault vault = FindAnyObjectByType<HallVault>(FindObjectsInactive.Include);
            if (vault != null) vault.Apply(true);
        }

        private void Apply(bool animate)
        {
            bool open = IsOpen;
            if (door != null) door.SetActive(!open);
            if (rewardChest != null) rewardChest.SetActive(open);
            if (doorLeaf == null) return;

            if (!closedCaptured)
            {
                closedRotation = doorLeaf.localRotation;
                closedCaptured = true;
            }
            Quaternion target = open ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;
            if (swing != null) StopCoroutine(swing);
            if (animate && open && isActiveAndEnabled)
            {
                if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(SFXClipType.DoorOpen, doorLeaf.position, 0.9f);
                swing = StartCoroutine(Swing(target));
            }
            else
            {
                doorLeaf.localRotation = target;
            }
        }

        private IEnumerator Swing(Quaternion target)
        {
            Quaternion from = doorLeaf.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, swingTime);
                // Heavy iron: slow to start, settles at the end
                float e = Mathf.SmoothStep(0f, 1f, t);
                doorLeaf.localRotation = Quaternion.Slerp(from, target, e);
                yield return null;
            }
            doorLeaf.localRotation = target;
            swing = null;
        }
    }

    /// <summary>Flag names shared by the hub characters.</summary>
    public static class ScriptedNpcFlags
    {
        public const string VaultOpened = HubDialogues.VaultOpenedFlag;
    }
}
