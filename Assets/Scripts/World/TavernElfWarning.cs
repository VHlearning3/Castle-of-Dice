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
    /// tracker; it only sets <see cref="MirrorTwinQuest.WarnedFlag"/>. The tavern scene belongs to the tavern's
    /// builder, so she is made talkable here when the tavern loads.
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
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null) Destroy(nodes[i]);
            }
            nodes.Clear();
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            node.Initialize(SpeakerName, MirrorTwinQuest.WarningLine, null, false);
            node.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Leave] ...I'll keep that in mind.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]")
            });
            nodes.Add(node);
            DialogueController.Instance?.StartDialogue(node, player);
        }
    }
}
