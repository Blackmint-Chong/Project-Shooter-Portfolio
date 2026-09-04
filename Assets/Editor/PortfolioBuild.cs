using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PortfolioBuild
{
    private const string MenuRoot = "Project Shooter/Build/";
    private const string WindowsOutputDirectory = "Builds/Windows";
    private const string WebOutputDirectory = "Builds/Web";

    private static readonly string[] ReleaseScenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/GameSetup.unity",
        "Assets/Scenes/Battle/FactoryMap.unity"
    };

    [MenuItem(MenuRoot + "Configure Release Settings", priority = 0)]
    public static void ConfigureReleaseSettings()
    {
        ValidateProjectSettings();
        ValidateReleaseScenes();

        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.allowFullscreenSwitch = false;

        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 540;
        PlayerSettings.WebGL.compressionFormat =
            WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;

        AssetDatabase.SaveAssets();
        Debug.Log(
            "Release build settings configured: 1280x720 windowed Windows, " +
            "960x540 Web, three game scenes, Gzip Web compression, and " +
            "decompression fallback.");
    }

    [MenuItem(MenuRoot + "Build Windows", priority = 20)]
    public static void BuildWindows()
    {
        ConfigureReleaseSettings();

        string outputDirectory = GetAbsolutePath(WindowsOutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        Build(
            BuildTarget.StandaloneWindows64,
            Path.Combine(outputDirectory, "Project Shooter.exe"));
    }

    [MenuItem(MenuRoot + "Build Web", priority = 21)]
    public static void BuildWeb()
    {
        ConfigureReleaseSettings();

        string outputDirectory = GetAbsolutePath(WebOutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        Build(BuildTarget.WebGL, outputDirectory);
    }

    [MenuItem(MenuRoot + "Build Windows and Web", priority = 40)]
    public static void BuildAll()
    {
        BuildWindows();
        BuildWeb();
    }

    private static void Build(BuildTarget target, string locationPathName)
    {
        BuildPlayerOptions options = new()
        {
            scenes = ReleaseScenes,
            locationPathName = locationPathName,
            target = target,
            options = BuildOptions.StrictMode
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"{target} build failed with {summary.totalErrors} errors.");
        }

        Debug.Log(
            $"{target} build completed at '{locationPathName}' " +
            $"({summary.totalSize} bytes, {summary.totalTime}).");
    }

    private static void ValidateReleaseScenes()
    {
        for (int index = 0; index < ReleaseScenes.Length; index++)
        {
            string scenePath = ReleaseScenes[index];
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new BuildFailedException(
                    $"Required release scene is missing: {scenePath}");
            }
        }
    }

    private static void ValidateProjectSettings()
    {
        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName)
            || PlayerSettings.companyName == "DefaultCompany")
        {
            throw new BuildFailedException(
                "Set a portfolio developer name in Player Settings > Company Name.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.productName))
        {
            throw new BuildFailedException(
                "Set Player Settings > Product Name before building.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
        {
            throw new BuildFailedException(
                "Set Player Settings > Version before building.");
        }
    }

    private static string GetAbsolutePath(string relativePath)
    {
        return Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", relativePath));
    }
}
