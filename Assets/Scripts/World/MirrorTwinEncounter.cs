using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The secret boss's entrance (Vili, 2026-10-08). When the hero comes near the great mirror in the Tower's
    /// hidden room, a cutscene plays: laughter rings out of the glass, it ripples violet, and the tavern elf's evil
    /// twin steps out of the mirror with her trident, laughs, says her piece and the fight begins. Once she is
    /// beaten (<see cref="MirrorTwinQuest.DefeatedFlag"/>) the mirror stays quiet.
    /// </summary>
    public class MirrorTwinEncounter : MonoBehaviour
    {
        [SerializeField] private PlanarMirror mirror;
        [SerializeField] private MirrorTwinBoss twin;
        [SerializeField] private DungeonRoomController room;
        [Tooltip("Where she first appears: on the plinth, just inside the glass.")]
        [SerializeField] private Transform emergePoint;
        [Tooltip("Where she steps down off the plinth.")]
        [SerializeField] private Transform stepDownPoint;
        [Tooltip("Where she stops to face the hero.")]
        [SerializeField] private Transform standPoint;
        [SerializeField] private Light mirrorFlash;
        [SerializeField] private AudioClip laugh;
        [Tooltip("The hero sets it off within this distance of the mirror (flat).")]
        [SerializeField] private float triggerDistance = 8f;

        private bool playing;
        private PlayerUnit cachedHero;
        private float nextHeroSearch;
        private readonly List<DialogueNodeSO> nodes = new List<DialogueNodeSO>();

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        public PlanarMirror Mirror { get => mirror; set => mirror = value; }
        public MirrorTwinBoss Twin { get => twin; set => twin = value; }
        public DungeonRoomController Room { get => room; set => room = value; }
        public Transform EmergePoint { get => emergePoint; set => emergePoint = value; }
        public Transform StepDownPoint { get => stepDownPoint; set => stepDownPoint = value; }
        public Transform StandPoint { get => standPoint; set => standPoint = value; }
        public Light MirrorFlash { get => mirrorFlash; set => mirrorFlash = value; }
        public AudioClip Laugh { get => laugh; set => laugh = value; }
        public float TriggerDistance { get => triggerDistance; set => triggerDistance = value; }

        /// <summary>Whether the cutscene is running.</summary>
        public bool IsPlaying => playing;

        /// <summary>Raised when the twin has said her piece and the fight starts.</summary>
        public static event System.Action FightStarted;

        private void OnEnable()
        {
            DungeonRoomController.OnRoomCleared += HandleRoomCleared;
        }

        private void OnDisable()
        {
            DungeonRoomController.OnRoomCleared -= HandleRoomCleared;
            DialogueController.OnDialogueEnded -= HandleDialogueEnded;
        }

        private void Start()
        {
            if (mirrorFlash != null) mirrorFlash.intensity = 0f;
        }

        private void Update()
        {
            if (playing || MirrorTwinQuest.IsDefeated || room == null || room.CurrentState != RoomState.Unexplored) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration) return;
            if (mirror == null) return;

            if (cachedHero == null && Time.unscaledTime >= nextHeroSearch)
            {
                nextHeroSearch = Time.unscaledTime + 0.5f;
                cachedHero = FindAnyObjectByType<PlayerUnit>();
            }
            PlayerUnit hero = cachedHero;
            if (hero == null || !hero.isActiveAndEnabled) return;
            Vector3 front = mirror.transform.position + mirror.WorldNormal * 2f;
            Vector3 d = hero.transform.position - front;
            d.y = 0f;
            if (d.sqrMagnitude > triggerDistance * triggerDistance) return;
            if (Vector3.Dot(hero.transform.position - mirror.transform.position, mirror.WorldNormal) < 0f) return;
            Begin(hero);
        }

        /// <summary>Plays the entrance for <paramref name="hero"/> (also used by tests).</summary>
        public void Begin(PlayerUnit hero)
        {
            if (playing || MirrorTwinQuest.IsDefeated || twin == null || room == null) return;
            StartCoroutine(Cutscene(hero));
        }

        private IEnumerator Cutscene(PlayerUnit hero)
        {
            playing = true;
            GameManager.Instance?.SetMode(GamePlayMode.Dialogue);
            PlayerExplorationMovement move = hero != null ? hero.GetComponent<PlayerExplorationMovement>() : null;
            if (move != null) move.StopMoving();

            // The camera turns to the mirror, the hero faces it
            Vector3 mirrorPos = mirror.transform.position;
            if (hero != null)
            {
                Vector3 look = mirrorPos - hero.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) hero.transform.rotation = Quaternion.LookRotation(look);
            }
            CameraFollow cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            var focus = new GameObject("Mirror_Cutscene_Focus").transform;
            focus.position = standPoint != null ? standPoint.position + mirror.WorldNormal * 2.5f : mirrorPos;
            if (cam != null)
            {
                cam.SetTarget(focus);
                cam.FaceDirection(-mirror.WorldNormal);
                cam.SetZoom(0.3f);
            }

            // Laughter out of the glass, which stirs and glows
            if (laugh != null && SFXManager.Instance != null) SFXManager.Instance.PlaySFX(laugh, mirrorPos, 1f);
            UI.LockpickMinigameUI.ShowToast("Laughter rings out of the mirror...");
            yield return Fade(0f, 1f, 1.6f);

            // She steps out of the mirror, down off the plinth and toward the hero
            Transform t = twin.transform;
            Vector3 facing = mirror.WorldNormal;
            facing.y = 0f;
            t.SetPositionAndRotation(emergePoint.position, Quaternion.LookRotation(facing));
            twin.gameObject.SetActive(true);
            Animator anim = twin.GetComponentInChildren<Animator>();
            if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (anim != null) anim.SetBool(IsMovingHash, true);
            yield return Walk(t, emergePoint.position, stepDownPoint.position, 1.3f);
            Vector3 down = new Vector3(stepDownPoint.position.x, standPoint.position.y, stepDownPoint.position.z) + facing * 0.7f;
            yield return Walk(t, stepDownPoint.position, down, 0.35f);
            StartCoroutine(Fade(1f, 0f, 1.2f));
            yield return Walk(t, down, standPoint.position, 0.9f);
            if (anim != null)
            {
                anim.SetBool(IsMovingHash, false);
                twin.TrySetAnimatorTrigger("Laugh");
            }
            if (laugh != null && SFXManager.Instance != null) SFXManager.Instance.PlaySFX(laugh, t.position, 0.8f);
            if (hero != null)
            {
                Vector3 toHero = hero.transform.position - t.position;
                toHero.y = 0f;
                if (toHero.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(toHero);
            }
            yield return new WaitForSeconds(2.2f);

            if (cam != null && hero != null) cam.SetTarget(hero.transform);
            Destroy(focus.gameObject);

            // Her piece, then the fight
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null)
            {
                DialogueController.OnDialogueEnded += HandleDialogueEnded;
                dialogue.StartDialogue(BuildTaunt(), hero);
                if (!dialogue.IsInDialogue) HandleDialogueEnded();
            }
            else
            {
                StartFight();
            }
        }

        private void HandleDialogueEnded()
        {
            DialogueController.OnDialogueEnded -= HandleDialogueEnded;
            StartFight();
        }

        private void StartFight()
        {
            playing = false;
            if (room != null && room.CurrentState == RoomState.Unexplored)
            {
                room.TriggerEncounter();
                FightStarted?.Invoke();
            }
        }

        private void HandleRoomCleared(DungeonRoomController cleared)
        {
            if (cleared == null || cleared != room || MirrorTwinQuest.IsDefeated) return;
            MirrorTwinQuest.RecordVictory();
            UI.LockpickMinigameUI.ShowToast("The twin shatters like glass and leaves her Black Trident behind. Secret quest: " + MirrorTwinQuest.Title + "!");
        }

        private DialogueNodeSO BuildTaunt()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null) Destroy(nodes[i]);
            }
            nodes.Clear();
            string text = StoryFlags.Has(MirrorTwinQuest.WarnedFlag)
                ? "So my sweet sister warned you. She always was the kind one. Pity you listened only halfway: you still came up here. Only one of us leaves this tower, traveler."
                : "Did you think the gold was the tower's treasure? It was only bait, and here you are. Only one of us leaves this tower, traveler.";
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.Initialize(MirrorTwinBoss.DisplayName, text, null, false);
            node.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Fight] Then it will be me.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            nodes.Add(node);
            return node;
        }

        private IEnumerator Walk(Transform t, Vector3 from, Vector3 to, float seconds)
        {
            float k = 0f;
            while (k < 1f)
            {
                k = Mathf.Min(1f, k + Time.deltaTime / Mathf.Max(0.01f, seconds));
                t.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float k = 0f;
            while (k < 1f)
            {
                k = Mathf.Min(1f, k + Time.deltaTime / Mathf.Max(0.01f, seconds));
                float v = Mathf.Lerp(from, to, k);
                if (mirror != null) mirror.Ripple = v;
                if (mirrorFlash != null) mirrorFlash.intensity = 3.5f * v;
                yield return null;
            }
        }
    }
}
