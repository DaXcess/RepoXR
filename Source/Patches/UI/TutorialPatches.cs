using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Player.Camera;
using TMPro;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class TutorialPatches
{
    private static TMP_SpriteAsset originalEmojis;

    [HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.Start))]
    [HarmonyPostfix]
    private static void OnTutorialStart(TutorialUI __instance)
    {
        // Copy a reference to the original sprite asset
        originalEmojis = __instance.Text.spriteAsset;

        // Update the dummy text to use our input icons
        __instance.dummyText.spriteAsset = AssetCollection.TMPInputsSpriteAsset;
    }
    
    /// <summary>
    /// Update the tutorial UI to use our input icons
    /// </summary>
    [HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.SetPage))]
    [HarmonyPrefix]
    private static void UpdateTextSpriteAtlas(TutorialUI __instance, ref string dummyTextString, bool transition)
    {
        __instance.Text.spriteAsset = transition ? originalEmojis : AssetCollection.TMPInputsSpriteAsset;

        dummyTextString = dummyTextString.Replace("keyboard", "controller");
    }

    /// <summary>
    /// Update the tips UI to use our input icons
    /// </summary>
    [HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.SetTipPage))]
    [HarmonyPrefix]
    private static void UpdateTipTextSpriteAtlas(TutorialUI __instance, ref string text)
    {
        __instance.Text.spriteAsset = AssetCollection.TMPInputsSpriteAsset;
        
        text = text.Replace("keyboard", "controller");
    }

    /// <summary>
    /// Make sure the sprite asset is reverted back to ours after the "good job" message
    /// </summary>
    [HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.SwitchPage), MethodType.Enumerator)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SwitchPagePatch(IEnumerable<CodeInstruction> instructions)
    {
        // In release: instance is `ldloc.1`
        // In debug: instance is `this.<>4__this`

        CodeInstruction[] ldThis = Debug.isDebugBuild
            ?
            [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld,
                    Field(Inner(typeof(TutorialUI), "<SwitchPage>d__30"), "<>4__this"))
            ]
            : [new CodeInstruction(OpCodes.Ldloc_1)];

        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))))
            .InsertAndAdvance([
                ..ldThis,
                new CodeInstruction(OpCodes.Call, ((Action<TutorialUI>)SetSpriteAtlas).Method)
            ])
            .InstructionEnumeration();

        static void SetSpriteAtlas(TutorialUI ui)
        {
            ui.Text.spriteAsset = AssetCollection.TMPInputsSpriteAsset;
        }
    }

    /// <summary>
    /// Replace some of the tutorial text to make more sense in VR
    /// </summary>
    [HarmonyPatch(typeof(TutorialDirector), nameof(TutorialDirector.Awake))]
    [HarmonyPostfix]
    private static void OnTutorialDirectorCreate(TutorialDirector __instance)
    {
        // Replace some tutorial text with VR equivalents
        var inventoryFill = __instance.tutorialPages[10];
        var inventoryEmpty = __instance.tutorialPages[11];
        var map = __instance.tutorialPages[12];
        var expressions = __instance.tutorialPages[25];

        inventoryFill.text =
            "Grab an Item and deposit it in one of the slots in front of you to put it in your inventory.";
        inventoryFill.dummyText =
            "Grab ([grab]) an item, hold it and deposit it in one of the three slots in front of you. Fill all spots in your inventory to complete the progress bar!";

        inventoryEmpty.text = "Use [grab] with your right hand to take an item out of your inventory.";
        inventoryEmpty.dummyText =
            "Use [grab] while holding your right hand over an item in your inventory, and empty all spots to fill the progress bar!";

        map.text =
            "Use [MapGrabLeft] or [MapGrabRight] to grab and view the map. The map is <b><u>important</b></u> for navigation.";
        map.dummyText =
            "Use your left ([MapGrabLeft]) or right ([MapGrabRight]) hand and press the respective button to pick up and view the map until you have filled the progress bar!";

        expressions.text = "Press and hold the [chat] button open the expressions wheel. You can even mix!";
    }

    /// <summary>
    /// Use a less impactful force rotate to look at the truck screen
    /// </summary>
    [HarmonyPatch(typeof(TutorialTruckTrigger), nameof(TutorialTruckTrigger.Update))]
    [HarmonyPostfix]
    private static void TruckForceRotate(TutorialTruckTrigger __instance)
    {
        if (__instance.lockLookTimer <= 0)
            return;

        VRCameraAim.instance.SetAimTarget(__instance.lookTarget.position + Vector3.down, 0.1f, 5, __instance.gameObject,
            90, true);
    }

    /// <summary>
    /// Make the spectate head prompt UI have the correct control sprites
    /// </summary>
    [HarmonyPatch(typeof(SpectateHeadUI), nameof(SpectateHeadUI.Awake))]
    [HarmonyPostfix]
    private static void OnSpectateHeadUICreate(SpectateHeadUI __instance)
    {
        __instance.promptText.spriteAsset = AssetCollection.TMPInputsSpriteAsset;
    }
}