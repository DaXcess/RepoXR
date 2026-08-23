using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Player;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

/// <summary>
/// Makes the shared hand-transform resolution (<see cref="PlayerLocalCameraExtensions.GetHandOverrideTransform"/>)
/// and the shared grab-button reads (<see cref="SemiFunc.InputHold"/>/<see cref="SemiFunc.InputDown"/> for
/// <see cref="InputKey.Grab"/>) aware of which PhysGrabber instance is currently running, so the off-hand clone
/// (see <see cref="VROffHandGrabber"/>) resolves against the off-hand instead of the dominant hand.
///
/// Note: PhysGrabber.Update() reads "is Grab held" via SemiFunc.InputHold(InputKey.Grab), which is a global read
/// completely independent of which PhysGrabber instance is asking - both the dominant-hand grabber and the
/// off-hand clone would otherwise see the exact same (dominant-hand-bound) value. This is the actual mechanism
/// that decides whether a grab starts *and* whether it continues to be held, so it has to be patched here at the
/// source rather than downstream in RayCheck (RayCheck's own "_grab" parameter is just an edge-triggered "attempt
/// a new grab this tick" pulse derived from this same read, not a continuous held state).
///
/// Every scope below saves and restores the previous flag value rather than clearing it, since these methods nest:
/// Update() calls into RayCheck()/ObjectTurning()/StartGrabbingPhysObject(), and a postfix that unconditionally
/// cleared the flag would drop the rest of the enclosing Update() back onto the dominant hand.
/// </summary>
[RepoXRPatch]
internal static class OffHandGrabberPatches
{
    private static bool IsOffHand(PhysGrabber grabber) =>
        VROffHandGrabber.Instance is { } offHand && grabber == offHand.Grabber;

    private static void BeginScope(PhysGrabber grabber, out bool state)
    {
        state = PlayerLocalCameraExtensions.UseOffHand;
        PlayerLocalCameraExtensions.UseOffHand = IsOffHand(grabber);

        // The off-hand grabber lives on an object of our own, which has to be moved into place before any of the
        // grabber's own maths runs against it
        if (PlayerLocalCameraExtensions.UseOffHand)
            VROffHandGrabber.Instance!.SyncTransform();
    }

    private static void EndScope(bool state)
    {
        PlayerLocalCameraExtensions.UseOffHand = state;
    }

    /// <summary>
    /// Scope hand resolution to the off-hand for the duration of the off-hand grabber's own Update (and everything
    /// it calls into, like RayCheck/ObjectTurning/StartGrabbingPhysObject).
    ///
    /// The off-hand's grab control is also sampled here rather than from VROffHandGrabber's own Update, because
    /// script execution order between two components on the same GameObject is undefined and the grabber must never
    /// read a stale "was pressed this frame" edge.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyPrefix]
    private static void UpdateBegin(PhysGrabber __instance, out bool __state)
    {
        BeginScope(__instance, out __state);

        if (PlayerLocalCameraExtensions.UseOffHand)
            VROffHandGrabber.Instance!.PollInput();

        // The clone claims this static in its own Start(); make sure it is pointing at the dominant grabber before
        // any game code that reads it runs this frame
        if (VROffHandGrabber.Instance is { } offHand && PhysGrabber.instance == offHand.Grabber && offHand.Primary)
            PhysGrabber.instance = offHand.Primary;
    }

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyPostfix]
    private static void UpdateEnd(bool __state) => EndScope(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.FixedUpdate))]
    [HarmonyPrefix]
    private static void FixedUpdateBegin(PhysGrabber __instance, out bool __state) =>
        BeginScope(__instance, out __state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.FixedUpdate))]
    [HarmonyPostfix]
    private static void FixedUpdateEnd(bool __state) => EndScope(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.LateUpdate))]
    [HarmonyPrefix]
    private static void LateUpdateBegin(PhysGrabber __instance, out bool __state) =>
        BeginScope(__instance, out __state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.LateUpdate))]
    [HarmonyPostfix]
    private static void LateUpdateEnd(bool __state) => EndScope(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.RayCheck))]
    [HarmonyPrefix]
    private static void RayCheckBegin(PhysGrabber __instance, out bool __state) =>
        BeginScope(__instance, out __state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.RayCheck))]
    [HarmonyPostfix]
    private static void RayCheckEnd(bool __state) => EndScope(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyPrefix]
    private static void ObjectTurningBegin(PhysGrabber __instance, out bool __state) =>
        BeginScope(__instance, out __state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
    [HarmonyPostfix]
    private static void ObjectTurningEnd(bool __state) => EndScope(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.StartGrabbingPhysObject))]
    [HarmonyPrefix]
    private static void StartGrabbingBegin(PhysGrabber __instance, out bool __state) =>
        BeginScope(__instance, out __state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.StartGrabbingPhysObject))]
    [HarmonyPostfix]
    private static void StartGrabbingEnd(bool __state) => EndScope(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.GrabStateClimb))]
    [HarmonyPrefix]
    private static void GrabStateClimbBegin(PhysGrabber __instance, out bool __state) =>
        BeginScope(__instance, out __state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.GrabStateClimb))]
    [HarmonyPostfix]
    private static void GrabStateClimbEnd(bool __state) => EndScope(__state);

    /// <summary>
    /// Redirect every SemiFunc.InputHold(InputKey)/InputDown(InputKey) call made from within PhysGrabber.Update()
    /// through an instance-aware wrapper. For any key other than Grab, or for any PhysGrabber that isn't our
    /// off-hand clone, behavior is unchanged (falls straight through to the original SemiFunc call).
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RedirectGrabInputReads(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputHold))))
            .Repeat(matcher => matcher
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call,
                    ((Func<InputKey, PhysGrabber, bool>)GetInputHold).Method)))
            .Start()
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputDown))))
            .Repeat(matcher => matcher
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call,
                    ((Func<InputKey, PhysGrabber, bool>)GetInputDown).Method)))
            .InstructionEnumeration();
    }

    private static bool GetInputHold(InputKey key, PhysGrabber grabber)
    {
        if (key == InputKey.Grab && VROffHandGrabber.Instance is { } offHand && grabber == offHand.Grabber)
            return offHand.GrabHeld;

        return SemiFunc.InputHold(key);
    }

    private static bool GetInputDown(InputKey key, PhysGrabber grabber)
    {
        if (key == InputKey.Grab && VROffHandGrabber.Instance is { } offHand && grabber == offHand.Grabber)
            return offHand.GrabDown;

        return SemiFunc.InputDown(key);
    }

    /// <summary>
    /// Scope hand resolution to the off-hand while the object currently being manipulated is the one held by the
    /// off-hand clone, rather than whatever the dominant hand is holding.
    ///
    /// This needs its own scope because PhysGrabObject drives this from its own FixedUpdate, outside of any of the
    /// grabber methods scoped above.
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.PhysicsGrabbingManipulation))]
    [HarmonyPrefix]
    private static void BeginObjectHandContext(PhysGrabObject __instance, out bool __state)
    {
        __state = PlayerLocalCameraExtensions.UseOffHand;

        // Each grabber pulls on the object through its own grab point, so both hands carrying one thing works on
        // its own. This flag only decides which hand is the *orientation* reference, and there can only be one
        // answer - so when both hands are on it the dominant hand keeps that role. Flipping it per frame would
        // make the object jitter between two reference frames.
        PlayerLocalCameraExtensions.UseOffHand = VROffHandGrabber.HeldByOffHandOnly(__instance);
    }

    [HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.PhysicsGrabbingManipulation))]
    [HarmonyPostfix]
    private static void EndObjectHandContext(bool __state) => EndScope(__state);
}
