using System.IO;
using RepoXR.Data;
using RepoXR.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Assets;

internal static class AssetCollection
{
    private static AssetBundle assetBundle;

    public static RemappableControls RemappableControls;
    
    public static GameObject RebindHeader;
    public static GameObject RebindButton;
    public static GameObject RebindButtonToggle;
    public static GameObject VRRig;
    public static GameObject CustomCamera;
    public static GameObject VRTumble;
    public static GameObject Keyboard;
    public static GameObject ExpressionWheel;

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
    
    public static bool LoadAssets()
    {
        assetBundle =
            AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Plugin.Config.AssemblyPath)!, "repoxrassets"));

        if (assetBundle == null)
        {
            Logger.LogError("Failed to load asset bundle!");
            return false;
        }

        RemappableControls = assetBundle.LoadAsset<GameObject>("RemappableControls").GetComponent<RemappableControls>();
        
        RebindHeader = assetBundle.LoadAsset<GameObject>("Rebind Header");
        RebindButton = assetBundle.LoadAsset<GameObject>("Rebind Button");
        RebindButtonToggle = assetBundle.LoadAsset<GameObject>("Rebind Button Toggle");
        VRRig = assetBundle.LoadAsset<GameObject>("VRRig");
        CustomCamera = assetBundle.LoadAsset<GameObject>("Custom Camera");
        VRTumble = assetBundle.LoadAsset<GameObject>("VRTumble");
        Keyboard = assetBundle.LoadAsset<GameObject>("NonNativeKeyboard");
        ExpressionWheel = assetBundle.LoadAsset<GameObject>("Expression Radial");
        
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

        if (RemappableControls?.controls == null)
        {
            Logger.LogError(
                "Unity failed to deserialize some assets. Are you missing the FixPluginTypesSerialization mod?");
            Logger.LogWarning(
                "I swear to god if you screenshot this and ask \"what is wrong?!\" without acknowledging the above error message I'm going to flip (IRL, and break my neck probably).");

            return false;
        }

        return true;
    }
}