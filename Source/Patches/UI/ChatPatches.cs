using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Input;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class ChatPatches
{
    /// <summary>
    /// Attach a custom VR chat script to the chat UI
    /// </summary>
    [HarmonyPatch(typeof(ChatUI), nameof(ChatUI.Start))]
    [HarmonyPostfix]
    private static void OnChatUICreate(ChatUI __instance)
    {
        __instance.gameObject.AddComponent<RepoXR.UI.ChatUI>();
    }

    /// <summary>
    /// Use the alternative input system to detect chat opens
    /// </summary>
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.StateInactive))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ChatOpenButtonPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputDown))))
            .Advance(-1)
            .SetAndAdvance(OpCodes.Ldsfld, Field(typeof(VRInputSystem), "_instance"))
            .SetAndAdvance(OpCodes.Callvirt, Method(typeof(VRInputSystem), nameof(VRInputSystem.ChatPressed)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make the chat button also close the chat
    /// </summary>
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.StateActive))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ChatCloseButtonPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)InputKey.Back))
            .SetAndAdvance(OpCodes.Ldsfld, Field(typeof(VRInputSystem), "_instance"))
            .SetAndAdvance(OpCodes.Callvirt, Method(typeof(VRInputSystem), nameof(VRInputSystem.ChatPressed)))
            .InstructionEnumeration();
    }
}