using System;
using BepInEx.Configuration;
using RepoXR.Assets;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using UnityEngine;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace RepoXR;

public class Config(string assemblyPath, ConfigFile file)
{
    public string AssemblyPath { get; } = assemblyPath;
    public ConfigFile File { get; } = file;

    // General configuration

    [ConfigDescriptor(customName: "Enable VR", trueText: "Disable", falseText: "Enable")]
    public ConfigEntry<bool> DisableVR { get; } = file.Bind("General", nameof(DisableVR), false,
        "Disables the main functionality of this mod, can be used if you want to play without VR while keeping the mod installed.");

    [ConfigDescriptor]
    public ConfigEntry<bool> VerboseLogging { get; } = file.Bind("General", nameof(VerboseLogging), false,
        "Enables verbose debug logging during OpenXR initialization");

    // Gameplay configuration

    [ConfigDescriptor]
    public ConfigEntry<bool> ReducedAimImpact { get; } = file.Bind("Gameplay", nameof(ReducedAimImpact), false,
        "When enabled, lowers the severity of force-look events (like the ceiling eye), which can be helpful for people with motion sickness");

    [ConfigDescriptor]
    public ConfigEntry<bool> RoomscaleCrouch { get; } = file.Bind("Gameplay", nameof(RoomscaleCrouch), true,
        "When enabled, allows for the player to physically crouch to also crouch in-game");

    [ConfigDescriptor(customName: "Dominant Hand", falseText: "Right", trueText: "Left")]
    public ConfigEntry<bool> LeftHandDominant { get; } = file.Bind("Gameplay", nameof(LeftHandDominant), false,
        "Whether to use the left or right hand as dominant hand (the hand used to pick up items)");

    [ConfigDescriptor]
    public ConfigEntry<HapticFeedbackOption> HapticFeedback { get; } =
        file.Bind("Gameplay", nameof(HapticFeedback), HapticFeedbackOption.All,
            new ConfigDescription(
                "Controls how much haptic feedback you will experience while playing with the VR mod.",
                new AcceptableValueEnum<HapticFeedbackOption>()));

    [ConfigDescriptor(falseText: "Disabled", trueText: "Enabled")]
    public ConfigEntry<bool> EyeTracking { get; } = file.Bind("Gameplay", nameof(EyeTracking), true,
        "Whether eye tracking should be used for certain gameplay elements (if supported by the hardware)");

    [ConfigDescriptor(falseText: "Attached", trueText: "Separated")]
    public ConfigEntry<bool> ArmsAttached { get; } = file.Bind("Gameplay", nameof(ArmsAttached), true,
        "Whether locally the arms should be attached to your body, or they should be free floating \"hands\"");
    
    [ConfigDescriptor(pointerSize: 0.01f, stepSize: 0.05f)]
    public ConfigEntry<float> HUDPlaneOffset { get; } = file.Bind("Gameplay", nameof(HUDPlaneOffset), -0.45f,
        new ConfigDescription("The default height offset for the HUD", new AcceptableValueRange<float>(-0.6f, 0.5f)));

    [ConfigDescriptor(pointerSize: 0.01f, stepSize: 0.05f)]
    public ConfigEntry<float> HUDGazePlaneOffset { get; } = file.Bind("Gameplay", nameof(HUDGazePlaneOffset), -0.25f,
        new ConfigDescription("The height offset for the HUD when looking at it", new AcceptableValueRange<float>(-0.6f, 0.5f)));

    [ConfigDescriptor(pointerSize: 0.05f, stepSize: 0.25f)]
    public ConfigEntry<float> SmoothCanvasDistance { get; } = file.Bind("Gameplay", nameof(SmoothCanvasDistance), 1.5f,
        new ConfigDescription("The distance that the smooth canvas should be away from the main camera",
            new AcceptableValueRange<float>(1.25f, 3)));

    // Performance configuration

    [ConfigDescriptor(stepSize: 5f, suffix: "%")]
    public ConfigEntry<int> CameraResolution { get; } = file.Bind("Performance", nameof(CameraResolution), 100,
        new ConfigDescription(
            "This setting configures the resolution scale of the game, lower values are more performant, but will make the game look worse.",
            new AcceptableValueRange<int>(5, 200)));

    // Input configuration
    
    [ConfigDescriptor(enumDisableBar: true)]
    public ConfigEntry<TurnProviderOption> TurnProvider { get; } = file.Bind("Input", nameof(TurnProvider),
        TurnProviderOption.Smooth,
        new ConfigDescription("Specify which turning provider your player uses, if any.",
            new AcceptableValueEnum<TurnProviderOption>()));

    [ConfigDescriptor(stepSize: 0.05f, pointerSize: 0.01f, suffix: "x")]
    public ConfigEntry<float> SmoothTurnSpeedModifier { get; } = file.Bind("Input", nameof(SmoothTurnSpeedModifier), 1f,
        new ConfigDescription(
            "A multiplier that is added to the smooth turning speed. Requires turn provider to be set to smooth.",
            new AcceptableValueRange<float>(0.25f, 5)));

