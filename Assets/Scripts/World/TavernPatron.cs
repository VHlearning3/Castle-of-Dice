using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A tavern guest: plays one looping animator state (Sit_Idle, Sit_Drink, Stand_Idle or Stand_Drink),
    /// started at a random point so neighbours never move in step.
    /// </summary>
    public class TavernPatron : MonoBehaviour
    {
        public const string SitIdle = "Sit_Idle";
        public const string SitDrink = "Sit_Drink";
        public const string StandIdle = "Stand_Idle";
        public const string StandDrink = "Stand_Drink";

        [Tooltip("Animator state to loop.")]
        [SerializeField] private string state = StandIdle;

        public string State { get => state; set => state = value; }

        private void Start()
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || string.IsNullOrEmpty(state)) return;
            animator.Play(state, 0, Random.value);
            animator.speed = Random.Range(0.92f, 1.08f);
        }
    }
}
