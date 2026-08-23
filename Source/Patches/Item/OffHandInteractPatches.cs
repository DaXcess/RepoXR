using System;
using HarmonyLib;
using RepoXR.Input;
using RepoXR.Player;
using UnityEngine.InputSystem;

namespace RepoXR.Patches.Item;

/// <summary>
/// Makes "use the item I'm holding" follow the hand that is actually holding it.
///
/// <see cref="ItemToggle"/> is what nearly every usable item routes through - guns, lights, anything with an on/off
/// state - and it only asks two questions: is this object held by the local player, and is Interact pressed. Neither
/// question knows about hands, so with two grabbers the dominant hand's Interact drives whatever *either* hand is
/// holding, and the off hand has no way to use its own item.
///
/// Rather than rewriting that logic, the input read itself is adjusted for the duration of an ItemToggle update.
/// Every other Interact read in the game is left exactly as it was.
/// </summary>
[RepoXRPatch]
internal static class OffHandInteractPatches
{
    private enum Source
    {
        /// <summary>Not an off-hand item; leave the read alone</summary>
        Unchanged,

        /// <summary>Held by the off hand alone, so only its own control counts</summary>
        OffHand,

        /// <summary>Carried in both hands, so either control counts</summary>
        EitherHand
    }

    /// <summary>
    /// The item currently being updated, if any
    /// </summary>
    private static ItemToggle? current;

    [HarmonyPatch(typeof(ItemToggle), nameof(ItemToggle.Update))]
    [HarmonyPrefix]
    private static void BeginItemContext(ItemToggle __instance, out ItemToggle? __state)
    {
        // Saved and restored rather than cleared, in case these ever nest
        __state = current;
        current = __instance;
    }

    [HarmonyPatch(typeof(ItemToggle), nameof(ItemToggle.Update))]
    [HarmonyPostfix]
    private static void EndItemContext(ItemToggle? __state)
    {
        current = __state;
    }

    // Postfixes rather than prefixes on purpose: the original read still runs, so nothing can be lost if the
    // adjustment below ever decides not to apply, and there is no skipped-original behaviour to get wrong

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.InputDown))]
    [HarmonyPostfix]
    private static void RedirectInteractDown(InputKey key, ref bool __result) =>
        Apply(key, ref __result, action => action.WasPressedThisFrame());

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.InputHold))]
    [HarmonyPostfix]
    private static void RedirectInteractHold(InputKey key, ref bool __result) =>
        Apply(key, ref __result, action => action.IsPressed());

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.InputUp))]
    [HarmonyPostfix]
    private static void RedirectInteractUp(InputKey key, ref bool __result) =>
        Apply(key, ref __result, action => action.WasReleasedThisFrame());

    private static void Apply(InputKey key, ref bool result, Func<InputAction, bool> read)
    {
        var source = Resolve(key);
        if (source == Source.Unchanged)
            return;

        var offHand = OffHandControls.Interact is { } action && read(action);

        // Held only by the off hand: its control replaces the dominant one, so the dominant hand can no longer
        // reach across and use it. Carried in both hands: either control works, which is what you'd expect when
        // both of your hands are on the thing.
        result = source == Source.OffHand ? offHand : result || offHand;
    }

    /// <summary>
    /// Only Interact reads made while updating an item the off hand has a grip on are adjusted. Everything else,
    /// including the dominant hand's own items, is left untouched.
    /// </summary>
    private static Source Resolve(InputKey key)
    {
        if (key != InputKey.Interact || current is not { physGrabObject: { } item } || !item)
            return Source.Unchanged;

        if (VROffHandGrabber.HeldByBothHands(item))
            return Source.EitherHand;

        return VROffHandGrabber.HeldByOffHandOnly(item) ? Source.OffHand : Source.Unchanged;
    }
}