    [ConfigDescriptor]
    public ConfigEntry<bool> DynamicSmoothSpeed { get; } = file.Bind("Input", nameof(DynamicSmoothSpeed), true,
        "When enabled, makes the speed of the smooth turning dependent on how far the analog stick is pushed.");

    [ConfigDescriptor(stepSize: 5, suffix: "°")]
    public ConfigEntry<float> SnapTurnSize { get; } = file.Bind("Input", nameof(SnapTurnSize), 45f,
        new ConfigDescription(
            "The amount of rotation that is applied when performing a snap turn. Requires turn provider to be set to snap.",
            new AcceptableValueRange<float>(10, 180)));

    // Rendering configuration

    [ConfigDescriptor]
    public ConfigEntry<bool> Vignette { get; } = file.Bind("Rendering", nameof(Vignette), true,
        "Enables the vignette shader used in certain scenarios and levels in the game.");

    [ConfigDescriptor]
    public ConfigEntry<bool> CustomCamera { get; } =
        file.Bind("Rendering", nameof(CustomCamera), false,
            "Adds a second camera mounted on top of the VR camera that will render separately from the VR camera to the display. This requires extra GPU power!");

    [ConfigDescriptor(stepSize: 5)]
    public ConfigEntry<float> CustomCameraFOV { get; } = file.Bind("Rendering", nameof(CustomCameraFOV), 75f,
        new ConfigDescription("The field of view that the custom camera should have.",
            new AcceptableValueRange<float>(45, 120)));

    [ConfigDescriptor(percentage: true, stepSize: 0.1f, pointerSize: 0.05f)]
    public ConfigEntry<float> CustomCameraSmoothing { get; } = file.Bind("Rendering", nameof(CustomCameraSmoothing),
        0.5f,
        new ConfigDescription("The amount of smoothing that is applied to the custom camera.",
            new AcceptableValueRange<float>(0, 1)));

    // Internal configuration

    public ConfigEntry<string> ControllerBindingsOverride { get; } = file.Bind("Internal",
        nameof(ControllerBindingsOverride), "", "FOR INTERNAL USE ONLY, DO NOT EDIT");

    public ConfigEntry<string> InputToggleBindings { get; } = file.Bind("Internal", nameof(InputToggleBindings), "",
        "FOR INTERNAL USE ONLY, DO NOT EDIT");

    public ConfigEntry<string> OpenXRRuntimeFile { get; } = file.Bind("Internal", nameof(OpenXRRuntimeFile), "",
        "FOR INTERNAL USE ONLY, DO NOT EDIT");

    private static bool leftHandedWarningShown;
    
    /// <summary>
    /// Create persistent callbacks that persist for the entire duration of the application
    /// </summary>
    public void SetupGlobalCallbacks()
    {
        if (!VRSession.InVR)
            return;

        CameraResolution.SettingChanged += (_, _) =>
        {
            XRSettings.eyeTextureResolutionScale = CameraResolution.Value / 100f;
        };

        CustomCamera.SettingChanged += (_, _) =>
        {
            if (CustomCamera.Value)
                Object.Instantiate(AssetCollection.CustomCamera, Camera.main!.transform.parent);
            else
                Object.Destroy(VRCustomCamera.instance.gameObject);
        };

        LeftHandDominant.SettingChanged += (_, _) =>
        {
            if (!LeftHandDominant.Value || leftHandedWarningShown)
                return;

            leftHandedWarningShown = true;
            MenuManager.instance.PagePopUpScheduled("Left Handed Notice", Color.yellow,
                "Left handed mode does not change your default bindings. To set up proper bindings for left handed mode, change your bindings by going to\nSettings -> Controls",
                "Will do", false);
            MenuManager.instance.PagePopUpScheduledShow();
        };
    }

    public enum HapticFeedbackOption
    {
        Off,
        Reduced,
        All
    }

    public enum TurnProviderOption
    {
        Snap,
        Smooth,
        Disabled
    }
}

internal class AcceptableValueEnum<T>() : AcceptableValueBase(typeof(T))
    where T: Enum
{
    private readonly string[] names = Enum.GetNames(typeof(T));

    public override object Clamp(object value) => value;
    public override bool IsValid(object value) => true;
    public override string ToDescriptionString() => $"# Acceptable values: {string.Join(", ", names)}";
}

[AttributeUsage(AttributeTargets.Property)]
public class ConfigDescriptorAttribute(
    string? customName = null,
    bool percentage = false,
    float stepSize = 1.0f,
    float pointerSize = 1.0f,
    string suffix = "",
    string trueText = "On",
    string falseText = "Off",
    bool enumDisableBar = false) : Attribute
{
    public string? CustomName => customName;
    public bool Percentage => percentage;
    public float StepSize => stepSize;
    public float PointerSize => pointerSize;
    public string Suffix => suffix;
    public string TrueText => trueText;
    public string FalseText => falseText;
    public bool EnumDisableBar => enumDisableBar;
}