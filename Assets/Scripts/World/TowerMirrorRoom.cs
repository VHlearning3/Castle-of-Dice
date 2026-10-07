using System.Collections;
using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The Treasure Tower's hidden mirror room (Vili, 2026-10-07). It lies behind the tower's north wall and
    /// stays sealed until the Tower challenge is complete (<see cref="TowerChallenge.CompletedFlag"/>): then the
    /// gold heaped against the wall fades away and the wall sinks into the floor, opening the way to the great
    /// mirror. The flag is part of the save, so the room stays open after loading.
    /// </summary>
    public class TowerMirrorRoom : MonoBehaviour
    {
        [Tooltip("The room itself (floor, walls, mirror, lights), hidden until it opens.")]
        [SerializeField] private GameObject room;

        [Tooltip("The tower wall that sinks away.")]
        [SerializeField] private Transform blockingWall;

        [Tooltip("Gold heaps and their glow in front of the wall, which fade away.")]
        [SerializeField] private GameObject[] goldHoard = new GameObject[0];

        [Tooltip("Names looked up when a gold reference is missing (the dressing builder recreates them).")]
        [SerializeField] private string[] goldHoardNames = { "Gold_Piles", "Gold_Glow" };

        [SerializeField] private float openSeconds = 3.2f;

        private Coroutine opening;

        public GameObject Room
        {
            get => room;
            set => room = value;
        }

        public Transform BlockingWall
        {
            get => blockingWall;
            set => blockingWall = value;
        }

        public GameObject[] GoldHoard
        {
            get => goldHoard;
            set => goldHoard = value;
        }

        /// <summary>Whether the room is open (the Tower challenge is done).</summary>
        public static bool IsOpen => StoryFlags.Has(TowerChallenge.CompletedFlag);

        private void OnEnable()
        {
            TowerChallenge.Completed += HandleCompleted;
        }

        private void OnDisable()
        {
            TowerChallenge.Completed -= HandleCompleted;
        }

        private void Start()
        {
            ResolveGold();
            Apply(IsOpen);
        }

        private void HandleCompleted()
        {
            if (opening != null || !isActiveAndEnabled) return;
            ResolveGold();
            opening = StartCoroutine(Open());
        }

        private void ResolveGold()
        {
            if (goldHoard == null || goldHoard.Length != goldHoardNames.Length) goldHoard = new GameObject[goldHoardNames.Length];
            for (int i = 0; i < goldHoard.Length; i++)
            {
                if (goldHoard[i] == null) goldHoard[i] = GameObject.Find(goldHoardNames[i]);
            }
        }

        /// <summary>Sets the open or sealed look at once (scene load).</summary>
        private void Apply(bool open)
        {
            if (room != null) room.SetActive(open);
            if (blockingWall != null) blockingWall.gameObject.SetActive(!open);
            for (int i = 0; i < goldHoard.Length; i++)
            {
                if (goldHoard[i] != null) goldHoard[i].SetActive(!open);
            }
        }

        private IEnumerator Open()
        {
            if (room != null) room.SetActive(true);
            if (SFXManager.Instance != null && blockingWall != null)
            {
                SFXManager.Instance.PlaySFX(SFXClipType.DoorOpen, blockingWall.position, 0.75f);
            }

            Vector3 wallFrom = blockingWall != null ? blockingWall.position : Vector3.zero;
            float sink = blockingWall != null ? blockingWall.lossyScale.y + 0.2f : 0f;
            var goldScale = new Vector3[goldHoard.Length];
            var lights = new Light[goldHoard.Length];
            var lightFrom = new float[goldHoard.Length];
            for (int i = 0; i < goldHoard.Length; i++)
            {
                if (goldHoard[i] == null) continue;
                goldScale[i] = goldHoard[i].transform.localScale;
                lights[i] = goldHoard[i].GetComponent<Light>();
                if (lights[i] != null) lightFrom[i] = lights[i].intensity;
            }

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, openSeconds));
                // The gold melts away first, the heavy wall grinds down after it
                float gold = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2f));
                float wall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.2f) / 0.8f));
                for (int i = 0; i < goldHoard.Length; i++)
                {
                    if (goldHoard[i] == null) continue;
                    if (lights[i] != null) lights[i].intensity = lightFrom[i] * (1f - gold);
                    else goldHoard[i].transform.localScale = new Vector3(goldScale[i].x, goldScale[i].y * (1f - gold), goldScale[i].z);
                }
                if (blockingWall != null) blockingWall.position = wallFrom + Vector3.down * (sink * wall);
                yield return null;
            }

            for (int i = 0; i < goldHoard.Length; i++)
            {
                if (goldHoard[i] == null) continue;
                goldHoard[i].SetActive(false);
                goldHoard[i].transform.localScale = goldScale[i];
                if (lights[i] != null) lights[i].intensity = lightFrom[i];
            }
            if (blockingWall != null)
            {
                blockingWall.gameObject.SetActive(false);
                blockingWall.position = wallFrom;
            }

            // Paths for click-to-move are rebuilt with the wall gone
            RuntimeNavMesh.Clear();
            UI.LockpickMinigameUI.ShowToast("The gold was only an illusion. Behind it the north wall sinks away, revealing a hidden chamber and a great mirror.");
            opening = null;
        }
    }
}
