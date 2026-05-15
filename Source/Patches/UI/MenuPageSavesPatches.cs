using HarmonyLib;
using RepoXR.UI.Menu;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class MenuPageSavesPatches
{
    /// <summary>
    /// Make sure to close the keyboard after renaming
    /// </summary>
    [HarmonyPatch(typeof(MenuPageSavesRename), nameof(MenuPageSavesRename.ButtonConfirm))]
    [HarmonyPostfix]
    private static void OnConfirm()
    {
        if (InputKeyboard.instance is {} instance && instance != null)
            instance.Close();
    }

    /// <summary>
    /// Make sure to close the keyboard after canceling the renaming
    /// </summary>
    [HarmonyPatch(typeof(MenuPageSavesRename), nameof(MenuPageSavesRename.ExitPage))]
    [HarmonyPostfix]
    private static void OnExitPage()
    {
        if (InputKeyboard.instance is {} instance && instance != null)
            instance.Close();
    }
}