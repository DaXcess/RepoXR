using HarmonyLib;
using RepoXR.Assets;
using UnityEngine;
using UnityEngine.UI;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class LoadingUIPatches
{
    /// <summary>
    /// Reset position when loading UI is shown
    /// </summary>
    [HarmonyPatch(typeof(LoadingUI), nameof(LoadingUI.StartLoading))]
    [HarmonyPostfix]
    private static void OnStartLoading()
    {
        Object.FindObjectOfType<RepoXR.UI.LoadingUI>()?.ResetPosition();
    }

    /// <summary>
    /// Fix the controller binding icon on the stuck text and mask it away when it's not shown
    /// </summary>
    [HarmonyPatch(typeof(LoadingUI), nameof(LoadingUI.Awake))]
    [HarmonyPostfix]
    private static void StuckTextPatches(LoadingUI __instance)
    {
        __instance.stuckText.spriteAsset = AssetCollection.TMPInputsSpriteAsset;

        var mask = new GameObject("Stuck Text Mask")
        {
            transform =
            {
                parent = __instance.transform,
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity
            }
        }.AddComponent<RectMask2D>();
        var maskTransform = mask.GetComponent<RectTransform>();
        var uiTransform = __instance.GetComponent<RectTransform>();

        maskTransform.sizeDelta = uiTransform.sizeDelta;

        __instance.stuckTransform.SetParent(maskTransform, false);
    }
}