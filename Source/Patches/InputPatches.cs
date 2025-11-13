using System;
using System.Linq;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.ThirdParty.MRTK;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        __result = Actions.Instance["Movement"].ReadValue<Vector2>().x;

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovementY))]
    [HarmonyPrefix]
    private static bool GetMovementY(InputManager __instance, ref float __result)
    {
        if (__instance.disableMovementTimer > 0)
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

        __result = __instance.GetAction(key).WasReleasedThisFrame();

        return false;
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyHold))]
    [HarmonyPrefix]
    private static bool KeyHold(InputManager __instance, ref InputKey key, ref bool __result)
    {
        if (key is InputKey.Jump or InputKey.Crouch or InputKey.Tumble && __instance.disableMovementTimer > 0)
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

    /// <summary>
    /// Button events are handled manually (unless it's the keyboard), so we block the OnPointerClick unless the
    /// <see cref="NonNativeKeyboard"/> component is present
    /// </summary>
    [HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
    [HarmonyPrefix]
    private static bool DisablePointerClick(Button __instance)
    {
        return __instance.GetComponentInParent<NonNativeKeyboard>() || Compat.IsLoaded(Compat.UnityExplorer);
    }
}
