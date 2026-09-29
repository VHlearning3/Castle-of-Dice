using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Blue d20 roll animation: geometry of the procedural die and the 2 second timing cap.
    /// </summary>
    [TestFixture]
    public class DiceRollAnimationTests
    {
        [Test]
        public void EveryFace_HasUnitOutwardNormal()
        {
            for (int face = 0; face < D20DieGraphic.FaceCount; face++)
            {
                Vector3 normal = D20DieGraphic.GetFaceNormal(face);
                Assert.AreEqual(1f, normal.magnitude, 1e-4f, $"Face {face}");
            }
        }

        [Test]
        public void FaceNormals_AreAllDistinct()
        {
            for (int a = 0; a < D20DieGraphic.FaceCount; a++)
            {
                for (int b = a + 1; b < D20DieGraphic.FaceCount; b++)
                {
                    float dot = Vector3.Dot(D20DieGraphic.GetFaceNormal(a), D20DieGraphic.GetFaceNormal(b));
                    Assert.Less(dot, 0.99f, $"Faces {a} and {b} overlap");
                }
            }
        }

        [Test]
        public void RestingRotation_TurnsFaceTowardViewer()
        {
            for (int face = 0; face < D20DieGraphic.FaceCount; face++)
            {
                Vector3 facing = D20DieGraphic.GetRestingRotation(face) * D20DieGraphic.GetFaceNormal(face);
                Assert.AreEqual(1f, facing.z, 1e-3f, $"Face {face} should point straight at the player");
            }
        }

        [Test]
        public void ClampToMaxSequence_KeepsWholeRollWithinTwoSeconds()
        {
            float roll = 0.8f;
            float hold = 2.5f; // the old scene value
            DiceUIController.ClampToMaxSequence(ref roll, ref hold);
            Assert.LessOrEqual(roll + hold, DiceUIController.MaxRollSequenceSeconds + 1e-4f);
            Assert.AreEqual(0.8f, roll, 1e-4f);

            roll = 5f;
            hold = 1f;
            DiceUIController.ClampToMaxSequence(ref roll, ref hold);
            Assert.LessOrEqual(roll + hold, DiceUIController.MaxRollSequenceSeconds + 1e-4f);
        }

        [Test]
        public void ClampToMaxSequence_LeavesDefaultTimingUntouched()
        {
            float roll = 1f;
            float hold = 1f;
            DiceUIController.ClampToMaxSequence(ref roll, ref hold);
            Assert.AreEqual(1f, roll, 1e-4f);
            Assert.AreEqual(1f, hold, 1e-4f);
        }

        [Test]
        public void DieGraphic_BuildsOutlineAndBevelledFaces()
        {
            GameObject go = new GameObject("D20DieTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(D20DieGraphic));
            VertexHelper vh = new VertexHelper();
            try
            {
                ((RectTransform)go.transform).sizeDelta = new Vector2(160f, 160f);
                D20DieGraphic die = go.GetComponent<D20DieGraphic>();
                die.Rotation = Quaternion.Euler(33f, 71f, 12f);
                die.Flash = 0.5f;

                MethodInfo populate = typeof(D20DieGraphic).GetMethod(
                    "OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
                Assert.IsNotNull(populate);
                populate.Invoke(die, new object[] { vh });

                // Every back face adds one outline triangle, every front face an edge and an inset triangle.
                int triangles = vh.currentIndexCount / 3;
                Assert.AreEqual(vh.currentVertCount, vh.currentIndexCount);
                Assert.Greater(triangles, D20DieGraphic.FaceCount);
                Assert.Less(triangles, D20DieGraphic.FaceCount * 2);
            }
            finally
            {
                vh.Dispose();
                Object.DestroyImmediate(go);
            }
        }
    }
}
