using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Fires an animator trigger now and then (the blonde elf's Smug pose in the tavern), so a character
    /// standing about does more than loop its idle.
    /// </summary>
    public class IdleFlourish : MonoBehaviour
    {
        [SerializeField] private string trigger = "Smug";
        [SerializeField] private Vector2 interval = new Vector2(8f, 16f);

        private Animator animator;
        private float next;

        public string Trigger { get => trigger; set => trigger = value; }
        public Vector2 Interval { get => interval; set => interval = value; }

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            next = Time.time + Random.Range(2f, interval.y * 0.5f);
        }

        private void Update()
        {
            if (animator == null || Time.time < next) return;
            animator.SetTrigger(trigger);
            next = Time.time + Random.Range(interval.x, interval.y);
        }
    }
}
