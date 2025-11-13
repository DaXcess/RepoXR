using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Input;
using RepoXR.Player.Camera;
using RepoXR.UI;
using UnityEngine;
using UnityEngine.UI;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class UIPatches
{
    /// <summary>
    /// Disable menu cursor
    /// </summary>
    [HarmonyPatch(typeof(MenuCursor), nameof(MenuCursor.Update))]
    [HarmonyPrefix]
    private static bool MenuCursorVRPatch(MenuCursor __instance)
    {
        __instance.gameObject.SetActive(false);

        return false;
    }

    /// <summary>
    /// Disable the menu page intro animation
    /// </summary>
    [HarmonyPatch(typeof(MenuPageMain), nameof(MenuPageMain.Start))]
    [HarmonyPostfix]
    private static void DisableMainMenuAnimation(MenuPageMain __instance)
    {
        __instance.menuPage.disableIntroAnimation = true;
        __instance.doIntroAnimation = false;
        __instance.transform.localPosition = Vector3.zero;
        __instance.waitTimer = 3;
        __instance.introDone = true;
    }

    /// <summary>
    /// Disable UI detection code if the <see cref="XRRayInteractorManager"/> is not yet present
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIMouseHover))]
    [HarmonyPrefix]
    private static bool DisableHoverOnEarly()
    {
        return XRRayInteractorManager.Instance is not null;
    }

    /// <summary>
    /// Block other UI hover logic if we're checking two different canvasses
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIMouseHover))]
    [HarmonyPrefix]
    private static bool UIPointerHoverOtherCanvasPatch(RectTransform rectTransform, ref bool __result)
    {
        if (XRRayInteractorManager.Instance is not { } manager)
            return true;

        var (interactor, _) = manager.GetActiveInteractor();
        if (!interactor.TryGetCurrentUIRaycastResult(out var result))
        {
            __result = false;
            return false;
        }

        if (result.gameObject.GetComponentInParent<Canvas>() != rectTransform.GetComponentInParent<Canvas>())
        {
            __result = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Detect UI hits using VR pointers instead of mouse cursor
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIMouseHover))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UIPointerHoverPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            // Get coords using VR interactor
            .MatchForward(false,
                new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.UIMousePosToUIPos))))
            .SetOperandAndAdvance(PropertyGetter(typeof(XRRayInteractorManager),
                nameof(XRRayInteractorManager.Instance)))
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Callvirt,
                    Method(typeof(XRRayInteractorManager), nameof(XRRayInteractorManager.GetUIHitPosition)))
            )
            // Fix scrollbox masking
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertyGetter(typeof(Transform), nameof(Transform.position))))
            .SetOperandAndAdvance(PropertyGetter(typeof(Transform), nameof(Transform.localPosition)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Properly set the mouse hold position
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Update))]
    [HarmonyPrefix]
    private static void UpdateMouseHoldPosition(MenuManager __instance)
    {
        var manager = XRRayInteractorManager.Instance;
        if (manager is null)
            return;

        if (manager.GetTriggerButton())
        {
            if (__instance.mouseHoldPosition == Vector2.zero)
                __instance.mouseHoldPosition = manager.GetUIHitPosition(manager.GetUIHitRectTransform());
        }
        else
        {
            __instance.mouseHoldPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Detect UI hits using VR pointers instead of mouse cursor
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIMouseGetLocalPositionWithinRectTransform))]
    [HarmonyPrefix]
    private static bool UIMouseGetLocalPositionWithinRectTransformPatch(RectTransform rectTransform,
        ref Vector2 __result)
    {
        if (XRRayInteractorManager.Instance is not { } manager)
            return true;

        var pointer = manager.GetUIHitPosition(rectTransform);
        var rect = SemiFunc.UIGetRectTransformPositionOnScreen(rectTransform, false);
        var pivotOff = new Vector2(
            rectTransform.rect.width * rectTransform.pivot.x, rectTransform.rect.height * rectTransform.pivot.y);

        __result = new Vector2(pointer.x - rect.x, pointer.y - rect.y) + pivotOff;

        return false;
    }

    /// <summary>
    /// Calculate component position on canvasses in local space since screen space canvasses are disabled
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIGetRectTransformPositionOnScreen))]
    [HarmonyPostfix]
    private static void UIGetRectTransformPositionOnCanvas(RectTransform rectTransform, ref Vector2 __result)
    {
        var canvas = rectTransform.GetComponentInParent<MenuPage>().transform;
        var local = canvas.InverseTransformPoint(rectTransform.position);

        local -= new Vector3(rectTransform.rect.width * rectTransform.pivot.x,
            rectTransform.rect.height * rectTransform.pivot.y, 0);

        __result = local;
    }

    /// <summary>
    /// Reset rotation on opened pages so it matches canvas rotation
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageOpen))]
    [HarmonyPostfix]
    private static void OnPageOpen(MenuPage __result)
    {
        __result.transform.localEulerAngles = Vector3.zero;
    }

    /// <summary>
    /// Fix the button hover outline position
    /// </summary>
    [HarmonyPatch(typeof(MenuSelectionBoxTop), nameof(MenuSelectionBoxTop.Update))]
    [HarmonyPostfix]
    private static void FixButtonOverlayPosition(MenuSelectionBoxTop __instance)
    {
        if (!MenuManager.instance.activeSelectionBox)
            return;

        var currentScale = __instance.GetComponentInParent<Canvas>().transform.localScale;
        var targetScale = MenuManager.instance.activeSelectionBox.GetComponentInParent<Canvas>().transform.localScale;
        var scaleFactor = new Vector3(
            currentScale.x != 0f ? targetScale.x / currentScale.x : 0f,
            currentScale.y != 0f ? targetScale.y / currentScale.y : 0f,
            currentScale.z != 0f ? targetScale.z / currentScale.z : 0f
        );

        __instance.rectTransform.position = MenuManager.instance.activeSelectionBox.rectTransform.position;
        __instance.rectTransform.rotation = MenuManager.instance.activeSelectionBox.rectTransform.rotation;
        __instance.rectTransform.parent.localScale = scaleFactor;
    }

    /// <summary>
    /// Handle VR inputs for scroll boxes
    /// </summary>
    [HarmonyPatch(typeof(MenuScrollBox), nameof(MenuScrollBox.Update))]
    [HarmonyPostfix]
    private static void HandleVRScrollLogic(MenuScrollBox __instance)
    {
        var manager = XRRayInteractorManager.Instance;

        if (!__instance.scrollBar.activeSelf || !__instance.scrollBoxActive || manager == null)
            return;

        if (manager.GetTriggerButton() && SemiFunc.UIMouseHover(__instance.parentPage, __instance.scrollBarBackground,
                __instance.menuSelectableElement.menuID))
        {
            var pos = SemiFunc.UIMouseGetLocalPositionWithinRectTransform(__instance.scrollBarBackground).y;

            if (pos < __instance.scrollHandle.sizeDelta.y / 2)
                pos = __instance.scrollHandle.sizeDelta.y / 2;

            if (pos > __instance.scrollBarBackground.rect.height - __instance.scrollHandle.sizeDelta.y / 2)
                pos = __instance.scrollBarBackground.rect.height - __instance.scrollHandle.sizeDelta.y / 2;

            __instance.scrollHandleTargetPosition = pos;
        }

        if (manager.GetUIScrollY() != 0 && SemiFunc.NoTextInputsActive())
        {
            __instance.scrollHandleTargetPosition += manager.GetUIScrollY() * 20 / (__instance.scrollHeight * 0.01f);

            if (__instance.scrollHandleTargetPosition < __instance.scrollHandle.sizeDelta.y / 2f)
                __instance.scrollHandleTargetPosition = __instance.scrollHandle.sizeDelta.y / 2f;
            if (__instance.scrollHandleTargetPosition >
                __instance.scrollBarBackground.rect.height - __instance.scrollHandle.sizeDelta.y / 2f)
                __instance.scrollHandleTargetPosition = __instance.scrollBarBackground.rect.height -
                                                        __instance.scrollHandle.sizeDelta.y / 2f;
        }
    }

    /// <summary>
    /// Handle VR button presses
    /// </summary>
    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonDown))]
    [HarmonyPrefix]
    private static bool MouseButtonDownVR(int button, ref bool __result)
    {
        var manager = XRRayInteractorManager.Instance;

        if (button != 0 || manager is null)
            return true;

        __result = manager.GetTriggerDown();

        return false;
    }

    /// <summary>
    /// Handle VR button holds
    /// </summary>
    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButton))]
    [HarmonyPrefix]
    private static bool MouseButtonHoldVR(int button, ref bool __result)
    {
        var manager = XRRayInteractorManager.Instance;

        if (button != 0 || manager is null)
            return true;

        __result = manager.GetTriggerButton();

        return false;
    }

    /// <summary>
    /// Disable scrolling using the built-in keybinds (Movement and Scroll), in favor of XR UI Scroll
    /// </summary>
    [HarmonyPatch(typeof(MenuScrollBox), nameof(MenuScrollBox.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ScrollDisableInputs(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputMovementY))))
            .Set(OpCodes.Ldc_R4, 0.0f)
            .MatchForward(false, new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.InputScrollY))))
            .Set(OpCodes.Ldc_R4, 0.0f)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Detect when the controls settings page is opened
    /// </summary>
    [HarmonyPatch(typeof(MenuPageSettingsControls), nameof(MenuPageSettingsControls.Start))]
    [HarmonyPostfix]
    private static void OnControlsPageOpened(MenuPageSettingsControls __instance)
    {
        __instance.gameObject.AddComponent<RebindManager>();
    }

    /// <summary>
    /// Detect if a save menu element is pressed
    /// </summary>
    [HarmonyPatch(typeof(MenuElementSaveFile), nameof(MenuElementSaveFile.Update))]
    [HarmonyPostfix]
    private static void OnSaveFilePressed(MenuElementSaveFile __instance)
    {
        var manager = XRRayInteractorManager.Instance;

        if (!__instance.menuElementHover.isHovering || manager == null || SemiFunc.InputDown(InputKey.Confirm) ||
            SemiFunc.InputDown(InputKey.Grab))
            return;

        if (!manager.GetTriggerDown())
            return;

        MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Confirm);
        __instance.parentPageSaves.SaveFileSelected(__instance.saveFileName, __instance.saveFileBackups);
    }

    /// <summary>
    /// Also hide the custom camera tumble UI (if used)
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIHideTumble))]
    [HarmonyPostfix]
    private static void HideCustomCameraTumble()
    {
        CustomTumbleUI.instance?.Hide();
    }

    /// <summary>
    /// Fix rotation issue with loading graphics
    /// </summary>
    [HarmonyPatch(typeof(MenuLoadingGraphics), nameof(MenuLoadingGraphics.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MenuGraphicsRotationFix(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertySetter(typeof(Transform), nameof(Transform.eulerAngles))))
            .SetOperandAndAdvance(PropertySetter(typeof(Transform), nameof(Transform.localEulerAngles)))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Fixes region element positioning since in VR the UI is scaled down
    /// </summary>
    [HarmonyPatch(typeof(MenuPageRegions), nameof(MenuPageRegions.GetRegions), MethodType.Enumerator)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FixRegionUIPositioning(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 32f))
            .SetOperandAndAdvance(1f)
            .InstructionEnumeration();
    }

    /// <summary>
    /// Disable the raycast target on the raw image of the menu selection box
    /// </summary>
    [HarmonyPatch(typeof(MenuSelectionBoxTop), nameof(MenuSelectionBoxTop.Start))]
    [HarmonyPostfix]
    private static void DisableRaycastSelectionBox(MenuSelectionBoxTop __instance)
    {
        __instance.GetComponentInChildren<RawImage>().raycastTarget = false;
    }
}