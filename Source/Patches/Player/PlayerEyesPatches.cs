using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerEyesPatches
{
    [HarmonyPatch(typeof(PlayerEyes), nameof(PlayerEyes.LookAtTransform))]
    [HarmonyPostfix]
    private static void LookAtTransformEyeTracking(PlayerEyes __instance)
    {
        // TODO: Check if we can just do this with the local player
        if (__instance.playerAvatar.isLocal)
            return;
        
        // TODO: Determine if certain occurrences in the game should override eye gaze

        if (!NetworkSystem.instance.GetNetworkPlayer(__instance.playerAvatar, out var player) && player.EyeTracking)
            return;

        __instance.lookAtActive = true;
        __instance.lookAt.transform.position = player.EyeGazePoint;
    }
}