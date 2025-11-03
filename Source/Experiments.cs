// ReSharper disable UnusedVariable

using System.Collections.Generic;
using System.Reflection.Emit;
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

        __instance.enemiesDifficulty1.Add(slowMouth);
        __instance.enemiesDifficulty2.Add(slowMouth);
        __instance.enemiesDifficulty3.Add(slowMouth);
    }

    private static bool done;

    [HarmonyPatch(typeof(MenuButton), nameof(MenuButton.OnHovering))]
    [HarmonyPrefix]
    private static void ForceMap()
    {
        if (done)
            return;

        done = true;

        var mgr = RunManager.instance;

        var station = mgr.levels[0];
        var manor = mgr.levels[1];
        var museum = mgr.levels[2];
        var hogwarts = mgr.levels[3];

        mgr.levels.Clear();
        mgr.levels.Add(museum);

        var boombox = museum.ValuablePresets[0].big[2];

        for (var i = 0; i < museum.ValuablePresets.Count; i++)
        {
            for (var j = 0; j < museum.ValuablePresets[i].big.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Big: {museum.ValuablePresets[i].big[j].prefabName}");
            for (var j = 0; j < museum.ValuablePresets[i].medium.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Medium: {museum.ValuablePresets[i].medium[j].prefabName}");
            for (var j = 0; j < museum.ValuablePresets[i].small.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Small: {museum.ValuablePresets[i].small[j].prefabName}");
            for (var j = 0; j < museum.ValuablePresets[i].tall.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Tall: {museum.ValuablePresets[i].tall[j].prefabName}");
            for (var j = 0; j < museum.ValuablePresets[i].tiny.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Tiny: {museum.ValuablePresets[i].tiny[j].prefabName}");
            for (var j = 0; j < museum.ValuablePresets[i].veryTall.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Very Tall: {museum.ValuablePresets[i].veryTall[j].prefabName}");
            for (var j = 0; j < museum.ValuablePresets[i].wide.Count; j++)
                Logger.LogDebug($"[{museum.ValuablePresets[i]}] Wide: {museum.ValuablePresets[i].wide[j].prefabName}");
        }

        museum.ValuablePresets[0].big.Clear();
        museum.ValuablePresets[0].medium.Clear();
        museum.ValuablePresets[0].small.Clear();
        museum.ValuablePresets[0].tall.Clear();
        museum.ValuablePresets[0].tiny.Clear();
        museum.ValuablePresets[0].veryTall.Clear();
        museum.ValuablePresets[0].wide.Clear();

        museum.ValuablePresets[0].big.Add(boombox);
        museum.ValuablePresets[0].medium.Add(boombox);
        museum.ValuablePresets[0].small.Add(boombox);
        museum.ValuablePresets[0].tall.Add(boombox);
        museum.ValuablePresets[0].tiny.Add(boombox);
        museum.ValuablePresets[0].veryTall.Add(boombox);
        museum.ValuablePresets[0].wide.Add(boombox);
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.FixedUpdate))]
    [HarmonyPostfix]
    private static void InfiniteSprintPatch(PlayerController __instance)
    {
        __instance.EnergyCurrent = __instance.EnergyStart;

        var script = PlayerController.instance?.playerAvatarScript;
        if (script != null) script.upgradeTumbleClimb = 100;
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

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.HeadEnergyLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FastRechargeHead(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 100f))
            .SetOperandAndAdvance(0.5f)
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.DebugTester))]
    [HarmonyPostfix]
    private static void IAmASurgeonIMeanTester(ref bool __result)
    {
        __result = true;
    }

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.DebugDev))]
    [HarmonyPostfix]
    private static void IAmASurgeonIMeanDeveloper(ref bool __result)
    {
        __result = true;
    }
}
#endif