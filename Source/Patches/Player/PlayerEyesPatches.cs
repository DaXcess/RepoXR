using HarmonyLib;
using RepoXR.Input;
using UnityEngine;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerEyesPatches
{
    [HarmonyPatch(typeof(PlayerEyes), nameof(PlayerEyes.LookAtTransform))]
    [HarmonyPostfix]
    private static void LookAtTransformEyeTracking(PlayerEyes __instance)
    {
        // TODO: Use synced values here if the remote player has eye tracking
        if (!__instance.playerAvatar.isLocal)
            return;
        
        // TODO: First check if setting is disabled, once setting is added
        if (!Actions.Instance.EyeGazeTracked.IsPressed())
            return;
        
        // TODO: Determine if certain occurrences in the game should override eye gaze
        // TODO: Network sync this stuff as well somehow

        var gazePosition = Actions.Instance.EyeGazePosition.ReadValue<Vector3>();
        var gazeRotation = Actions.Instance.EyeGazeRotation.ReadValue<Quaternion>();
        
        // Some magic raycasting bs here
        
        __instance.lookAtActive = true;
        __instance.lookAt.transform.position = Vector3.zero;  /* raycast using gazePosition and gazeRotation */
    }
}