using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using RepoXR.UI;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Enemy;

[RepoXRPatch]
internal static class EnemyCeilingEyePatches
{
    /// <summary>
    /// Replace <see cref="CameraAim.AimTargetSoftSet"/> with <see cref="VRCameraAim.SetAimTargetSoft"/>
    /// </summary>
    [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SetCameraSoftRotationPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraAim), nameof(CameraAim.AimTargetSoftSet))))
            .Advance(-11)
            .SetOperandAndAdvance(Field(typeof(VRCameraAim), nameof(VRCameraAim.instance)))
            .Advance(10)
            // Make the rotation less severe if reduced aim impact is enabled
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, Plugin.GetConfigGetter()))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt,
                PropertyGetter(typeof(Config), nameof(Config.ReducedAimImpact))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt,
                PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<bool>.Value))))
            .SetOperandAndAdvance(Method(typeof(VRCameraAim), nameof(VRCameraAim.SetAimTargetSoft)))
            // Set focus sphere target to look at the ceiling eye
            .Insert(
                // VRSession.Instance.FocusSphere
                new CodeInstruction(OpCodes.Call, PropertyGetter(typeof(VRSession), nameof(VRSession.Instance))),
                new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(VRSession), nameof(VRSession.FocusSphere))),

                // target: this.enemy.CenterTransform
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.enemy))),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(global::Enemy), nameof(global::Enemy.CenterTransform))),

                // time: 1f
                new CodeInstruction(OpCodes.Ldc_R4, 0.5f),

                // speed: 2f
                new CodeInstruction(OpCodes.Ldc_R4, 2f),

                // strength: 1f (100%)
                new CodeInstruction(OpCodes.Ldc_R4, 1f),
                new CodeInstruction(OpCodes.Callvirt, Method(typeof(FocusSphere), nameof(FocusSphere.SetLookAtTarget)))
            )
            .InstructionEnumeration();
    }

    /// <summary>
    /// Replace <see cref="CameraAim.AimTargetSet"/> with <see cref="VRCameraAim.SetAimTarget"/>
    /// </summary>
    [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.UpdateStateRPC))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SetCameraRotationPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraAim), nameof(CameraAim.AimTargetSet))))
            .Advance(-10)
            .SetOperandAndAdvance(Field(typeof(VRCameraAim), nameof(VRCameraAim.instance)))
            .Advance(9)
            // Make the rotation less severe if reduced aim impact is enabled
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, Plugin.GetConfigGetter()))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt,
                PropertyGetter(typeof(Config), nameof(Config.ReducedAimImpact))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt,
                PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<bool>.Value))))
            .SetOperandAndAdvance(Method(typeof(VRCameraAim), nameof(VRCameraAim.SetAimTarget)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Provide haptic feedback while attached to the ceiling eye
    /// </summary>
    [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.Update))]
    [HarmonyPostfix]
    private static void EyeAttachHapticFeedback(EnemyCeilingEye __instance)
    {
        if (__instance.currentState != EnemyCeilingEye.State.HasTarget)
            return;

        if (!__instance.targetPlayer || !__instance.targetPlayer.isLocal)
            return;

        // Want a higher priority than damage, so we use 11
        HapticManager.Impulse(HapticManager.Hand.Both, HapticManager.Type.Continuous,
            0.2f * AssetCollection.EyeAttachHapticCurve.EvaluateTimed(0.25f), priority: 11);
    }
}