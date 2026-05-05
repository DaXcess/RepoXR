// ReSharper disable UnusedVariable

using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace RepoXR;

#if DEBUG
internal static class Experiments
{
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    [HarmonyPostfix]
    private static void InfiniteSprintPatch(PlayerController __instance)
    {
        __instance.EnergyCurrent = __instance.EnergyStart;
    }

    [HarmonyPatch(typeof(ResultScreenUI), nameof(ResultScreenUI.StateFadeIn))]
    [HarmonyPrefix]
    private static void HmmYesCosmetics(ResultScreenUI __instance)
    {
        if (!__instance.stateImpulse) return;

        RoundDirector.instance.cosmeticWorldObjectsExtracted.AddRange([
            SemiFunc.Rarity.Common, SemiFunc.Rarity.Common, SemiFunc.Rarity.Uncommon
        ]);
    }

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.HeadEnergyLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FastRechargeHead(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 100f))
            .SetOperandAndAdvance(0.5f)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.DebugTester))]
    [HarmonyPostfix]
    private static void IAmASurgeonIMeanTester(ref bool __result)
    {
        __result = true;
    }

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.DebugDev))]
    [HarmonyPostfix]
    private static void IAmASurgeonIMeanDeveloper(ref bool __result)
    {
        __result = true;
    }
}
#endif