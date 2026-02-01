using HarmonyLib;
using RepoXR.Managers;

namespace RepoXR.Patches.Enemy;

[RepoXRPatch]
internal static class EnemySlowMouthPatches
{
    /// <summary>
    /// Make the slow mouth enemy puking cause haptic feedback if it's attached to the player
    /// </summary>
    [HarmonyPatch(typeof(EnemySlowMouthAttached), nameof(EnemySlowMouthAttached.StatePuke))]
    [HarmonyPostfix]
    private static void PukeHaptics()
    {
        HapticManager.Impulse(HapticManager.Hand.Both, HapticManager.Type.Continuous, 0.25f, 0.1f, 3);
    }
}