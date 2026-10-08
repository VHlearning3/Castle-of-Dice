using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The old hermit sitting on a log by the cottage in the Whispering Woods. Talking to him always gets the
    /// same line in a speech bubble above his head: no dialogue panel, no choices, and he can't be fought.
    /// He loops Sit_Idle and plays Sit_Speak (nods, lifts a hand) each time he talks.
    /// </summary>
    [SelectionBase]
    public class ForestHermit : Interactable
    {
        public const string Line = "Stay a while and listen.";
        public const string IdleState = "Sit_Idle";
        public const string SpeakState = "Sit_Speak";

        [Tooltip("Height of the speech bubble above his feet.")]
        [SerializeField] private float bubbleHeight = 3.6f;
        [Tooltip("Seconds the bubble stays up.")]
        [SerializeField] private float bubbleSeconds = 3.5f;

        private Animator animator;
        private SpeechBubble bubble;

        /// <summary>The bubble above his head (created on first use).</summary>
        public SpeechBubble Bubble => bubble;

        protected override void Awake()
        {
            base.Awake();
            if (string.IsNullOrEmpty(promptMessage) || promptMessage == "Interact") promptMessage = "Talk to the Old Hermit";
            animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play(IdleState, 0, Random.value);
            }
        }

        public override void Interact(PlayerUnit player)
        {
            if (bubble == null)
            {
                var anchor = new GameObject("Speech_Anchor");
                anchor.layer = 2;
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(0f, bubbleHeight, 0f);
                bubble = anchor.AddComponent<SpeechBubble>();
            }
            bubble.Show(Line, bubbleSeconds);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.CrossFadeInFixedTime(SpeakState, 0.25f, 0, 0f);
            }
        }
    }
}
