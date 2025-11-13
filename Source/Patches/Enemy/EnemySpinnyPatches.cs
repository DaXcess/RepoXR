using HarmonyLib;
using RepoXR.Player.Camera;

namespace RepoXR.Patches.Enemy;

[RepoXRPatch]
internal static class EnemySpinnyPatches
{
    /// <summary>
    /// Make sure to always look at the little gambling machine
    /// </summary>
    [HarmonyPatch(typeof(EnemySpinny), nameof(EnemySpinny.OverrideTargetPlayerCameraAim))]
    [HarmonyPrefix]
    private static bool OverrideVRCameraAim(EnemySpinny __instance, float _strenght, float _strenghtNoAim)
    {
        // Always low impact, there's not really a need to force up-down look here
        VRCameraAim.instance.SetAimTargetSoft(__instance.spinnyWheel.position, 0.1f, _strenght, _strenghtNoAim,
            __instance.gameObject, 100, true);

        return false;
    }
}