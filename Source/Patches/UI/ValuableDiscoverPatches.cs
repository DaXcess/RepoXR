using System.Runtime.CompilerServices;
using CustomDiscoverStateLib;
using HarmonyLib;
using RepoXR.Assets;
using UnityEngine;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class ValuableDiscoverPatches
{
    /// <summary>
    /// Replace the in-game <see cref="ValuableDiscoverGraphic"/> with a custom one that renders a 3d cube instead of
    /// a canvas element
    /// </summary>
    [HarmonyPatch(typeof(ValuableDiscover), nameof(ValuableDiscover.New))]
    [HarmonyPrefix]
    private static bool OnValuableDiscovered(ValuableDiscover __instance, PhysGrabObject _target,
        ValuableDiscoverGraphic.State _state)
    {
        var component = Object.Instantiate(AssetCollection.ValuableDiscover)
            .GetComponent<RepoXR.UI.ValuableDiscoverGraphic>();
        component.target = _target;

        if (Compat.IsLoaded(Compat.CustomDiscoverStateLib) &&
            AttemptSetupCustom(component, __instance, _target, _state))
            return false; // Return early if CustomDiscoverState is applicable to this valuable

        if (_state == ValuableDiscoverGraphic.State.Reminder)
            component.ReminderSetup();

        if (_state == ValuableDiscoverGraphic.State.Bad)
            component.BadSetup();

        return false;
    }

    /// <summary>
    /// Attempt to set up custom colors provided by CustomDiscoverStateLib if possible
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AttemptSetupCustom(RepoXR.UI.ValuableDiscoverGraphic graphic, ValuableDiscover valuable,
        PhysGrabObject target, ValuableDiscoverGraphic.State state)
    {
        if (CustomDiscoverState.customStates.TryGetValue(state, out var customState))
        {
            graphic.CustomSetup(state, customState.ColorMiddle, customState.ColorCorner);
            return true;
        }

        foreach (var (condState, condDelegate) in CustomDiscoverState.conditionalStates)
        {
            if (!condDelegate(valuable, target) || CustomDiscoverState.customStates[condState] == null)
                continue;

            customState = CustomDiscoverState.customStates[condState];
            graphic.CustomSetup(condState, customState.ColorMiddle, customState.ColorCorner);

            return true;
        }

        foreach (var (dynState, dynDelegate) in CustomDiscoverState.dynamicStates)
        {
            if (!dynDelegate(valuable, target, out var middle, out var corner))
                continue;

            graphic.CustomSetup(dynState, middle!.Value, corner!.Value);
            return true;
        }

        return false;
    }
}