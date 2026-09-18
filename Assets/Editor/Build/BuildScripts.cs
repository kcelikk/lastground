using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastGround.EditorTools.Build
{
    /// <summary>
    /// OS-independent build entry points (TDD_02 §26.3). CLI:
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android
    ///         -executeMethod LastGround.EditorTools.Build.BuildScripts.BuildAndroidDevelopment
    /// Output: Builds/Android/LastGround-dev.apk
    /// </summary>
    public static class BuildScripts
    {
        public const string AndroidDevPath = "Builds/Android/LastGround-dev.apk";

        [MenuItem("LastGround/Build/Android Development APK")]
        public static void BuildAndroidDevelopment()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.connectProfiler = true;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = AndroidDevPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler,
            };
            Run(options);
        }

        static void Run(BuildPlayerOptions options)
        {
            if (options.scenes.Length == 0)
                throw new InvalidOperationException("No scenes in Build Settings. Run LastGround/Setup first.");

            Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName) ?? ".");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            // summary.totalSize counts intermediate data on Android; report the real output file size.
            long bytes = File.Exists(summary.outputPath) ? new FileInfo(summary.outputPath).Length : 0;
            Debug.Log($"[Build] {summary.result}: {summary.outputPath} " +
                      $"({bytes / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalMinutes:0.0} min, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings)");

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
