using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Puts Oakhaven's two lore notes (critical review C3) next to existing village objects when the village
    /// loads. The village scene carries Vili's hand edits and is never rebuilt or edited by tools, so its notes
    /// are placed at runtime instead; zones 2-7 get theirs from the Review Content builder.
    /// </summary>
    public static class RuntimeLoreSpawner
    {
        private const string VillageScene = "Zone_1_VillageAndCellar";

        private struct Placement
        {
            public string Anchor;
            public Vector3 Offset;
            public string NoteId;
        }

        private static readonly Placement[] VillageNotes =
        {
            new Placement { Anchor = "StartSpawn", Offset = new Vector3(2.6f, 0f, 2.2f), NoteId = "village_notice" },
            new Placement { Anchor = "NPC_Othelia", Offset = new Vector3(1.9f, 0f, -1.4f), NoteId = "othelia_letter" },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != VillageScene) return;
            SpawnVillageNotes();
        }

        /// <summary>Places the village notes (skips any that are already there).</summary>
        public static int SpawnVillageNotes()
        {
            int placed = 0;
            for (int i = 0; i < VillageNotes.Length; i++)
            {
                Placement p = VillageNotes[i];
                if (GameObject.Find("LoreNote_" + p.NoteId) != null) continue;

                GameObject anchor = GameObject.Find(p.Anchor);
                if (anchor == null) continue;

                Vector3 pos = anchor.transform.position + p.Offset;
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    pos.y = hit.point.y;
                }
                LoreNote.Create(p.NoteId, pos, anchor.transform.eulerAngles.y);
                placed++;
            }
            return placed;
        }
    }
}
