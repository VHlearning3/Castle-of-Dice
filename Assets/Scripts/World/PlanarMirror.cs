using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A flat mirror (the Tower's great mirror). While the mirror is on screen, a small camera mirrored across
    /// the glass renders the room into a low-resolution texture; the glass shader (CastleOfDice/PlanarMirror)
    /// samples it in screen space, flipped left to right. The camera sleeps whenever the mirror is out of view
    /// or far away, so the cost stays on screen only.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PlanarMirror : MonoBehaviour
    {
        private static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");
        private static readonly int HasReflectionId = Shader.PropertyToID("_HasReflection");

        [Tooltip("The glass's facing direction in its own space (into the room).")]
        [SerializeField] private Vector3 localNormal = Vector3.back;

        [Tooltip("Height of the reflection texture in pixels; the width follows the screen's aspect.")]
        [SerializeField] private int textureHeight = 540;

        [Tooltip("Beyond this distance from the camera the reflection stops updating.")]
        [SerializeField] private float maxDistance = 60f;

        [SerializeField] private float clipOffset = 0.03f;

        private Renderer glass;
        private Material material;
        private Camera mirrorCamera;
        private RenderTexture texture;
        private Camera viewer;
        private readonly Plane[] frustum = new Plane[6];

        public Vector3 LocalNormal
        {
            get => localNormal;
            set => localNormal = value;
        }

        /// <summary>The camera drawing the reflection (null until the mirror is first seen).</summary>
        public Camera MirrorCamera => mirrorCamera;

        /// <summary>The glass's facing direction in the world.</summary>
        public Vector3 WorldNormal => transform.TransformDirection(localNormal).normalized;

        private void Awake()
        {
            glass = GetComponent<Renderer>();
            material = glass.material;
            material.SetFloat(HasReflectionId, 0f);
        }

        private void OnDisable()
        {
            if (mirrorCamera != null) mirrorCamera.enabled = false;
        }

        private void OnDestroy()
        {
            if (mirrorCamera != null) Destroy(mirrorCamera.gameObject);
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
            if (material != null) Destroy(material);
        }

        private void LateUpdate()
        {
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            bool show = viewer != null && InView()
                && (viewer.transform.position - transform.position).sqrMagnitude < maxDistance * maxDistance
                && Vector3.Dot(viewer.transform.position - transform.position, WorldNormal) > 0f;
            if (!show)
            {
                if (mirrorCamera != null) mirrorCamera.enabled = false;
                return;
            }

            EnsureCamera();
            EnsureTexture();

            // The viewer's pose mirrored across the glass. A plain (right-handed) camera there sees the true
            // reflection flipped left to right; the shader flips it back.
            Vector3 n = WorldNormal;
            Vector3 p = transform.position;
            Transform v = viewer.transform;
            Vector3 pos = v.position - 2f * Vector3.Dot(v.position - p, n) * n;
            Vector3 fwd = Vector3.Reflect(v.forward, n);
            Vector3 up = Vector3.Reflect(v.up, n);
            mirrorCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));

            mirrorCamera.fieldOfView = viewer.fieldOfView;
            mirrorCamera.aspect = viewer.aspect;
            mirrorCamera.nearClipPlane = viewer.nearClipPlane;
            mirrorCamera.farClipPlane = viewer.farClipPlane;
            mirrorCamera.clearFlags = viewer.clearFlags;
            mirrorCamera.backgroundColor = viewer.backgroundColor;
            mirrorCamera.depth = viewer.depth - 1f;
            mirrorCamera.ResetProjectionMatrix();

            // Only what lies in front of the glass: an oblique near plane on the mirror
            Matrix4x4 view = mirrorCamera.worldToCameraMatrix;
            Vector3 cp = view.MultiplyPoint(p + n * clipOffset);
            Vector3 cn = view.MultiplyVector(n).normalized;
            var plane = new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn));
            mirrorCamera.projectionMatrix = mirrorCamera.CalculateObliqueMatrix(plane);
            mirrorCamera.enabled = true;
        }

        private bool InView()
        {
            GeometryUtility.CalculateFrustumPlanes(viewer, frustum);
            return GeometryUtility.TestPlanesAABB(frustum, glass.bounds);
        }

        private void EnsureCamera()
        {
            if (mirrorCamera != null) return;
            var go = new GameObject("Mirror_Reflection_Camera");
            go.hideFlags = HideFlags.DontSave;
            mirrorCamera = go.AddComponent<Camera>();
            mirrorCamera.enabled = false;
            mirrorCamera.cullingMask = ~(1 << 5); // no UI
            mirrorCamera.allowMSAA = false;
            mirrorCamera.allowHDR = false;
            UniversalAdditionalCameraData data = mirrorCamera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderShadows = false;
                data.renderPostProcessing = false;
                data.requiresColorTexture = false;
                data.requiresDepthTexture = false;
            }
        }

        private void EnsureTexture()
        {
            int h = Mathf.Max(64, textureHeight);
            int w = Mathf.Max(64, Mathf.RoundToInt(h * Mathf.Max(0.5f, viewer.aspect)));
            if (texture != null && texture.width == w && texture.height == h) return;
            if (texture != null)
            {
                mirrorCamera.targetTexture = null;
                texture.Release();
                Destroy(texture);
            }
            texture = new RenderTexture(w, h, 24) { name = "Mirror_Reflection" };
            mirrorCamera.targetTexture = texture;
            material.SetTexture(ReflectionTexId, texture);
            material.SetFloat(HasReflectionId, 1f);
        }
    }
}
