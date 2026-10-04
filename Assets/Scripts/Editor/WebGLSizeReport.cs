using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Critical review D4: builds the WebGL player, measures what the browser downloads, shows which folders
    /// fill the build, and lists the asset-pack files nothing in the build uses (candidates to remove; this
    /// tool deletes nothing). Batch: -buildTarget WebGL -executeMethod CastleOfTheD20.Editor.WebGLSizeReport.Run
    /// Output folder: Builds/WebGL_SizeCheck; report: WEBGL_SIZE_REPORT env var or Logs/webgl_size_report.txt.
    /// </summary>
    public static class WebGLSizeReport
    {
        private const string OutputDir = "Builds/WebGL_SizeCheck";
        private const string PackFolder = "Assets/LowPolyVillageAll";

        [MenuItem("CastleOfDice/Tools/WebGL Size Report", false, 60)]
        public static void Run()
        {
            string reportPath = System.Environment.GetEnvironmentVariable("WEBGL_SIZE_REPORT");
            if (string.IsNullOrEmpty(reportPath)) reportPath = "Logs/webgl_size_report.txt";

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Castle of Dice - WebGL size report");
            sb.AppendLine("Build result: " + report.summary.result + ", " + report.summary.totalTime);
            sb.AppendLine();

            // What the browser downloads
            long download = 0;
            string buildFolder = Path.Combine(OutputDir, "Build");
            if (Directory.Exists(buildFolder))
            {
                sb.AppendLine("Download (Build folder):");
                foreach (string file in Directory.GetFiles(buildFolder))
                {
                    long size = new FileInfo(file).Length;
                    download += size;
                    sb.AppendLine($"  {Path.GetFileName(file),-48} {Mb(size),10}");
                }
                sb.AppendLine($"  {"TOTAL",-48} {Mb(download),10}");
            }
            sb.AppendLine();

            // Uncompressed asset sizes in the build, by folder
            Dictionary<string, ulong> byFolder = new Dictionary<string, ulong>();
            Dictionary<string, ulong> byAsset = new Dictionary<string, ulong>();
            foreach (PackedAssets packed in report.packedAssets)
            {
                foreach (PackedAssetInfo info in packed.contents)
                {
                    string path = info.sourceAssetPath ?? "";
                    string folder = TopFolder(path);
                    byFolder[folder] = (byFolder.TryGetValue(folder, out ulong f) ? f : 0) + info.packedSize;
                    byAsset[path] = (byAsset.TryGetValue(path, out ulong a) ? a : 0) + info.packedSize;
                }
            }
            sb.AppendLine("Build content by folder (uncompressed):");
            foreach (var kv in byFolder.OrderByDescending(k => k.Value).Take(20))
            {
                sb.AppendLine($"  {kv.Key,-60} {Mb((long)kv.Value),10}");
            }
            sb.AppendLine();
            sb.AppendLine("Largest assets in the build:");
            foreach (var kv in byAsset.OrderByDescending(k => k.Value).Take(25))
            {
                sb.AppendLine($"  {kv.Key,-80} {Mb((long)kv.Value),10}");
            }
            sb.AppendLine();

            AppendUnusedPackFiles(sb, scenes);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, sb.ToString());
            Debug.Log("[WebGLSizeReport] Wrote " + reportPath);
        }

        /// <summary>
        /// Files in the LowPolyVillageAll pack that no build scene and no Resources asset depends on.
        /// They are not in the build already; removing them shrinks the repository, not the download.
        /// </summary>
        private static void AppendUnusedPackFiles(StringBuilder sb, string[] scenes)
        {
            List<string> roots = new List<string>(scenes);
            foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("/Resources/")) roots.Add(p);
            }
            HashSet<string> used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));

            long unusedBytes = 0;
            long packBytes = 0;
            List<(string path, long size)> unused = new List<(string, long)>();
            foreach (string file in Directory.GetFiles(PackFolder, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                string assetPath = file.Replace('\\', '/');
                long size = new FileInfo(file).Length;
                packBytes += size;
                if (used.Contains(assetPath)) continue;
                unusedBytes += size;
                unused.Add((assetPath, size));
            }

            sb.AppendLine($"{PackFolder}: {Mb(packBytes)} on disk, {Mb(unusedBytes)} of it used by nothing in the build ({unused.Count} files).");
            sb.AppendLine("Unused pack files by folder:");
            foreach (var group in unused.GroupBy(u => Path.GetDirectoryName(u.path).Replace('\\', '/')).OrderByDescending(g => g.Sum(x => x.size)))
            {
                sb.AppendLine($"  {group.Key,-70} {group.Count(),5} files {Mb(group.Sum(x => x.size)),10}");
            }
            sb.AppendLine();
            sb.AppendLine("Largest unused pack files:");
            foreach (var u in unused.OrderByDescending(x => x.size).Take(40))
            {
                sb.AppendLine($"  {u.path,-90} {Mb(u.size),10}");
            }
        }

        private static string TopFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(built-in / generated)";
            string[] parts = path.Split('/');
            return parts.Length >= 3 ? parts[0] + "/" + parts[1] + "/" + parts[2] : path;
        }

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("F1") + " MB";
    }
}
