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

        if (_state == ValuableDiscoverGraphic.State.Reminder)
            component.ReminderSetup();

        if (_state == ValuableDiscoverGraphic.State.Bad)
            component.BadSetup();

        return false;
    }
}