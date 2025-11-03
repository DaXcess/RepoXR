using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Networking;

namespace RepoXR.Patches.Player;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class PlayerEyesPatches
{
    /// <summary>
    /// Make players that use eye tracking have their eyes controller by their *real* eyes
    /// </summary>
    [HarmonyPatch(typeof(PlayerEyes), nameof(PlayerEyes.LookAtTransform))]
    [HarmonyPostfix]
    private static void LookAtTransformEyeTracking(PlayerEyes __instance)
    {
        if (!__instance.playerAvatar || __instance.playerAvatar.isLocal)
            return;
        
        // TODO: Determine if certain occurrences in the game should override eye gaze

        if (!NetworkSystem.instance.GetNetworkPlayer(__instance.playerAvatar, out var player) || !player.EyeTracking)
            return;

        __instance.lookAtActive = true;
        __instance.lookAt.transform.position = player.EyeGazePoint;
    }

    /// <summary>
    /// Make sure eye tracked players don't move their heads by merely looking around
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatarVisuals), nameof(PlayerAvatarVisuals.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> KeepHeadRotationPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 40f))
            .SetAndAdvance(OpCodes.Ldarg_0, null)
            .Insert(new CodeInstruction(OpCodes.Call, ((Func<PlayerAvatarVisuals, float>)GetMaxAngle).Method))
            .InstructionEnumeration();

        static float GetMaxAngle(PlayerAvatarVisuals visuals)
        {
            if (visuals.isMenuAvatar)
                return 40;

            if (!NetworkSystem.instance.GetNetworkPlayer(visuals.playerAvatar, out var player) || !player.EyeTracking)
                return 40;

            // Do not angle the head with the eyes if eye tracking is enabled
            return 0;
        }
    }
}