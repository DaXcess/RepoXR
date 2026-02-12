using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class DebugPatches
{
    /// <summary>
    /// Add "(VR)" to the version number in the tester debug UI
    /// </summary>
    [HarmonyPatch(typeof(DebugTesterUI), nameof(DebugTesterUI.OnGUI))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> InjectVRTextPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldstr, "{0}\n{1} ({2})"))
            .SetOperandAndAdvance("{0} (VR)\n{1} ({2})")
            .InstructionEnumeration();
    }
}