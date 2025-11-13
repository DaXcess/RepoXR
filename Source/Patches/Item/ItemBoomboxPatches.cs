using HarmonyLib;
using RepoXR.Player.Camera;
using UnityEngine;

namespace RepoXR.Patches.Item;

[RepoXRPatch]
internal static class ItemBoomboxPatches
{
    /// <summary>
    /// Make the boombox head bop work in VR
    /// </summary>
    [HarmonyPatch(typeof(ValuableBoombox), nameof(ValuableBoombox.Update))]
    [HarmonyPostfix]
    private static void BoomboxAimPatch(ValuableBoombox __instance)
    {
        if (!__instance.physGrabObject.grabbed || !PhysGrabber.instance ||
            PhysGrabber.instance.grabbedObject != __instance.physGrabObject.rb)
            return;
        
        var bopSpeed = Plugin.Config.ReducedAimImpact.Value ? 5 : 15;
        var bopMultiplier = Plugin.Config.ReducedAimImpact.Value ? 0.15f : 0.5f;

        var cameraPosition = PhysGrabber.instance.playerAvatar.localCamera.transform.position;
        var cameraForward = PhysGrabber.instance.playerAvatar.localCamera.transform.forward * 2;
        var upOffset = Vector3.up * Mathf.Sin(Time.time * bopSpeed) * bopMultiplier;
        var lookAtPosition = cameraPosition + cameraForward + upOffset;

        VRCameraAim.instance.SetAimTargetSoft(lookAtPosition, 0.01f, 10, 10, __instance.gameObject, 100);
    }
}