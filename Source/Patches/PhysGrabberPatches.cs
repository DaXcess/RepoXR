using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Networking;
using RepoXR.Player;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class PhysGrabberPatches
{
    /// <summary>
    /// Make certain phys grabber operations operate from the hand transform instead of the camera transform
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UpdatePatches(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Repeat(matcher => matcher.Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Slow down the push/pull logic since it's way too fast in VR
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PushPullSlowdownPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.2f))
            .SetOperandAndAdvance(0.1f)
            .Insert(
                new CodeInstruction(OpCodes.Ldsfld, Field(typeof(InputManager), nameof(InputManager.instance))),
                new CodeInstruction(OpCodes.Callvirt,
                    Method(typeof(InputManager), nameof(InputManager.KeyPullAndPush))),
                new CodeInstruction(OpCodes.Mul)
            )
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.2f))
            .SetOperandAndAdvance(-0.1f)
            .Insert(
                new CodeInstruction(OpCodes.Ldsfld, Field(typeof(InputManager), nameof(InputManager.instance))),
                new CodeInstruction(OpCodes.Callvirt,
                    Method(typeof(InputManager), nameof(InputManager.KeyPullAndPush))),
                new CodeInstruction(OpCodes.Mul)
            )
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make sure the <see cref="PhysGrabber.physGrabPointPlane"/> and <see cref="PhysGrabber.physGrabPointPuller"/> are
    /// manually updated if we are holding something.
    ///
    /// <para>
    /// This is normally done by having these be a child of the camera, however this doesn't work in VR since
    /// we use our hand to move items, not the main camera.
    /// </para>
    ///
    /// <para>
    /// We do not touch the transforms if a camera override is active (walkie-talkie)
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyPostfix]
    private static void UpdatePhysGrabPlane(PhysGrabber __instance)
    {
        if (!__instance.isLocal || !__instance.grabbedObjectTransform ||
            __instance.playerAvatar.localCamera.GetOverrideActive())
            return;

        var hand = __instance.playerAvatar.localCamera.GetHandOverrideTransform();
        var distancePlane = Vector3.Distance(hand.position, __instance.physGrabPointPlane.position);
        var distancePuller = Vector3.Distance(hand.position, __instance.physGrabPointPuller.position);

        __instance.physGrabPointPlane.position = hand.position + hand.forward * distancePlane;
        __instance.physGrabPointPuller.position = hand.position + hand.forward * distancePuller;
    }

    /// <summary>
    /// Provide haptic feedback while something is grabbed
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyPostfix]
    private static void HapticFeedbackPatch(PhysGrabber __instance)
    {
        if (!__instance.isLocal)
            return;

        var grabbed = __instance.grabbed
            ? AssetCollection.GrabberHapticCurve.EvaluateTimed(__instance.loopSound.Source.pitch * 1.12667f) * 0.1f
            : 0;
        var overcharge = __instance.physGrabBeamOverChargeFloat * 0.4f *
                         AssetCollection.OverchargeHapticCurve.EvaluateTimed(__instance.physGrabBeamOverChargeFloat *
                                                                             3);

        if (grabbed + overcharge <= 0)
            return;

        HapticManager.Impulse(HapticManager.Hand.Dominant, HapticManager.Type.Continuous, grabbed + overcharge);
    }

    /// <summary>
    /// When grabbing items, shoot rays out of the hand, instead of the camera
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.RayCheck))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RayCheckPatches(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make the object turning input use the controller inputs instead of mouse inputs
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ObjectTurningPatches(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            // Use VR controls for rotation instead of mouse inputs
            .MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Mouse X"))
            .RemoveInstructions(10)
            .Insert(
                // TODO: Check these operands in Debug vs Release builds
                new CodeInstruction(OpCodes.Ldloca_S, Debug.isDebugBuild ? 13 : 5), // Mouse X
                new CodeInstruction(OpCodes.Ldloca_S, Debug.isDebugBuild ? 14 : 6), // Mouse Y
                new CodeInstruction(OpCodes.Call, Method(typeof(PhysGrabberPatches), nameof(GetRotationInput)))
            )
            .InstructionEnumeration();
    }

    private static void GetRotationInput(ref float x, ref float y)
    {
        var input = Actions.Instance["Rotation"].ReadValue<Vector2>();

        x = input.x;
        y = input.y;
    }

    /// <summary>
    /// Allow a custom override to disable object turning
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisableTurningPatch(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputHold))))
            .Advance(1);

        var jmp = matcher.Instruction;

        matcher.Advance(1).InsertAndAdvance(
            new CodeInstruction(OpCodes.Call, PropertyGetter(typeof(VRSession), nameof(VRSession.Instance))),
            new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(VRSession), nameof(VRSession.Player))),
            new CodeInstruction(OpCodes.Ldfld, Field(typeof(VRPlayer), nameof(VRPlayer.disableRotateTimer))),
            new CodeInstruction(OpCodes.Ldc_R4, 0f),
            new CodeInstruction(OpCodes.Bgt_Un_S, jmp.operand)
        );

        return matcher
            .InstructionEnumeration();
    }

    /// <summary>
    /// Detect item release and try to equip item if possible
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ReleaseObject))]
    [HarmonyPrefix]
    private static void OnReleaseObject(PhysGrabber __instance)
    {
        if (!__instance.grabbed || !__instance.isLocal || !__instance.grabbedObject ||
            !__instance.grabbedObject.TryGetComponent<ItemEquippable>(out var item))
            return;

        if (VRSession.Instance is not { } session)
            return;

        session.Player.Rig.inventoryController.TryEquipItem(item);
    }

    /// <summary>
    /// Make sure the force grab timer also works in VR, where the grab key is the same as the "take from inventory" key
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ForceHoldPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld,
                    Field(typeof(GameplayManager), nameof(GameplayManager.itemUnequipAutoHold))))
            .Advance(-2)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(PhysGrabber), nameof(PhysGrabber.overrideGrabTimer))),
                new CodeInstruction(OpCodes.Call, ((Func<bool, float, bool>)ShouldToggleGrabOff).Method)
            )
            .InstructionEnumeration();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ShouldToggleGrabOff(bool grabHeld, float grabTimer) => grabHeld && grabTimer <= 0;
    }
}

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class PhysGrabberUniversalPatches
{
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ObjectTurningPatches(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            // Replace camera transform with hand transform (local player)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            // Replace camera transform with hand transform (remote player)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt,
                    Method(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.GetOverrideTransform))))
            .Set(OpCodes.Call, PlayerLocalCameraExtensions.GetHandOverrideTransformMethod)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make our tumble climb follow our hand rotation instead of the camera rotation
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.GrabStateClimb))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HandBasedTumbleClimbPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Stloc_2))
            .Advance(-2)
            .SetAndAdvance(OpCodes.Call, ((Func<PhysGrabber, Quaternion>)GetRotation).Method)
            .RemoveInstruction()
            .InstructionEnumeration();

        static Quaternion GetRotation(PhysGrabber grabber)
        {
            if (grabber.playerAvatar.isLocal)
                return VRSession.InVR
                    ? VRSession.Instance.Player.MainHand.rotation * Quaternion.Euler(0, 180, 0)
                    : grabber.climbStickTransform.rotation;

            if (!NetworkSystem.instance)
                return grabber.climbStickTransform.rotation;

            return NetworkSystem.instance.GetNetworkPlayer(grabber.playerAvatar, out var networkPlayer)
                ? networkPlayer.PrimaryHand.rotation * Quaternion.Euler(0, 180, 0)
                : grabber.climbStickTransform.rotation;
        }
    }
}