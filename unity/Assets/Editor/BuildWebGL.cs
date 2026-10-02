using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildWebGL
{
    private const string OutputPath = "../build/WebGL/SatTrak";

    public static void Run()
    {
        var staleBuild = Path.Combine(OutputPath, "Build");
        if (Directory.Exists(staleBuild))
            Directory.Delete(staleBuild, true);

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        Debug.Log($"BUILD scenes: {string.Join(", ", scenes)}");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
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

        if (summary.result == BuildResult.Succeeded)
            StampBuildId();

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static void StampBuildId()
    {
        using var sha = SHA256.Create();
        foreach (var file in Directory.GetFiles(Path.Combine(OutputPath, "Build")).OrderBy(f => f, StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(file);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        var id = BitConverter.ToString(sha.Hash).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        var index = Path.Combine(OutputPath, "index.html");
        File.WriteAllText(index, File.ReadAllText(index).Replace("__BUILD_ID__", id));
        Debug.Log($"BUILD id: {id}");
    }
}
