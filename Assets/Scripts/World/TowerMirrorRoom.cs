using System.Collections;
using System.Collections.Generic;
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
    /// While sealed, the room is still woken and drawn once off screen as the zone loads, then kept invisible
    /// (renderers, lights and solid colliders off). Opening only switches those back on, so the reveal does not
    /// stutter with first-time setup and shader work (Vili 2026-10-10).
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
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private readonly List<Light> hiddenLights = new List<Light>();
        private readonly List<Collider> hiddenColliders = new List<Collider>();
        private bool hidden;

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

        /// <summary>Whether the room can be seen (false while it waits, loaded but invisible, behind the wall).</summary>
        public bool IsRevealed => room != null && room.activeInHierarchy && !hidden;

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
            if (opening != null) return; // a save from before the mimic was optional finished the challenge on load
            Apply(IsOpen);
            if (!IsOpen) Preload();
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
            if (open) Reveal();
            else if (room != null && !hidden) room.SetActive(false);
            if (blockingWall != null) blockingWall.gameObject.SetActive(!open);
            for (int i = 0; i < goldHoard.Length; i++)
            {
                if (goldHoard[i] != null) goldHoard[i].SetActive(!open);
            }
        }

        /// <summary>
        /// Wakes the sealed room while the zone loads: every Awake/OnEnable runs, one off-screen render uploads
        /// its meshes and compiles its shaders, then it is made invisible and solid-less until it opens.
        /// </summary>
        private void Preload()
        {
            if (room == null || hidden) return;
            room.SetActive(true);

            Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
            RenderOnce(renderers);
            PlanarMirror[] mirrors = room.GetComponentsInChildren<PlanarMirror>();
            for (int i = 0; i < mirrors.Length; i++) mirrors[i].Prewarm();

            hiddenRenderers.Clear();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                renderers[i].enabled = false;
                hiddenRenderers.Add(renderers[i]);
            }
            hiddenLights.Clear();
            foreach (Light l in room.GetComponentsInChildren<Light>())
            {
                if (!l.enabled) continue;
                l.enabled = false;
                hiddenLights.Add(l);
            }
            // Solid colliders only: rooms switch their own walk-in triggers, and the hero cannot reach them here
            hiddenColliders.Clear();
            foreach (Collider c in room.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger) continue;
                c.enabled = false;
                hiddenColliders.Add(c);
            }
            hidden = true;
        }

        /// <summary>Makes the preloaded room visible and solid again.</summary>
        private void Reveal()
        {
            if (room != null) room.SetActive(true);
            if (!hidden) return;
            for (int i = 0; i < hiddenRenderers.Count; i++)
            {
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = true;
            }
            for (int i = 0; i < hiddenLights.Count; i++)
            {
                if (hiddenLights[i] != null) hiddenLights[i].enabled = true;
            }
            for (int i = 0; i < hiddenColliders.Count; i++)
            {
                if (hiddenColliders[i] != null) hiddenColliders[i].enabled = true;
            }
            hiddenRenderers.Clear();
            hiddenLights.Clear();
            hiddenColliders.Clear();
            hidden = false;
        }

        /// <summary>One small render from straight above the room, so the GPU has met all of it before the reveal.</summary>
        private static void RenderOnce(Renderer[] renderers)
        {
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            var go = new GameObject("Mirror_Room_Prewarm") { hideFlags = HideFlags.DontSave };
            Camera cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.aspect = 1f;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.z) + 1f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = b.size.y + 20f;
            cam.cullingMask = ~(1 << 5); // no UI
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.transform.SetPositionAndRotation(b.center + Vector3.up * (b.extents.y + 10f), Quaternion.Euler(90f, 0f, 0f));
            RenderTexture rt = RenderTexture.GetTemporary(128, 128, 24);
            cam.targetTexture = rt;
            try
            {
                cam.Render();
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Destroy(go);
            }
        }

        private IEnumerator Open()
        {
            Reveal();
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

            // Paths for click-to-move are updated in the background with the wall gone
            Combat.PlayerUnit hero = FindAnyObjectByType<Combat.PlayerUnit>();
            RuntimeNavMesh.RefreshAsync(hero != null ? hero.transform : null);
            UI.LockpickMinigameUI.ShowToast("The gold was only an illusion. Behind it the north wall sinks away, revealing a hidden chamber and a great mirror.");
            opening = null;
        }
    }
}
