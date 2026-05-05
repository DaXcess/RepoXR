using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class InfoTextTriggerFinderPatches
{
    /// <summary>
    /// Trigger info text by aiming your hand at it instead of your head
    /// </summary>
    [HarmonyPatch(typeof(InfoTextTriggerFinder), nameof(InfoTextTriggerFinder.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RaycastFromArmPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }
}