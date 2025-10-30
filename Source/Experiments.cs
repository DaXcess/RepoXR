// ReSharper disable UnusedVariable

using HarmonyLib;

namespace RepoXR;

#if DEBUG
internal static class Experiments
{
    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.Awake))]
    [HarmonyPostfix]
    private static void FuckLolEnemy(EnemyDirector __instance)
    {
        // Difficulty 1 enemies
        var ceileingEye = __instance.enemiesDifficulty1[0];
        var thinMan = __instance.enemiesDifficulty1[1];
        var gnome = __instance.enemiesDifficulty1[2];
        var duck = __instance.enemiesDifficulty1[3];
        var slowMouth = __instance.enemiesDifficulty1[4];

        // Difficulty 2 enemies
        var valuableThrower = __instance.enemiesDifficulty2[0];
        var animal = __instance.enemiesDifficulty2[1];
        var upscream = __instance.enemiesDifficulty2[2];
        var hidden = __instance.enemiesDifficulty2[3];
        var tumbler = __instance.enemiesDifficulty2[4];
        var bowtie = __instance.enemiesDifficulty2[5];
        var floater = __instance.enemiesDifficulty2[6];
        var bang = __instance.enemiesDifficulty2[7];

        // Difficulty 3 enemies
        var head = __instance.enemiesDifficulty3[0];
        var robe = __instance.enemiesDifficulty3[1];
        var hunter = __instance.enemiesDifficulty3[2];
        var runner = __instance.enemiesDifficulty3[3];
        var beamer = __instance.enemiesDifficulty3[4];
        var slowWalker = __instance.enemiesDifficulty3[5];

        __instance.enemiesDifficulty1.Clear();
        __instance.enemiesDifficulty2.Clear();
        __instance.enemiesDifficulty3.Clear();

        __instance.enemiesDifficulty1.Add(ceileingEye);
        __instance.enemiesDifficulty2.Add(ceileingEye);
        __instance.enemiesDifficulty3.Add(ceileingEye);
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    [HarmonyPostfix]
    private static void InfiniteSprintPatch(PlayerController __instance)
    {
        __instance.EnergyCurrent = __instance.EnergyStart;
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Hurt))]
    [HarmonyPrefix]
    private static bool NoDamage()
    {
        return false;
    }

    [HarmonyPatch(typeof(EnemyThinMan), nameof(EnemyThinMan.TentacleLogic))]
    [HarmonyPostfix]
    private static void NoHurtMe(EnemyThinMan __instance)
    {
        if (__instance.tentacleLerp >= 1f)
            __instance.tentacleLerp = 0.99f;
    }
}
#endif