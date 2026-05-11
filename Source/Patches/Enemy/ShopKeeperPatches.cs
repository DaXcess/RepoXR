using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using RepoXR.Player.Camera;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Enemy;

[RepoXRPatch]
internal static class ShopKeeperPatches
{
    /// <summary>
    /// Force look at the shopkeeper when we're being dragged by it
    /// </summary>
    [HarmonyPatch(typeof(ShopKeeper), nameof(ShopKeeper.PlayerEyesOverrideLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PlayerEyesVROverride(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldsfld, Field(typeof(CameraAim), nameof(CameraAim.Instance))))
            .Repeat(matcher => matcher
                .SetOperandAndAdvance(Field(typeof(VRCameraAim), nameof(VRCameraAim.instance)))
                .MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraAim), nameof(CameraAim.AimTargetSet))))
                .SetAndAdvance(OpCodes.Call, Plugin.GetConfigGetter())
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Callvirt,
                        PropertyGetter(typeof(Config), nameof(Config.ReducedAimImpact))),
                    new CodeInstruction(OpCodes.Callvirt,
                        PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<>.Value))),
                    new CodeInstruction(OpCodes.Callvirt, Method(typeof(VRCameraAim), nameof(VRCameraAim.SetAimTarget)))
                )
                // Need to match again since the previous MatchForward messes up the repeat
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldsfld, Field(typeof(CameraAim), nameof(CameraAim.Instance))))
            )
            .InstructionEnumeration();
    }
}