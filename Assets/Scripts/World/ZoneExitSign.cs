using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Dresses a zone exit (a <see cref="DoorTeleporter"/> that loads another scene): a hanging wooden sign
    /// floats above the arch with the destination's name, always turned to the camera, and a brazier with a
    /// flickering flame burns on top of each gate pillar. Built at runtime from the door's own geometry, so
    /// every zone (the hand-edited village included) gets it without touching a scene.
    /// </summary>
    public class ZoneExitSign : MonoBehaviour
    {
        public const string SignName = "Zone_Exit_Sign";

        private static readonly Color PlankColor = new Color(0.23f, 0.14f, 0.08f, 1f);
        private static readonly Color TrimColor = new Color(0.62f, 0.47f, 0.2f, 1f);
        private static readonly Color IronColor = new Color(0.16f, 0.15f, 0.15f, 1f);
        private static readonly Color FlameCore = new Color(1f, 0.85f, 0.4f, 1f);
        private static readonly Color FlameEdge = new Color(1f, 0.42f, 0.08f, 1f);
        private static readonly Color TorchLight = new Color(1f, 0.62f, 0.3f, 1f);

        private const float SignGap = 1.9f;
        private const float BobHeight = 0.12f;

        private static Material s_plankMaterial;
        private static Material s_trimMaterial;
        private static Material s_ironMaterial;
        private static Camera s_camera;

        private Transform board;
        private Vector3 boardBase;
        private Light glow;
        private float glowBase;
        private float seed;

        /// <summary>The name written on the sign.</summary>
        public string Destination { get; private set; }

        /// <summary>The sign's text component (for tests and tweaks).</summary>
        public TextMeshPro Label { get; private set; }

        /// <summary>Braziers placed on the gate's pillars.</summary>
        public int BrazierCount { get; private set; }

        /// <summary>Adds the sign to a scene-changing door (once). Doors inside a zone get none.</summary>
        public static ZoneExitSign Attach(DoorTeleporter door)
        {
            if (door == null || string.IsNullOrEmpty(door.TargetSceneName)) return null;
            Transform root = GateRoot(door.transform);
            Transform existing = root.Find(SignName);
            if (existing != null && existing.TryGetComponent(out ZoneExitSign old)) return old;

            GameObject go = new GameObject(SignName);
            go.layer = 2; // Ignore Raycast
            go.transform.SetParent(root, false);
            ZoneExitSign sign = go.AddComponent<ZoneExitSign>();
            sign.Build(door, root);
            return sign;
        }

        /// <summary>Name shown for a door's destination: the zone banner's name, or the scene's.</summary>
        public static string DestinationName(DoorTeleporter door)
        {
            if (door == null) return string.Empty;
            if (SecretForestPath.IsScene(door.TargetSceneName)) return SecretForestPath.DisplayName;
            if (!string.IsNullOrEmpty(door.DestinationZone) && Enum.TryParse(door.DestinationZone, true, out GameLocation loc))
            {
                return DungeonMapUIController.GetZoneDisplayName(loc);
            }

            string scene = door.TargetSceneName ?? string.Empty;
            if (scene.Contains("Village")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.Village);
            if (scene.Contains("Forest")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.Forest);
            if (scene.Contains("Courtyard")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.Courtyard);
            if (scene.Contains("Library")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.Library);
            if (scene.Contains("CastleHall")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.CastleHall);
            if (scene.Contains("Tower")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.Tower);
            if (scene.Contains("Throne")) return DungeonMapUIController.GetZoneDisplayName(GameLocation.CrownHall);
            return scene.Replace('_', ' ');
        }

        /// <summary>The door's gate group ("Door_X" holding the trigger, arch and pillars), or the door itself.</summary>
        private static Transform GateRoot(Transform door)
        {
            Transform parent = door.parent;
            if (parent != null && parent.name.StartsWith("Door_", StringComparison.Ordinal)
                && (parent.Find("Arch_Model") != null || parent.GetComponentInChildren<SwingDoor>(true) != null))
            {
                return parent;
            }
            return door;
        }

        private void Build(DoorTeleporter door, Transform root)
        {
            seed = UnityEngine.Random.value * 100f;
            EnsureMaterials();
            Destination = DestinationName(door);

            // The arch and pillars set where the sign hangs; a bare trigger (the village gate) uses its box
            Bounds gate;
            bool hasModel = TryRendererBounds(root, out gate);
            Collider trigger = door.GetComponent<Collider>();
            if (!hasModel)
            {
                gate = trigger != null ? trigger.bounds : new Bounds(door.transform.position, new Vector3(4f, 3f, 1f));
            }
            Vector3 center = trigger != null && trigger.enabled ? trigger.bounds.center : gate.center;
            transform.position = new Vector3(center.x, gate.max.y + SignGap, center.z);

            BuildBoard();
            BuildBraziers(root);
        }

        private void BuildBoard()
        {
            board = new GameObject("Board").transform;
            board.gameObject.layer = 2;
            board.SetParent(transform, false);
            boardBase = Vector3.zero;

            GameObject textGo = new GameObject("Destination_Text");
            textGo.layer = 2;
            textGo.transform.SetParent(board, false);
            textGo.transform.localPosition = new Vector3(0f, -0.08f, -0.09f);
            TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = Destination;
            tmp.fontSize = 6.5f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UITheme.GoldAccent;
            tmp.outlineColor = new Color32(20, 10, 4, 255);
            tmp.outlineWidth = 0.22f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(12f, 1.4f);
            Label = tmp;

            GameObject capGo = new GameObject("Caption_Text");
            capGo.layer = 2;
            capGo.transform.SetParent(board, false);
            capGo.transform.localPosition = new Vector3(0f, 0.5f, -0.09f);
            TextMeshPro cap = capGo.AddComponent<TextMeshPro>();
            cap.text = "TO";
            cap.fontSize = 3f;
            cap.fontStyle = FontStyles.Bold;
            cap.characterSpacing = 12f;
            cap.alignment = TextAlignmentOptions.Center;
            cap.color = UITheme.ParchmentText;
            cap.outlineColor = new Color32(20, 10, 4, 255);
            cap.outlineWidth = 0.2f;
            cap.rectTransform.sizeDelta = new Vector2(4f, 0.6f);

            Vector2 size = tmp.GetPreferredValues(Destination, 30f, 2f);
            float width = Mathf.Clamp(size.x + 1.1f, 3.2f, 11f);
            const float height = 1.55f;

            Cube("Trim", new Vector3(0f, 0.12f, 0.02f), new Vector3(width + 0.22f, height + 0.22f, 0.12f), s_trimMaterial);
            Cube("Plank", new Vector3(0f, 0.12f, 0f), new Vector3(width, height, 0.16f), s_plankMaterial);

            // Two short chains up to an iron bar make it read as a hanging sign
            Cube("Hanger_Bar", new Vector3(0f, height * 0.5f + 0.75f, 0.03f), new Vector3(width * 0.7f, 0.1f, 0.1f), s_ironMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                Cube("Chain", new Vector3(side * width * 0.3f, height * 0.5f + 0.45f, 0.03f), new Vector3(0.06f, 0.55f, 0.06f), s_ironMaterial);
            }
        }

        private void BuildBraziers(Transform root)
        {
            Light first = null;
            foreach (Vector3 top in PillarTops(root))
            {
                GameObject brazier = new GameObject("Brazier");
                brazier.layer = 2;
                brazier.transform.SetParent(transform, true);
                brazier.transform.position = top;

                Primitive(PrimitiveType.Cylinder, "Bowl", brazier.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.95f, 0.14f, 0.95f), s_ironMaterial);
                Primitive(PrimitiveType.Cylinder, "Coals", brazier.transform, new Vector3(0f, 0.26f, 0f), new Vector3(0.75f, 0.03f, 0.75f), s_trimMaterial);
                CreateFlame(brazier.transform);
                BrazierCount++;

                if (first == null)
                {
                    // One warm light for the whole gate, between the braziers
                    GameObject lightGo = new GameObject("Gate_Light");
                    lightGo.layer = 2;
                    lightGo.transform.SetParent(transform, false);
                    lightGo.transform.position = new Vector3(transform.position.x, top.y + 0.6f, transform.position.z);
                    first = lightGo.AddComponent<Light>();
                    first.type = LightType.Point;
                    first.shadows = LightShadows.None;
                    first.color = TorchLight;
                    first.range = 9f;
                    first.intensity = 2.2f;
                }
            }

            glow = first;
            glowBase = first != null ? first.intensity : 0f;
        }

        /// <summary>Name prefix of the empty marks on top of a castle door model's pillars, where the braziers stand.</summary>
        public const string PillarTopPrefix = "PillarTop";

        /// <summary>Tops of the gate's pillars: the old DecoPillar props, or the PillarTop marks on a castle door model.</summary>
        private static List<Vector3> PillarTops(Transform root)
        {
            var tops = new List<Vector3>(2);
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("DecoPillar", StringComparison.Ordinal)) continue;
                if (!TryRendererBounds(child, out Bounds pillar)) continue;
                tops.Add(new Vector3(pillar.center.x, pillar.max.y, pillar.center.z));
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith(PillarTopPrefix, StringComparison.Ordinal)) tops.Add(t.position);
            }
            return tops;
        }

        private static void CreateFlame(Transform parent)
        {
            GameObject go = new GameObject("Flame");
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(FlameCore, FlameEdge);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 26f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.22f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = PickupBeacon.SoftGlowMaterial;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
        }

        private void LateUpdate()
        {
            float t = Time.time;
            board.localPosition = boardBase + new Vector3(0f, Mathf.Sin(t * 1.3f + seed) * BobHeight, 0f);

            if (glow != null)
            {
                glow.intensity = glowBase * (0.85f + 0.3f * Mathf.PerlinNoise(t * 3.1f, seed));
            }

            if (s_camera == null || !s_camera.isActiveAndEnabled) s_camera = Camera.main;
            if (s_camera != null) board.rotation = s_camera.transform.rotation;
        }

        #region Helpers

        private static bool TryRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponentInParent<ZoneExitSign>() != null) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        private void Cube(string partName, Vector3 localPos, Vector3 scale, Material mat)
        {
            Primitive(PrimitiveType.Cube, partName, board, localPos, scale, mat);
        }

        private static void Primitive(PrimitiveType type, string partName, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.layer = 2;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void EnsureMaterials()
        {
            if (s_plankMaterial != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            s_plankMaterial = MakeLit(lit, "ZoneExitSign_Plank", PlankColor, 0.1f);
            s_trimMaterial = MakeLit(lit, "ZoneExitSign_Trim", TrimColor, 0.45f);
            s_ironMaterial = MakeLit(lit, "ZoneExitSign_Iron", IronColor, 0.3f);
        }

        private static Material MakeLit(Shader shader, string matName, Color color, float smoothness)
        {
            Material m = new Material(shader) { name = matName };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.color = color;
            return m;
        }

        #endregion
    }
}
