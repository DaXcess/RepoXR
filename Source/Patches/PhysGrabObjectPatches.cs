using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class PhysGrabObjectPatches
{
    private static Transform GetTargetTransform(PlayerAvatar player)
    {
        if (player.isLocal)
            return VRSession.Instance is { } session ? session.Player.MainHand : player.localCamera.transform;

        return NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer)
            ? networkPlayer.PrimaryHand
            : player.localCamera.transform;
    }

    private static Quaternion GetTargetRotation(PlayerAvatar player)
    {
        if (player.isLocal)
            return VRSession.Instance is { } session
                ? session.Player.MainHand.rotation
                : player.localCamera.transform.rotation;

        return NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer)
            ? networkPlayer.PrimaryHand.rotation
            : player.localCamera.transform.rotation;
    }

    private static Vector3 GetTargetPosition(PlayerAvatar player)
    {
        if (player.isLocal)
            return VRSession.Instance is { } session
                ? session.Player.MainHand.position
                : player.localCamera.transform.position;

        return NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer)
            ? networkPlayer.PrimaryHand.position
            : player.localCamera.transform.position;
    }

    private static Transform GetCartSteerTransform(PhysGrabber grabber)
    { 
        if (grabber.playerAvatar.isLocal)
            return VRSession.Instance is { } session ? session.Player.MainHand : grabber.transform;

        return NetworkSystem.instance.GetNetworkPlayer(grabber.playerAvatar, out var networkPlayer)
            ? networkPlayer.PrimaryHand
            : grabber.transform;
    }

    /// <summary>
    /// Apply object rotation based on hand rotation instead of camera rotation
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.PhysicsGrabbingManipulation))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandRelativeMovementPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.localCamera))))
            .Repeat(matcher =>
                matcher.SetInstruction(new CodeInstruction(OpCodes.Call,
                    ((Func<PlayerAvatar, Transform>)GetTargetTransform).Method)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Apply cart steering rotation based on hand rotation instead of camera rotation
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabCart), nameof(PhysGrabCart.CartSteer))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandRelativeCartPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    Method(typeof(Mathf), nameof(Mathf.Clamp), [typeof(float), typeof(float), typeof(float)])))
            .MatchBack(false,
                new CodeMatch(OpCodes.Callvirt, PropertyGetter(typeof(Component), nameof(Component.transform))))
            // physGrabber.transform.forward
            .SetInstruction(new CodeInstruction(OpCodes.Call,
                ((Func<PhysGrabber, Transform>)GetCartSteerTransform).Method))
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertyGetter(typeof(Transform), nameof(Transform.rotation))))
            .Advance(-1)
            .SetInstruction(new CodeInstruction(OpCodes.Call,
                ((Func<PhysGrabber, Transform>)GetCartSteerTransform).Method))
            .MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    Method(typeof(Quaternion), nameof(Quaternion.LookRotation), [typeof(Vector3), typeof(Vector3)])))
            .Advance(-7)
            .SetInstruction(new CodeInstruction(OpCodes.Call,
                ((Func<PhysGrabber, Transform>)GetCartSteerTransform).Method))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Apply cart cannon rotations based on the hand position and rotation
    /// </summary>
    [HarmonyPatch(typeof(ItemCartCannonMain), nameof(ItemCartCannonMain.GrabLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandRelativeCartCannonPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.localCamera))))
            .SetAndAdvance(OpCodes.Call, ((Func<PlayerAvatar, Quaternion>)GetTargetRotation).Method)
            .RemoveInstructions(2)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.localCamera))))
            .SetAndAdvance(OpCodes.Call, ((Func<PlayerAvatar, Quaternion>)GetTargetRotation).Method)
            .RemoveInstructions(2)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.localCamera))))
            .SetAndAdvance(OpCodes.Call, ((Func<PlayerAvatar, Vector3>)GetTargetPosition).Method)
            .RemoveInstructions(2)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Update the rotation target based on the hand rotation
    /// </summary>
    [HarmonyPatch(typeof(ItemCartCannonMain), nameof(ItemCartCannonMain.CorrectorAndLightLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RotationTargetHandRelative(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.localCamera))))
            .SetAndAdvance(OpCodes.Call, ((Func<PlayerAvatar, Quaternion>)GetTargetRotation).Method)
            .RemoveInstructions(2)
            .InstructionEnumeration();
    }
}