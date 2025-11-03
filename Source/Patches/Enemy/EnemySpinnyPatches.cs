using HarmonyLib;
using RepoXR.Player.Camera;

namespace RepoXR.Patches.Enemy;

[RepoXRPatch]
internal static class EnemySpinnyPatches
{
    /// <summary>
    /// TODO: I don't yet know what this enemy does or looks like
    /// </summary>
    [HarmonyPatch(typeof(EnemySpinny), nameof(EnemySpinny.OverrideTargetPlayerCameraAim))]
    [HarmonyPrefix]
    private static bool OverrideVRCameraAim(EnemySpinny __instance, float _strenght, float _strenghtNoAim)
    {
        VRCameraAim.instance.SetAimTargetSoft(__instance.spinnyWheel.position, 0.1f, _strenght, _strenghtNoAim,
            __instance.gameObject, 100, Plugin.Config.ReducedAimImpact.Value);

        return false;
    }
}