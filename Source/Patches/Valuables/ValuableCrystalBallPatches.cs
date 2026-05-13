using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Valuables;

[RepoXRPatch]
internal static class ValuableCrystalBallPatches
{
    /// <summary>
    /// Force look at the player (aka ourselves) when we're holding the crystal ball
    /// </summary>
    [HarmonyPatch(typeof(CrystalBallValuable), nameof(CrystalBallValuable.StateActive))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LookAtPlayerPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraAim), nameof(CameraAim.AimTargetSet))))
            .MatchBack(false, new CodeMatch(OpCodes.Ldsfld, Field(typeof(CameraAim), nameof(CameraAim.Instance))))
            .SetOperandAndAdvance(Field(typeof(VRCameraAim), nameof(VRCameraAim.instance)))
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraAim), nameof(CameraAim.AimTargetSet))))
            .SetAndAdvance(OpCodes.Call, Plugin.GetConfigGetter())
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(Config), nameof(Config.ReducedAimImpact))),
                new CodeInstruction(OpCodes.Callvirt,
                    PropertyGetter(typeof(ConfigEntry<bool>), nameof(ConfigEntry<>.Value))),
                new CodeInstruction(OpCodes.Callvirt, Method(typeof(VRCameraAim), nameof(VRCameraAim.SetAimTarget)))
            )
            .InstructionEnumeration();
    }

    private static float crystalBallPlayerRotation;

    /// <summary>
    /// Update the player rotation while holding the crystal ball
    /// </summary>
    [HarmonyPatch(typeof(CrystalBallValuable), nameof(CrystalBallValuable.StateActive))]
    [HarmonyPrefix]
    private static void UpdateCrystalBallRotation(CrystalBallValuable __instance)
    {
        if (__instance.physGrabObject.playerGrabbing.Count == 0 ||
            !__instance.physGrabObject.playerGrabbing.Contains(PlayerAvatar.instance.physGrabber))
            return;

        // Reset rotation when first grabbing
        if (!__instance.activeLocal)
            crystalBallPlayerRotation = GameDirector.instance.MainCamera.transform.eulerAngles.y;

        var value = Actions.Instance["Turn"].ReadValue<float>();

        if (!Plugin.Config.AnalogSmoothTurn.Value)
            value = value == 0 ? 0 : Math.Sign(value);

        crystalBallPlayerRotation += 180 * Time.deltaTime * value * 0.5f;

        if (crystalBallPlayerRotation > 360f)
            crystalBallPlayerRotation -= 360f;

        if (crystalBallPlayerRotation < 0f)
            crystalBallPlayerRotation += 360f;
    }

    /// <summary>
    /// Make our arm movement move the crystal ball around
    /// </summary>
    [HarmonyPatch(typeof(CrystalBallValuable), nameof(CrystalBallValuable.StateActive))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ArmMovePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    Method(typeof(Quaternion), nameof(Quaternion.Euler),
                        [typeof(float), typeof(float), typeof(float)])))
            .SetOperandAndAdvance(((Func<CrystalBallValuable, Quaternion>)GetRotationAngle).Method)
            .Advance(-5)
            .RemoveInstructions(4)
            .InstructionEnumeration();

        static Quaternion GetRotationAngle(CrystalBallValuable valuable)
        {
            if (VRSession.Instance is not { } instance)
                return Quaternion.Euler(valuable.aimVertical, valuable.aimHorizontal, 0);

            var mainHand = VRSession.IsLeftHanded
                ? instance.Player.Rig.leftArmTarget
                : instance.Player.Rig.rightArmTarget;

            var verticalAngle = Quaternion.LookRotation(-mainHand.forward).eulerAngles.x;

            return Quaternion.Euler(verticalAngle, crystalBallPlayerRotation, 0);
        }
    }

    /// <summary>
    /// Reset player rotation upon releasing the crystal ball
    /// </summary>
    [HarmonyPatch(typeof(CrystalBallValuable), nameof(CrystalBallValuable.ResetCameraAim))]
    [HarmonyPostfix]
    private static void ResetCameraAim(CrystalBallValuable __instance)
    {
        VRCameraAim.instance.SetPlayerAim(__instance.lastPlayerRotation.eulerAngles.y, true);
    }
}