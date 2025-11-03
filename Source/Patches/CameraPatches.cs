using HarmonyLib;
using RepoXR.Managers;
using UnityEngine;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class CameraPatches
{
    /// <summary>
    /// Prevent setting camera target texture since in VR we need to render directly from the gameplay camera
    /// </summary>
    [HarmonyPatch(typeof(Camera), nameof(Camera.targetTexture), MethodType.Setter)]
    [HarmonyPrefix]
    private static void DisableTargetTextureOverride(Camera __instance, ref RenderTexture? value)
    {
        // We make an exception for our manually rendered custom camera
        if (__instance.name.StartsWith("Custom Camera"))
            return;

        value = null;
    }

    /// <summary>
    /// Disable the main menu camera pan when booting the game
    /// </summary>
    [HarmonyPatch(typeof(CameraMainMenu), nameof(CameraMainMenu.Awake))]
    [HarmonyPostfix]
    private static void DisableMainMenuAnimation(CameraMainMenu __instance)
    {
        __instance.introLerp = 1;
    }

    /// <summary>
    /// Disable aim offset (the small rotation animation when loading into a level)
    /// </summary>
    [HarmonyPatch(typeof(CameraAimOffset), nameof(CameraAimOffset.Awake))]
    [HarmonyPostfix]
    private static void DisableCameraAimOffset(CameraAimOffset __instance)
    {
        __instance.enabled = false;
    }

    /// <summary>
    /// Disable the camera top fade, which is only used for the map tool
    /// </summary>
    [HarmonyPatch(typeof(CameraTopFade), nameof(CameraTopFade.Set))]
    [HarmonyPrefix]
    private static bool DisableCameraTopFade()
    {
        return false;
    }
    
    /// <summary>
    /// Patch to see if something is visible in the VR camera space
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.OnScreen))]
    [HarmonyPrefix]
    private static bool OnScreenVRPatch(Vector3 position, ref float paddWidth, ref float paddHeight, ref bool __result)
    {
        // Add some extra padding if it's too small since in VR the edges are almost never visible to the eye
        if (paddWidth is < 0 and >= -0.1f)
            paddWidth -= 0.1f;

        if (paddHeight is < 0 and >= -0.1f)
            paddHeight -= 0.05f;
        
        __result = OnScreenVR(position, paddWidth, paddHeight);

        return false;
    }

    private static bool OnScreenVR(Vector3 position, float padWidth, float padHeight)
    {
        var cam = CameraUtils.Instance.MainCamera;
        var screenPoint = cam.WorldToViewportPoint(position);

        if (screenPoint.z < 0)
            return false;

        return screenPoint.x > -padWidth && screenPoint.x < 1 + padWidth && 
               screenPoint.y > -padHeight && screenPoint.y < 1 + padHeight;
    }

    /// <summary>
    /// Assign the <see cref="PlayerLocalCamera"/> transform the same values as our VR camera
    /// </summary>
    [HarmonyPatch(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.Update))]
    [HarmonyPostfix]
    private static void AlignWithVRCameraPatch(PlayerLocalCamera __instance)
    {
        if (SemiFunc.IsMultiplayer() && !__instance.photonView.IsMine)
            return;

        if (VRSession.Instance is not { } session)
            return;

        __instance.transform.position = session.MainCamera.transform.position;
        __instance.transform.rotation = session.MainCamera.transform.rotation;
    }
}