using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The blonde elf in the tavern's gallery (Vili, 2026-10-08) gives the only hint of the secret boss quest:
    /// "Beware my evil twin on top of the magic tower, traveler." Talking to her starts nothing in the quest
    /// tracker; it only sets <see cref="MirrorTwinQuest.WarnedFlag"/>. Once the twin is beaten, the hero can give
    /// her the twin's Black Trident, which plays the secret ending "You cleansed the tower". The tavern scene
    /// belongs to the tavern's builder, so she is made talkable here when the tavern loads.
    /// </summary>
    public class TavernElfWarning : Interactable
    {
        public const string ElfObjectName = "NPC_Blonde_Elf";
        public const string SpeakerName = "Blonde Elf";

        private readonly List<DialogueNodeSO> nodes = new List<DialogueNodeSO>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != TavernEntrance.SceneName) return;
            Attach();
        }

        /// <summary>Makes the tavern's blonde elf talkable (once). Returns her warning, or null when she is missing.</summary>
        public static TavernElfWarning Attach()
        {
            GameObject elf = GameObject.Find(ElfObjectName);
            if (elf == null) return null;
            TavernElfWarning existing = elf.GetComponent<TavernElfWarning>();
            if (existing != null) return existing;

            if (elf.GetComponent<Collider>() == null)
            {
                // A body-sized capsule from her renderers, so a click on her lands
                Bounds b = new Bounds(elf.transform.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in elf.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                CapsuleCollider col = elf.AddComponent<CapsuleCollider>();
                Vector3 lossy = elf.transform.lossyScale;
                float s = Mathf.Max(0.01f, lossy.y);
                float height = any ? b.size.y : 1.7f;
                col.height = height / s;
                col.radius = Mathf.Min(0.45f, height * 0.18f) / s;
                col.center = elf.transform.InverseTransformPoint(any ? b.center : elf.transform.position + Vector3.up * 0.85f);
            }
            return elf.AddComponent<TavernElfWarning>();
        }

        protected override void Awake()
        {
            base.Awake();
            promptMessage = "Talk to the blonde elf";
            interactionRadius = Mathf.Max(interactionRadius, 3f);
        }

        public override void Interact(PlayerUnit player)
        {
            StoryFlags.Set(MirrorTwinQuest.WarnedFlag);
            ClearNodes();

            // With her sister's trident in the bag, the hero can hand it over: the secret ending (Vili, 2026-10-08)
            if (MirrorTwinQuest.IsDefeated && MirrorTwinQuest.HasTrident)
            {
                StartTridentHandIn(player);
                return;
            }

            DialogueNodeSO node = NewNode(MirrorTwinQuest.WarningLine);
            node.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] ...I'll keep that in mind.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            DialogueController.Instance?.StartDialogue(node, player);
        }

        #region The Black Trident (secret ending)

        private DialogueNodeSO handedOverNode;
        private bool tridentHandedOver;
        private PlayerUnit endingHero;

        /// <summary>The elf's first words when the hero comes back with the trident.</summary>
        public const string TridentGreeting =
            "That trident... it is hers. Then you went up the tower, and my sister is gone. Will you give it to me, traveler?";

        /// <summary>
        /// The hand-in: give the trident (the secret ending plays when the talk ends) or keep it for now and go on
        /// to the Gargoyle King.
        /// </summary>
        public DialogueNodeSO BuildTridentDialogue()
        {
            DialogueNodeSO last = NewNode(
                "Then Oakhaven is safe, even if the castle sleeps on under its stone. Sit, traveler. Tonight the Foaming Tankard drinks to you.");
            last.SetOptions(new List<DialogueOption> { new DialogueOption("[End] I'll take that drink.", null) });

            handedOverNode = NewNode(
                "She closes her hand around the black shaft, and the violet sparks die. \"The mirror kept her bound to the tower, and the tower kept its hunger. " +
                "No one will climb down from it to hunt this village again. The Gargoyle King still sits his throne... but he will never reach us here.\"");
            handedOverNode.SetOptions(new List<DialogueOption> { new DialogueOption("And the castle?", last) });

            DialogueNodeSO offer = NewNode(TridentGreeting);
            offer.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Give her the Black Trident] It's over. Take it.", handedOverNode),
                new DialogueOption("[Not yet] I still need it.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            return offer;
        }

        private void StartTridentHandIn(PlayerUnit player)
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue == null) return;

            endingHero = player;
            tridentHandedOver = false;
            DialogueController.OnDialogueUpdated -= HandleDialogueUpdated;
            DialogueController.OnDialogueEnded -= HandleDialogueEnded;
            DialogueController.OnDialogueUpdated += HandleDialogueUpdated;
            DialogueController.OnDialogueEnded += HandleDialogueEnded;
            dialogue.StartDialogue(BuildTridentDialogue(), player);
        }

        private void HandleDialogueUpdated(DialogueNodeSO node)
        {
            if (node == null || node != handedOverNode || tridentHandedOver) return;
            tridentHandedOver = true;
            MirrorTwinQuest.ReturnTrident();
        }

        private void HandleDialogueEnded()
        {
            DialogueController.OnDialogueUpdated -= HandleDialogueUpdated;
            DialogueController.OnDialogueEnded -= HandleDialogueEnded;
            if (!tridentHandedOver) return;
            ThroneRoomEnding.PlayTowerCleansedEnding(endingHero != null ? endingHero : FindAnyObjectByType<PlayerUnit>());
        }

        private void OnDestroy()
        {
            DialogueController.OnDialogueUpdated -= HandleDialogueUpdated;
            DialogueController.OnDialogueEnded -= HandleDialogueEnded;
            ClearNodes();
        }

        #endregion

        private DialogueNodeSO NewNode(string text)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.hideFlags = HideFlags.DontSave;
            node.Initialize(SpeakerName, text, null, false);
            nodes.Add(node);
            return node;
        }

        private void ClearNodes()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null) continue;
                if (Application.isPlaying) Destroy(nodes[i]);
                else DestroyImmediate(nodes[i]);
            }
            nodes.Clear();
        }
    }
}
