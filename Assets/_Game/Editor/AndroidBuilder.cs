using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FreshCatch.EditorTools
{
    /// <summary>
    /// Development APK build for device playtests. Menu: FreshCatch/Build Android APK.
    /// CLI: unity build &lt;project&gt; --target Android --execute-method FreshCatch.EditorTools.AndroidBuilder.Build --output-path Builds/Android/FreshCatch.apk
    /// Signed with Unity's debug key: fine for sideloading, not for the Play Store.
    /// </summary>
    public static class AndroidBuilder
    {
        public const string DefaultOutput = "Builds/Android/FreshCatch.apk";
        public const string PackageName = "com.freshcatch.prototype";

        [MenuItem("FreshCatch/Build Android APK")]
        public static void BuildMenu()
        {
            var report = Build(DefaultOutput);
            EditorUtility.RevealInFinder(report);
        }

        /// <summary>Entry point for -executeMethod; honors -buildOutput.</summary>
        public static void Build() => Build(ReadArg("-buildOutput") ?? DefaultOutput);

        /// <summary>Configures Android player settings. Safe to run repeatedly.</summary>
        public static void Configure()
        {
            PlayerSettings.companyName = "FreshCatch";
            PlayerSettings.productName = "Fresh Catch";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageName);
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        /// <summary>Builds and returns the APK path. Throws on failure (never quits the Editor).</summary>
        public static string Build(string outputPath)
        {
            Configure();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new Exception("Could not switch the active build target to Android (is the Android module installed?).");

            var scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
            if (scenes.Length == 0) scenes = new[] { PrototypeSceneBuilder.ScenePath };

            var full = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = full,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            });

            var s = report.summary;
            var msg = $"Android build {s.result}: {full} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s)";
            if (s.result != BuildResult.Succeeded) throw new Exception(msg);
            Debug.Log(msg);
            return full;
        }

        private static string ReadArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
