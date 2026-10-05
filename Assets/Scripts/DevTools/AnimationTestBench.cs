using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace CastleOfTheD20.DevTools
{
    /// <summary>
    /// Drives Assets/Scenes/Test/AnimationTest.unity: spawns every animated character from its real prefab,
    /// strips the gameplay scripts so no game managers are needed, and gives a small UI to play each
    /// animator state, fire each trigger and play each clip on its own. Built by CastleOfDice/Build Animation Test Scene.
    /// The scene is not in Build Settings, so none of this runs in the WebGL build.
    /// </summary>
    public class AnimationTestBench : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public string displayName;
            public string group;
            public GameObject prefab;
            [Tooltip("Child to keep, e.g. Knight_Model inside PlayerHero. Empty keeps the whole prefab.")]
            public string modelChild;
            [Tooltip("Hang the class weapons from HeroWeaponMounts on the model (Elira's staff, Corvo's daggers).")]
            public bool attachHeroWeapons;
            public CharacterClassType heroClass;
            public string note;
            [Tooltip("Base layer state names, baked by the scene builder.")]
            public string[] stateNames = new string[0];
            [Tooltip("Full paths of the same states (Base Layer.Idle), baked by the scene builder.")]
            public string[] statePaths = new string[0];
        }

        [System.Serializable]
        public class MissingEntry
        {
            public string displayName;
            public string reason;
        }

        /// <summary>One spawned character and what is playing on it.</summary>
        public class Station
        {
            public Entry entry;
            public Transform root;
            public Animator animator;
            public AnimationClip[] clips = new AnimationClip[0];
            public string[] triggers = new string[0];
            public string[] bools = new string[0];
            public int[] stateHashes = new int[0];
            public Bounds bounds;
            public PlayableGraph graph;
            public AnimationClipPlayable clipPlayable;
            public AnimationClip playingClip;
        }

        public Entry[] entries = new Entry[0];
        public MissingEntry[] missing = new MissingEntry[0];
        public Camera viewCamera;
        [Tooltip("Gap between characters in a row, in metres.")]
        public float gap = 1.0f;

        private static readonly string[] GroupOrder = { "Heroes", "NPCs", "Enemies", "Bosses" };
        private static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        private static readonly Color ButtonColor = new Color(0.24f, 0.2f, 0.16f, 1f);
        private static readonly Color SelectedColor = new Color(0.62f, 0.45f, 0.16f, 1f);
        private static readonly Color HeaderColor = new Color(0.95f, 0.78f, 0.42f, 1f);

        private readonly List<Station> stations = new List<Station>();
        private readonly List<Image> characterButtons = new List<Image>();
        private readonly List<AnimatorClipInfo> clipInfoBuffer = new List<AnimatorClipInfo>();
        private int selected = -1;
        private bool loopClips = true;
        private float speed = 1f;
        private Coroutine playAllRoutine;

        private RectTransform leftPanel;
        private RectTransform rightPanel;
        private RectTransform detailContent;
        private TextMeshProUGUI readout;
        private TextMeshProUGUI speedLabel;
        private TextMeshProUGUI loopLabel;
        private TextMeshProUGUI playAllLabel;
        private Transform selectionDisc;
        private float readoutTimer;

        // Orbit camera
        private Vector3 pivot;
        private Vector3 pivotTarget;
        private float yaw = 15f;
        private float pitch = 18f;
        private float distance = 6f;

        public IReadOnlyList<Station> Stations => stations;
        public int Selected => selected;
        public bool Ready { get; private set; }

        private void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            SpawnAll();
            BuildUI();
            Ready = true;
            if (stations.Count > 0) Select(0);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < stations.Count; i++) StopClip(stations[i]);
        }

        // ---------------------------------------------------------------- spawning

        private void SpawnAll()
        {
            // Instantiating under an inactive holder keeps Awake/OnEnable from running on the gameplay scripts
            GameObject holder = new GameObject("_SpawnHolder");
            holder.SetActive(false);

            float rowZ = 0f;
            for (int g = 0; g < GroupOrder.Length; g++)
            {
                float x = 0f;
                float rowDepth = 0f;
                List<Station> row = new List<Station>();
                for (int i = 0; i < entries.Length; i++)
                {
                    Entry e = entries[i];
                    if (e == null || e.prefab == null || e.group != GroupOrder[g]) continue;
                    Station s = Spawn(e, holder.transform);
                    if (s == null) continue;
                    // Renderer bounds are empty while inactive, so measure once the model is out of the holder
                    s.root.SetParent(transform, false);
                    // Face the camera, which starts on the -Z side of the line-up
                    s.root.rotation = Quaternion.Euler(0f, 180f, 0f);
                    s.bounds = LocalBounds(s.root);
                    row.Add(s);
                }

                // Lay the row out left to right by each model's width
                for (int i = 0; i < row.Count; i++)
                {
                    Station s = row[i];
                    float half = Mathf.Max(0.4f, s.bounds.extents.x);
                    x += half;
                    s.root.position = new Vector3(x, 0f, rowZ);
                    x += half + gap;
                    rowDepth = Mathf.Max(rowDepth, s.bounds.size.z);
                }

                // Centre the row on x = 0
                float shift = (x - gap) * 0.5f;
                for (int i = 0; i < row.Count; i++)
                {
                    row[i].root.position -= new Vector3(shift, 0f, 0f);
                    stations.Add(row[i]);
                }
                if (row.Count > 0) rowZ += Mathf.Max(2.5f, rowDepth) + gap * 2f;
            }

            Destroy(holder);

            for (int i = 0; i < stations.Count; i++) CacheAnimatorInfo(stations[i]);

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "SelectionDisc";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, false);
            Renderer r = disc.GetComponent<Renderer>();
            r.material.color = SelectedColor;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            selectionDisc = disc.transform;
        }

        private Station Spawn(Entry e, Transform holder)
        {
            GameObject root = new GameObject(e.displayName);
            root.transform.SetParent(holder, false);

            GameObject instance = Instantiate(e.prefab, root.transform, false);
            GameObject model = instance;
            if (!string.IsNullOrEmpty(e.modelChild))
            {
                Transform child = FindDeep(instance.transform, e.modelChild);
                if (child == null)
                {
                    Debug.LogWarning($"[AnimationTest] '{e.modelChild}' not found in '{e.prefab.name}'.");
                    Destroy(root);
                    return null;
                }
                model = child.gameObject;
                model.SetActive(true);
                model.transform.SetParent(root.transform, true);
                model.transform.localPosition = Vector3.zero;
                DestroyImmediate(instance);
            }

            if (e.attachHeroWeapons && HeroWeaponMountsSO.Instance != null)
                HeroWeaponMountsSO.Instance.AttachTo(model, e.heroClass);

            StripGameplay(model);

            Station s = new Station { entry = e, root = root.transform };
            s.animator = model.GetComponentInChildren<Animator>(true);
            if (s.animator != null)
            {
                s.animator.applyRootMotion = false;
                s.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            return s;
        }

        /// <summary>Removes scripts, colliders, rigidbodies, audio and canvases so only meshes and the Animator remain.</summary>
        private static void StripGameplay(GameObject go)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                bool removed = false;
                Component[] all = go.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Component c = all[i];
                    if (c == null || c is Transform || c is Animator) continue;
                    bool strip = c is Behaviour || c is Collider || c is Rigidbody;
                    if (!strip || IsRequiredByOther(c)) continue;
                    DestroyImmediate(c);
                    removed = true;
                }
                if (!removed) break;
            }
        }

        private static bool IsRequiredByOther(Component target)
        {
            System.Type t = target.GetType();
            Component[] siblings = target.GetComponents<Component>();
            for (int i = 0; i < siblings.Length; i++)
            {
                Component other = siblings[i];
                if (other == null || other == target) continue;
                object[] attrs = other.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                for (int a = 0; a < attrs.Length; a++)
                {
                    RequireComponent rc = (RequireComponent)attrs[a];
                    if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(t)) ||
                        (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(t)) ||
                        (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(t)))
                        return true;
                }
            }
            return false;
        }

        private static Bounds LocalBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(root.position + Vector3.up, Vector3.one);
            bool first = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (first) { b = renderers[i].bounds; first = false; }
                else b.Encapsulate(renderers[i].bounds);
            }
            b.center -= root.position;
            return b;
        }

        private void CacheAnimatorInfo(Station s)
        {
            Animator a = s.animator;
            if (a == null || a.runtimeAnimatorController == null) return;

            // Clips the controller references, without duplicates
            AnimationClip[] raw = a.runtimeAnimatorController.animationClips;
            List<AnimationClip> clips = new List<AnimationClip>();
            for (int i = 0; i < raw.Length; i++)
                if (raw[i] != null && !clips.Contains(raw[i])) clips.Add(raw[i]);
            clips.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            s.clips = clips.ToArray();

            List<string> triggers = new List<string>();
            List<string> bools = new List<string>();
            AnimatorControllerParameter[] ps = a.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == AnimatorControllerParameterType.Trigger) triggers.Add(ps[i].name);
                else if (ps[i].type == AnimatorControllerParameterType.Bool) bools.Add(ps[i].name);
            }
            s.triggers = triggers.ToArray();
            s.bools = bools.ToArray();

            string[] paths = s.entry.statePaths ?? new string[0];
            s.stateHashes = new int[paths.Length];
            for (int i = 0; i < paths.Length; i++) s.stateHashes[i] = Animator.StringToHash(paths[i]);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ---------------------------------------------------------------- playback

        public void Select(int index)
        {
            if (index < 0 || index >= stations.Count) return;
            StopPlayAll();
            selected = index;
            Station s = stations[index];
            pivotTarget = s.root.position + s.bounds.center;
            distance = Mathf.Clamp(s.bounds.size.magnitude * 1.6f, 2.5f, 25f);
            if (selected >= 0 && characterButtons.Count == stations.Count)
                for (int i = 0; i < characterButtons.Count; i++)
                    characterButtons[i].color = i == selected ? SelectedColor : ButtonColor;

            float radius = Mathf.Clamp(Mathf.Max(s.bounds.extents.x, s.bounds.extents.z) * 0.9f, 0.5f, 3f);
            selectionDisc.position = s.root.position + new Vector3(0f, 0.01f, 0f);
            selectionDisc.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            RebuildDetails();
        }

        public void PlayState(int stateIndex)
        {
            Station s = Current;
            if (s == null || s.animator == null || stateIndex < 0 || stateIndex >= s.stateHashes.Length) return;
            StopClip(s);
            s.animator.speed = speed;
            s.animator.Play(s.stateHashes[stateIndex], 0, 0f);
        }

        public void FireTrigger(int triggerIndex)
        {
            Station s = Current;
            if (s == null || s.animator == null || triggerIndex < 0 || triggerIndex >= s.triggers.Length) return;
            StopClip(s);
            s.animator.speed = speed;
            s.animator.SetTrigger(s.triggers[triggerIndex]);
        }

        public void ToggleBool(int boolIndex)
        {
            Station s = Current;
            if (s == null || s.animator == null || boolIndex < 0 || boolIndex >= s.bools.Length) return;
            StopClip(s);
            string p = s.bools[boolIndex];
            s.animator.SetBool(p, !s.animator.GetBool(p));
            RebuildDetails();
        }

        /// <summary>Plays one clip straight on the model through a PlayableGraph, bypassing the state machine.</summary>
        public void PlayClip(int clipIndex)
        {
            Station s = Current;
            if (s == null || s.animator == null || clipIndex < 0 || clipIndex >= s.clips.Length) return;
            StopClip(s);
            AnimationClip clip = s.clips[clipIndex];
            s.graph = PlayableGraph.Create("AnimationTest_" + s.entry.displayName);
            s.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(s.graph, "Clip", s.animator);
            s.clipPlayable = AnimationClipPlayable.Create(s.graph, clip);
            s.clipPlayable.SetSpeed(speed);
            output.SetSourcePlayable(s.clipPlayable);
            s.graph.Play();
            s.playingClip = clip;
        }

        /// <summary>Stops a clip preview and hands the model back to its animator controller.</summary>
        public void StopClip(Station s)
        {
            if (s == null || !s.graph.IsValid()) return;
            s.graph.Destroy();
            s.playingClip = null;
            if (s.animator != null) s.animator.Rebind();
        }

        public void ResetCurrent()
        {
            Station s = Current;
            if (s == null || s.animator == null) return;
            StopPlayAll();
            StopClip(s);
            s.animator.Rebind();
            s.animator.speed = speed;
            RebuildDetails();
        }

        public void SetSpeed(float value)
        {
            speed = Mathf.Clamp(value, 0.1f, 3f);
            if (speedLabel != null) speedLabel.text = "Speed " + speed.ToString("0.0", CultureInfo.InvariantCulture) + "x";
            for (int i = 0; i < stations.Count; i++)
            {
                Station s = stations[i];
                if (s.animator != null) s.animator.speed = speed;
                if (s.graph.IsValid()) s.clipPlayable.SetSpeed(speed);
            }
        }

        private void TogglePlayAll()
        {
            if (playAllRoutine != null) { StopPlayAll(); return; }
            playAllRoutine = StartCoroutine(PlayAllRoutine());
            RefreshPlayAllLabel();
        }

        private string PlayAllText() => playAllRoutine != null ? "Stop" : "Play all";

        private void RefreshPlayAllLabel()
        {
            if (playAllLabel != null) playAllLabel.text = PlayAllText();
        }

        private void StopPlayAll()
        {
            if (playAllRoutine == null) return;
            StopCoroutine(playAllRoutine);
            playAllRoutine = null;
            RefreshPlayAllLabel();
        }

        /// <summary>Plays every state and then every clip of the selected character, one after another.</summary>
        private IEnumerator PlayAllRoutine()
        {
            Station s = Current;
            if (s == null) { playAllRoutine = null; yield break; }
            for (int i = 0; i < s.stateHashes.Length; i++)
            {
                PlayState(i);
                yield return null;
                float len = s.animator.GetCurrentAnimatorStateInfo(0).length;
                yield return new WaitForSeconds(Mathf.Clamp(len / speed, 0.6f, 4f));
            }
            for (int i = 0; i < s.clips.Length; i++)
            {
                PlayClip(i);
                yield return new WaitForSeconds(Mathf.Clamp(s.clips[i].length / speed, 0.6f, 4f));
            }
            StopClip(s);
            playAllRoutine = null;
            RefreshPlayAllLabel();
        }

        private Station Current => selected >= 0 && selected < stations.Count ? stations[selected] : null;

        // ---------------------------------------------------------------- update

        private void Update()
        {
            HandleKeys();
            HandleCamera();
            LoopClips();

            readoutTimer -= Time.unscaledDeltaTime;
            if (readoutTimer <= 0f)
            {
                readoutTimer = 0.1f;
                UpdateReadout();
            }
        }

        private void LoopClips()
        {
            for (int i = 0; i < stations.Count; i++)
            {
                Station s = stations[i];
                if (!loopClips || !s.graph.IsValid() || s.playingClip == null) continue;
                if (s.clipPlayable.GetTime() >= s.playingClip.length) s.clipPlayable.SetTime(0);
            }
        }

        private void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || stations.Count == 0) return;
            if (kb.tabKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame)
                Select((selected + (kb.shiftKey.isPressed ? stations.Count - 1 : 1)) % stations.Count);
            if (kb.pageUpKey.wasPressedThisFrame) Select((selected + stations.Count - 1) % stations.Count);
            if (kb.fKey.wasPressedThisFrame) Select(selected);
            if (kb.spaceKey.wasPressedThisFrame) TogglePlayAll();
        }

        private void HandleCamera()
        {
            if (viewCamera == null) return;
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;

            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                bool overUI = IsOver(leftPanel, pos) || IsOver(rightPanel, pos);
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed && !overUI)
                {
                    yaw += delta.x * 0.25f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.2f, -10f, 80f);
                }
                if (mouse.middleButton.isPressed && !overUI)
                {
                    Vector3 pan = (-viewCamera.transform.right * delta.x - viewCamera.transform.up * delta.y) * distance * 0.002f;
                    pivotTarget += pan;
                    pivot += pan;
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (!overUI && Mathf.Abs(scroll) > 0.01f)
                    distance = Mathf.Clamp(distance * (scroll > 0f ? 0.88f : 1.12f), 1f, 60f);
            }

            if (kb != null)
            {
                Vector3 flatForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Vector3 flatRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += flatForward;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= flatForward;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += flatRight;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= flatRight;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move -= Vector3.up;
                if (move != Vector3.zero)
                {
                    Vector3 step = move.normalized * (kb.shiftKey.isPressed ? 12f : 4f) * dt;
                    pivotTarget += step;
                    pivot += step;
                }
            }

            pivot = Vector3.Lerp(pivot, pivotTarget, 1f - Mathf.Exp(-8f * dt));
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            viewCamera.transform.SetPositionAndRotation(pivot - rot * Vector3.forward * distance, rot);
        }

        /// <summary>Puts the camera on the selected character straight away (used by the screenshot test).</summary>
        public void SnapCamera()
        {
            pivot = pivotTarget;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            if (viewCamera != null) viewCamera.transform.SetPositionAndRotation(pivot - rot * Vector3.forward * distance, rot);
        }

        /// <summary>Pulls the camera back to show the whole line-up.</summary>
        public void FrameAll()
        {
            if (stations.Count == 0) return;
            Bounds all = new Bounds(stations[0].root.position + stations[0].bounds.center, stations[0].bounds.size);
            for (int i = 1; i < stations.Count; i++)
                all.Encapsulate(new Bounds(stations[i].root.position + stations[i].bounds.center, stations[i].bounds.size));
            pivotTarget = all.center;
            distance = Mathf.Clamp(all.size.magnitude * 0.85f, 5f, 60f);
            SnapCamera();
        }

        private bool IsOver(RectTransform panel, Vector2 screenPos)
        {
            return panel != null && RectTransformUtility.RectangleContainsScreenPoint(panel, screenPos, viewCamera);
        }

        private void UpdateReadout()
        {
            Station s = Current;
            if (readout == null) return;
            if (s == null || s.animator == null) { readout.text = "No animator"; return; }

            if (s.graph.IsValid() && s.playingClip != null)
            {
                double t = s.clipPlayable.GetTime();
                readout.text = "Clip preview: " + s.playingClip.name + "\n" +
                               t.ToString("0.00", CultureInfo.InvariantCulture) + " / " + s.playingClip.length.ToString("0.00", CultureInfo.InvariantCulture) + " s" +
                               (loopClips ? " (looping)" : "");
                return;
            }

            AnimatorStateInfo info = s.animator.GetCurrentAnimatorStateInfo(0);
            string stateName = "?";
            for (int i = 0; i < s.stateHashes.Length; i++)
                if (s.stateHashes[i] == info.fullPathHash) { stateName = s.entry.stateNames[i]; break; }

            s.animator.GetCurrentAnimatorClipInfo(0, clipInfoBuffer);
            string clipName = clipInfoBuffer.Count > 0 && clipInfoBuffer[0].clip != null ? clipInfoBuffer[0].clip.name : "none";
            readout.text = "State: " + stateName + (s.animator.IsInTransition(0) ? " (blending)" : "") + "\n" +
                           "Clip: " + clipName + "\n" +
                           "Time: " + (info.normalizedTime % 1f * info.length).ToString("0.00", CultureInfo.InvariantCulture) + " / " + info.length.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }

        // ---------------------------------------------------------------- UI

        private void BuildUI()
        {
            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                InputSystemUIInputModule module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            GameObject canvasGo = new GameObject("AnimationTestCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            // Screen Space - Camera so the panels also show up in Camera.Render screenshots
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewCamera;
            canvas.planeDistance = viewCamera != null ? viewCamera.nearClipPlane + 0.05f : 0.5f;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            // Left: character list
            leftPanel = MakePanel(canvasRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 16f), new Vector2(316f, -16f));
            TextMeshProUGUI title = MakeText(leftPanel, "Animation Test", 26, TextAlignmentOptions.TopLeft, HeaderColor);
            title.fontStyle = FontStyles.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -44f), new Vector2(-12f, -8f));
            TextMeshProUGUI help = MakeText(leftPanel, "Right drag: orbit   Wheel: zoom   Middle drag / WASD QE: move\nTab: next   F: focus   Space: play all", 13, TextAlignmentOptions.TopLeft, new Color(0.8f, 0.76f, 0.7f));
            SetRect(help.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -84f), new Vector2(-12f, -46f));
            RectTransform list = MakeScrollList(leftPanel, 88f, 8f);

            characterButtons.Clear();
            string lastGroup = null;
            for (int i = 0; i < stations.Count; i++)
            {
                Station s = stations[i];
                if (s.entry.group != lastGroup)
                {
                    lastGroup = s.entry.group;
                    MakeHeader(list, lastGroup);
                }
                int index = i;
                string label = s.entry.displayName + (s.animator == null ? "  (no animator)" : "");
                characterButtons.Add(MakeButton(list, label, () => Select(index)).GetComponent<Image>());
            }
            if (missing != null && missing.Length > 0)
            {
                MakeHeader(list, "Not animated");
                for (int i = 0; i < missing.Length; i++)
                    MakeText(list, missing[i].displayName + ": " + missing[i].reason, 14, TextAlignmentOptions.TopLeft, new Color(0.7f, 0.66f, 0.6f));
            }

            // Right: details of the selected character
            rightPanel = MakePanel(canvasRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-396f, 16f), new Vector2(-16f, -16f));
            detailContent = MakeScrollList(rightPanel, 8f, 8f);
        }

        private void RebuildDetails()
        {
            if (detailContent == null) return;
            for (int i = detailContent.childCount - 1; i >= 0; i--) Destroy(detailContent.GetChild(i).gameObject);

            Station s = Current;
            if (s == null) return;

            TextMeshProUGUI name = MakeText(detailContent, s.entry.displayName, 24, TextAlignmentOptions.TopLeft, HeaderColor);
            name.fontStyle = FontStyles.Bold;
            string controller = s.animator != null && s.animator.runtimeAnimatorController != null ? s.animator.runtimeAnimatorController.name : "no controller";
            MakeText(detailContent, s.entry.prefab.name + (string.IsNullOrEmpty(s.entry.modelChild) ? "" : " / " + s.entry.modelChild) + "\n" + controller, 13, TextAlignmentOptions.TopLeft, new Color(0.8f, 0.76f, 0.7f));
            if (!string.IsNullOrEmpty(s.entry.note))
                MakeText(detailContent, s.entry.note, 13, TextAlignmentOptions.TopLeft, new Color(0.8f, 0.76f, 0.7f));

            readout = MakeText(detailContent, "", 15, TextAlignmentOptions.TopLeft, Color.white);
            readout.GetComponent<LayoutElement>().minHeight = 60f;

            RectTransform row = MakeRow(detailContent);
            playAllLabel = MakeButton(row, PlayAllText(), TogglePlayAll).GetComponentInChildren<TextMeshProUGUI>();
            MakeButton(row, "Reset", ResetCurrent);
            loopLabel = MakeButton(row, LoopText(), ToggleLoop).GetComponentInChildren<TextMeshProUGUI>();

            RectTransform row2 = MakeRow(detailContent);
            MakeButton(row2, "Slower", () => SetSpeed(speed - 0.25f));
            speedLabel = MakeButton(row2, "Speed " + speed.ToString("0.0", CultureInfo.InvariantCulture) + "x", () => SetSpeed(1f)).GetComponentInChildren<TextMeshProUGUI>();
            MakeButton(row2, "Faster", () => SetSpeed(speed + 0.25f));

            if (s.animator == null) return;

            MakeHeader(detailContent, "States (" + s.stateHashes.Length + ")");
            for (int i = 0; i < s.stateHashes.Length; i++)
            {
                int index = i;
                MakeButton(detailContent, s.entry.stateNames[i], () => PlayState(index));
            }

            MakeHeader(detailContent, "Triggers (" + s.triggers.Length + ")");
            for (int i = 0; i < s.triggers.Length; i++)
            {
                int index = i;
                MakeButton(detailContent, s.triggers[i], () => FireTrigger(index));
            }

            if (s.bools.Length > 0)
            {
                MakeHeader(detailContent, "Bools");
                for (int i = 0; i < s.bools.Length; i++)
                {
                    int index = i;
                    MakeButton(detailContent, s.bools[i] + ": " + (s.animator.GetBool(s.bools[i]) ? "on" : "off"), () => ToggleBool(index));
                }
            }

            MakeHeader(detailContent, "Clips (" + s.clips.Length + ")");
            for (int i = 0; i < s.clips.Length; i++)
            {
                int index = i;
                AnimationClip c = s.clips[i];
                MakeButton(detailContent, c.name + "  " + c.length.ToString("0.0", CultureInfo.InvariantCulture) + "s", () => PlayClip(index));
            }
            readoutTimer = 0f;
        }

        private void ToggleLoop()
        {
            loopClips = !loopClips;
            if (loopLabel != null) loopLabel.text = LoopText();
        }

        private string LoopText() => loopClips ? "Loop clips: on" : "Loop clips: off";

        private static RectTransform NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static RectTransform MakePanel(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rt = NewUI("Panel", parent);
            SetRect(rt, anchorMin, anchorMax, offsetMin, offsetMax);
            rt.gameObject.AddComponent<Image>().color = PanelColor;
            return rt;
        }

        private static RectTransform MakeScrollList(RectTransform parent, float top, float bottom)
        {
            RectTransform view = NewUI("Scroll", parent);
            SetRect(view, Vector2.zero, Vector2.one, new Vector2(4f, bottom), new Vector2(-4f, -top));
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            view.gameObject.AddComponent<RectMask2D>();
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;

            RectTransform content = NewUI("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.padding = new RectOffset(8, 8, 6, 6);
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = view;
            sr.content = content;
            return content;
        }

        private static RectTransform MakeRow(RectTransform parent)
        {
            RectTransform row = NewUI("Row", parent);
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 30f;
            return row;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, string text, int size, TextAlignmentOptions anchor, Color color)
        {
            RectTransform rt = NewUI("Text", parent);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            rt.gameObject.AddComponent<LayoutElement>();
            return t;
        }

        private static void MakeHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI t = MakeText(parent, text, 17, TextAlignmentOptions.BottomLeft, HeaderColor);
            t.fontStyle = FontStyles.Bold;
            t.GetComponent<LayoutElement>().minHeight = 28f;
        }

        private static Button MakeButton(RectTransform parent, string label, UnityAction onClick)
        {
            RectTransform rt = NewUI(label, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = ButtonColor;
            Button b = rt.gameObject.AddComponent<Button>();
            ColorBlock colors = b.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            TextMeshProUGUI t = MakeText(rt, label, 15, TextAlignmentOptions.Left, Color.white);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            SetRect(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-4f, 0f));
            return b;
        }
    }
}
