using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace RepoXR.Patches;

/// <summary>
/// Special patches for testers
/// </summary>
[RepoXRPatch]
internal static class DebugPatches
{
    /// <summary>
    /// Make sure the enter key still works on keyboard
    /// </summary>
    [HarmonyPatch(typeof(DebugConsoleUI), nameof(DebugConsoleUI.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> KeepEnterKeyThing(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)9))
            .SetOperandAndAdvance((sbyte)KeyCode.Return)
            .SetOperandAndAdvance(AccessTools.Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown),
                [typeof(KeyCode)]))
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)13))
            .SetOperandAndAdvance((sbyte)KeyCode.Backspace)
            .SetOperandAndAdvance(AccessTools.Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown),
                [typeof(KeyCode)]))
            .InstructionEnumeration();
    }
}