using HarmonyLib;
using RepoXR.UI;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class CrosshairPatches
{
    /// <summary>
    /// Create a VR crosshair
    /// </summary>
    [HarmonyPatch(typeof(Aim), nameof(Aim.Awake))]
    [HarmonyPostfix]
    private static void OnCrosshairCreate(Aim __instance)
    {
        // The off-hand reticle is a copy of the finished crosshair, so the Aim inside it must not wrap itself in
        // yet another canvas when it wakes up
        if (__instance.GetComponentInParent<Crosshair>())
            return;

        var canvas = new GameObject("Crosshair").AddComponent<Canvas>();
        var offset = new GameObject("Crosshair Offset")
            { transform = { parent = canvas.transform, localEulerAngles = new Vector3(270, 270, 0) } };
        var rect = canvas.GetComponent<RectTransform>();
        
        canvas.renderMode = RenderMode.WorldSpace;
        rect.sizeDelta = new Vector2(40, 40);
        rect.localScale = Vector3.one * 0.01f;

        __instance.transform.parent = offset.transform;
        __instance.transform.localPosition = Vector3.zero;
        __instance.transform.localRotation = Quaternion.identity;
        __instance.transform.localScale = Vector3.one;

        canvas.gameObject.AddComponent<Crosshair>();
    }

    /// <summary>
    /// Re-parent the overcharge UI onto the crosshair
    /// </summary>
    [HarmonyPatch(typeof(OverchargeRoundBarUI), nameof(OverchargeRoundBarUI.Start))]
    [HarmonyPostfix]
    private static void OnOverchargeCreate(OverchargeRoundBarUI __instance)
    {
        __instance.transform.SetParent(Crosshair.instance.transform, false);
        __instance.transform.localEulerAngles = new Vector3(-90, -90, 0);
        __instance.transform.localPosition = Vector3.zero;
        __instance.transform.localScale = Vector3.one * 0.65f;

        Crosshair.instance.gameObject.SetLayerRecursively(5); // Needs to be visible through walls
    }
}