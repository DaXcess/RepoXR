using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class PresencePatches
{
    /// <summary>
    /// Insert VR into Discord presence if enabled
    /// </summary>
    [HarmonyPatch(typeof(DiscordManager), nameof(DiscordManager.UpdateDiscordRichPresence))]
    [HarmonyPrefix]
    private static void DiscordVRPresence(ref string details)
    {
        details = details.StripSuffix(" [VR]");

        if (Plugin.Config.DisableVRPresence.Value)
            return;

        details += " [VR]";
    }

    /// <summary>
    /// Insert VR into Steam presence if enabled
    /// </summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.UpdateSteamRichPresence))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SteamVRPresence(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(true, new CodeMatch(OpCodes.Ldloc_3))
            .Advance(1)
            .Insert(
                new CodeInstruction(OpCodes.Call, ((Func<string, string>)GetSteamPresence).Method)
            )
            .InstructionEnumeration();

        static string GetSteamPresence(string levelName)
        {
            if (Plugin.Config.DisableVRPresence.Value)
                return levelName;

            return levelName + " [VR]";
        }
    }
}