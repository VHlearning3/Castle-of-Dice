using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// One-click WebGL player build of the enabled Build Settings scenes into Builds/WebGL (outside Assets/).
    /// </summary>
    public static class WebGLBuilder
    {
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("CastleOfDice/Build WebGL")]
        public static void BuildWebGL()
        {
            GenerateUIThemeEditor.GenerateUITheme();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuilder] No enabled scenes in Build Settings.");
                return;
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuilder] WebGL build succeeded: {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s -> {OutputPath}");
            }
            else
            {
                Debug.LogError($"[WebGLBuilder] WebGL build {summary.result} with {summary.totalErrors} error(s).");
            }
        }
    }
}
