using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using RepoXR.Player.Camera;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Item;

[RepoXRPatch]
internal static class ItemGumballPatches
{
    /// <summary>
    /// Make the gumball valuable force you to look at it if another player is holding it
    /// </summary>
    [HarmonyPatch(typeof(GumballValuable), nameof(GumballValuable.CameraAimAndZoom))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LookAtPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldsfld, Field(typeof(CameraAim), nameof(CameraAim.Instance))))
            .SetOperandAndAdvance(Field(typeof(VRCameraAim), nameof(VRCameraAim.instance)))
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraAim), nameof(CameraAim.AimTargetSoftSet))))
            .SetAndAdvance(OpCodes.Call, Plugin.GetConfigGetter())
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(Config), nameof(Config.ReducedAimImpact))),
                new CodeInstruction(OpCodes.Callvirt,
                    PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<bool>.Value))),
                new CodeInstruction(OpCodes.Callvirt, Method(typeof(VRCameraAim), nameof(VRCameraAim.SetAimTargetSoft)))
            )
            .InstructionEnumeration();
    }
}