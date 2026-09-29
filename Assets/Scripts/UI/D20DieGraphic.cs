using UnityEngine;
using UnityEngine.UI;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Procedural blue D20 (icosahedron) drawn straight into the UI canvas mesh.
    /// No camera, RenderTexture or sprite is needed: the 12 vertices are rotated by
    /// <see cref="Rotation"/>, projected with a mild perspective and flat-shaded per face.
    /// All buffers are preallocated so rebuilding the mesh every frame produces no garbage.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class D20DieGraphic : MaskableGraphic
    {
        #region Serialized Fields

        [Header("Palette")]
        [Tooltip("Face colour on the side turned away from the light.")]
        [SerializeField] private Color shadowColor = new Color(0.04f, 0.12f, 0.42f, 1f);

        [Tooltip("Face colour on the side facing the light.")]
        [SerializeField] private Color litColor = new Color(0.25f, 0.55f, 1f, 1f);

        [Tooltip("Colour of the bevelled edges between faces.")]
        [SerializeField] private Color edgeColor = new Color(0.62f, 0.82f, 1f, 1f);

        [Tooltip("Dark silhouette outline so the die reads on any panel colour.")]
        [SerializeField] private Color outlineColor = new Color(0.02f, 0.05f, 0.18f, 1f);

        [Header("Shape")]
        [Tooltip("How far each face is shrunk toward its centre to reveal the edge bevel (1 = no bevel).")]
        [Range(0.8f, 1f)]
        [SerializeField] private float faceInset = 0.9f;

        [Tooltip("Silhouette outline thickness relative to the die radius.")]
        [Range(1f, 1.15f)]
        [SerializeField] private float outlineScale = 1.06f;

        [Tooltip("Perspective strength (0 = orthographic).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float perspective = 0.18f;

        #endregion

        #region Geometry

        public const int FaceCount = 20;

        private static readonly Vector3[] BaseVertices = BuildVertices();

        // Outward-wound triangles of a regular icosahedron (indices into BaseVertices).
        private static readonly int[] FaceIndices =
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        };

        // +1 when a face's index order winds counter-clockwise seen from outside, -1 otherwise.
        private static readonly float[] FaceWinding = BuildWinding();

        private static readonly Vector3 LightDirection = new Vector3(-0.45f, 0.6f, 0.66f).normalized;

        private readonly Vector3[] rotated = new Vector3[12];
        private readonly Vector2[] projected = new Vector2[12];

        private Quaternion rotation = Quaternion.identity;
        private Vector2 offset;
        private float scale = 1f;
        private float flash;

        #endregion

        #region Public API

        /// <summary>Current orientation of the die. Setting it rebuilds the mesh.</summary>
        public Quaternion Rotation
        {
            get => rotation;
            set { rotation = value; SetVerticesDirty(); }
        }

        /// <summary>Pixel offset of the die inside its rect (used for the bounce).</summary>
        public Vector2 Offset
        {
            get => offset;
            set { offset = value; SetVerticesDirty(); }
        }

        /// <summary>Uniform size multiplier (used for the drop-in and landing squash).</summary>
        public float Scale
        {
            get => scale;
            set { scale = value; SetVerticesDirty(); }
        }

        /// <summary>0..1 brightening of every face, used for the landing flash.</summary>
        public float Flash
        {
            get => flash;
            set { flash = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        /// <summary>Outward unit normal of the given face in the die's local space.</summary>
        public static Vector3 GetFaceNormal(int face)
        {
            int i = face * 3;
            Vector3 a = BaseVertices[FaceIndices[i]];
            Vector3 b = BaseVertices[FaceIndices[i + 1]];
            Vector3 c = BaseVertices[FaceIndices[i + 2]];
            return (a + b + c).normalized;
        }

        /// <summary>
        /// Orientation that turns <paramref name="face"/> straight toward the viewer (+Z)
        /// with one of its corners pointing up, like the top face of a resting d20.
        /// </summary>
        public static Quaternion GetRestingRotation(int face)
        {
            face = Mathf.Clamp(face, 0, FaceCount - 1);
            Quaternion toViewer = Quaternion.FromToRotation(GetFaceNormal(face), Vector3.forward);

            Vector3 centre = GetFaceNormal(face);
            Vector3 corner = toViewer * (BaseVertices[FaceIndices[face * 3]] - centre * Vector3.Dot(BaseVertices[FaceIndices[face * 3]], centre));
            float angle = Mathf.Atan2(corner.x, corner.y) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle, Vector3.forward) * toViewer;
        }

        #endregion

        #region Mesh Building

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();
            float radius = Mathf.Min(r.width, r.height) * 0.5f * 0.92f * scale;
            if (radius <= 0f) return;

            Vector2 centre = r.center + offset;
            for (int i = 0; i < 12; i++)
            {
                Vector3 v = rotation * BaseVertices[i];
                rotated[i] = v;
                float p = 1f / (1f - v.z * perspective);
                projected[i] = centre + new Vector2(v.x, v.y) * (radius * p);
            }

            // 1. Silhouette outline: back faces pushed outward, drawn first.
            Color32 outline = outlineColor * color;
            for (int f = 0; f < FaceCount; f++)
            {
                int i = f * 3;
                if (IsFrontFacing(f)) continue;
                AddTriangle(vh,
                    Scaled(centre, projected[FaceIndices[i]], outlineScale),
                    Scaled(centre, projected[FaceIndices[i + 1]], outlineScale),
                    Scaled(centre, projected[FaceIndices[i + 2]], outlineScale),
                    outline);
            }

            // 2. Visible faces: bevel edge triangle, then inset shaded face on top.
            for (int f = 0; f < FaceCount; f++)
            {
                int i = f * 3;
                Vector3 a = rotated[FaceIndices[i]];
                Vector3 b = rotated[FaceIndices[i + 1]];
                Vector3 c = rotated[FaceIndices[i + 2]];
                if (!IsFrontFacing(f)) continue;
                Vector3 normal = (a + b + c).normalized;

                float lambert = Mathf.Clamp01(Vector3.Dot(normal, LightDirection));
                float rim = Mathf.Pow(normal.z, 6f) * 0.15f;
                Color face = Color.Lerp(shadowColor, litColor, lambert * 0.85f + rim);
                face = Color.Lerp(face, Color.white, flash * 0.55f);
                Color edge = Color.Lerp(edgeColor, Color.white, flash * 0.6f);

                Vector2 pa = projected[FaceIndices[i]];
                Vector2 pb = projected[FaceIndices[i + 1]];
                Vector2 pc = projected[FaceIndices[i + 2]];
                AddTriangle(vh, pa, pb, pc, edge * color);

                Vector2 mid = (pa + pb + pc) / 3f;
                AddTriangle(vh,
                    Scaled(mid, pa, faceInset),
                    Scaled(mid, pb, faceInset),
                    Scaled(mid, pc, faceInset),
                    face * color);
            }
        }

        // The projected winding is exact under perspective, so front faces never overlap.
        private bool IsFrontFacing(int face)
        {
            int i = face * 3;
            Vector2 a = projected[FaceIndices[i]];
            Vector2 ab = projected[FaceIndices[i + 1]] - a;
            Vector2 ac = projected[FaceIndices[i + 2]] - a;
            return (ab.x * ac.y - ab.y * ac.x) * FaceWinding[face] > 0f;
        }

        private static Vector2 Scaled(Vector2 origin, Vector2 point, float factor)
        {
            return origin + (point - origin) * factor;
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero);
            vh.AddVert(b, col, Vector4.zero);
            vh.AddVert(c, col, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static float[] BuildWinding()
        {
            float[] w = new float[FaceCount];
            for (int f = 0; f < FaceCount; f++)
            {
                int i = f * 3;
                Vector3 a = BaseVertices[FaceIndices[i]];
                Vector3 b = BaseVertices[FaceIndices[i + 1]];
                Vector3 c = BaseVertices[FaceIndices[i + 2]];
                w[f] = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) >= 0f ? 1f : -1f;
            }
            return w;
        }

        private static Vector3[] BuildVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        #endregion
    }
}
