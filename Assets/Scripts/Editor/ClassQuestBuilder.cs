using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Places each hero's own quest (Vili, 2026-10-06) under a "Class_Quests" root: the knight's boarded-up
    /// crates in the Castle Hall's north-east corner, the mage's warded lectern, great bookshelves and secret
    /// room by the Library's north wall, and the rogue's strongbox on the Hidden Trail. Re-running replaces
    /// only that root; nothing else in the scenes is touched. Menu: CastleOfDice/Class Quests.
    /// Batch: -executeMethod CastleOfTheD20.Editor.ClassQuestBuilder.BatchBuildAndScreenshots
    /// </summary>
    public static class ClassQuestBuilder
    {
        public const string RootName = "Class_Quests";
        private const string ScreenshotDir = "Screenshots/ClassQuests";
        private const string ChestPrefabPath = "Assets/PREFABS/Chest.prefab";
        private const string ChestLidPrefabPath = "Assets/PREFABS/Chest_cover.prefab";
        private const string MatDir = "Assets/Materials/ClassQuests";

        private static Transform root;

        [MenuItem("CastleOfDice/Class Quests/Build All")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir);
            Build("Zone_5_CastleHall", BuildHall);
            Build("Zone_4_Library", BuildLibrary);
            Build(SecretForestPathScene, BuildTrail);
            AssetDatabase.SaveAssets();
        }

        public static void BatchBuildAndScreenshots()
        {
            BuildAll();
            BatchScreenshots();
        }

        private const string SecretForestPathScene = "Zone_2.5_SecretForestPath";

        private static void Build(string sceneName, System.Action build)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName) Object.DestroyImmediate(go);
            }
            root = new GameObject(RootName).transform;
            Physics.SyncTransforms();
            build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ClassQuestBuilder] Built {RootName} in {sceneName}.");
        }

        #region Knight: Castle Hall crates

        private static void BuildHall()
        {
            // The hall's inner wall faces at the north-east corner
            float wallX = WallFace(new Vector3(30f, 1.5f, 36f), Vector3.right, 40f);
            float wallZ = WallFace(new Vector3(36f, 1.5f, 30f), Vector3.forward, 40f);
            Vector3 corner = new Vector3(wallX, 0f, wallZ);

            // The corner pillar becomes part of the barricade: crates run from it to both walls
            Bounds pillar = CornerPillar(corner + new Vector3(-4f, 1.5f, -4f));
            float floorY = FloorY(new Vector3(pillar.center.x - 2f, 0f, pillar.center.z - 2f));
            Debug.Log($"[ClassQuestBuilder] Castle Hall corner {corner}, pillar {pillar.center} size {pillar.size}");

            float px = pillar.center.x, pz = pillar.center.z;
            float northStart = pillar.max.z, eastStart = pillar.max.x;

            GameObject barricade = new GameObject("Knight_Crates");
            barricade.transform.SetParent(root, false);
            barricade.transform.position = new Vector3((px + wallX) * 0.5f, floorY, (pz + wallZ) * 0.5f);

            // The whole nook is one trigger box, so clicking anywhere on the barricade (or the chest behind it) reaches the crates
            BoxCollider nook = barricade.AddComponent<BoxCollider>();
            nook.isTrigger = true;
            nook.center = new Vector3(0f, 1.5f, 0f);
            nook.size = new Vector3(wallX - px + 1.2f, 3f, wallZ - pz + 1.2f);

            List<Transform> crates = new List<Transform>();
            List<Collider> blockers = new List<Collider> { nook };
            Material wood = Mat("Crate_Wood", new Color(0.40f, 0.27f, 0.15f), Color.black);
            Material plankMat = Mat("Crate_Planks", new Color(0.58f, 0.42f, 0.25f), Color.black);

            // North run: from the pillar to the north wall; east run: from the pillar to the east wall
            blockers.Add(CrateRun(barricade.transform, "North", new Vector3(px, floorY, northStart), new Vector3(px, floorY, wallZ), wood, plankMat, crates));
            blockers.Add(CrateRun(barricade.transform, "East", new Vector3(eastStart, floorY, pz), new Vector3(wallX, floorY, pz), wood, plankMat, crates));

            BreakableCrates breakable = barricade.AddComponent<BreakableCrates>();
            breakable.Crates = crates.ToArray();
            breakable.Blockers = blockers.ToArray();
            breakable.InteractionRadius = 3f;

            // The King's old armoury chest, in the boarded-up nook
            Vector3 chestPos = new Vector3((eastStart + wallX) * 0.5f + 0.2f, floorY, (northStart + wallZ) * 0.5f + 0.2f);
            Chest(root, "Knight_Armoury_Chest", chestPos, 225f, CharacterClassType.Warrior, 25);
        }

        private static Bounds CornerPillar(Vector3 around)
        {
            Bounds best = new Bounds(around, new Vector3(1.6f, 6f, 1.6f));
            float bestDist = float.MaxValue;
            foreach (Collider c in Physics.OverlapBox(around, new Vector3(2.5f, 1f, 2.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                Bounds b = c.bounds;
                if (b.size.x > 3.5f || b.size.z > 3.5f || b.size.y < 2f) continue;
                float d = Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(around.x, around.z));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }
            if (bestDist == float.MaxValue) Debug.LogWarning("[ClassQuestBuilder] No corner pillar found; assuming one.");
            return best;
        }

        /// <summary>A row of nailed-shut crates from <paramref name="from"/> to <paramref name="to"/>, two high in places, with its blocking collider.</summary>
        private static Collider CrateRun(Transform parent, string name, Vector3 from, Vector3 to, Material wood, Material plank, List<Transform> crates)
        {
            const float size = 1.1f;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            dir /= length;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / (size + 0.05f)));
            float step = length / count;
            var rng = new System.Random(name.Length * 17 + count);

            for (int i = 0; i < count; i++)
            {
                Vector3 p = from + dir * (step * (i + 0.5f));
                float yaw = (float)(rng.NextDouble() - 0.5) * 14f;
                crates.Add(BuildCrate(parent, $"Crate_{name}_{i + 1}", p, yaw, size, wood, plank));
                if (i % 2 == 0)
                {
                    crates.Add(BuildCrate(parent, $"Crate_{name}_{i + 1}_Top", p + Vector3.up * size, yaw + 9f, size * 0.9f, wood, plank));
                }
            }

            GameObject block = new GameObject("Blocker_" + name);
            block.transform.SetParent(parent, false);
            block.transform.position = (from + to) * 0.5f;
            block.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            BoxCollider bc = block.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.5f, 0f);
            bc.size = new Vector3(size + 0.3f, 3f, length + 0.2f);
            return bc;
        }

        /// <summary>A low-poly wooden crate: a box with a lighter frame and a nailed cross-plank on each side.</summary>
        private static Transform BuildCrate(Transform parent, string name, Vector3 floorPos, float yaw, float size, Material wood, Material plank)
        {
            GameObject crate = new GameObject(name);
            crate.transform.SetParent(parent, false);
            crate.transform.position = floorPos;
            crate.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            float h = size * 0.5f, t = size * 0.08f;
            Part(crate.transform, "Body", new Vector3(0f, h, 0f), Vector3.one * size * 0.96f, Quaternion.identity, wood);
            foreach (float x in new[] { -h, h })
            {
                foreach (float z in new[] { -h, h })
                {
                    Part(crate.transform, "Post", new Vector3(x, h, z), new Vector3(t, size, t), Quaternion.identity, plank);
                }
            }
            foreach (float y in new[] { t * 0.5f, size - t * 0.5f })
            {
                Part(crate.transform, "Rim", new Vector3(0f, y, -h), new Vector3(size, t, t), Quaternion.identity, plank);
                Part(crate.transform, "Rim", new Vector3(0f, y, h), new Vector3(size, t, t), Quaternion.identity, plank);
                Part(crate.transform, "Rim", new Vector3(-h, y, 0f), new Vector3(t, t, size), Quaternion.identity, plank);
                Part(crate.transform, "Rim", new Vector3(h, y, 0f), new Vector3(t, t, size), Quaternion.identity, plank);
            }
            float diag = size * 1.3f;
            Part(crate.transform, "Brace", new Vector3(0f, h, -h - t * 0.3f), new Vector3(diag, t * 1.2f, t * 0.6f), Quaternion.Euler(0f, 0f, 45f), plank);
            Part(crate.transform, "Brace", new Vector3(0f, h, h + t * 0.3f), new Vector3(diag, t * 1.2f, t * 0.6f), Quaternion.Euler(0f, 0f, -45f), plank);
            Part(crate.transform, "Brace", new Vector3(-h - t * 0.3f, h, 0f), new Vector3(t * 0.6f, t * 1.2f, diag), Quaternion.Euler(45f, 0f, 0f), plank);
            Part(crate.transform, "Brace", new Vector3(h + t * 0.3f, h, 0f), new Vector3(t * 0.6f, t * 1.2f, diag), Quaternion.Euler(-45f, 0f, 0f), plank);
            return crate.transform;
        }

        private static void Part(Transform parent, string name, Vector3 localPos, Vector3 scale, Quaternion localRot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        #endregion

        #region Mage: Library shelves and secret room

        private static void BuildLibrary()
        {
            const float centreX = 14f;
            float wallZ = WallFace(new Vector3(centreX, 1.5f, 30f), Vector3.forward, 50f);
            Debug.Log($"[ClassQuestBuilder] Library north wall face at z {wallZ}");
            float floorY = FloorY(new Vector3(centreX, 0f, wallZ - 4f));

            Renderer wallRenderer = HitRenderer(new Vector3(centreX, 1.5f, 30f), Vector3.forward, 50f);
            Material stone = wallRenderer != null ? wallRenderer.sharedMaterial : Mat("SecretRoom_Stone", new Color(0.25f, 0.23f, 0.3f), Color.black);

            GameObject room = new GameObject("Mage_Secret_Room");
            room.transform.SetParent(root, false);

            const float roomWidth = 10f;
            const float depth = 8f;
            const float height = 7f;
            const float doorway = 4.6f;
            float frontZ = wallZ - depth;
            float left = centreX - roomWidth * 0.5f;
            float right = centreX + roomWidth * 0.5f;

            // Front wall with a doorway in the middle, side walls back to the library's north wall
            float sideW = (roomWidth - doorway) * 0.5f;
            Block(room.transform, "Front_Left", new Vector3(left + sideW * 0.5f, floorY + height * 0.5f, frontZ), new Vector3(sideW, height, 1f), stone);
            Block(room.transform, "Front_Right", new Vector3(right - sideW * 0.5f, floorY + height * 0.5f, frontZ), new Vector3(sideW, height, 1f), stone);
            Block(room.transform, "Front_Lintel", new Vector3(centreX, floorY + 5.6f + (height - 5.6f) * 0.5f, frontZ), new Vector3(doorway, height - 5.6f, 1f), stone);
            Block(room.transform, "Side_West", new Vector3(left, floorY + height * 0.5f, frontZ + depth * 0.5f), new Vector3(1f, height, depth), stone);
            Block(room.transform, "Side_East", new Vector3(right, floorY + height * 0.5f, frontZ + depth * 0.5f), new Vector3(1f, height, depth), stone);
            GameObject roof = Block(room.transform, "Roof", new Vector3(centreX, floorY + height + 0.15f, frontZ + depth * 0.5f), new Vector3(roomWidth + 1f, 0.3f, depth + 1f), stone);

            // Inside: the reward chest, a writing table, a warm light
            Chest(room.transform, "Mage_Secret_Chest", new Vector3(centreX, floorY, wallZ - 1.8f), 180f, CharacterClassType.Mage, 25);
            Block(room.transform, "Study_Table", new Vector3(centreX - 3f, floorY + 0.45f, wallZ - 2.5f), new Vector3(1.6f, 0.9f, 1f), Mat("Study_Wood", new Color(0.33f, 0.21f, 0.12f), Color.black));
            Block(room.transform, "Study_Candle", new Vector3(centreX - 3f, floorY + 1.05f, wallZ - 2.5f), new Vector3(0.12f, 0.3f, 0.12f), Mat("Candle_Glow", new Color(1f, 0.9f, 0.6f), new Color(1f, 0.7f, 0.3f)));
            AddLight(room.transform, "Study_Light", new Vector3(centreX, floorY + 3.2f, wallZ - 3f), new Color(1f, 0.78f, 0.5f), 4f, 9f);

            // The great bookshelves in front of the doorway (the library's own shelf mesh)
            MeshFilter shelfSource = FindShelfMesh();
            float shelfZ = frontZ - 0.5f - 0.65f;
            Transform leftShelf = Shelf(room.transform, "Great_Shelf_West", shelfSource, new Vector3(centreX - 2.25f, floorY, shelfZ), stone);
            Transform rightShelf = Shelf(room.transform, "Great_Shelf_East", shelfSource, new Vector3(centreX + 2.25f, floorY, shelfZ), stone);

            // The warded lectern
            GameObject lectern = new GameObject("Mage_Warded_Lectern");
            lectern.transform.SetParent(room.transform, false);
            lectern.transform.position = new Vector3(centreX, floorY, shelfZ - 2.6f);
            Material lecternWood = Mat("Lectern_Wood", new Color(0.30f, 0.18f, 0.10f), Color.black);
            Block(lectern.transform, "Stand", lectern.transform.position + new Vector3(0f, 0.55f, 0f), new Vector3(0.35f, 1.1f, 0.35f), lecternWood, false);
            GameObject top = Block(lectern.transform, "Top", lectern.transform.position + new Vector3(0f, 1.18f, 0f), new Vector3(0.9f, 0.08f, 0.65f), lecternWood, false);
            top.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            Block(lectern.transform, "Open_Book", lectern.transform.position + new Vector3(0f, 1.25f, 0.02f), new Vector3(0.7f, 0.04f, 0.45f), Mat("Book_Pages", new Color(0.9f, 0.85f, 0.7f), new Color(0.2f, 0.15f, 0.35f)), false)
                .transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            AddLight(lectern.transform, "Ward_Glow", lectern.transform.position + new Vector3(0f, 1.8f, 0f), new Color(0.65f, 0.45f, 1f), 2.5f, 5f);
            BoxCollider lc = lectern.AddComponent<BoxCollider>();
            lc.center = new Vector3(0f, 0.7f, 0f);
            lc.size = new Vector3(1f, 1.4f, 0.8f);

            TomeShelfPuzzle puzzle = lectern.AddComponent<TomeShelfPuzzle>();
            puzzle.LeftShelf = leftShelf;
            puzzle.RightShelf = rightShelf;
            puzzle.Roof = roof;
            puzzle.SlideDistance = 4.6f;
            puzzle.InteractionRadius = 3f;
        }

        private static MeshFilter FindShelfMesh()
        {
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.name == "Aisle_Shelf" || mf.name.StartsWith("Bookshelf_1_Unit")) return mf;
            }
            return null;
        }

        private static Transform Shelf(Transform parent, string name, MeshFilter source, Vector3 pos, Material fallback)
        {
            GameObject shelf = new GameObject(name);
            shelf.transform.SetParent(parent, false);
            shelf.transform.position = pos;
            if (source != null)
            {
                shelf.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                shelf.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                BoxCollider bc = shelf.AddComponent<BoxCollider>();
                bc.center = source.sharedMesh.bounds.center;
                bc.size = source.sharedMesh.bounds.size;
            }
            else
            {
                Block(shelf.transform, "Shelf_Block", pos + new Vector3(0f, 2.5f, 0f), new Vector3(4.5f, 5f, 1.2f), fallback);
            }
            return shelf.transform;
        }

        #endregion

        #region Rogue: Hidden Trail strongbox

        private static void BuildTrail()
        {
            Vector3 spot = Free(new Vector3(-7.5f, 0f, 1.5f), 0.9f);
            Chest(root, "Rogue_Smugglers_Strongbox", spot, 90f, CharacterClassType.Rogue, 40);
            Debug.Log($"[ClassQuestBuilder] Rogue strongbox at {spot}");
        }

        #endregion

        #region Shared helpers

        private static void Chest(Transform parent, string name, Vector3 pos, float yaw, CharacterClassType cls, int gold)
        {
            GameObject chest = new GameObject(name);
            chest.transform.SetParent(parent, false);
            chest.transform.position = new Vector3(pos.x, FloorY(pos), pos.z);
            chest.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
            Transform lid = null;
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, chest.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                // Closed lid on top (both prefabs were saved from one scene, so their offset is the closed pose)
                GameObject lidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestLidPrefabPath);
                if (lidPrefab != null)
                {
                    GameObject lidObj = (GameObject)PrefabUtility.InstantiatePrefab(lidPrefab, chest.transform);
                    lidObj.name = "Chest_Lid";
                    lidObj.transform.localPosition = prefab.transform.InverseTransformPoint(lidPrefab.transform.position);
                    lidObj.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * lidPrefab.transform.rotation;
                    lidObj.transform.localScale = Vector3.one;
                    lid = lidObj.transform;
                }
            }
            ChestRewardInteraction reward = chest.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = gold;
            reward.InteractionRadius = 2.2f;
            if (lid != null)
            {
                SerializedObject so = new SerializedObject(reward);
                so.FindProperty("chestLid").objectReferenceValue = lid;
                so.FindProperty("openLidRotation").vector3Value = new Vector3(70f, 0f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            chest.AddComponent<ClassQuestReward>().QuestClass = cls;
        }

        private static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void AddLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
        }

        private static float WallFace(Vector3 from, Vector3 dir, float max)
        {
            if (Physics.Raycast(from, dir, out RaycastHit hit, max, ~0, QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"[ClassQuestBuilder] Wall hit '{hit.collider.name}' at {hit.point}");
                return Vector3.Dot(hit.point, dir);
            }
            Debug.LogWarning($"[ClassQuestBuilder] No wall found from {from} towards {dir}; assuming 40 m.");
            return Vector3.Dot(from, dir) + 10f;
        }

        private static Renderer HitRenderer(Vector3 from, Vector3 dir, float max)
        {
            return Physics.Raycast(from, dir, out RaycastHit hit, max, ~0, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInChildren<Renderer>() : null;
        }

        private static Vector3 Free(Vector3 preferred, float radius)
        {
            for (float r = 0f; r <= 6f; r += 0.5f)
            {
                int steps = r < 0.5f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r);
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Vector3 p = preferred + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float floorY = FloorY(p);
                    Vector3 center = new Vector3(p.x, floorY + 1.2f, p.z);
                    if (!Physics.CheckBox(center, new Vector3(radius, 0.9f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return new Vector3(p.x, floorY, p.z);
                    }
                }
            }
            Debug.LogWarning($"[ClassQuestBuilder] No free spot near {preferred}; using it anyway.");
            return new Vector3(preferred.x, FloorY(preferred), preferred.z);
        }

        private static float FloorY(Vector3 p)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 8f, p.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                float y = hits[i].point.y;
                if (y < 0.8f && y > best) best = y;
            }
            return float.IsNegativeInfinity(best) ? 0f : best;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static Material Mat(string name, Color baseColor, Color emission)
        {
            string path = $"{MatDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (emission.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        #endregion

        #region Screenshots

        private struct Shot
        {
            public string Scene;
            public Vector3 Focus;
            public float Size;
            public string Name;
            public bool TopDown;
            public Vector3 CamPos;
        }

        private static readonly Shot[] Shots =
        {
            new Shot { Scene = "Zone_5_CastleHall", Focus = new Vector3(34f, 0f, 34f), Size = 9f, Name = "hall_crates_top", TopDown = true },
            new Shot { Scene = "Zone_5_CastleHall", Focus = new Vector3(33f, 0f, 33f), Size = 8f, Name = "hall_crates", TopDown = false },
            new Shot { Scene = "Zone_4_Library", Focus = new Vector3(14f, 0f, 36f), Size = 11f, Name = "library_room_top", TopDown = true },
            new Shot { Scene = "Zone_4_Library", Focus = new Vector3(14f, 2f, 34f), Size = 9f, Name = "library_shelves", TopDown = false, CamPos = new Vector3(5f, 6.5f, 27f) },
            new Shot { Scene = "Zone_2.5_SecretForestPath", Focus = new Vector3(-5f, 0f, 1.5f), Size = 9f, Name = "trail_strongbox_top", TopDown = true },
            new Shot { Scene = "Zone_2.5_SecretForestPath", Focus = new Vector3(-6f, 0f, 1.5f), Size = 7f, Name = "trail_strongbox", TopDown = false },
        };

        /// <summary>Top-down and angled shots of the three quest spots, plus the Library room opened (batch mode).</summary>
        public static void BatchScreenshots()
        {
            Directory.CreateDirectory(ScreenshotDir);
            foreach (Shot shot in Shots)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + shot.Scene + ".unity", OpenSceneMode.Single);
                Render(shot.Name, shot.Focus, shot.Size, shot.TopDown, shot.CamPos);
            }

            // The Library with the shelves slid open and the roof gone (not saved)
            EditorSceneManager.OpenScene("Assets/Scenes/Zone_4_Library.unity", OpenSceneMode.Single);
            TomeShelfPuzzle puzzle = Object.FindAnyObjectByType<TomeShelfPuzzle>();
            if (puzzle != null)
            {
                Vector3 l = puzzle.LeftShelf.position, r = puzzle.RightShelf.position;
                puzzle.LeftShelf.position = l + puzzle.transform.right * -puzzle.SlideDistance;
                puzzle.RightShelf.position = r + puzzle.transform.right * puzzle.SlideDistance;
                puzzle.Roof.SetActive(false);
                Render("library_room_open", new Vector3(14f, 1.5f, 37f), 9f, false, new Vector3(6f, 6.5f, 28f));
                Render("library_room_open_top", new Vector3(14f, 0f, 36f), 11f, true);
            }
        }

        /// <summary>A picture of the mage's book minigame with two tomes shelved (batch mode).</summary>
        public static void BatchMinigameShot()
        {
            Directory.CreateDirectory(ScreenshotDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject camObj = new GameObject("MinigameShotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.1f, 0.16f);

            UI.TomeShelfMinigameUI.Open(null);
            UI.TomeShelfMinigameUI ui = UI.TomeShelfMinigameUI.Instance;
            ui.ClickTome(0);
            ui.ClickSlot(0);
            ui.ClickTome(1);
            ui.ClickSlot(1);
            ui.ClickTome(4);
            Canvas canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ScreenshotDir, "mage_minigame.png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
        }

        /// <summary>Top-down overview of the three zones (batch mode), used to pick the spots.</summary>
        public static void BatchOverview()
        {
            Directory.CreateDirectory(ScreenshotDir);
            foreach (string scene in new[] { "Zone_4_Library", "Zone_5_CastleHall", SecretForestPathScene })
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
                Render(scene + "_top", Vector3.zero, 48f, true);
            }
        }

        private static void Render(string name, Vector3 focus, float size, bool topDown, Vector3 camPos = default)
        {
            GameObject camObj = new GameObject("ClassQuestShotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            if (topDown)
            {
                cam.orthographic = true;
                cam.orthographicSize = size;
                camObj.transform.position = new Vector3(focus.x, 6.5f, focus.z);
                cam.nearClipPlane = 0.1f;
                camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                cam.fieldOfView = 50f;
                camObj.transform.position = camPos != Vector3.zero ? camPos : focus + new Vector3(0f, size * 0.9f, -size * 1.3f);
                camObj.transform.LookAt(focus);
            }
            cam.farClipPlane = 200f;

            RenderTexture rt = new RenderTexture(1280, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ScreenshotDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camObj);
        }

        #endregion
    }
}
