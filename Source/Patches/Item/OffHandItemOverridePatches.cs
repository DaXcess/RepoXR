using HarmonyLib;
using RepoXR.Player;
using UnityEngine;

namespace RepoXR.Patches.Item;

/// <summary>
/// Makes an item's grabber overrides land on the hand that is actually holding it.
///
/// Items reach back into their grabber to pin how they are held - a gun keeps itself at a fixed distance, melee
/// weapons adjust their grab point, and so on. Those calls resolve the grabber through the shared
/// <see cref="PhysGrabber.instance"/> static, which always points at the dominant hand, so an item held in the off
/// hand pins the *dominant* hand instead of itself.
///
/// The visible result is a swap: hold a gun in one hand and a hammer in the other, and the gun's "stay close"
/// override lands on the hammer, leaving the hammer pinned and the gun free to be pushed and pulled - exactly
/// backwards.
///
/// The item being updated is tracked, and any override addressed to the wrong grabber while an off-hand item is
/// updating is forwarded to the right one.
/// </summary>
[RepoXRPatch]
internal static class OffHandItemOverridePatches
{
    /// <summary>
    /// The object belonging to the item currently being updated, if any
    /// </summary>
    private static PhysGrabObject? current;

    private static void Begin(PhysGrabObject? item, out PhysGrabObject? state)
    {
        // Saved and restored rather than cleared, in case these ever nest
        state = current;
        current = item;
    }

    private static void End(PhysGrabObject? state)
    {
        current = state;
    }

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.Update))]
    [HarmonyPrefix]
    private static void GunUpdateBegin(ItemGun __instance, out PhysGrabObject? __state) =>
        Begin(__instance.physGrabObject, out __state);

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.Update))]
    [HarmonyPostfix]
    private static void GunUpdateEnd(PhysGrabObject? __state) => End(__state);

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.FixedUpdate))]
    [HarmonyPrefix]
    private static void GunFixedUpdateBegin(ItemGun __instance, out PhysGrabObject? __state) =>
        Begin(__instance.physGrabObject, out __state);

    [HarmonyPatch(typeof(ItemGun), nameof(ItemGun.FixedUpdate))]
    [HarmonyPostfix]
    private static void GunFixedUpdateEnd(PhysGrabObject? __state) => End(__state);

    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.Update))]
    [HarmonyPrefix]
    private static void MeleeUpdateBegin(ItemMelee __instance, out PhysGrabObject? __state) =>
        Begin(__instance.physGrabObject, out __state);

    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.Update))]
    [HarmonyPostfix]
    private static void MeleeUpdateEnd(PhysGrabObject? __state) => End(__state);

    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.FixedUpdate))]
    [HarmonyPrefix]
    private static void MeleeFixedUpdateBegin(ItemMelee __instance, out PhysGrabObject? __state) =>
        Begin(__instance.physGrabObject, out __state);

    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.FixedUpdate))]
    [HarmonyPostfix]
    private static void MeleeFixedUpdateEnd(PhysGrabObject? __state) => End(__state);

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.OverrideGrabDistance))]
    [HarmonyPrefix]
    private static bool GrabDistance(PhysGrabber __instance, float dist)
    {
        if (!ShouldForward(__instance, out var target))
            return true;

        target.OverrideGrabDistance(dist);

        return false;
    }

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.OverrideMinimumGrabDistance))]
    [HarmonyPrefix]
    private static bool MinimumGrabDistance(PhysGrabber __instance, float dist)
    {
        if (!ShouldForward(__instance, out var target))
            return true;

        target.OverrideMinimumGrabDistance(dist);

        return false;
    }

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.OverrideGrabPoint))]
    [HarmonyPrefix]
    private static bool GrabPoint(PhysGrabber __instance, Transform grabPoint)
    {
        if (!ShouldForward(__instance, out var target))
            return true;

        target.OverrideGrabPoint(grabPoint);

        return false;
    }

    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.OverrideGrabStrength))]
    [HarmonyPrefix]
    private static bool GrabStrength(PhysGrabber __instance, float strength, float time)
    {
        if (!ShouldForward(__instance, out var target))
            return true;

        target.OverrideGrabStrength(strength, time);

        return false;
    }

    /// <summary>
    /// Whether this override was aimed at the wrong grabber, and which one it should go to instead.
    ///
    /// Forwarding is deliberately narrow: it only applies while an item is being updated, only when that item is
    /// held by the off hand alone, and never to a call already addressed to the off-hand grabber - which also stops
    /// the forwarded call from bouncing back here.
    /// </summary>
    private static bool ShouldForward(PhysGrabber instance, out PhysGrabber target)
    {
        target = null!;

        if (current is not { } item || !item)
            return false;

        if (VROffHandGrabber.ActiveGrabber is not { } offHand || instance == offHand)
            return false;

        if (!VROffHandGrabber.HeldByOffHandOnly(item))
            return false;

        target = offHand;

        return true;
    }
}
