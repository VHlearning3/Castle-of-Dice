using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Makes the Resources assets the tavern's runtime touches load (Vili, 2026-10-10): the castle door's arch with
    /// box UVs so a plank texture tiles evenly on it, its two timber materials, and the chest model the rogue picks.
    /// Batch: -executeMethod CastleOfTheD20.Editor.TavernFindsAssets.Build. Touches no scene.
    /// </summary>
    public static class TavernFindsAssets
    {
        private const string Folder = "Assets/Resources/Tavern";
        private const string DoorFbx = "Assets/Models/Doors/Door_Single.fbx";
        private const string PlankTexture = "Assets/Materials/Cellar/Cellar_WoodPlanks.png";
        private const string ChestPrefab = "Assets/PREFABS/Chest.prefab";
        private const string ChestLidPrefab = "Assets/PREFABS/Chest_cover.prefab";

        /// <summary>The tavern door's scale (CastleDoorSetup.SingleDoorScale): UVs are laid out in world metres.</summary>
        private const float DoorScale = 1.4f;
        private const float PlankTile = 2.4f;   // same tiling as the tavern's timber (M_Tavern_Timber)
        private const float ChestScale = 0.76f; // the tavern's chests stand 1 m tall

        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Tavern");
            BuildDoorFrame();
            BuildMaterials();
            BuildChest();
            AssetDatabase.SaveAssets();
            Debug.Log("[TavernFindsAssets] Tavern find assets built in " + Folder);
        }

        private static void BuildDoorFrame()
        {
            Mesh source = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(DoorFbx))
            {
                if (o is Mesh m && m.name == TavernDressing.DoorFrameName) source = m;
            }
            if (source == null)
            {
                Debug.LogError("[TavernFindsAssets] No " + TavernDressing.DoorFrameName + " mesh in " + DoorFbx);
                return;
            }

            Vector3[] v = source.vertices;
            Vector3[] n = source.normals;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i] * (DoorScale / PlankTile);
                Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                // Grain runs up the posts and along the tops: planks are horizontal in the texture, so u follows height
                if (a.y >= a.x && a.y >= a.z) uv[i] = new Vector2(p.x, p.z);
                else if (a.x >= a.z) uv[i] = new Vector2(p.y, p.z);
                else uv[i] = new Vector2(p.y, p.x);
            }

            string path = Folder + "/TavernDoorFrame_Wood.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "TavernDoorFrame_Wood";
            mesh.indexFormat = source.indexFormat;
            mesh.vertices = v;
            mesh.normals = n;
            mesh.uv = uv;
            mesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(source.GetTriangles(s), s);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
        }

        private static void BuildMaterials()
        {
            Texture2D planks = AssetDatabase.LoadAssetAtPath<Texture2D>(PlankTexture);
            MakeMaterial(Folder + "/M_TavernDoor_Frame.mat", planks, new Color(0.62f, 0.46f, 0.36f));
            MakeMaterial(Folder + "/M_TavernDoor_FrameTrim.mat", planks, new Color(0.42f, 0.3f, 0.23f));
        }

        private static void MakeMaterial(string path, Texture2D tex, Color tint)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
        }

        private static void BuildChest()
        {
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab);
            GameObject lidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestLidPrefab);
            if (chestPrefab == null || lidPrefab == null)
            {
                Debug.LogError("[TavernFindsAssets] Chest prefabs missing.");
                return;
            }

            var root = new GameObject("Tavern_Chest");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * ChestScale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, visual.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Closed lid on top (as ClassQuestBuilder: both prefabs were saved from one scene, their offset is the closed pose)
            var lid = (GameObject)PrefabUtility.InstantiatePrefab(lidPrefab, visual.transform);
            lid.name = "Chest_Lid";
            lid.transform.localPosition = chestPrefab.transform.InverseTransformPoint(lidPrefab.transform.position);
            lid.transform.localRotation = Quaternion.Inverse(chestPrefab.transform.rotation) * lidPrefab.transform.rotation;
            lid.transform.localScale = Vector3.one;

            // Stand on the floor, one box for clicks and the NavMesh
            Bounds b = RendererBounds(root);
            visual.transform.localPosition += Vector3.up * -b.min.y;
            b = RendererBounds(root);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;

            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Tavern_Chest.prefab");
            Object.DestroyImmediate(root);
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var list = new List<Renderer>(root.GetComponentsInChildren<Renderer>());
            if (list.Count == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds b = list[0].bounds;
            for (int i = 1; i < list.Count; i++) b.Encapsulate(list[i].bounds);
            return b;
        }
    }
}
