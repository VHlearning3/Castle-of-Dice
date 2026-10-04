using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Writes the world positions of the gameplay objects in every zone scene (spawns, doors, encounters,
    /// bosses, NPCs, chests, pickups, grids) to a text file. Used to place new content next to them.
    /// Batch: -executeMethod CastleOfTheD20.Editor.ZoneAnchorDump.Run (output: ZONE_ANCHOR_DUMP env var or Logs/zone_anchors.txt).
    /// Read-only: scenes are opened but never saved.
    /// </summary>
    public static class ZoneAnchorDump
    {
        private static readonly string[] Scenes =
        {
            "Zone_1_VillageAndCellar", "Zone_2_ForestPath", "Zone_3_CastleCourtyard", "Zone_4_Library",
            "Zone_5_CastleHall", "Zone_6_Tower", "Zone_7_ThroneRoom"
        };

        private static readonly string[] Keywords =
        {
            "spawn", "door", "gate", "encounter", "room", "boss", "npc", "chest", "pickup", "herb", "ring", "elixir",
            "pedestal", "altar", "savepoint", "shrine", "grid", "trigger", "decoy", "skeleton", "zombie", "rat",
            "teleport", "exit", "entrance", "portal", "stair", "barrier", "lock", "throne", "bookshelf", "table",
            "pillar", "scrap", "torch", "banner", "floor", "camera"
        };

        public static void Run()
        {
            string path = System.Environment.GetEnvironmentVariable("ZONE_ANCHOR_DUMP");
            if (string.IsNullOrEmpty(path)) path = "Logs/zone_anchors.txt";

            StringBuilder sb = new StringBuilder();
            foreach (string sceneName in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                sb.Append("=== ").Append(sceneName).AppendLine();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Dump(root.transform, sb, 0);
                }
            }
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[ZoneAnchorDump] Wrote " + path);
        }

        private static void Dump(Transform t, StringBuilder sb, int depth)
        {
            string lower = t.name.ToLowerInvariant();
            bool interesting = depth == 0;
            for (int i = 0; i < Keywords.Length && !interesting; i++)
            {
                if (lower.Contains(Keywords[i])) interesting = true;
            }
            if (t.GetComponent<MonoBehaviour>() != null && depth <= 3) interesting = true;

            // Skip big decor groups below the top two levels
            if (depth > 6) return;
            if (lower == "zone_dressing" && depth == 0)
            {
                sb.Append(t.name).Append(" (decor, ").Append(t.childCount).AppendLine(" children)");
                return;
            }

            if (interesting)
            {
                sb.Append(new string(' ', depth * 2)).Append(t.name);
                if (!t.gameObject.activeInHierarchy) sb.Append(" [inactive]");
                Vector3 p = t.position;
                sb.Append(" @(").Append(p.x.ToString("F1")).Append(", ").Append(p.y.ToString("F1")).Append(", ").Append(p.z.ToString("F1")).Append(')');
                sb.Append(" rotY ").Append(t.eulerAngles.y.ToString("F0"));
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("CastleOfTheD20")) sb.Append(" <").Append(mb.GetType().Name).Append('>');
                }
                Collider col = t.GetComponent<Collider>();
                if (col != null) sb.Append(" col ").Append(col.bounds.size.ToString("F1"));
                sb.AppendLine();
            }

            for (int i = 0; i < t.childCount; i++)
            {
                Dump(t.GetChild(i), sb, depth + 1);
            }
        }
    }
}
