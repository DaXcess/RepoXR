using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerAvatarPatches
{
    /// <summary>
    /// Detect when the player has died
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathDone))]
    [HarmonyPostfix]
    private static void OnPlayerDeath(PlayerAvatar __instance)
    {
        if (!__instance.isLocal || VRSession.Instance is not { } session)
            return;

        session.Player.Rig.SetVisible(false);
    }

    /// <summary>
    /// Detect when the player has been revived
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ReviveRPC))]
    [HarmonyPostfix]
    private static void OnPlayerRevive(PlayerAvatar __instance)
    {
        if (!__instance.isLocal || VRSession.Instance is not { } session)
            return;

        session.Player.Rig.SetVisible(true);

        // Reset CameraAimOffset (for when revived during the top-down death sequence)
        var offsetTransform = CameraAimOffset.Instance.transform;
        offsetTransform.localRotation = Quaternion.identity;
        offsetTransform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Look at the enemy that killed you (if possible)
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PlayerDeathLookAtEnemyPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldsfld, Field(typeof(CameraAim), nameof(CameraAim.Instance))))
            .SetOperandAndAdvance(Field(typeof(VRCameraAim), nameof(VRCameraAim.instance)))
            .Advance(9)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
            .SetOperandAndAdvance(Method(typeof(VRCameraAim), nameof(VRCameraAim.SetAimTarget)))
            .InstructionEnumeration();
    }
}