using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RepoXR.Managers;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class CrouchPatches
{
    /// <summary>
    /// Disable the crouch position offset if we're roomscale crouching
    /// </summary>
    [HarmonyPatch(typeof(CameraCrouchPosition), nameof(CameraCrouchPosition.Update))]
    [HarmonyPostfix]
    private static void CrouchPositionPatch(CameraCrouchPosition __instance)
    {
        if (VRSession.Instance is not { } session)
            return;

        if (!session.Player.physicalCrouch)
            return;

        __instance.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Since we are making use of the <see cref="PlayerController.toggleCrouch"/> field, prevent the game from resetting it
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> KeepCrouchTogglePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.isTumbling))))
            .MatchBack(false,
                new CodeMatch(OpCodes.Stfld, Field(typeof(PlayerController), nameof(PlayerController.toggleCrouch))))
            .Advance(-1)
            .RemoveInstruction()
            .Insert(
                new CodeInstruction(OpCodes.Call, ((Func<bool>)IsRoomscaleCrouching).Method)
            )
            .InstructionEnumeration();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsRoomscaleCrouching()
        {
            return VRSession.Instance is { Player.physicalCrouch: true };
        }
    }
}