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
        var previousVersion = PlayerSettings.bundleVersion;
        PlayerSettings.bundleVersion = ResolveVersion();
        Debug.Log($"BUILD version: {PlayerSettings.bundleVersion}");

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

        PlayerSettings.bundleVersion = previousVersion;

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static string ResolveVersion()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-sattrakVersion");
        var version = index >= 0 && index + 1 < args.Length ? args[index + 1] : GitDescribe();

        if (string.IsNullOrWhiteSpace(version)) return "dev";
        version = version.Trim();
        if (version.StartsWith("dev-") && version.Length > 11) return version.Substring(0, 11);
        return version.StartsWith("v") && version.Length > 1 && char.IsDigit(version[1]) ? version.Substring(1) : version;
    }

    private static string GitDescribe()
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "describe --tags --always --dirty",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
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
