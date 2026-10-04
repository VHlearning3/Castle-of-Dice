using UnityEngine;
using TMPro;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A turning rune pillar of the Tower puzzle (critical review C5). Each click turns it to the next rune
    /// (SUN, MOON, STAR, EYE); the rune's name floats above it and its crystal changes colour.
    /// </summary>
    public class RunePillar : Interactable
    {
        public enum Rune
        {
            Sun = 0,
            Moon = 1,
            Star = 2,
            Eye = 3
        }

        private static readonly string[] Names = { "SUN", "MOON", "STAR", "EYE" };
        private static readonly Color[] Colors =
        {
            new Color(1f, 0.75f, 0.2f),
            new Color(0.6f, 0.7f, 1f),
            new Color(0.95f, 0.95f, 0.7f),
            new Color(0.7f, 0.3f, 0.9f)
        };

        [SerializeField] private Rune current = Rune.Eye;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Renderer crystal;
        [SerializeField] private Light glow;

        private bool locked;

        /// <summary>The puzzle this pillar belongs to (set by the challenge).</summary>
        public TowerChallenge Challenge { get; set; }

        /// <summary>Rune the pillar shows now.</summary>
        public Rune Current => current;

        public TMP_Text Label
        {
            get => label;
            set => label = value;
        }

        public Renderer Crystal
        {
            get => crystal;
            set => crystal = value;
        }

        public Light Glow
        {
            get => glow;
            set => glow = value;
        }

        protected override void Awake()
        {
            base.Awake();
            promptMessage = "Turn the rune pillar";
            interactionRadius = Mathf.Max(interactionRadius, 3f);
        }

        private void Start()
        {
            Refresh();
        }

        public override void Interact(PlayerUnit player)
        {
            if (locked) return;
            SetRune((Rune)(((int)current + 1) % Names.Length));
            if (Challenge == null) Challenge = FindAnyObjectByType<TowerChallenge>();
            Challenge?.OnPillarTurned();
        }

        /// <summary>Shows <paramref name="rune"/> (no puzzle check).</summary>
        public void SetRune(Rune rune)
        {
            current = rune;
            Refresh();
        }

        /// <summary>Stops the pillar turning once the puzzle is solved.</summary>
        public void Lock()
        {
            locked = true;
            isInteractable = false;
        }

        /// <summary>Display name of a rune.</summary>
        public static string NameOf(Rune rune) => Names[(int)rune];

        private void Refresh()
        {
            Color c = Colors[(int)current];
            if (label != null)
            {
                label.text = Names[(int)current];
                label.color = c;
            }
            if (glow != null) glow.color = c;
            if (crystal != null && crystal.sharedMaterial != null && Application.isPlaying)
            {
                Material m = crystal.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.5f);
            }
        }
    }
}
