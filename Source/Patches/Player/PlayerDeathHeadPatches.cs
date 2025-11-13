using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Player;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerDeathHeadPatches
{
    /// <summary>
    /// Replace the "on screen" detection with custom detection that is better suited for VR and supports
    /// eye tracking
    /// </summary>
    [HarmonyPatch(typeof(PlayerDeathHead), nameof(PlayerDeathHead.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LookAtHeadPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.OnScreen))))
            .SetOperandAndAdvance(Method(typeof(VREyeTracking), nameof(VREyeTracking.LookingAt)))
            .InstructionEnumeration();
    }
}