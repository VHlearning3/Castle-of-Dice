using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-10: Sir Roland's eye slits and the gap at his neck showed the ground behind him when seen
    /// from the front. Renders the playing knight from around the front, up to the game camera's height. A hole
    /// is a pixel where the background shows (it changes with the clear colour) although the armour shell
    /// covers it (a render with every face drawn from both sides). Gaps past his outline, such as between a
    /// raised shoulder plate and the helmet, don't count. With KNIGHT_SHOTS_DIR set, saves each view with its
    /// holes in yellow.
    /// </summary>
    public class KnightSeeThroughPlayTests
    {
        private static readonly Color BgA = new Color(1f, 0f, 1f);
        private static readonly Color BgB = new Color(0f, 1f, 0f);
        private const int Size = 512;

        [UnityTest, Timeout(180000)]
        public IEnumerator SirRoland_HasNoSeeThroughHoles()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Players/PlayerHero.prefab");
            GameObject hero = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
            hero.GetComponent<HeroClassModels>().Apply(CharacterClassType.Warrior);
            foreach (MonoBehaviour mb in hero.GetComponents<MonoBehaviour>()) mb.enabled = false;   // only the model plays

            Transform head = null;
            Renderer eyes = null;
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(false))
            {
                if (t.name == "mixamorig:Head") head = t;
                if (t.name == "Knight_EyeSockets") eyes = t.GetComponent<Renderer>();
            }
            Assert.IsNotNull(head, "The knight's head bone is exposed.");
            Assert.IsNotNull(eyes, "The knight wears the eye sockets.");
            // KNIGHT_TEST_UNFIXED=1 takes the fixes off, to see that the test catches the holes
            if (Environment.GetEnvironmentVariable("KNIGHT_TEST_UNFIXED") == "1")
            {
                foreach (Transform t in hero.GetComponentsInChildren<Transform>(false))
                {
                    if (t.name == "Knight_EyeSockets" || t.name == "Knight_NeckFill") t.gameObject.SetActive(false);
                    SkinnedMeshRenderer body = t.GetComponent<SkinnedMeshRenderer>();
                    if (body != null && body.sharedMaterials.Length > 1) body.sharedMaterials = new[] { body.sharedMaterials[0] };
                }
            }
            for (int i = 0; i < 30; i++) yield return null;   // let the idle play

            Light sun = new GameObject("TestSun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Camera cam = new GameObject("HoleCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 30f;
            RenderTexture rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Material shell = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            shell.SetColor("_BaseColor", Color.white);
            shell.SetFloat("_Cull", (float)CullMode.Off);
            // The body and what plugs it; the weapons' thin blades are not part of this
            List<Renderer> renderers = new List<Renderer>();
            foreach (Renderer r in hero.GetComponentsInChildren<Renderer>(false))
                if (r is SkinnedMeshRenderer || r.name == "Knight_EyeSockets" || r.name == "Knight_NeckFill") renderers.Add(r);
            List<Renderer> others = new List<Renderer>(hero.GetComponentsInChildren<Renderer>(false));
            others.RemoveAll(renderers.Contains);

            string dir = Environment.GetEnvironmentVariable("KNIGHT_SHOTS_DIR");
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // Front = the way his face (the eye sockets) points; the idle turns him a little
            Vector3 face = Vector3.ProjectOnPlane(eyes.bounds.center - head.position, Vector3.up).normalized;
            Vector3 target = head.position + Vector3.down * 0.25f;
            List<string> failures = new List<string>();
            foreach (float yaw in new[] { -40f, -20f, 0f, 20f, 40f })
            {
                foreach (float pitch in new[] { 0f, 25f, 45f, 60f })
                {
                    Vector3 from = Quaternion.LookRotation(face) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                    cam.transform.position = target + from * 2.4f;
                    cam.transform.LookAt(target);
                    yield return null;

                    Color32[] a = Render(cam, rt, BgA);
                    Color32[] b = Render(cam, rt, BgB);
                    Color32[] covered = RenderShell(cam, rt, renderers, others, shell);
                    int holes = CountHoles(a, b, covered, out bool[] mask);
                    if (holes > 4) failures.Add($"yaw {yaw} pitch {pitch}: {holes} px");
                    if (!string.IsNullOrEmpty(dir)) Save(dir, $"knight_y{yaw}_p{pitch}", a, mask);
                }
            }

            cam.targetTexture = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(shell);
            UnityEngine.Object.Destroy(hero);
            Assert.IsEmpty(failures, "Background shows through Sir Roland at: " + string.Join("; ", failures));
#else
            yield break;
#endif
        }

        private static Color32[] Render(Camera cam, RenderTexture rt, Color bg)
        {
            cam.backgroundColor = bg;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Color32[] px = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
            return px;
        }

        // Every body face drawn white from both sides over black: where the armour shell is, open or not
        private static Color32[] RenderShell(Camera cam, RenderTexture rt, List<Renderer> renderers, List<Renderer> others, Material shell)
        {
            Dictionary<Renderer, Material[]> saved = new Dictionary<Renderer, Material[]>();
            foreach (Renderer r in renderers)
            {
                saved[r] = r.sharedMaterials;
                Material[] m = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < m.Length; i++) m[i] = shell;
                r.sharedMaterials = m;
            }
            foreach (Renderer r in others) r.enabled = false;
            Color32[] px = Render(cam, rt, Color.black);
            foreach (Renderer r in others) r.enabled = true;
            foreach (KeyValuePair<Renderer, Material[]> kv in saved) kv.Key.sharedMaterials = kv.Value;
            return px;
        }

        private static int CountHoles(Color32[] a, Color32[] b, Color32[] covered, out bool[] hole)
        {
            int n = Size * Size, count = 0;
            hole = new bool[n];
            for (int i = 0; i < n; i++)
            {
                bool background = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 60;
                bool shell = covered[i].r > 128;
                if (background && shell) { hole[i] = true; count++; }
            }
            return count;
        }

        private static void Save(string dir, string name, Color32[] shot, bool[] hole)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Color32[] px = (Color32[])shot.Clone();
            for (int i = 0; i < px.Length; i++)
                if (hole[i]) px[i] = new Color32(255, 255, 0, 255);   // holes in yellow
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
    }
}
