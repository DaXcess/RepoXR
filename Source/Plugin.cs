﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using BepInEx;
using JetBrains.Annotations;
using RepoXR.Patches;
using UnityEngine.InputSystem;

namespace RepoXR;

[PublicAPI]
[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "io.daxcess.repoxr";
    public const string PLUGIN_NAME = "RepoXR";
    public const string PLUGIN_VERSION = "0.1.0";
    
    #if DEBUG
    private const string SKIP_CHECKSUM_VAR = $"--repoxr-skip-checksum={PLUGIN_VERSION}-dev";
    #else
    private const string SKIP_CHECKSUM_VAR = $"--repoxr-skip-checksum={PLUGIN_VERSION}";
    #endif

    private const string HASHES_OVERRIDE_URL = "https://gist.githubusercontent.com/DaXcess/033e8ff514c505d2372e6f55a412dc00/raw";

    private readonly string[] GAME_ASSEMBLY_HASHES =
    [
        // Nothing for now
    ];
    
    public new static Config Config { get; private set; } = null!;
    public static Flags Flags { get; private set; } = 0;
    
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

        // Verify game assembly to detect compatible version
        var allowUnverified = Environment.GetCommandLineArgs().Contains(SKIP_CHECKSUM_VAR);

        if (!VerifyGameVersion())
        {
            if (allowUnverified)
            {
                Logger.LogWarning("Warning: Unsupported game version, or corrupted/pirated game detected!");
                Logger.LogWarning("RepoXR might not work properly. Please consider updating your game and RepoXR before creating bug reports.");
            }
            else
            {
                Logger.LogError("Error: Unsupported game version, or corrupted/pirated game detected!");
                Logger.LogError("RepoXR only supports legitimate Steam copies of R.E.P.O.");
                Logger.LogError("R.E.P.O. might have been updated recently, which will also trigger this error.");
                Logger.LogDebug(
                    $"To bypass this check, add the following flag to your launch options in Steam: {SKIP_CHECKSUM_VAR}");

                return;
            }
        }
        
        if (!PreloadRuntimeDependencies())
        {
            Logger.LogError("Disabling mod because required runtime dependencies could not be loaded!");
            return;
        }

        if (!Assets.AssetCollection.LoadAssets())
        {
            Logger.LogError("Disabling mod because assets could not be loaded!");
            return;
        }

        if (!disableVr && InitializeVR())
            Flags |= Flags.VR;
        
        HarmonyPatcher.PatchUniversal();
        
        Logger.LogDebug("Inserted universal patches using Harmony");
        
        Native.BringGameWindowToFront();
    }

    private static string GetCommitHash()
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

    private bool VerifyGameVersion()
    {
        var location = Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll");
        var hash = BitConverter.ToString(Utils.ComputeHash(File.ReadAllBytes(location))).Replace("-", "");
        
        // Attempt local lookup first
        if (GAME_ASSEMBLY_HASHES.Contains(hash))
        {
            Logger.LogInfo("Game version verified using local hashes");

            return true;
        }
        
        Logger.LogWarning("Failed to verify ame version using local hashes, checking remotely for updated hashes...");
        
        // Attempt to fetch a gist with known working assembly hashes
        // This allows me to keep RepoXR up and running if the game updates, without having to push an update out
        try
        {
            var contents = new WebClient().DownloadString(HASHES_OVERRIDE_URL);
            var hashes = Utils.ParseConfig(contents);

            if (!hashes.Contains(hash))
                return false;

            Logger.LogInfo("Game version verified using remote hashes");

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to verify using remote hashes: {ex.Message}");

            return false;
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

                // Ignore known unmanaged libraries
                if (filename is "UnityOpenXR.dll" or "openxr_loader.dll")
                    continue;

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
            Logger.LogError($"Unexpected error occured while preloading runtime dependencies (incorrect folder structure?): {ex.Message}");
            
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
        
        if (OpenXR.GetActiveRuntimeName(out var name) && OpenXR.GetActiveRuntimeVersion(out var major, out var minor, out var patch))
            RepoXR.Logger.LogInfo($"OpenXR runtime being used: {name} ({major}.{minor}.{patch})");
        else
            RepoXR.Logger.LogError("Could not get OpenXR runtime info?");

        HarmonyPatcher.PatchVR();
        
        RepoXR.Logger.LogDebug("Inserted VR patches using Harmony");
        
        // TODO: Change render pipeline settings if needed
        
        // Input settings (TODO: maybe make configurable)
        InputSystem.settings.defaultButtonPressPoint = 0.5f;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // Prevent VR from getting disabled when losing focus

        return true;
    }
}

[Flags]
public enum Flags
{
    VR = 1 << 0,
    StartupFailed = 1 << 1
}