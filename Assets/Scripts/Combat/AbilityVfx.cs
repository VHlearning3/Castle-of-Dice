using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Short, purely visual effects for the combat moves that relocate a unit instantly:
    /// Blink (arcane flash out and in), Shadow Step (dark smoke at both ends), War Cry
    /// (shockwave ring, camera shake, pushed enemies sliding back) and Malakor's teleport (purple vortex).
    /// Game logic has already moved the unit when an effect starts; the effect only animates the
    /// unit's model and plays particles, so it never delays turns or changes occupancy.
    /// Built at runtime from pooled particle systems and an always-included shader (WebGL safe).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class AbilityVfx : MonoBehaviour
    {
        #region Constants

        private const float BlinkAppearDelay = 0.12f;
        private const float BlinkAppearDuration = 0.22f;
        private const float ShadowStepAppearDelay = 0.15f;
        private const float ShadowStepAppearDuration = 0.3f;
        private const float MalakorAppearDelay = 0.18f;
        private const float MalakorAppearDuration = 0.35f;
        private const float PushSlideDuration = 0.28f;
        private const float WarCryRingDuration = 0.4f;
        private const int RingSegments = 48;
        private const float HiddenScale = 0.01f;

        private static readonly Color ArcaneCore = new Color(0.75f, 0.92f, 1f, 1f);
        private static readonly Color ArcaneBlue = new Color(0.25f, 0.6f, 1f, 1f);
        private static readonly Color ShadowSmokeColor = new Color(0.08f, 0.04f, 0.12f, 0.85f);
        private static readonly Color ShadowWispColor = new Color(0.55f, 0.25f, 0.85f, 1f);
        private static readonly Color VortexPurple = new Color(0.7f, 0.2f, 1f, 1f);
        private static readonly Color VortexCore = new Color(0.95f, 0.7f, 1f, 1f);
        private static readonly Color ShockwaveColor = new Color(1f, 0.8f, 0.45f, 1f);
        private static readonly Color DustColor = new Color(0.55f, 0.47f, 0.38f, 0.75f);

        #endregion

        #region Singleton

        private static AbilityVfx instance;

        /// <summary>Returns the scene's effect player, creating it on first use. Null outside Play Mode.</summary>
        private static AbilityVfx GetOrCreate()
        {
            if (!Application.isPlaying) return null;
            if (instance == null)
            {
                new GameObject("_AbilityVfx").AddComponent<AbilityVfx>();
            }
            return instance;
        }

        #endregion

        #region State

        private class VisualAnim
        {
            public Vector3 BaseScale;
            public Quaternion BaseRotation;
            public Coroutine Routine;
        }

        private readonly Dictionary<Transform, VisualAnim> visualAnims = new Dictionary<Transform, VisualAnim>();
        private readonly Dictionary<CombatUnit, Coroutine> slides = new Dictionary<CombatUnit, Coroutine>();

        private Material particleMaterial;
        private Material lineMaterial;
        private ParticleSystem sparks;
        private ParticleSystem flash;
        private ParticleSystem smoke;
        private ParticleSystem wisps;
        private ParticleSystem dust;
        private ParticleSystem vortexOut;
        private ParticleSystem vortexIn;
        private readonly LineRenderer[] rings = new LineRenderer[2];
        private int nextRing;

        private Transform shakeCamera;
        private Vector3 shakeOffset;
        private float shakeTimeLeft;
        private float shakeDuration;
        private float shakeAmplitude;

        #endregion

        #region Public API

        /// <summary>Mage Blink: arcane burst where the mage stood, then a flash as they pop back in.</summary>
        public static void PlayBlink(CombatUnit unit, Vector3 fromWorld)
        {
            AbilityVfx vfx = GetOrCreate();
            if (vfx == null || unit == null) return;

            Vector3 fromGround = vfx.GroundPoint(unit, fromWorld);
            Vector3 toGround = vfx.GroundPoint(unit, unit.transform.position);

            vfx.EmitArcaneBurst(fromGround, ArcaneBlue);
            vfx.StartCoroutine(vfx.EmitArcaneBurstLater(toGround, BlinkAppearDelay));
            vfx.AnimateAppear(unit, BlinkAppearDelay, BlinkAppearDuration, new Vector3(0.1f, 0.1f, 0.1f), 0f, 1.12f);
            PlaySound(SFXClipType.SpellCast, fromWorld, 0.8f);
        }

        /// <summary>Rogue Shadow Step: dark smoke at both ends; the rogue rises tall and thin out of the smoke.</summary>
        public static void PlayShadowStep(CombatUnit unit, Vector3 fromWorld)
        {
            AbilityVfx vfx = GetOrCreate();
            if (vfx == null || unit == null) return;

            Vector3 fromGround = vfx.GroundPoint(unit, fromWorld);
            Vector3 toGround = vfx.GroundPoint(unit, unit.transform.position);

            vfx.EmitShadowSmoke(fromGround);
            vfx.StartCoroutine(vfx.EmitShadowSmokeLater(toGround, ShadowStepAppearDelay * 0.5f));
            vfx.AnimateAppear(unit, ShadowStepAppearDelay, ShadowStepAppearDuration, new Vector3(0.15f, 1.35f, 0.15f), 0f, 1.06f);
            PlaySound(SFXClipType.SpellCast, fromWorld, 0.45f);
        }

        /// <summary>Malakor's teleport: a purple vortex swallows him and another spits him out spinning.</summary>
        public static void PlayMalakorTeleport(CombatUnit boss, Vector3 fromWorld)
        {
            AbilityVfx vfx = GetOrCreate();
            if (vfx == null || boss == null) return;

            Vector3 fromGround = vfx.GroundPoint(boss, fromWorld);
            Vector3 toGround = vfx.GroundPoint(boss, boss.transform.position);

            vfx.EmitVortex(vfx.vortexOut, fromGround);
            vfx.StartCoroutine(vfx.EmitVortexLater(toGround, MalakorAppearDelay * 0.4f));
            vfx.AnimateAppear(boss, MalakorAppearDelay, MalakorAppearDuration, new Vector3(0.1f, 0.1f, 0.1f), 360f, 1.08f);
            PlaySound(SFXClipType.SpellCast, fromWorld, 0.9f);
        }

        /// <summary>War Cry: a shockwave ring and dust burst around the warrior, a roar pulse and a camera shake.</summary>
        public static void PlayWarCry(CombatUnit caster)
        {
            AbilityVfx vfx = GetOrCreate();
            if (vfx == null || caster == null) return;

            Vector3 ground = vfx.GroundPoint(caster, caster.transform.position);
            float tile = GridManager.Instance != null ? GridManager.Instance.EffectiveTileSize : 1.6f;

            vfx.StartCoroutine(vfx.ExpandRing(ground + Vector3.up * 0.08f, 0.3f, tile * 1.7f));
            vfx.EmitDustRing(ground, tile * 0.45f, 28, 3.5f);
            vfx.flash.Emit(vfx.FlashParams(ground + Vector3.up * 0.9f, ShockwaveColor, 2.6f), 1);
            vfx.AnimatePunch(caster, 1.15f, 0.3f);
            vfx.Shake(0.28f, 0.14f);
        }

        /// <summary>
        /// War Cry knockback: the pushed unit's tile has already changed; slide its body back from
        /// <paramref name="fromWorld"/> to where it now stands, kicking up dust.
        /// </summary>
        public static void PlayPushSlide(CombatUnit unit, Vector3 fromWorld)
        {
            AbilityVfx vfx = GetOrCreate();
            if (vfx == null || unit == null) return;
            if ((unit.transform.position - fromWorld).sqrMagnitude < 0.0001f) return;

            if (vfx.slides.TryGetValue(unit, out Coroutine running) && running != null)
            {
                vfx.StopCoroutine(running);
            }
            vfx.slides[unit] = vfx.StartCoroutine(vfx.SlideRoutine(unit, fromWorld, unit.transform.position));
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            BuildEffects();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (particleMaterial != null) Destroy(particleMaterial);
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void OnDisable()
        {
            // Never leave a model shrunken, spun or mid-slide if the scene tears down mid-effect
            foreach (KeyValuePair<Transform, VisualAnim> pair in visualAnims)
            {
                if (pair.Key == null) continue;
                pair.Key.localScale = pair.Value.BaseScale;
                pair.Key.localRotation = pair.Value.BaseRotation;
            }
            visualAnims.Clear();
            slides.Clear();
            RemoveShakeOffset();
            shakeTimeLeft = 0f;
        }

        private void Update()
        {
            // Undo last frame's shake before the camera rig computes this frame's position
            RemoveShakeOffset();
        }

        private void LateUpdate()
        {
            // Runs after CameraFollow (execution order 1000), so the offset only affects what is rendered
            if (shakeTimeLeft <= 0f) return;

            if (shakeCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null) { shakeTimeLeft = 0f; return; }
                shakeCamera = cam.transform;
            }

            shakeTimeLeft -= Time.deltaTime;
            float strength = shakeAmplitude * Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            shakeOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * strength;
            shakeOffset = shakeCamera.rotation * shakeOffset;
            shakeCamera.position += shakeOffset;
        }

        #endregion

        #region Unit Animations

        /// <summary>
        /// Hides the unit's model, then grows it back from <paramref name="startScale"/> (relative to its normal size)
        /// with a small overshoot, optionally spinning around its vertical axis.
        /// </summary>
        private void AnimateAppear(CombatUnit unit, float delay, float duration, Vector3 startScale, float spinDegrees, float overshoot)
        {
            Transform visual = GetVisualRoot(unit);
            VisualAnim anim = BeginVisualAnim(visual);
            anim.Routine = StartCoroutine(AppearRoutine(visual, anim, delay, duration, startScale, spinDegrees, overshoot));
        }

        private IEnumerator AppearRoutine(Transform visual, VisualAnim anim, float delay, float duration, Vector3 startScale, float spinDegrees, float overshoot)
        {
            visual.localScale = anim.BaseScale * HiddenScale;

            float t = 0f;
            while (t < delay)
            {
                if (visual == null) yield break;
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < duration)
            {
                if (visual == null) yield break;
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                // Ease out with a bump past full size at ~70%, settling back to 1
                float grow = 1f - (1f - p) * (1f - p);
                float bump = Mathf.Sin(p * Mathf.PI) * (overshoot - 1f);
                Vector3 relative = Vector3.LerpUnclamped(startScale, Vector3.one, grow) + Vector3.one * bump;
                visual.localScale = Vector3.Scale(anim.BaseScale, relative);

                if (spinDegrees != 0f)
                {
                    visual.localRotation = anim.BaseRotation * Quaternion.Euler(0f, spinDegrees * (1f - grow), 0f);
                }
                yield return null;
            }

            EndVisualAnim(visual, anim);
        }

        /// <summary>Quick swell and settle on the unit's model (the warrior's roar).</summary>
        private void AnimatePunch(CombatUnit unit, float peakScale, float duration)
        {
            Transform visual = GetVisualRoot(unit);
            VisualAnim anim = BeginVisualAnim(visual);
            anim.Routine = StartCoroutine(PunchRoutine(visual, anim, peakScale, duration));
        }

        private IEnumerator PunchRoutine(Transform visual, VisualAnim anim, float peakScale, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (visual == null) yield break;
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float swell = Mathf.Sin(p * Mathf.PI) * (1f - p * 0.5f);
                visual.localScale = anim.BaseScale * (1f + (peakScale - 1f) * swell);
                yield return null;
            }
            EndVisualAnim(visual, anim);
        }

        private VisualAnim BeginVisualAnim(Transform visual)
        {
            if (visualAnims.TryGetValue(visual, out VisualAnim existing))
            {
                // Restart from the model's true resting pose, not from a mid-effect frame
                if (existing.Routine != null) StopCoroutine(existing.Routine);
                visual.localScale = existing.BaseScale;
                visual.localRotation = existing.BaseRotation;
                existing.Routine = null;
                return existing;
            }

            VisualAnim anim = new VisualAnim { BaseScale = visual.localScale, BaseRotation = visual.localRotation };
            visualAnims[visual] = anim;
            return anim;
        }

        private void EndVisualAnim(Transform visual, VisualAnim anim)
        {
            visual.localScale = anim.BaseScale;
            visual.localRotation = anim.BaseRotation;
            visualAnims.Remove(visual);
        }

        private IEnumerator SlideRoutine(CombatUnit unit, Vector3 from, Vector3 to)
        {
            Transform body = unit.transform;
            CharacterController cc = unit.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (ccWasEnabled) cc.enabled = false;

            Vector3 flat = to - from;
            flat.y = 0f;
            Vector3 back = flat.sqrMagnitude > 0.0001f ? -flat.normalized : Vector3.zero;
            Vector2Int expectedTile = unit.GridPosition;

            float t = 0f;
            float nextDust = 0f;
            while (t < PushSlideDuration)
            {
                // Something else moved the unit (a new push, a walk): let it own the transform
                if (unit == null || unit.GridPosition != expectedTile) break;

                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / PushSlideDuration);
                float eased = 1f - (1f - p) * (1f - p) * (1f - p);
                body.position = Vector3.LerpUnclamped(from, to, eased);

                if (t >= nextDust && unit.gameObject.activeInHierarchy)
                {
                    nextDust = t + 0.05f;
                    EmitDustPuff(GroundPoint(unit, body.position), back);
                }
                yield return null;
            }

            if (unit != null)
            {
                if (unit.GridPosition == expectedTile) body.position = to;
                Physics.SyncTransforms();
                if (ccWasEnabled && cc != null) cc.enabled = true;
                slides.Remove(unit);
            }
        }

        /// <summary>
        /// The transform to scale or spin: the model under the Animator when it sits on a child,
        /// so the unit's own collider and CharacterController are left alone.
        /// </summary>
        private static Transform GetVisualRoot(CombatUnit unit)
        {
            Animator animator = unit.UnitAnimator;
            if (animator != null && animator.transform != unit.transform && animator.transform.IsChildOf(unit.transform))
            {
                return animator.transform;
            }
            return unit.transform;
        }

        #endregion

        #region Particle Emission

        private IEnumerator EmitArcaneBurstLater(Vector3 ground, float delay)
        {
            yield return WaitScaled(delay);
            EmitArcaneBurst(ground, ArcaneCore);
        }

        private void EmitArcaneBurst(Vector3 ground, Color flashColor)
        {
            Vector3 center = ground + Vector3.up * 0.9f;
            flash.Emit(FlashParams(center, flashColor, 2.2f), 1);

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = center,
                applyShapeToPosition = true,
                startColor = ArcaneBlue
            };
            sparks.Emit(p, 22);
            p.startColor = ArcaneCore;
            sparks.Emit(p, 14);

            // A column of motes streaking upward through the body
            for (int i = 0; i < 10; i++)
            {
                Vector2 disc = Random.insideUnitCircle * 0.35f;
                ParticleSystem.EmitParams mote = new ParticleSystem.EmitParams
                {
                    position = ground + new Vector3(disc.x, Random.Range(0f, 0.6f), disc.y),
                    velocity = Vector3.up * Random.Range(3.5f, 6f),
                    startColor = i % 2 == 0 ? ArcaneBlue : ArcaneCore,
                    startSize = Random.Range(0.06f, 0.12f),
                    startLifetime = Random.Range(0.3f, 0.45f)
                };
                sparks.Emit(mote, 1);
            }
        }

        private IEnumerator EmitShadowSmokeLater(Vector3 ground, float delay)
        {
            yield return WaitScaled(delay);
            EmitShadowSmoke(ground);
        }

        private void EmitShadowSmoke(Vector3 ground)
        {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = ground + Vector3.up * 0.4f,
                applyShapeToPosition = true
            };
            smoke.Emit(p, 22);

            p.position = ground + Vector3.up * 0.3f;
            wisps.Emit(p, 12);
        }

        private IEnumerator EmitVortexLater(Vector3 ground, float delay)
        {
            yield return WaitScaled(delay);
            EmitVortex(vortexIn, ground);
            flash.Emit(FlashParams(ground + Vector3.up * 1f, VortexCore, 2.4f), 1);
        }

        private void EmitVortex(ParticleSystem vortex, Vector3 ground)
        {
            vortex.transform.position = ground + Vector3.up * 0.15f;
            vortex.Emit(46);
            flash.Emit(FlashParams(ground + Vector3.up * 1f, VortexPurple, 1.8f), 1);

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = ground + Vector3.up * 0.2f,
                applyShapeToPosition = true
            };
            smoke.Emit(p, 8);
        }

        private void EmitDustRing(Vector3 ground, float radius, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
                {
                    position = ground + dir * radius + Vector3.up * 0.15f,
                    velocity = dir * speed * Random.Range(0.75f, 1.15f) + Vector3.up * Random.Range(0.2f, 0.8f)
                };
                dust.Emit(p, 1);
            }
        }

        private void EmitDustPuff(Vector3 ground, Vector3 back)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.25f;
                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
                {
                    position = ground + new Vector3(jitter.x, 0.1f, jitter.y),
                    velocity = back * Random.Range(0.3f, 0.9f) + Vector3.up * Random.Range(0.3f, 0.7f),
                    startSize = Random.Range(0.25f, 0.45f)
                };
                dust.Emit(p, 1);
            }
        }

        private ParticleSystem.EmitParams FlashParams(Vector3 position, Color color, float size)
        {
            return new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color,
                startSize = size
            };
        }

        private IEnumerator ExpandRing(Vector3 center, float startRadius, float endRadius)
        {
            LineRenderer ring = rings[nextRing];
            nextRing = (nextRing + 1) % rings.Length;
            ring.enabled = true;

            float t = 0f;
            while (t < WarCryRingDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / WarCryRingDuration);
                float eased = 1f - (1f - p) * (1f - p);
                float radius = Mathf.Lerp(startRadius, endRadius, eased);

                for (int i = 0; i < RingSegments; i++)
                {
                    float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
                    ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }

                Color c = ShockwaveColor;
                c.a = 1f - p;
                ring.startColor = c;
                ring.endColor = c;
                ring.widthMultiplier = Mathf.Lerp(0.35f, 0.06f, p);
                yield return null;
            }

            ring.enabled = false;
        }

        private static object WaitScaled(float seconds)
        {
            return seconds > 0f ? new WaitForSeconds(seconds) : null;
        }

        private static void PlaySound(SFXClipType clip, Vector3 position, float volume)
        {
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(clip, position, volume);
            }
        }

        #endregion

        #region Camera Shake

        private void Shake(float duration, float amplitude)
        {
            // Take back any offset still applied from last frame before switching cameras
            RemoveShakeOffset();
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeTimeLeft = shakeDuration;
            shakeAmplitude = amplitude;
            shakeCamera = null;
        }

        private void RemoveShakeOffset()
        {
            if (shakeCamera != null && shakeOffset != Vector3.zero)
            {
                shakeCamera.position -= shakeOffset;
            }
            shakeOffset = Vector3.zero;
        }

        #endregion

        #region Setup

        /// <summary>Feet position under <paramref name="worldPos"/>, on the combat grid when there is one.</summary>
        private Vector3 GroundPoint(CombatUnit unit, Vector3 worldPos)
        {
            GridManager grid = GridManager.Instance;
            float y = grid != null ? grid.GetWorldPosition(unit.GridPosition).y + 0.05f : worldPos.y - 0.9f;
            return new Vector3(worldPos.x, y, worldPos.z);
        }

        private void BuildEffects()
        {
            // Sprites/Default is in Always Included Shaders, so it survives WebGL shader stripping
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            particleMaterial = new Material(shader) { name = "AbilityVfx_Particle", mainTexture = BuildSoftDot() };
            lineMaterial = new Material(shader) { name = "AbilityVfx_Line" };

            sparks = CreateSystem("Sparks", ParticleSystemSimulationSpace.World, 200,
                lifetime: new ParticleSystem.MinMaxCurve(0.3f, 0.6f),
                speed: new ParticleSystem.MinMaxCurve(2.5f, 5.5f),
                size: new ParticleSystem.MinMaxCurve(0.07f, 0.16f),
                gravity: 0f);
            SetShapeSphere(sparks, 0.35f);
            SetFade(sparks, 1f, 0f);
            SetShrink(sparks);
            ParticleSystemRenderer sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = 0.06f;
            sparkRenderer.lengthScale = 1.5f;

            flash = CreateSystem("Flash", ParticleSystemSimulationSpace.World, 8,
                lifetime: new ParticleSystem.MinMaxCurve(0.28f),
                speed: new ParticleSystem.MinMaxCurve(0f),
                size: new ParticleSystem.MinMaxCurve(2f),
                gravity: 0f);
            SetFade(flash, 0.9f, 0f);
            ParticleSystem.SizeOverLifetimeModule flashSize = flash.sizeOverLifetime;
            flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.2f)));

            smoke = CreateSystem("ShadowSmoke", ParticleSystemSimulationSpace.World, 120,
                lifetime: new ParticleSystem.MinMaxCurve(0.6f, 1.0f),
                speed: new ParticleSystem.MinMaxCurve(0.4f, 1.3f),
                size: new ParticleSystem.MinMaxCurve(0.6f, 1.1f),
                gravity: -0.12f);
            ParticleSystem.MainModule smokeMain = smoke.main;
            smokeMain.startColor = ShadowSmokeColor;
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            SetShapeSphere(smoke, 0.45f);
            SetFade(smoke, 0.85f, 0f);
            ParticleSystem.SizeOverLifetimeModule smokeSize = smoke.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.4f)));

            wisps = CreateSystem("ShadowWisps", ParticleSystemSimulationSpace.World, 60,
                lifetime: new ParticleSystem.MinMaxCurve(0.5f, 0.8f),
                speed: new ParticleSystem.MinMaxCurve(0.3f, 0.9f),
                size: new ParticleSystem.MinMaxCurve(0.06f, 0.14f),
                gravity: -0.6f);
            ParticleSystem.MainModule wispMain = wisps.main;
            wispMain.startColor = ShadowWispColor;
            SetShapeSphere(wisps, 0.4f);
            SetFade(wisps, 1f, 0f);

            dust = CreateSystem("Dust", ParticleSystemSimulationSpace.World, 150,
                lifetime: new ParticleSystem.MinMaxCurve(0.45f, 0.75f),
                speed: new ParticleSystem.MinMaxCurve(0f),
                size: new ParticleSystem.MinMaxCurve(0.35f, 0.6f),
                gravity: 0.15f);
            ParticleSystem.MainModule dustMain = dust.main;
            dustMain.startColor = DustColor;
            dustMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.LimitVelocityOverLifetimeModule dustDrag = dust.limitVelocityOverLifetime;
            dustDrag.enabled = true;
            dustDrag.drag = new ParticleSystem.MinMaxCurve(3f);
            SetFade(dust, 0.75f, 0f);
            ParticleSystem.SizeOverLifetimeModule dustSize = dust.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));

            vortexOut = CreateVortex("VortexOut");
            vortexIn = CreateVortex("VortexIn");

            for (int i = 0; i < rings.Length; i++)
            {
                GameObject ringObj = new GameObject("ShockwaveRing" + i);
                ringObj.transform.SetParent(transform, false);
                LineRenderer ring = ringObj.AddComponent<LineRenderer>();
                ring.sharedMaterial = lineMaterial;
                ring.useWorldSpace = true;
                ring.loop = true;
                ring.positionCount = RingSegments;
                ring.numCornerVertices = 2;
                ring.alignment = LineAlignment.TransformZ;
                ringObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.enabled = false;
                rings[i] = ring;
            }
        }

        private ParticleSystem CreateVortex(string name)
        {
            ParticleSystem vortex = CreateSystem(name, ParticleSystemSimulationSpace.Local, 120,
                lifetime: new ParticleSystem.MinMaxCurve(0.55f, 0.8f),
                speed: new ParticleSystem.MinMaxCurve(0f),
                size: new ParticleSystem.MinMaxCurve(0.08f, 0.2f),
                gravity: 0f);
            ParticleSystem.MainModule main = vortex.main;
            main.startColor = new ParticleSystem.MinMaxGradient(VortexPurple, VortexCore);

            ParticleSystem.ShapeModule shape = vortex.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.2f;
            shape.radiusThickness = 0.25f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // Swirl inward and upward around the system's position
            ParticleSystem.VelocityOverLifetimeModule velocity = vortex.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(7f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-1.6f);

            SetFade(vortex, 1f, 0f);
            ParticleSystemRenderer renderer = vortex.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1.2f;
            return vortex;
        }

        private ParticleSystem CreateSystem(string name, ParticleSystemSimulationSpace space, int maxParticles,
            ParticleSystem.MinMaxCurve lifetime, ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size, float gravity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = space;
            main.maxParticles = maxParticles;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            // Particles come only from Emit calls; the system just simulates them
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }

        private static void SetShapeSphere(ParticleSystem ps, float radius)
        {
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        private static void SetFade(ParticleSystem ps, float startAlpha, float endAlpha)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.35f), new GradientAlphaKey(endAlpha, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetShrink(ParticleSystem ps)
        {
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        /// <summary>Soft round particle sprite, generated once so no texture asset is needed.</summary>
        private static Texture2D BuildSoftDot()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AbilityVfx_SoftDot",
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 1f - d;
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        #endregion
    }
}
