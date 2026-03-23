using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Item;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class ItemVehiclePatches
{
    /// <summary>
    /// Require a higher amount of X axis input before initiating a drift for VR players
    /// </summary>
    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleDrift))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DriftDeadzonePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.1f))
            .SetOpcodeAndAdvance(OpCodes.Ldarg_0)
            .Insert(new CodeInstruction(OpCodes.Call, ((Func<ItemVehicle, float>)GetDriftDeadzone).Method))
            .InstructionEnumeration();

        static float GetDriftDeadzone(ItemVehicle vehicle) => vehicle.currentDriver.IsVRPlayer() ? 0.5f : 0.1f;
    }

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleMovement))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ApplyVehicleMovementPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Repeat(m => m.Set(OpCodes.Call, ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleSteering))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ApplyVehicleSteeringPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.ApplyVehicleDrift))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ApplyVehicleDriftPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.UpdateSteering))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UpdateSteeringPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method)
            .InstructionEnumeration();
    }

    // TODO: Should we override GetOverrideTransform in LocalMeshUpdate?
    // Looks like LocalMeshUpdate does whatever LateUpdate did before, so we can maybe keep this code as-is
    [HarmonyPatch(typeof(ItemVehicle), nameof(ItemVehicle.LocalMeshUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> LateUpdatePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            // Skip first match
            .Advance(1)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, ((Func<PlayerLocalCamera, Transform>)GetVehicleTargetForward).Method)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Get the correct forward based on the target player's setting and whether they're in VR
    /// </summary>
    private static Transform GetVehicleTargetForward(PlayerLocalCamera camera)
    {
        var player = camera.playerAvatar;

        if (player.isLocal && VRSession.InVR)
            return
                VRSession.Instance.Player.NetworkPlayer.VehicleHeadForward
                    ? player.localCamera.GetOverrideTransform()
                    : player.localCamera.GetHandOverrideTransform();

        if (NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer))
            return
                networkPlayer.VehicleHeadForward
                    ? player.localCamera.GetOverrideTransform()
                    : player.localCamera.GetHandOverrideTransform();

        return player.localCamera.GetOverrideTransform();
    }
}