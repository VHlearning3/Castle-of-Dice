using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The way into the tavern (Zone_Tavern, Vili 2026-10-07): the door of Oakhaven's "Tavern" building behind
    /// Barnaby. The village scene carries Vili's hand edits and is never edited by tools, so the door trigger,
    /// its sign and the spot where the hero comes back out are made here when the village loads.
    /// </summary>
    public static class TavernEntrance
    {
        /// <summary>The tavern's name on the zone banner, the map and the door sign.</summary>
        public const string DisplayName = "The Foaming Tankard";

        public const string SceneName = "Zone_Tavern";
        public const string VillageSceneName = "Zone_1_VillageAndCellar";

        /// <summary>Where the hero lands inside the tavern.</summary>
        public const string SpawnInTavern = "Spawn_From_Village";

        /// <summary>Where the hero lands in the village when leaving the tavern (made at runtime).</summary>
        public const string SpawnInVillage = "Spawn_From_Tavern";

        public const string DoorGroupName = "Door_Village_Tavern";
        private const string BuildingPath = "Area_Tavern/Tavern";
        private const string BuildingName = "Tavern";
        private const string DoorAnchorName = "NPC_Barnaby";

        // Sizes at the village's scale (the building is ~9 x 11 m, a villager ~3 m)
        private const float TriggerWidth = 2.6f;
        private const float TriggerDepth = 2.4f;
        private const float TriggerHeight = 4f;
        private const float SpawnDistance = 2.3f; // between the steps and the patio tables

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != VillageSceneName) return;
            Build();
        }

        /// <summary>
        /// Makes the tavern door and the return spot in the village (skips them when already there).
        /// Returns the door group, or null when the village has no tavern building.
        /// </summary>
        public static GameObject Build()
        {
            GameObject existing = GameObject.Find(DoorGroupName);
            if (existing != null) return existing;

            Transform building = FindBuilding();
            if (building == null)
            {
                Debug.LogWarning("[TavernEntrance] No 'Tavern' building found in the village; no tavern door.");
                return null;
            }

            if (!TryFindDoor(building, out Vector3 doorBase, out Vector3 outward)) return null;

            // Group faces into the building (ZoneExitSign hangs the sign flat on the wall, facing out)
            var group = new GameObject(DoorGroupName);
            group.transform.SetPositionAndRotation(doorBase, Quaternion.LookRotation(-outward, Vector3.up));

            var anchor = new GameObject(ZoneExitSign.SignAnchorName);
            anchor.transform.SetParent(group.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 4.4f, 0.4f); // just over the door arch

            var trigger = new GameObject(DoorGroupName + "_Trigger");
            trigger.layer = 2; // Ignore Raycast: clicks go to the building, the walk there opens the door
            trigger.transform.SetParent(group.transform, false);
            trigger.transform.localPosition = new Vector3(0f, TriggerHeight * 0.5f, TriggerDepth * 0.5f - 0.6f); // the steps up to the door
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(TriggerWidth, TriggerHeight, TriggerDepth);
            var door = trigger.AddComponent<DoorTeleporter>();
            door.InitializeSceneTeleporter(SceneName, SpawnInTavern, "Tavern", "Enter " + DisplayName, 4.5f, true);

            var spawn = new GameObject(SpawnInVillage);
            Vector3 spawnPos = doorBase + outward * SpawnDistance;
            spawnPos.y = GroundHeight(spawnPos, doorBase.y);
            spawn.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(outward, Vector3.up));

            Debug.Log($"[TavernEntrance] Tavern door at {doorBase}, facing {outward}.");
            return group;
        }

        private static Transform FindBuilding()
        {
            GameObject byPath = GameObject.Find(BuildingPath);
            if (byPath != null) return byPath.transform;
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.sharedMesh != null && mf.sharedMesh.name == BuildingName) return mf.transform;
            }
            return null;
        }

        /// <summary>
        /// The door is the middle of the building side that faces Barnaby (the gable ends hold centred doors).
        /// <paramref name="doorBase"/> is on the ground just outside that wall; <paramref name="outward"/> points away from it.
        /// </summary>
        public static bool TryFindDoor(Transform building, out Vector3 doorBase, out Vector3 outward)
        {
            doorBase = Vector3.zero;
            outward = Vector3.forward;

            MeshFilter mf = null;
            foreach (MeshFilter candidate in building.GetComponentsInChildren<MeshFilter>(true))
            {
                if (candidate.sharedMesh == null || candidate.name.StartsWith("UCX")) continue;
                mf = candidate;
                break;
            }
            if (mf == null) return false;

            Bounds local = mf.sharedMesh.bounds;
            Transform t = mf.transform;
            GameObject anchorObj = GameObject.Find(DoorAnchorName);
            Vector3 anchor = anchorObj != null ? anchorObj.transform.position : t.position - t.forward * 10f;

            // The asset pack's models keep their modelling axes (often z-up): find the local axis that points up
            int upAxis = 0;
            float upDot = 0f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = Vector3.zero;
                axis[i] = 1f;
                float d = Vector3.Dot(t.TransformDirection(axis).normalized, Vector3.up);
                if (Mathf.Abs(d) > Mathf.Abs(upDot)) { upDot = d; upAxis = i; }
            }

            float best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (i == upAxis) continue;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 a = Vector3.zero;
                    a[i] = sign;
                    Vector3 faceLocal = local.center + Vector3.Scale(a, local.extents);
                    faceLocal[upAxis] = upDot > 0f ? local.min[upAxis] : local.max[upAxis];
                    Vector3 faceWorld = t.TransformPoint(faceLocal);
                    float d = (new Vector3(anchor.x, 0f, anchor.z) - new Vector3(faceWorld.x, 0f, faceWorld.z)).sqrMagnitude;
                    if (d >= best) continue;
                    best = d;
                    doorBase = faceWorld;
                    Vector3 dir = t.TransformDirection(a);
                    dir.y = 0f;
                    outward = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
                }
            }
            doorBase.y = GroundHeight(doorBase + outward * 0.8f, doorBase.y);
            return true;
        }

        private static float GroundHeight(Vector3 at, float fallback)
        {
            Vector3 origin = new Vector3(at.x, at.y + 6f, at.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 14f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float bestY = float.MinValue;
            foreach (RaycastHit h in hits)
            {
                // the building's own hull is not ground
                if (h.collider.transform.root.name == "Village_Reference" && h.collider.name.StartsWith("Tavern")) continue;
                if (h.point.y > bestY && h.point.y < at.y + 2.5f) bestY = h.point.y;
            }
            return bestY > float.MinValue ? bestY : fallback;
        }
    }
}
