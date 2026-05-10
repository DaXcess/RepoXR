using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RepoXR.Data;
using RepoXR.Input;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RepoXR.Assets;

internal static class AssetCollection
{
    private static AssetBundle assetBundle;

    public static OpenXRFeaturePack OpenXRFeatures;

    public static RemappableControls RemappableControls;
    
    public static GameObject RebindHeader;
    public static GameObject RebindButton;
    public static GameObject RebindButtonToggle;
    public static GameObject VRRig;
    public static GameObject CustomCamera;
    public static GameObject VRTumble;
    public static GameObject Keyboard;
    public static GameObject ExpressionWheel;
    public static GameObject ValuableDiscover;
    public static GameObject FocusSphere;

    public static GameObject MenuSettings;
    public static GameObject MenuSettingsCategory;
    public static GameObject MenuShowcase;
    public static GameObject RuntimeSetting;
    public static GameObject BoolSetting;
    public static GameObject SliderSetting;
    public static GameObject VRSettingsButton;
    
    public static InputActionAsset DefaultXRActions;
    public static InputActionAsset VRInputs;

    public static Material DefaultLine;
    public static Material VideoOverlay;

    public static TMP_SpriteAsset TMPInputsSpriteAsset;

    public static Sprite Logo;

    public static Shader VignetteShader;

    public static AnimationCurveData OverchargeHapticCurve;
    public static AnimationCurveData GrabberHapticCurve;
    public static AnimationCurveData HurtHapticCurve;
    public static AnimationCurveData EyeAttachHapticCurve;
    public static AnimationCurveData KeyboardAnimation;

    public static GameObject Cube;

    [PublicAPI]
    public static string AddressablesPath =>
        Path.Combine(Path.GetDirectoryName(Plugin.Config.AssemblyPath)!, "Addressables");
    
    public static bool LoadAssets()
    {
        if (!ValidateFPTSVersion())
        {
            Logger.LogError("Failed to validate FixPluginTypesSerialization version. Make sure that this mod is installed and up-to-date before launching RepoXR.");
            Logger.LogError("In the case that the mod already appears to be up-to-date: uninstall it, clear your mod manager cache, and reinstall it.");

            return false;
        }

        assetBundle =
            AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Plugin.Config.AssemblyPath)!, "repoxrassets"));

        if (assetBundle == null)
        {
            Logger.LogError("Failed to load asset bundle!");
            return false;
        }

        // Load translations
        Addressables.LoadContentCatalogAsync(Path.Combine(AddressablesPath, "catalog_0.1.json"), true)
            .WaitForCompletion();

        OpenXRFeatures = assetBundle.LoadAsset<OpenXRFeaturePack>("OpenXRFeatures");

        RemappableControls = assetBundle.LoadAsset<GameObject>("RemappableControls").GetComponent<RemappableControls>();
        
        RebindHeader = assetBundle.LoadAsset<GameObject>("Rebind Header");
        RebindButton = assetBundle.LoadAsset<GameObject>("Rebind Button");
        RebindButtonToggle = assetBundle.LoadAsset<GameObject>("Rebind Button Toggle");
        VRRig = assetBundle.LoadAsset<GameObject>("VRRig");
        CustomCamera = assetBundle.LoadAsset<GameObject>("Custom Camera");
        VRTumble = assetBundle.LoadAsset<GameObject>("VRTumble");
        Keyboard = assetBundle.LoadAsset<GameObject>("NonNativeKeyboard");
        ExpressionWheel = assetBundle.LoadAsset<GameObject>("Expression Radial");
        ValuableDiscover = assetBundle.LoadAsset<GameObject>("Valuable Discover");
        FocusSphere = assetBundle.LoadAsset<GameObject>("Focus Sphere");
        
        MenuSettings = assetBundle.LoadAsset<GameObject>("VR Settings Page");
        MenuSettingsCategory = assetBundle.LoadAsset<GameObject>("VR Settings Page - Category");
        MenuShowcase = assetBundle.LoadAsset<GameObject>("VR Showcase Page");
        RuntimeSetting = assetBundle.LoadAsset<GameObject>("Runtime Setting");
        BoolSetting = assetBundle.LoadAsset<GameObject>("Bool Setting");
        SliderSetting = assetBundle.LoadAsset<GameObject>("Slider Setting");
        VRSettingsButton = assetBundle.LoadAsset<GameObject>("VR Settings Button");
        
        DefaultXRActions = assetBundle.LoadAsset<InputActionAsset>("DefaultXRActions");
        VRInputs = assetBundle.LoadAsset<InputActionAsset>("VRInputs");
        
        DefaultLine = assetBundle.LoadAsset<Material>("Default-Line");
        VideoOverlay = assetBundle.LoadAsset<Material>("Video Overlay");
        
        TMPInputsSpriteAsset = assetBundle.LoadAsset<TMP_SpriteAsset>("TMPInputsSpriteAsset");

        Logo = assetBundle.LoadAsset<Sprite>("REPOXR_Splash_Logo");
        
        VignetteShader = assetBundle.LoadAsset<Shader>("VignetteVR");

        GrabberHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("GrabberHapticCurve");
        OverchargeHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("OverchargeHapticCurve");
        HurtHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("HurtHapticCurve");
        EyeAttachHapticCurve = assetBundle.LoadAsset<AnimationCurveData>("EyeAttachHapticCurve");
        KeyboardAnimation = assetBundle.LoadAsset<AnimationCurveData>("KeyboardAnimation");

        Cube = assetBundle.LoadAsset<GameObject>("JustACube");

        return true;
    }

    public static AsyncOperationHandle<LocalizedAsset> GetLocalizedAsset(string name)
    {
        return Addressables.LoadAssetAsync<LocalizedAsset>($"LocalizedAsset XR - {name}");
    }

    private static bool ValidateFPTSVersion()
    {
        var fpts = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(asm => asm.GetName().Name == "FixPluginTypesSerialization");
        if (fpts == null)
        {
            Logger.LogError("FPTS check failed: Assembly missing from AppDomain");
            return false;
        }

        // Interface was added with the switch to Unity 2022.3.67f3
        var hasInterface = fpts.DefinedTypes.Any(type =>
            type.FullName == "FixPluginTypesSerialization.UnityPlayer.Structs.Default.IIsFileCreatedParam");
        if (hasInterface) return true;

        Logger.LogError("FPTS check failed: Missing required interface (mod is outdated)");
        return false;
    }
}