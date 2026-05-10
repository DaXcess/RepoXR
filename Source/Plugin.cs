using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using JetBrains.Annotations;
using RepoXR.Assets;
using RepoXR.Patches;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace RepoXR;

[PublicAPI]
[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "io.daxcess.repoxr";
    public const string PLUGIN_NAME = "RepoXR";
    public const string PLUGIN_VERSION = "1.2.0";

    public new static Config Config { get; private set; } = null!;
    public static Flags Flags { get; private set; } = 0;

    public static string GameVersion => Environment.GetEnvironmentVariable("REPO_VERSION") ?? "v?";
    public static bool DebugBuild => false; //Debug.isDebugBuild;

    private void Awake()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        InputSystem.PerformDefaultPluginInitialization();

        RepoXR.Logger.source = Logger;

        Config = new Config(Info.Location, base.Config);

        Logger.LogInfo($"Starting {PLUGIN_NAME} v{PLUGIN_VERSION} ({GetCommitHash()})");

        // Allow disabling VR via config and command line
        var disableVr = Config.DisableVR.Value ||
                        Environment.GetCommandLineArgs().Contains("--disable-vr", StringComparer.OrdinalIgnoreCase);

        if (disableVr)
            Logger.LogWarning("VR has been disabled by config or the `--disable-vr` command line flag");

        if (!PreloadRuntimeDependencies())
        {
            Logger.LogError("Disabling mod because required runtime dependencies could not be loaded!");
            return;
        }

        if (!AssetCollection.LoadAssets())
        {
            Logger.LogError("Disabling mod because assets could not be loaded!");
            return;
        }

        if (!disableVr && InitializeVR())
            Flags |= Flags.VR;

        HarmonyPatcher.PatchUniversal();
        HarmonyPatcher.PatchNetworkRPCs();

        Logger.LogDebug("Inserted universal patches using Harmony");

#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--repoxr-enable-experiments", StringComparer.OrdinalIgnoreCase))
            HarmonyPatcher.PatchClass(typeof(Experiments));
#endif

        if (Environment.GetCommandLineArgs().Contains("--repoxr-debug-eyetracking", StringComparer.OrdinalIgnoreCase))
            Flags |= Flags.EyeTrackingDebug;

        Native.BringGameWindowToFront();
        Config.SetupGlobalCallbacks();

        SceneManager.sceneLoaded += (scene, _) => UniversalEntrypoint.OnSceneLoad(scene.name);
    }

    public static string GetCommitHash()
    {
        try
        {
            var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            return attribute?.InformationalVersion.Split('+')[1][..7] ?? "unknown";
        }
        catch
        {
            RepoXR.Logger.LogWarning("Failed to retrieve commit hash (compiled outside of git repo?).");

            return "unknown";
        }
    }

    private bool PreloadRuntimeDependencies()
    {
        try
        {
            var deps = Path.Combine(Path.GetDirectoryName(Info.Location)!, "RuntimeDeps");

            foreach (var file in Directory.GetFiles(deps, "*.dll"))
            {
                var filename = Path.GetFileName(file);

                try
                {
                    Assembly.LoadFile(file);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to preload '{filename}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(
                $"Unexpected error occured while preloading runtime dependencies (incorrect folder structure?): {ex.Message}");

            return false;
        }

        return true;
    }

    private static bool InitializeVR()
    {
        RepoXR.Logger.LogInfo("Loading VR...");

        if (!OpenXR.Loader.InitializeXR())
        {
            RepoXR.Logger.LogError("Failed to start in VR Mode! Only Non-VR features are available!");
            RepoXR.Logger.LogWarning("You may ignore the previous error if you meant to play without VR");

            Flags |= Flags.StartupFailed;

            return false;
        }

        if (OpenXR.GetActiveRuntimeName(out var name) &&
            OpenXR.GetActiveRuntimeVersion(out var major, out var minor, out var patch))
            RepoXR.Logger.LogInfo($"OpenXR runtime being used: {name} ({major}.{minor}.{patch})");
        else
            RepoXR.Logger.LogError("Could not get OpenXR runtime info?");

        HarmonyPatcher.PatchVR();

        RepoXR.Logger.LogDebug("Inserted VR patches using Harmony");

        // Change render pipeline settings if needed
        XRSettings.eyeTextureResolutionScale = Config.CameraResolution.Value / 100f;

        // Input settings
        InputSystem.settings.backgroundBehavior =
            InputSettings.BackgroundBehavior.IgnoreFocus; // Prevent VR from getting disabled when losing focus

        return true;
    }

    public static MethodInfo GetConfigGetter()
    {
        return typeof(Plugin).GetProperty(nameof(Config),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
            .GetGetMethod(true);
    }
}

[Flags]
public enum Flags
{
    VR = 1 << 0,
    StartupFailed = 1 << 1,
    EyeTrackingDebug = 1 << 2
}