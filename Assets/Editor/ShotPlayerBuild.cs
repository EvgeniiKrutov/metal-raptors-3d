using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetalRaptors
{
    public static class ShotPlayerBuild
    {
        const string Scene = "Assets/Scenes/CampaignLevel1.unity";
        const string Output = "Builds/ShotPlayer/ShotPlayer.app";

        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"ShotPlayerBuild: {report.summary.result}, {Path.GetFullPath(Output)}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
