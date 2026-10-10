using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Touches to The Foaming Tankard made when Zone_Tavern loads (Vili, 2026-10-10), so the scene Vili edits by hand
    /// stays untouched: the castle door's pale stone arch becomes timber to match the plank walls, and a soft, flickering
    /// fire burns in the stone hearth (flames, a few sparks and a thin smoke).
    /// </summary>
    public static class TavernDressing
    {
        public const string DoorGroupName = "Door_Tavern_Village";
        public const string DoorFrameName = "Door_Single_Frame";
        public const string FireboxName = "Fireplace_Opening";
        public const string HearthFireName = "Tavern_HearthFire";

        /// <summary>The door frame mesh with box UVs, made by TavernFindsAssets from the castle door's frame.</summary>
        public const string WoodFrameMeshPath = "Tavern/TavernDoorFrame_Wood";
        public const string WoodFrameMaterialPath = "Tavern/M_TavernDoor_Frame";
        public const string WoodTrimMaterialPath = "Tavern/M_TavernDoor_FrameTrim";

        private static readonly Color FlameCore = new Color(1f, 0.86f, 0.45f, 1f);
        private static readonly Color FlameOrange = new Color(1f, 0.5f, 0.12f, 1f);
        private static readonly Color FlameTip = new Color(0.75f, 0.16f, 0.04f, 1f);

        private static Material flameMaterial;
        private static Material sparkMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != TavernEntrance.SceneName) return;
            Apply();
        }

        /// <summary>Makes the door frame wooden and lights the hearth. Safe to call twice.</summary>
        public static void Apply()
        {
            MakeDoorFrameWooden();
            LightHearth();
        }

        /// <summary>Swaps the stone arch around the tavern door for the same arch in timber. True when it is wooden.</summary>
        public static bool MakeDoorFrameWooden()
        {
            GameObject group = GameObject.Find(DoorGroupName);
            if (group == null) return false;
            Transform frame = FindDeep(group.transform, DoorFrameName);
            if (frame == null) return false;
            MeshFilter filter = frame.GetComponent<MeshFilter>();
            MeshRenderer renderer = frame.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return false;

            Mesh wood = Resources.Load<Mesh>(WoodFrameMeshPath);
            Material plank = Resources.Load<Material>(WoodFrameMaterialPath);
            Material trim = Resources.Load<Material>(WoodTrimMaterialPath);
            if (wood == null || plank == null || trim == null)
            {
                Debug.LogWarning("[TavernDressing] Wooden door frame assets missing in Resources/Tavern.");
                return false;
            }
            if (filter.sharedMesh == wood) return true;

            // The frame's submeshes are stone, then stone trim (Door_Single.fbx)
            filter.sharedMesh = wood;
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = i == 0 ? plank : trim;
            renderer.sharedMaterials = mats;
            return true;
        }

        /// <summary>Makes the fire in the hearth (once). Returns its root, or null when the tavern has no firebox.</summary>
        public static GameObject LightHearth()
        {
            GameObject existing = GameObject.Find(HearthFireName);
            if (existing != null) return existing;
            GameObject box = GameObject.Find(FireboxName);
            Renderer boxRenderer = box != null ? box.GetComponent<Renderer>() : null;
            if (boxRenderer == null) return null;

            Bounds b = boxRenderer.bounds;
            var root = new GameObject(HearthFireName);
            root.layer = 2; // Ignore Raycast
            // On the embers, a little in front of the back wall; the box opens towards +x
            root.transform.position = new Vector3(b.center.x + 0.05f, b.min.y + 0.24f, b.center.z);

            if (flameMaterial == null)
            {
                // Sprites/Default is in Always Included Shaders, so it survives WebGL shader stripping (as AbilityVfx)
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                flameMaterial = new Material(shader) { name = "Tavern_HearthFlame", mainTexture = FlameTongue() };
                sparkMaterial = new Material(shader) { name = "Tavern_HearthSpark", mainTexture = SoftDot() };
            }

            float width = Mathf.Max(0.6f, b.size.z - 0.9f);
            Flames(root.transform, width);
            Core(root.transform, width);
            Sparks(root.transform, width);
            // Start only now that every module is set: particles emitted earlier would keep the defaults (big white dots)
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
            return root;
        }

        /// <summary>Tall orange tongues licking up from the logs.</summary>
        private static void Flames(Transform parent, float width)
        {
            ParticleSystem ps = System(parent, "Flames", 50, new Vector3(0.4f, 0.05f, width), flameMaterial);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.8f, 1.25f);
            main.startSizeZ = 1f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 34f;
            Rise(ps, 0.5f, 0.9f);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.8f;
            Ramp(ps, new[] { FlameCore, FlameOrange, FlameTip },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.15f), new GradientAlphaKey(0.45f, 0.65f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.35f)));
        }

        /// <summary>Small bright flames low on the embers, the hot heart of the fire.</summary>
        private static void Core(Transform parent, float width)
        {
            ParticleSystem ps = System(parent, "Core", 24, new Vector3(0.3f, 0.05f, width * 0.8f), flameMaterial);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.32f, 0.5f);
            main.startSizeZ = 1f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 30f;
            Rise(ps, 0.3f, 0.55f);
            Ramp(ps, new[] { new Color(1f, 0.95f, 0.7f), FlameCore, FlameOrange },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
        }

        /// <summary>A few sparks drifting up the chimney.</summary>
        private static void Sparks(Transform parent, float width)
        {
            ParticleSystem ps = System(parent, "Sparks", 16, new Vector3(0.35f, 0.1f, width), sparkMaterial);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlameCore, FlameOrange);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 4f;
            Rise(ps, 0.9f, 1.6f);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 2f;
            Ramp(ps, new[] { Color.white, Color.white, Color.white },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        }

        private static ParticleSystem System(Transform parent, string name, int maxParticles, Vector3 box, Material material)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startSpeed = 0f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 2f;
            return ps;
        }

        private static void Rise(ParticleSystem ps, float min, float max)
        {
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(min, max);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        private static void Ramp(ParticleSystem ps, Color[] colors, GradientAlphaKey[] alphas)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(colors[0], 0f), new GradientColorKey(colors[1], 0.45f), new GradientColorKey(colors[2], 1f) }, alphas);
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>A flame tongue: wide and soft at the bottom, narrowing to a point at the top.</summary>
        private static Texture2D FlameTongue()
        {
            const int w = 32, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tavern_FlameTongue", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;                                  // 0 bottom .. 1 top
                float half = 0.95f * Mathf.Pow(1f - t, 0.5f) * Mathf.Clamp01(t * 5f + 0.4f);
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float a = half > 0.001f ? Mathf.Clamp01(1f - u / half) : 0f;
                    a = a * a * (3f - 2f * a);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>A soft round dot (bright middle, feathered edge) for the flames.</summary>
        private static Texture2D SoftDot()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Tavern_FireDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
