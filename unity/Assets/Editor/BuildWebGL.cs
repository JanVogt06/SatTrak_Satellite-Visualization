using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWebGL
{
    public static void Run()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        Debug.Log($"BUILD scenes: {string.Join(", ", scenes)}");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "../build/WebGL/SatTrak",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"BUILD result: {summary.result}");
        Debug.Log($"BUILD size: {summary.totalSize / (1024 * 1024)} MB");
        Debug.Log($"BUILD time: {summary.totalTime}");
        Debug.Log($"BUILD errors: {summary.totalErrors} warnings: {summary.totalWarnings}");

        foreach (var step in report.steps)
        {
            foreach (var message in step.messages)
            {
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    Debug.Log($"BUILD problem [{step.name}] {message.content}");
            }
        }

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
