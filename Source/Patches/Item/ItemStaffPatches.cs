using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Item;

[RepoXRPatch]
internal static class ItemStaffPatches
{
    /// <summary>
    /// Activate the torque staff based on hand rotation instead of head rotation
    /// </summary>
    [HarmonyPatch(typeof(ItemStaffTorque), nameof(ItemStaffTorque.SetCameraPitch))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TorqueStaffPitchPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Activate the void staff based on hand rotation instead of head rotation
    /// </summary>
    [HarmonyPatch(typeof(ItemStaffVoid), nameof(ItemStaffVoid.SetCameraPitch))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> VoidStaffPitchPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Activate the zero gravity staff based on hand rotation instead of head rotation
    /// </summary>
    [HarmonyPatch(typeof(ItemStaffZeroGravity), nameof(ItemStaffZeroGravity.SetCameraPitch))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ZeroGravityStaffPitchPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Properly align the torque staff when it is not charged
    /// </summary>
    [HarmonyPatch(typeof(ItemStaffTorque), nameof(ItemStaffTorque.AlignToWorldUp))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TorqueStaffWorldUpPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Properly align the void staff when it is not charged
    /// </summary>
    [HarmonyPatch(typeof(ItemStaffVoid), nameof(ItemStaffVoid.AlignToWorldUp))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> VoidStaffWorldUpPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Properly align the zero gravity staff when it is not charged
    /// </summary>
    [HarmonyPatch(typeof(ItemStaffZeroGravity), nameof(ItemStaffZeroGravity.AlignToWorldUp))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ZeroGravityStaffWorldUpPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }
}