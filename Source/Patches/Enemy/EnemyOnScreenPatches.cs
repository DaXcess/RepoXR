using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Player;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Enemy;

[RepoXRPatch]
internal static class EnemyOnScreenPatches
{
    /// <summary>
    /// Replace the "on screen" detection in enemies with custom detection that is better suited for VR and supports
    /// eye tracking
    /// </summary>
    [HarmonyPatch(typeof(EnemyOnScreen), nameof(EnemyOnScreen.Logic), MethodType.Enumerator)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LogicPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.OnScreen))))
            .SetOperandAndAdvance(Method(typeof(VREyeTracking), nameof(VREyeTracking.LookingAt)))
            .InstructionEnumeration();
    }
}