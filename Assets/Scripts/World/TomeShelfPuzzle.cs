
using System.Collections;
using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The book-ordering puzzle on its own (no Unity objects), so it can be tested: five tomes, five slots,
    /// one right order. Tomes are numbered in that right order (0 = Sun ... 4 = Dark).
    /// </summary>
    public sealed class TomeOrderPuzzle
    {
        /// <summary>Spine titles, in the order the Witch kept them.</summary>
        public static readonly string[] Titles =
        {
            "Of the Rising Sun",
            "Of the Watching Moon",
            "Of the Guiding Star",
            "Of the Mountain's Bone",
            "Of the Waiting Dark"
        };

        /// <summary>The riddle carved on the shelf.</summary>
        public const string Riddle =
            "Dawn wakes first, and the pale watcher follows.\n" +
            "Then comes the light that guides the lost,\n" +
            "the mountain's bone stands after,\n" +
            "and the dark waits at the end of every shelf.";

        private readonly int[] slots;

        public TomeOrderPuzzle()
        {
            slots = new int[Titles.Length];
            Clear();
        }

        public int SlotCount => slots.Length;

        /// <summary>The tome in a slot, or -1 when it is empty.</summary>
        public int TomeIn(int slot) => slot >= 0 && slot < slots.Length ? slots[slot] : -1;

        /// <summary>The slot holding a tome, or -1 when it is not on the shelf.</summary>
        public int SlotOf(int tome)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == tome) return i;
            }
            return -1;
        }

        /// <summary>Puts a tome in a slot (moving it if it was elsewhere; a tome already there goes back to the table).</summary>
        public void Place(int tome, int slot)
        {
            if (tome < 0 || tome >= Titles.Length || slot < 0 || slot >= slots.Length) return;
            int from = SlotOf(tome);
            if (from >= 0) slots[from] = -1;
            slots[slot] = tome;
        }

        /// <summary>Takes the tome out of a slot.</summary>
        public void Remove(int slot)
        {
            if (slot >= 0 && slot < slots.Length) slots[slot] = -1;
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = -1;
        }

        /// <summary>Every slot holds a tome.</summary>
        public bool IsFull
        {
            get
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] < 0) return false;
                }
                return true;
            }
        }

        /// <summary>Every tome sits in its own slot.</summary>
        public bool IsSolved
        {
            get
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != i) return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// The mage's own quest (Vili, 2026-10-06): a warded lectern in front of two great bookshelves in the
    /// Library. Only the mage can read the ward; doing so opens the book-ordering minigame. Put the Witch's
    /// tomes back in order and the shelves slide apart on a secret room with the reward chest. The open state
    /// is a story flag, so the room stays open on every later visit.
    /// </summary>
    public class TomeShelfPuzzle : Interactable
    {
        /// <summary>Story flag set once the shelves have opened.</summary>
        public const string OpenedFlag = "LibraryShelvesOpened";

        /// <summary>Shown to the knight and the rogue.</summary>
        public const string WardMessage = "A ward hums over the shelves.\nOnly a scholar could read it.";

        [Tooltip("The two great bookshelves covering the secret doorway.")]
        [SerializeField] private Transform leftShelf;
        [SerializeField] private Transform rightShelf;

        [Tooltip("How far each shelf slides sideways when the puzzle is solved.")]
        [SerializeField] private float slideDistance = 4.6f;

        [Tooltip("Roof over the secret room (keeps it dark from above until it opens).")]
        [SerializeField] private GameObject roof;

        private bool opened;
        private Vector3 leftClosed, rightClosed;

        public Transform LeftShelf { get => leftShelf; set => leftShelf = value; }
        public Transform RightShelf { get => rightShelf; set => rightShelf = value; }
        public GameObject Roof { get => roof; set => roof = value; }
        public float SlideDistance { get => slideDistance; set => slideDistance = value; }

        /// <summary>Whether the shelves have slid open.</summary>
        public bool IsOpened => opened;

        protected override void Awake()
        {
            base.Awake();
            interactionRadius = Mathf.Max(interactionRadius, 3f);
            if (leftShelf != null) leftClosed = leftShelf.localPosition;
            if (rightShelf != null) rightClosed = rightShelf.localPosition;
            if (StoryFlags.Has(OpenedFlag)) SetOpened(1f);
            else promptMessage = "Study the warded shelves";
        }

        public override bool CanInteract(PlayerUnit player)
        {
            return !opened && base.CanInteract(player);
        }

        public override void Interact(PlayerUnit player)
        {
            if (opened) return;
            if (!IsScholar(player))
            {
                LockpickMinigameUI.ShowToast(WardMessage);
                return;
            }
            TomeShelfMinigameUI.Open(solved =>
            {
                if (solved) Solve();
            });
        }

        /// <summary>Only the mage can read the ward.</summary>
        public static bool IsScholar(PlayerUnit player)
        {
            return player != null && player.CharacterClass != null && player.CharacterClass.ClassType == CharacterClassType.Mage;
        }

        /// <summary>Opens the shelves (the puzzle was solved).</summary>
        public void Solve()
        {
            if (opened) return;
            StoryFlags.Set(OpenedFlag);
            SFXManager.Instance?.PlaySFX(SFXClipType.DoorOpen, transform.position);
            if (Application.isPlaying) AbilityVfx.PlayCameraShake(1.2f, 0.08f);
            LockpickMinigameUI.ShowToast("The great shelves grind apart...");
            if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(Slide());
            else SetOpened(1f);
        }

        private IEnumerator Slide()
        {
            opened = true;
            isInteractable = false;
            if (roof != null) roof.SetActive(false);
            const float duration = 2.2f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetOpened(Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            SetOpened(1f);
        }

        private void SetOpened(float k)
        {
            opened = k > 0f;
            isInteractable = !opened;
            if (opened) promptMessage = string.Empty;
            if (roof != null && opened) roof.SetActive(false);
            if (leftShelf != null) leftShelf.localPosition = leftClosed + LocalSlide(leftShelf, -1f) * k;
            if (rightShelf != null) rightShelf.localPosition = rightClosed + LocalSlide(rightShelf, 1f) * k;
        }

        private Vector3 LocalSlide(Transform shelf, float side)
        {
            Vector3 world = transform.right * side * slideDistance;
            return shelf.parent != null ? shelf.parent.InverseTransformVector(world) : world;
        }
    }
}
