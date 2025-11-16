using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerControllerPatches
{
    /// <summary>
    /// Use the camera's world rotation instead of local rotation for determining which direction to walk in
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ApplyCorrectWalkingForce(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerController), nameof(PlayerController.cameraGameObject))))
            .Advance(2)
            .SetOperandAndAdvance(PropertyGetter(typeof(Transform), nameof(Transform.rotation)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Only apply movement normalization if it's configured to do so
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConditionalApplyNormalizedMove(
        IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call, PropertyGetter(typeof(Vector3), nameof(Vector3.normalized))))
            .Set(OpCodes.Call, ((Func<Vector3, Vector3>)ConditionalNormalize).Method)
            .Advance(-2)
            .RemoveInstructions(2)
            .InstructionEnumeration();

        static Vector3 ConditionalNormalize(Vector3 input) =>
            Plugin.Config.NormalizeMovement.Value ? input.normalized : input;
    }
}