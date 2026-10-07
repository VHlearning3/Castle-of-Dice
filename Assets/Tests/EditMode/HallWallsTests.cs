using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Editor;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Castle Hall walls: 16 m tall all round (doorways closed above the doors), one even stone material,
    /// and UVs that give every wall face the same stone size, upright.
    /// </summary>
    [TestFixture]
    public class HallWallsTests
    {
        [SetUp]
        public void OpenHall()
        {
            EditorSceneManager.OpenScene(HallDoorsSetup.ScenePath, OpenSceneMode.Single);
        }

        [TearDown]
        public void LeaveEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Walls_AreTallAllRound()
        {
            int walls = 0;
            foreach (Transform t in GameObject.Find("Walls").transform)
            {
                if (!t.name.StartsWith("Hall_Wall_")) continue;
                Assert.GreaterOrEqual(t.GetComponent<Renderer>().bounds.max.y, HallWallsSetup.WallHeight - 0.01f, t.name);
                walls++;
            }
            Assert.AreEqual(8, walls);

            // Above every doorway the wall reaches the same height
            foreach (string gate in new[] { "Door_Hall_Courtyard", "Door_Hall_Library", "Door_Hall_ThroneRoom", HallDoorsSetup.TowerGateName })
            {
                Transform g = GameObject.Find(gate).transform;
                float top = 0f;
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                {
                    Vector3 c = r.bounds.center;
                    if (Mathf.Abs(Vector3.Dot(c - g.position, g.right)) < 1.5f) top = Mathf.Max(top, r.bounds.max.y);
                }
                Assert.GreaterOrEqual(top, HallWallsSetup.WallHeight - 0.05f, gate + " is walled up to the top above the door");
            }
        }

        [Test]
        public void WallStone_IsEvenAndUpright()
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(HallWallsSetup.MaterialPath);
            Assert.IsNotNull(stone);
            int checkedFaces = 0;
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    Assert.AreNotEqual("M_Ruined_walls", m != null ? m.name : string.Empty, r.name + " still uses the stretched atlas");
                }
                int sub = System.Array.IndexOf(r.sharedMaterials, stone);
                if (sub < 0) continue;

                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] v = mesh.vertices;
                Vector2[] uv = mesh.uv;
                int[] tris = mesh.GetTriangles(sub);
                Matrix4x4 m2w = r.transform.localToWorldMatrix;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = m2w.MultiplyPoint3x4(v[tris[i]]), b = m2w.MultiplyPoint3x4(v[tris[i + 1]]), c = m2w.MultiplyPoint3x4(v[tris[i + 2]]);
                    float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    if (area < 0.05f) continue;
                    Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(n.y) > 0.05f) continue; // walls only; tops and arch soffits are not checked
                    Vector2 ua = uv[tris[i]], ub = uv[tris[i + 1]], uc = uv[tris[i + 2]];
                    float uvArea = Mathf.Abs((ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y)) * 0.5f;
                    float expected = area / (HallWallsSetup.TileMetres * HallWallsSetup.TileMetres);
                    Assert.AreEqual(expected, uvArea, expected * 0.02f + 1e-4f, $"{r.name}: stone size is the same everywhere");

                    // Upright: going up the wall moves straight up the texture
                    float dvPerMetre = Gradient(a, b, c, ua.y, ub.y, uc.y, Vector3.up);
                    Assert.AreEqual(1f / HallWallsSetup.TileMetres, dvPerMetre, 0.005f, $"{r.name}: texture upright");
                    checkedFaces++;
                }
            }
            Assert.Greater(checkedFaces, 40);
        }

        /// <summary>Rate of change of a value across a triangle along a direction lying in its plane.</summary>
        private static float Gradient(Vector3 a, Vector3 b, Vector3 c, float fa, float fb, float fc, Vector3 dir)
        {
            Vector3 e1 = b - a, e2 = c - a;
            Vector3 n = Vector3.Cross(e1, e2);
            dir = Vector3.ProjectOnPlane(dir, n.normalized);
            float d11 = Vector3.Dot(e1, e1), d12 = Vector3.Dot(e1, e2), d22 = Vector3.Dot(e2, e2);
            float det = d11 * d22 - d12 * d12;
            float g1 = fb - fa, g2 = fc - fa;
            // Gradient = alpha e1 + beta e2 with grad·e1 = g1, grad·e2 = g2
            float alpha = (g1 * d22 - g2 * d12) / det;
            float beta = (g2 * d11 - g1 * d12) / det;
            Vector3 grad = alpha * e1 + beta * e2;
            return Vector3.Dot(grad, dir);
        }
    }
}
