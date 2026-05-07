using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class MenuButtonPatches
{
    /// <summary>
    /// Fix for the middle align code using the wrong positioning logic
    /// </summary>
    [HarmonyPatch(typeof(MenuButton), nameof(MenuButton.ResizeToText))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MiddleAlignFixPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertyGetter(typeof(Transform), nameof(Transform.position))))
            .SetOperandAndAdvance(PropertyGetter(typeof(Transform), nameof(Transform.localPosition)))
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertySetter(typeof(Transform), nameof(Transform.position))))
            .SetOperandAndAdvance(PropertySetter(typeof(Transform), nameof(Transform.localPosition)))
            .InstructionEnumeration();
    }
}