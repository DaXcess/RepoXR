using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using UnityEngine;
using static HarmonyLib.AccessTools;

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
        if (__instance.name is "Camera Overlay" or "Camera Main")
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

    /// <summary>
    /// Make sure to synchronize the VR camera transforms instead of only the aim transforms
    /// </summary>
    [HarmonyPatch(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.OnPhotonSerializeView))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CameraSerializeVRParams(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldsfld, Field(typeof(CameraAim), nameof(CameraAim.Instance))))
            .Repeat(matcher => matcher.SetOpcodeAndAdvance(OpCodes.Ldarg_0))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make sure the local camera override actually knows about VR position overrides as well
    /// </summary>
    [HarmonyPatch(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideActive))]
    [HarmonyPrefix]
    private static bool GetOverrideActivePatch(PlayerLocalCamera __instance, ref bool __result)
    {
        __result = __instance.clientPositionOverride ||
                   (__instance.playerAvatar.isLocal && VRCameraPosition.instance.overridePositionActive);

        return false;
    }

    /// <summary>
    /// Force disable preview camera when the menu avatar is destroyed as the game thinks the preview camera is the
    /// main camera, causing a brief flicker between the preview camera and the main camera
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatarMenuHover), nameof(PlayerAvatarMenuHover.OnDestroy))]
    [HarmonyPostfix]
    private static void DisableMenuPlayerCamera(PlayerAvatarMenuHover __instance)
    {
        if (__instance.renderTextureInstance && __instance.previewCamera) __instance.previewCamera.enabled = false;
    }
}