using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Photon.Pun;
using RepoXR.Managers;
using RepoXR.Networking;
using RepoXR.Player;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class MapToolPatches
{
    internal const float MAP_HOLD_ANGLE = 300f;
    
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Start))]
    [HarmonyPostfix]
    private static void OnMapToolCreated(MapToolController __instance)
    {
        VRMapTool.Create();
    }

    /// <summary>
    /// Disable all the input detection code in the map tool as we're shipping our own
    /// </summary>
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapToolDisableInput(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .Start()
            .MatchForward(false, new CodeMatch(OpCodes.Ldloc_0))
            .Set(OpCodes.Ldc_I4_0, null)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Set the minimum size of the map tool to be 25% instead of 0%
    /// </summary>
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapToolScalePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(MapToolController), nameof(MapToolController.IntroCurve))))
            .Advance(-3)
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call,
                ((Func<MapToolController, float>)GetMaximumScale).Method))
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call,
                ((Func<MapToolController, float>)GetMinimumScale).Method))
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(MapToolController), nameof(MapToolController.OutroCurve))))
            .Advance(-3)
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call,
                ((Func<MapToolController, float>)GetMaximumScale).Method))
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call,
                ((Func<MapToolController, float>)GetMinimumScale).Method))
            .InstructionEnumeration();

        static float GetMaximumScale(MapToolController controller) =>
            controller.PlayerAvatar.isLocal && !SemiFunc.MenuLevel() ? 0.75f : 1;

        static float GetMinimumScale(MapToolController controller) =>
            controller.PlayerAvatar.isLocal && !SemiFunc.MenuLevel() ? 0.25f : 0;
    }

    /// <summary>
    /// Make sure the map tool doesn't disappear when it's not held
    /// </summary>
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapToolVisibilityPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld,
                    Field(typeof(MapToolController), nameof(MapToolController.VisualTransform))))
            .Advance(3)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Func<bool, MapToolController, bool>)FuckYouSpraty).Method)
            )
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld,
                    Field(typeof(MapToolController), nameof(MapToolController.VisualTransform))))
            .Advance(3)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Func<bool, MapToolController, bool>)FuckYouSpraty).Method)
            )
            .InstructionEnumeration();

        // For lore reasons this name cannot change
        static bool FuckYouSpraty(bool original, MapToolController controller)
        {
            return controller.PlayerAvatar.isLocal || original;
        }
    }

    /// <summary>
    /// Disable camera shake when picking up the map tool
    /// </summary>
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> NoShakePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Callvirt, Method(typeof(CameraShake), nameof(CameraShake.Shake))))
            .Repeat(matcher => matcher
                .Advance(-4)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .RemoveInstructions(4)
            )
            .InstructionEnumeration();
    }
}

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class UniversalMapToolPatches
{
    /// <summary>
    /// Fix VR player's map tool's transforms
    /// </summary>
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapToolTransformPatch(IEnumerable<CodeInstruction> instructions)
    {
        var isLocalOrVRPlayerInstr =
            new CodeInstruction(OpCodes.Call, ((Func<bool, PhotonView, bool>)IsLocalOrVRPlayer).Method);
        var isLocalPlayerMatch = new CodeMatch(OpCodes.Ldloc_0);

        return new CodeMatcher(instructions)
            // Patch hardcoded 90-degree angle to account for VR usage
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 90f))
            .SetAndAdvance(OpCodes.Ldarg_0, null)
            .Insert(new CodeInstruction(OpCodes.Call, ((Func<MapToolController, float>)GetHoldAngle).Method))
            // Do not reset _hideAmount if target player is in VR
            .MatchForward(false, isLocalPlayerMatch)
            .SetAndAdvance(OpCodes.Ldloc_0, null) // Check if is local player
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld,
                    Field(typeof(MapToolController), nameof(MapToolController.photonView))),
                isLocalOrVRPlayerInstr
            )
            // Do not reset _speedMult if target player is in VR
            .MatchForward(false, isLocalPlayerMatch)
            .SetAndAdvance(OpCodes.Ldloc_0, null)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld,
                    Field(typeof(MapToolController), nameof(MapToolController.photonView))),
                isLocalOrVRPlayerInstr
            )
            // Do not apply FollowTransformClient transforms if target player is in VR
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld,
                    Field(typeof(MapToolController), nameof(MapToolController.FollowTransformClient))))
            .MatchBack(false, isLocalPlayerMatch)
            .SetAndAdvance(OpCodes.Ldloc_0, null)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld,
                    Field(typeof(MapToolController), nameof(MapToolController.photonView))),
                isLocalOrVRPlayerInstr
            )
            // Only reset transforms conditionally (when local player is not in VR)
            .MatchForward(false, new CodeMatch(OpCodes.Call, PropertyGetter(typeof(Vector3), nameof(Vector3.zero))))
            .Advance(-2)
            .SetAndAdvance(OpCodes.Call, ((Action<MapToolController>)ConditionalResetTransform).Method)
            .RemoveInstructions(Plugin.GameDebugBuild ? 14 : 12)
            .InstructionEnumeration();

        static bool IsLocalOrVRPlayer(bool isLocal, PhotonView view)
        {
            return isLocal || NetworkSystem.instance.IsVRView(view);
        }

        // Only reset transforms if current player is not in VR
        static void ConditionalResetTransform(MapToolController controller)
        {
            if (VRSession.InVR)
                return;

            controller.transform.parent.localPosition = Vector3.zero;
            controller.transform.parent.localRotation = Quaternion.identity;
            controller.mainSpringTransform.localRotation = Quaternion.identity;
        }

        static float GetHoldAngle(MapToolController mapTool)
        {
            if (mapTool.PlayerAvatar.isLocal && VRSession.InVR)
                return MapToolPatches.MAP_HOLD_ANGLE;

            if (!mapTool.PlayerAvatar.isLocal && NetworkSystem.instance.IsVRPlayer(mapTool.PlayerAvatar))
                return MapToolPatches.MAP_HOLD_ANGLE;

            return 90;
        }
    }

    /// <summary>
    /// Fix VR player's map tool's animations
    /// </summary>
    [HarmonyPatch(typeof(MapToolController), nameof(MapToolController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MapToolAnimationPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            // Fix intro animation
            .MatchForward(false,
                new CodeMatch(OpCodes.Call,
                    Method(typeof(Quaternion), nameof(Quaternion.Euler),
                        [typeof(float), typeof(float), typeof(float)])))
            .Advance(4) // ldfld MapToolController::HideLerp
            .SetInstruction(new CodeInstruction(OpCodes.Call, ((Func<MapToolController, float>)GetIntroLerp).Method))
            // Fix outro animation
            .MatchForward(false, new CodeMatch(OpCodes.Ldfld, Field(typeof(MapToolController), nameof(MapToolController.OutroCurve))))
            .Advance(6) // after stfld MapToolController::HideScale
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Action<MapToolController>)OutroAnimation).Method)
            )
            .InstructionEnumeration();

        static float GetIntroLerp(MapToolController controller)
        {
            if (!controller.PlayerAvatar.IsVRPlayer())
                return controller.HideLerp;

            return 1 - controller.HideLerp;
        }

        static void OutroAnimation(MapToolController controller)
        {
            if (!controller.PlayerAvatar.IsVRPlayer())
                return;
            
            controller.HideTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(MapToolPatches.MAP_HOLD_ANGLE, 0, 0),
                Quaternion.identity, controller.OutroCurve.Evaluate(controller.HideLerp));
        }
    }
}