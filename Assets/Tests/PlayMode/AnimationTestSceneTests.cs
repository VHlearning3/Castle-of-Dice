using System;
using System.Collections;
using System.IO;
using System.Text;
using CastleOfTheD20.DevTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Opens the animation test scene, plays every animator state, trigger and clip of every character,
    /// and fails on any error log. Set ANIM_TEST_SCREENSHOT_DIR to also save two screenshots there.
    /// </summary>
    public class AnimationTestSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Test/AnimationTest.unity";

        [UnityTest]
        public IEnumerator EveryCharacterPlaysEveryStateTriggerAndClip()
        {
#if UNITY_EDITOR
            if (!File.Exists(ScenePath)) Assert.Ignore("Run CastleOfDice/Build Animation Test Scene first.");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));

            AnimationTestBench bench = null;
            for (int f = 0; f < 300 && (bench == null || !bench.Ready); f++)
            {
                bench = Object.FindAnyObjectByType<AnimationTestBench>();
                yield return null;
            }
            Assert.IsNotNull(bench, "No AnimationTestBench in the scene.");
            Assert.IsTrue(bench.Ready, "The bench never finished spawning.");
            Assert.AreEqual(bench.entries.Length, bench.Stations.Count, "Some characters failed to spawn.");

            StringBuilder report = new StringBuilder("[AnimationTest] Played:\n");
            for (int i = 0; i < bench.Stations.Count; i++)
            {
                bench.Select(i);
                AnimationTestBench.Station s = bench.Stations[i];
                string who = s.entry.displayName;
                Assert.IsNotNull(s.animator, who + " has no Animator.");
                Assert.IsNotNull(s.animator.runtimeAnimatorController, who + " has no controller.");
                Assert.Greater(s.stateHashes.Length, 0, who + " has no baked states.");

                for (int j = 0; j < s.stateHashes.Length; j++)
                {
                    Assert.IsTrue(s.animator.HasState(0, s.stateHashes[j]), who + " lacks state " + s.entry.statePaths[j]);
                    bench.PlayState(j);
                    yield return null;
                    yield return null;
                    AnimatorStateInfo info = s.animator.GetCurrentAnimatorStateInfo(0);
                    Assert.IsTrue(info.fullPathHash == s.stateHashes[j] || s.animator.IsInTransition(0),
                        who + " did not enter " + s.entry.statePaths[j]);
                }
                for (int j = 0; j < s.triggers.Length; j++)
                {
                    bench.FireTrigger(j);
                    yield return null;
                    yield return null;
                }
                for (int j = 0; j < s.clips.Length; j++)
                {
                    bench.PlayClip(j);
                    yield return null;
                    yield return null;
                    Assert.IsTrue(s.graph.IsValid(), who + " could not preview clip " + s.clips[j].name);
                }
                bench.ResetCurrent();
                report.AppendLine($"  {who}: {s.stateHashes.Length} states, {s.triggers.Length} triggers, {s.clips.Length} clips");
            }
            Debug.Log(report.ToString());

            string dir = Environment.GetEnvironmentVariable("ANIM_TEST_SCREENSHOT_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                bench.Select(0);
                bench.FrameAll();
                yield return new WaitForSeconds(0.5f);
                yield return Capture(bench, Path.Combine(dir, "AnimationTest_Overview.png"));

                int elira = FindStation(bench, "Scholar Elira");
                bench.Select(elira >= 0 ? elira : 0);
                bench.SnapCamera();
                int fireball = Array.IndexOf(bench.Stations[bench.Selected].entry.stateNames, "mage_fireball");
                bench.PlayState(fireball >= 0 ? fireball : 0);
                yield return new WaitForSeconds(1.4f);
                yield return Capture(bench, Path.Combine(dir, "AnimationTest_Elira.png"));
            }
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private static int FindStation(AnimationTestBench bench, string name)
        {
            for (int i = 0; i < bench.Stations.Count; i++)
                if (bench.Stations[i].entry.displayName == name) return i;
            return -1;
        }

        private static IEnumerator Capture(AnimationTestBench bench, string path)
        {
            Camera cam = bench.viewCamera;
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            yield return null;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Debug.Log("[AnimationTest] Screenshot " + path);
        }
    }
}
