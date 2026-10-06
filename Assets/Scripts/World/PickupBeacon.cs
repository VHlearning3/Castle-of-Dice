using System;
using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A soft pulsing glow that makes a pickup easy to spot: a column of light rising from it, a halo
    /// and a ring on the ground, and a few motes drifting up. Each pickup type attaches one in Awake
    /// (<see cref="Attach"/>) with a check for "still there"; the beacon hides itself for good once the
    /// check says the item has been taken. Built from code (no prefab or scene edits), so it also works
    /// in the hand-edited village and on loot that drops during play.
    /// </summary>
    public class PickupBeacon : MonoBehaviour
    {
        /// <summary>Gold, scrap, chests and treasure.</summary>
        public static readonly Color TreasureColor = new Color(1f, 0.82f, 0.32f, 1f);

        /// <summary>Herbs and other plants to gather.</summary>
        public static readonly Color HerbColor = new Color(0.55f, 1f, 0.45f, 1f);

        /// <summary>Unread letters and pages.</summary>
        public static readonly Color NoteColor = new Color(1f, 0.93f, 0.75f, 1f);

        public const string BeaconName = "Pickup_Beacon";
        private const string MaterialResource = "VFX/M_PickupBeacon";

        private const float BeamHeight = 3.6f;
        private const float BeamWidth = 0.75f;
        private const float HaloSize = 1.5f;
        private const float RingSize = 1.9f;
        private const float PulseSpeed = 2.6f;

        private static Material s_beamMaterial;
        private static Material s_dotMaterial;
        private static Material s_ringMaterial;
        private static Mesh s_beamMesh;
        private static Mesh s_quadMesh;
        private static Camera s_camera;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Func<bool> isAvailable;
        private Color color = TreasureColor;
        private float scale = 1f;
        private float phase;
        private Transform beam;
        private Transform halo;
        private Transform ring;
        private Renderer beamRenderer;
        private Renderer haloRenderer;
        private Renderer ringRenderer;
        private ParticleSystem motes;
        private MaterialPropertyBlock block;

        /// <summary>Whether the item is still there to be picked up (the beacon shows).</summary>
        public bool IsShowing => gameObject.activeSelf;

        /// <summary>Glow colour.</summary>
        public Color Color => color;

        /// <summary>
        /// Adds a beacon under <paramref name="owner"/> (once). <paramref name="available"/> is polled each frame;
        /// when it returns false the beacon hides for good.
        /// </summary>
        public static PickupBeacon Attach(Transform owner, Color tint, Func<bool> available, float size = 1f)
        {
            if (owner == null) return null;
            Transform existing = owner.Find(BeaconName);
            if (existing != null && existing.TryGetComponent(out PickupBeacon old)) return old;

            GameObject go = new GameObject(BeaconName);
            go.layer = 2; // Ignore Raycast: never steals a click from the pickup
            go.transform.SetParent(owner, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Keep the glow the same size whatever the pickup's own scale is
            Vector3 lossy = owner.lossyScale;
            go.transform.localScale = new Vector3(SafeInverse(lossy.x), SafeInverse(lossy.y), SafeInverse(lossy.z));

            PickupBeacon beacon = go.AddComponent<PickupBeacon>();
            beacon.isAvailable = available;
            beacon.color = tint;
            beacon.scale = Mathf.Max(0.3f, size);
            beacon.Build();
            if (available != null && !available()) go.SetActive(false);
            return beacon;
        }

        private static float SafeInverse(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;

        private void Build()
        {
            EnsureSharedAssets();
            block = new MaterialPropertyBlock();
            phase = UnityEngine.Random.value * Mathf.PI * 2f;

            beam = CreatePart("Beam", s_beamMesh, s_beamMaterial, out beamRenderer);
            beam.localScale = new Vector3(BeamWidth * scale, BeamHeight * scale, 1f);

            halo = CreatePart("Halo", s_quadMesh, s_dotMaterial, out haloRenderer);
            halo.localPosition = new Vector3(0f, 0.45f * scale, 0f);

            ring = CreatePart("Ring", s_quadMesh, s_ringMaterial, out ringRenderer);
            ring.localPosition = new Vector3(0f, 0.04f, 0f);
            ring.localRotation = Quaternion.Euler(90f, 0f, 0f);

            motes = CreateMotes();
            ApplyColor(1f);
        }

        private Transform CreatePart(string partName, Mesh mesh, Material material, out Renderer renderer)
        {
            GameObject part = new GameObject(partName);
            part.layer = 2;
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = part.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer = mr;
            return part.transform;
        }

        private ParticleSystem CreateMotes()
        {
            GameObject go = new GameObject("Motes");
            go.layer = 2;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f * scale, 0.14f * scale);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 6f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f * scale;

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = s_dotMaterial;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;

            ps.Play();
            return ps;
        }

        private void LateUpdate()
        {
            if (isAvailable != null && !isAvailable())
            {
                gameObject.SetActive(false);
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed + phase);
            ApplyColor(0.65f + 0.35f * pulse);

            float haloScale = HaloSize * scale * (0.85f + 0.25f * pulse);
            halo.localScale = new Vector3(haloScale, haloScale, 1f);
            float ringScale = RingSize * scale * (0.8f + 0.35f * pulse);
            ring.localScale = new Vector3(ringScale, ringScale, 1f);

            if (s_camera == null || !s_camera.isActiveAndEnabled) s_camera = Camera.main;
            if (s_camera == null) return;

            // The halo faces the camera; the beam only turns about the vertical so it stays upright
            Vector3 camForward = s_camera.transform.forward;
            halo.rotation = Quaternion.LookRotation(camForward, s_camera.transform.up);
            Vector3 flat = new Vector3(camForward.x, 0f, camForward.z);
            if (flat.sqrMagnitude > 0.0001f) beam.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }

        private void ApplyColor(float intensity)
        {
            Color c = color;
            c.a = intensity;
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            beamRenderer.SetPropertyBlock(block);
            haloRenderer.SetPropertyBlock(block);
            c.a = intensity * 0.8f;
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            ringRenderer.SetPropertyBlock(block);
        }

        #region Shared Assets

        /// <summary>The soft round glow (additive where the Resources material exists), shared with other small effects.</summary>
        public static Material SoftGlowMaterial
        {
            get
            {
                EnsureSharedAssets();
                return s_dotMaterial;
            }
        }

        private static void EnsureSharedAssets()
        {
            if (s_beamMaterial != null) return;

            Material template = Resources.Load<Material>(MaterialResource);
            if (template == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                template = new Material(shader);
            }

            s_beamMaterial = MakeMaterial(template, "PickupBeacon_Beam", BuildBeamTexture());
            s_dotMaterial = MakeMaterial(template, "PickupBeacon_Dot", BuildDotTexture());
            s_ringMaterial = MakeMaterial(template, "PickupBeacon_Ring", BuildRingTexture());
            s_beamMesh = BuildQuad(0f);
            s_quadMesh = BuildQuad(-0.5f);
        }

        private static Material MakeMaterial(Material template, string matName, Texture2D tex)
        {
            Material m = new Material(template) { name = matName, mainTexture = tex };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            return m;
        }

        /// <summary>A unit quad facing -z; <paramref name="bottom"/> 0 puts the pivot at its foot (for the beam).</summary>
        private static Mesh BuildQuad(float bottom)
        {
            Mesh mesh = new Mesh { name = bottom == 0f ? "PickupBeacon_Beam" : "PickupBeacon_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, bottom, 0f), new Vector3(0.5f, bottom, 0f),
                new Vector3(-0.5f, bottom + 1f, 0f), new Vector3(0.5f, bottom + 1f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D NewTexture(int w, int h, string texName)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        /// <summary>Bright in the middle, soft at the sides, fading out towards the top.</summary>
        private static Texture2D BuildBeamTexture()
        {
            const int w = 32, h = 64;
            Texture2D tex = NewTexture(w, h, "PickupBeacon_BeamTex");
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float vertical = Mathf.Pow(1f - v, 1.6f) * Mathf.Clamp01(v * 12f);
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs(x / (float)(w - 1) - 0.5f) * 2f;
                    float across = Mathf.Pow(Mathf.Clamp01(1f - u), 2.2f);
                    px[y * w + x] = new Color(1f, 1f, 1f, across * vertical * 0.95f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildDotTexture()
        {
            const int s = 64;
            Texture2D tex = NewTexture(s, s, "PickupBeacon_DotTex");
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildRingTexture()
        {
            const int s = 64;
            Texture2D tex = NewTexture(s, s, "PickupBeacon_RingTex");
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.16f);
                    float fill = Mathf.Clamp01(1f - d) * 0.25f;
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * band + fill));
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        #endregion
    }
}
