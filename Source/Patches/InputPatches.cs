using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

[RepoXRPatch]
internal static class InputPatches
{
    /// <summary>
    /// Fuck you Unity, I want my tracking
    /// </summary>
    [HarmonyPatch(typeof(InputSettings), nameof(InputSettings.backgroundBehavior), MethodType.Setter)]
    [HarmonyPrefix]
    private static void AllowBackgroundTracking(ref InputSettings.BackgroundBehavior value)
    {
        value = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    /// <summary>
    /// Add additional mapping tags during <see cref="InputManager" /> startup
    /// </summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.Start))]
    [HarmonyPostfix]
    private static void OnInputManagerStart(InputManager __instance)
    {
        var offset = Enum.GetNames(typeof(InputKey)).Length;

        for (var i = 0; i < AssetCollection.RemappableControls.additionalBindings.Length; i++)
        {
            var binding = AssetCollection.RemappableControls.additionalBindings[i];

            __instance.tagDictionary.Add($"[{binding.action.name}]", (InputKey)(i + offset));
        }
    }

    /// <summary>
    /// Create a custom <see cref="VRInputSystem"/> component on the <see cref="InputManager"/>, allowing the use of <see cref="InputActionAsset"/>s
    /// </summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.InitializeInputs))]
    [HarmonyPostfix]
    private static void OnInitializeInputManager(InputManager __instance)
    {
        new GameObject("VR Tracking Input").AddComponent<TrackingInput>();
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetAction))]
    [HarmonyPrefix]
    private static bool GetAction(ref InputKey key, ref InputAction __result)
    {
        var bindings = Enum.GetNames(typeof(InputKey)).Length;

        try
        {
            __result = (int)key >= bindings
                ? AssetCollection.RemappableControls.additionalBindings[(int)key - bindings]
                : Actions.Instance[key.ToString()];

            return false;
        }
        catch
        {
            // If no key was found, fall back to vanilla keybind (likely won't work with VR controllers though)
            return true;
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovement))]
    [HarmonyPrefix]
    private static bool GetMovement(InputManager __instance, ref Vector2 __result)
    {
        if (__instance.disableMovementTimer > 0)
            return true;

        if (__instance.disableControlsExceptTimer > 0 && !__instance.disableControlsExceptList.Contains(InputKey.Movement))
            return true;

        __result = Actions.Instance["Movement"].ReadValue<Vector2>();

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovementAction))]
    [HarmonyPrefix]
    private static bool GetMovementAction(ref InputAction __result)
    {
        __result = Actions.Instance["Movement"];

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovementX))]
    [HarmonyPrefix]
    private static bool GetMovementX(InputManager __instance, ref float __result)
    {
        if (__instance.disableMovementTimer > 0)
            return true;

        if (__instance.disableControlsExceptTimer > 0 && !__instance.disableControlsExceptList.Contains(InputKey.Movement))
            return true;

        __result = Actions.Instance["Movement"].ReadValue<Vector2>().x;

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovementY))]
    [HarmonyPrefix]
    private static bool GetMovementY(InputManager __instance, ref float __result)
    {
        if (__instance.disableMovementTimer > 0)
            return true;

        if (__instance.disableControlsExceptTimer > 0 && !__instance.disableControlsExceptList.Contains(InputKey.Movement))
            return true;

        __result = Actions.Instance["Movement"].ReadValue<Vector2>().y;

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetScrollY))]
    [HarmonyPrefix]
    private static bool GetScrollY(InputManager __instance, ref float __result)
    {
        __result = Actions.Instance["Scroll"].ReadValue<float>();

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyDown))]
    [HarmonyPrefix]
    private static bool KeyDown(InputManager __instance, ref InputKey key, ref bool __result)
    {
        if (__instance.disableControlsExceptTimer > 0 && !__instance.disableControlsExceptList.Contains(key))
            return true;

        switch (key)
        {
            case InputKey.Jump or InputKey.Crouch or InputKey.Tumble or InputKey.Inventory1 or InputKey.Inventory2
                or InputKey.Inventory3 or InputKey.Interact when __instance.disableMovementTimer > 0:
                return true;

            // Do not allow pause menu during loading
            case InputKey.Menu when LoadingUI.instance.isActiveAndEnabled:

            // Do not allow to swap spectated player if chatting or in a menu
            case InputKey.SpectateNext or InputKey.SpectatePrevious
                when ChatManager.instance.chatActive || MenuManager.instance.currentMenuPage:
                __result = false;
                return false;

            default:
                __result = __instance.GetAction(key).WasPressedThisFrame();
                return false;
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyUp))]
    [HarmonyPrefix]
    private static bool KeyUp(InputManager __instance, ref InputKey key, ref bool __result)
    {
        if (key is InputKey.Jump or InputKey.Crouch or InputKey.Tumble && __instance.disableMovementTimer > 0)
            return true;

        if (key is InputKey.Push or InputKey.Pull)
            return true;

        if (__instance.disableControlsExceptTimer > 0 && !__instance.disableControlsExceptList.Contains(key))
            return true;

        __result = __instance.GetAction(key).WasReleasedThisFrame();

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyHold))]
    [HarmonyPrefix]
    private static bool KeyHold(InputManager __instance, ref InputKey key, ref bool __result)
    {
        if (key is InputKey.Jump or InputKey.Crouch or InputKey.Tumble && __instance.disableMovementTimer > 0)
            return true;

        if (__instance.disableControlsExceptTimer > 0 && !__instance.disableControlsExceptList.Contains(key))
            return true;

        __result = __instance.GetAction(key).IsPressed();

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyPullAndPush))]
    [HarmonyPrefix]
    private static bool KeyPullAndPush(ref float __result)
    {
        var push = Actions.Instance["Push"].ReadValue<float>();
        if (push > 0)
        {
            __result = push;
            return false;
        }

        var pull = Actions.Instance["Pull"].ReadValue<float>();
        if (pull > 0)
            __result = -pull;

        return false;
    }

    /// <summary>
    /// Retrieve a sprite name given an <see cref="InputKey"/> instead of a control name
    /// </summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.InputDisplayGet))]
    [HarmonyPrefix]
    private static bool InputDisplayGet(InputManager __instance, InputKey _inputKey, ref string __result)
    {
        var action = __instance.GetAction(_inputKey);
        if (action == null)
        {
            __result = "Unassigned";

            return false;
        }

        var index = action.GetBindingIndex(VRInputSystem.Instance.CurrentControlScheme);

        __result = __instance.InputDisplayGetString(action, index);

        return false;
    }

    /// <summary>
    /// Retrieve a sprite name given an <see cref="InputAction"/> and a binding index instead of a control name
    /// </summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.InputDisplayGetString))]
    [HarmonyPrefix]
    private static bool InputDisplayGetString(InputAction action, int bindingIndex, ref string __result)
    {
        var binding = action.bindings[bindingIndex].effectivePath;
        __result = Utils.GetControlSpriteString(binding);

        return false;
    }

    /// <summary>
    /// Use our own <see cref="VRInputSystem.InputToggleGet"/> since we might have other bindings not present in the base game
    /// </summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.InputToggleGet))]
    [HarmonyPrefix]
    private static bool InputToggleGet(ref InputKey key, ref bool __result)
    {
        __result = VRInputSystem.Instance.InputToggleGet(key.ToString());

        return false;
    }

    /// <summary>
    /// Prevent the addition of underlines in the input text
    /// </summary>
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.InputDisplayReplaceTags))]
    [HarmonyPrefix]
    private static bool NoUnderlinePatch(InputManager __instance, ref string __result, ref string _text)
    {
        _text = __instance.tagDictionary.Aggregate(_text,
            (current, keyValuePair) => current.Replace(keyValuePair.Key,
                __instance.InputDisplayGet(keyValuePair.Value, MenuKeybind.KeyType.InputKey, MovementDirection.Up)));

        __result = _text;

        return false;
    }

    /// <summary>
    /// Make the reset controls button reset only the VR controls, and keep flatscreen controls as-is
    /// </summary>
    [HarmonyPatch(typeof(MenuPageSettingsControls), nameof(MenuPageSettingsControls.ResetControls))]
    [HarmonyPrefix]
    private static bool ResetVRControls()
    {
        RebindManager.Instance.ResetControls();

        return false;
    }

    #region Mouse Input Patches

    [HarmonyPatch(typeof(MenuButton), nameof(MenuButton.HoverLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PatchHoverLogicMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        PatchMenuManagerMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuScrollBox), nameof(MenuScrollBox.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        PatchMenuScrollBoxMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuSlider), nameof(MenuSlider.PointerLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PatchMenuSliderPointerLogicMouseInputs(
        IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuSlider), nameof(MenuSlider.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PatchMenuSliderUpdateMouseInputs(
        IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuButtonArrow), nameof(MenuButtonArrow.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        PatchMenuButtonArrowMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuElementRegion), nameof(MenuElementRegion.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        PatchMenuElementRegionMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuElementSaveFile), nameof(MenuElementSaveFile.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PatchMenuElementSaveFile(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuElementServer), nameof(MenuElementServer.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        PatchMenuElementServerMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuElementSliderPlayerMicGainSteam), nameof(MenuElementSliderPlayerMicGainSteam.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        MenuElementSliderPlayerMicGainSteamMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    [HarmonyPatch(typeof(MenuPlayerListedSteam), nameof(MenuPlayerListedSteam.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        PatchMenuPlayerListedSteamMouseInputs(IEnumerable<CodeInstruction> instructions) =>
        new CodeMatcher(instructions).PatchMouseInputs().InstructionEnumeration();

    /// <summary>
    /// Many UI button on click events are handled by Input.GetMouseButton/Down, so having OnPointerClick fire will
    /// cause the button to be pressed twice. This patch prevents this event from being fired when not needed.
    /// </summary>
    [HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
    [HarmonyPrefix]
    private static bool PreventDoublePressPatch(Button __instance)
    {
        return !__instance.TryGetComponent<MenuButton>(out _);
    }

    private static bool GetMouseButtonVR(int button)
    {
        var manager = XRRayInteractorManager.Instance;

        if (button != 0 || manager is null)
            return UnityEngine.Input.GetMouseButton(button);

        return manager.GetTriggerButton();
    }

    private static bool GetMouseButtonDownVR(int button)
    {
        var manager = XRRayInteractorManager.Instance;

        if (button != 0 || manager is null)
            return UnityEngine.Input.GetMouseButtonDown(button);

        return manager.GetTriggerDown();
    }

    private static CodeMatcher PatchMouseInputs(this CodeMatcher matcher)
    {
        var matchButton = new CodeMatch(OpCodes.Call,
            Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButton)));
        var matchButtonDown = new CodeMatch(OpCodes.Call,
            Method(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonDown)));

        return matcher
            .MatchForward(false, matchButton)
            .Repeat(m => m.SetOperandAndAdvance(((Func<int, bool>)GetMouseButtonVR).Method))
            .Start()
            .MatchForward(false, matchButtonDown)
            .Repeat(m => m.SetOperandAndAdvance(((Func<int, bool>)GetMouseButtonDownVR).Method));
    }

    #endregion

    // TODO: Old Unity UI hooks have been removed, find patches for (if needed):
    //  - Button.OnPointerClick
    //  - Input.GetMouseButtonDown
    //  - Input.GetMouseButton
}