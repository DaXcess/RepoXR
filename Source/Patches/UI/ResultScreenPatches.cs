using HarmonyLib;
using RepoXR.Assets;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class ResultScreenPatches
{
    /// <summary>
    /// Reset the loading screen position and fix skip font on the first frame of the result screen
    /// </summary>
    [HarmonyPatch(typeof(ResultScreenUI), nameof(ResultScreenUI.StateFadeIn))]
    [HarmonyPrefix]
    private static void InitializeScreenPatch(ResultScreenUI __instance)
    {
        if (!__instance.stateImpulse) return;

        __instance.skipText.spriteAsset = AssetCollection.TMPInputsSpriteAsset;
        RepoXR.UI.LoadingUI.instance.ResetPosition();
    }

    /// <summary>
    /// Disable player movement and rotation while the result animation plays (fixes some particle system funkyness)
    /// </summary>
    [HarmonyPatch(typeof(ResultScreenUI), nameof(ResultScreenUI.Update))]
    [HarmonyPostfix]
    private static void DisableTurningPatch(ResultScreenUI __instance)
    {
        if (__instance.fadeLerp < 1) return;

        PlayerController.instance.InputDisable(0.1f);
    }
}