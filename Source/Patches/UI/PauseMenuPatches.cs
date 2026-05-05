using HarmonyLib;
using RepoXR.Managers;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class PauseMenuPatches
{
    /// <summary>
    /// Detect if the pause menu is opened
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageOpen))]
    [HarmonyPrefix]
    private static void OnPageOpen(MenuManager __instance, MenuPageIndex menuPageIndex)
    {
        if (menuPageIndex != MenuPageIndex.Escape)
            return;

        if (VRSession.Instance is not { } session)
            return;
        
        session.HUD.PauseGame();
    }

    /// <summary>
    /// Detect if the pause menu is closed
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageCloseAll))]
    [HarmonyPrefix]
    private static void OnAllPagesClose(MenuManager __instance)
    {
        if (!__instance.currentMenuPage || __instance.currentMenuPage.menuPageIndex != MenuPageIndex.Escape)
            return;

        if (VRSession.Instance is not { } session)
            return;
        
        session.HUD.ResumeGame();
    }

    /// <summary>
    /// Detect if the pause menu is closed
    /// </summary>
    [HarmonyPatch(typeof(MenuPageEsc), nameof(MenuPageEsc.ButtonEventContinue))]
    [HarmonyPrefix]
    private static void OnEscapeMenuClose()
    {
        if (VRSession.Instance is not { } session)
            return;
        
        session.HUD.ResumeGame();
    }

    /// <summary>
    /// Detect if when player is self-destructing
    /// </summary>
    [HarmonyPatch(typeof(MenuPageEsc), nameof(MenuPageEsc.ButtonEventSelfDestruct))]
    [HarmonyPrefix]
    private static void OnSelfDestruct()
    {
        if (VRSession.Instance is not { } session)
            return;
        
        session.HUD.ResumeGame();
    }
    
    /// <summary>
    /// Close the main menu if the outro is starting (meaning we're leaving the game)
    /// </summary>
    [HarmonyPatch(typeof(GameDirector), nameof(GameDirector.OutroStart))]
    [HarmonyPostfix]
    private static void OnStartOutro()
    {
        if (VRSession.Instance is not { } session)
            return;
        
        session.HUD.ResumeGame(true);
    }
}