using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Input;
using RepoXR.Player.Camera;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class SpectatePatches
{
    /// <summary>
    /// Aim camera towards top-down view on death
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.UpdateState))]
    [HarmonyPostfix]
    private static void OnUpdateState(SpectateCamera __instance, SpectateCamera.State _state)
    {
        var offsetTransform = CameraAimOffset.Instance.transform;
        
        if (_state == SpectateCamera.State.Death)
        {
            offsetTransform.localEulerAngles = Camera.main!.transform.localEulerAngles.y * Vector3.down;
            offsetTransform.localPosition = Vector3.back * 10;
        }
        else
        {
            offsetTransform.localRotation = Quaternion.identity;
            offsetTransform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// Keep the original small near clip plane value since we can move our head around (which breaks the original logic)
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.DeathNearClipLogic))]
    [HarmonyPostfix]
    private static void NearClipPatches(SpectateCamera __instance)
    {
        __instance.MainCamera.nearClipPlane = 0.01f;
    }

    /// <summary>
    /// Double the far clip plane to prevent some visual issues in VR
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateDeath))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> IncreaseFarPlanePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 90f))
            .SetOperandAndAdvance(180f)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Prevent the mouse from rotating the spectator camera during the top-down view
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateDeath))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisableMouseRotationPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    Method(typeof(Quaternion), nameof(Quaternion.Euler),
                        [typeof(float), typeof(float), typeof(float)])))
            .Advance(-3)
            .RemoveInstructions(2)
            .Insert(new CodeInstruction(OpCodes.Ldc_R4, 0f))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Fix spectate camera position and rotation to not affect VR camera rotations
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateNormal))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CameraRotationPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    PropertySetter(typeof(RenderSettings), nameof(RenderSettings.fogStartDistance))))
            .Advance(6)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, Field(typeof(SpectateCamera), nameof(SpectateCamera.MainCamera))),
                new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(Component), nameof(Component.transform))),
                new CodeInstruction(OpCodes.Callvirt,
                    PropertyGetter(typeof(Transform), nameof(Transform.localPosition))),
                new CodeInstruction(OpCodes.Call, Method(typeof(Vector3), "op_Subtraction"))
            )
            .Advance(3)
            .RemoveInstructions(3)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldc_R4, 0f),
                new CodeInstruction(OpCodes.Ldsfld, Field(typeof(SpectatePatches), nameof(spectateTurnAmount))),
                new CodeInstruction(OpCodes.Ldc_R4, 0f),
                new CodeInstruction(OpCodes.Newobj, Constructor(typeof(Vector3), [typeof(float), typeof(float), typeof(float)]))
            )
            .SetOperandAndAdvance(PropertySetter(typeof(Transform), nameof(Transform.eulerAngles)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Make the spectate camera pivot using VR controls instead of the mouse
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateNormal))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> InputPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputMouseX))))
            .SetOperandAndAdvance(((Func<float>)GetRotationX).Method)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputMouseY))))
            .SetOperandAndAdvance(((Func<float>)GetRotationY).Method)
            .InstructionEnumeration();

        static float GetRotationX() => -Actions.Instance["Movement"].ReadValue<Vector2>().x;
        static float GetRotationY() => -Actions.Instance["Movement"].ReadValue<Vector2>().y;
    }

    private static bool turnedLastInput;
    private static float spectateTurnAmount;

    /// <summary>
    /// Allow snap/smooth turning in the spectate camera
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateNormal))]
    [HarmonyPostfix]
    private static void CameraTurnPatch(SpectateCamera __instance)
    {
        var value = Actions.Instance["Turn"].ReadValue<float>();

        switch (Plugin.Config.TurnProvider.Value)
        {
            case Config.TurnProviderOption.Snap:
                var should = Mathf.Abs(value) > 0.75f;
                var snapSize = Plugin.Config.SnapTurnSize.Value;

                if (!turnedLastInput && should)
                    if (value > 0)
                        spectateTurnAmount += snapSize;
                    else
                        spectateTurnAmount -= snapSize;

                turnedLastInput = should;

                break;
            
            case Config.TurnProviderOption.Smooth:
                if (!Plugin.Config.AnalogSmoothTurn.Value)
                    value = value == 0 ? 0 : Math.Sign(value);

                spectateTurnAmount += 180 * Time.deltaTime * Plugin.Config.SmoothTurnSpeedModifier.Value * value;
                break;

            case Config.TurnProviderOption.Disabled:
                break;
        }
    }

    /// <summary>
    /// Allow snap/smooth turning in the spectator head camera
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateHead))]
    [HarmonyPostfix]
    private static void HeadCameraTurnPatch(SpectateCamera __instance)
    {
        var value = Actions.Instance["Turn"].ReadValue<float>();

        switch (Plugin.Config.TurnProvider.Value)
        {
            case Config.TurnProviderOption.Snap:
                var should = Mathf.Abs(value) > 0.75f;
                var snapSize = Plugin.Config.SnapTurnSize.Value;

                if (!turnedLastInput && should)
                    if (value > 0)
                        VRCameraAim.instance.TurnAimNow(snapSize);
                    else
                        VRCameraAim.instance.TurnAimNow(-snapSize);

                turnedLastInput = should;

                break;

            case Config.TurnProviderOption.Smooth:
                if (!Plugin.Config.AnalogSmoothTurn.Value)
                    value = value == 0 ? 0 : Math.Sign(value);

                VRCameraAim.instance.TurnAimNow(180 * Time.deltaTime * Plugin.Config.SmoothTurnSpeedModifier.Value * value);
                break;

            case Config.TurnProviderOption.Disabled:
                break;
        }
    }

    /// <summary>
    /// Make sure the VR camera is always centered to the player's death head (3-DoF)
    /// </summary>
    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateHead))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AlignCameraWithHeadPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, PropertyGetter(typeof(Time), nameof(Time.deltaTime))))
            .Advance(-3)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .SetAndAdvance(OpCodes.Call, ((Func<PlayerDeathHead, SpectateCamera, Vector3>)GetTargetPoint).Method)
            .RemoveInstruction()
            .InstructionEnumeration();

        static Vector3 GetTargetPoint(PlayerDeathHead head, SpectateCamera camera)
        {
            var cameraPos = camera.transform.InverseTransformPoint(Camera.main!.transform.position);

            return head.physGrabObject.centerPoint - camera.transform.rotation * cameraPos;
        }
    }
}
