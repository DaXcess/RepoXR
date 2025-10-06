using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using RepoXR.Assets;
using RepoXR.UI.Expressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Input;

public class VRInputSystem : MonoBehaviour
{
    public static VRInputSystem Instance => _instance ?? InputManager.instance.gameObject.AddComponent<VRInputSystem>();
    private static VRInputSystem? _instance;

    private PlayerInput playerInput;

    public InputActionAsset Actions => playerInput.actions;
    public string CurrentControlScheme => playerInput.currentControlScheme;

    private Dictionary<string, bool> inputToggle = [];
    
    private void Awake()
    {
        _instance = this;
        
        playerInput = gameObject.AddComponent<PlayerInput>();
        playerInput.actions = AssetCollection.VRInputs;
        playerInput.defaultActionMap = "VR Actions";
        playerInput.defaultControlScheme = "Oculus";
        playerInput.neverAutoSwitchControlSchemes = false;
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        
        playerInput.actions.LoadBindingOverridesFromJson(Plugin.Config.ControllerBindingsOverride.Value);
        playerInput.ActivateInput();

        inputToggle =
            JsonConvert.DeserializeObject<Dictionary<string, bool>>(Plugin.Config.InputToggleBindings.Value) ?? [];
    }

    private void OnEnable()
    {
        playerInput.actions["Chat"].performed += ChatPerformed;
        playerInput.actions["Chat"].canceled += ChatCanceled;
    }

    private void OnDisable()
    {
        playerInput.actions["Chat"].performed -= ChatPerformed;
        playerInput.actions["Chat"].canceled -= ChatCanceled;
    }

    // Special chat input section

    private Coroutine? chatHoldCoroutine;
    private bool chatHoldTriggered;
    private int chatChoice;    

    private void ChatPerformed(InputAction.CallbackContext context)
    {
        // Disable chat when expression radial is open
        if (ExpressionRadial.instance &&
            (ExpressionRadial.instance.isActive || ExpressionRadial.instance.closedLastPress))
            return;

        // Disable chat when pause menu is open
        if (!SemiFunc.MenuLevel() && MenuManager.instance.currentMenuPage)
            return;
        
        chatHoldTriggered = false;
        chatHoldCoroutine = StartCoroutine(HoldTimer());
    }

    private void ChatCanceled(InputAction.CallbackContext context)
    {
        // Disable chat when expression radial is open
        if (ExpressionRadial.instance &&
            (ExpressionRadial.instance.isActive || ExpressionRadial.instance.closedLastPress))
        {
            ExpressionRadial.instance.closedLastPress = false;
            return;
        }
        
        // Disable chat when pause menu is open
        if (!SemiFunc.MenuLevel() && MenuManager.instance.currentMenuPage)
            return;
        
        if (chatHoldCoroutine != null)
        {
            StopCoroutine(chatHoldCoroutine);
            chatHoldCoroutine = null;
        }

        if (chatHoldTriggered)
        {
            chatHoldTriggered = false;
            return;
        }

        chatChoice = 1;
    }

    private IEnumerator HoldTimer()
    {
        yield return new WaitForSeconds(0.3f);

        if (!playerInput.actions["Chat"].IsPressed())
            yield break;

        chatHoldTriggered = true;
        chatChoice = 2;
    }

    public bool ChatPressed()
    {
        if (chatChoice != 1)
            return false;

        chatChoice = 0;
        return true;
    }

    public bool ExpressionPressed()
    {
        if (chatChoice != 2)
            return false;

        chatChoice = 0;
        return true;
    }
    
    // Other input system related methods

    public void ActivateInput()
    {
        playerInput.ActivateInput();
    }

    public void DeactivateInput()
    {
        playerInput.DeactivateInput();
    }

    public PlayerInput GetPlayerInput()
    {
        return playerInput;
    }
    
    public void InputToggleRebind(string inputAction, bool toggle)
    {
        inputToggle[inputAction] = toggle;
        
        SaveInputToggles();
    }

    public bool InputToggleGet(string action)
    {
        if (inputToggle.TryGetValue(action, out var value))
            return value;
        
        // Check for default
        foreach (var control in AssetCollection.RemappableControls.controls)
            if (control.currentInput.action.name == action)
            {
                inputToggle[action] = control.defaultToggle;

                return control.defaultToggle;
            }

        // If all else fails: default to hold
        return false;
    }

    private void SaveInputToggles()
    {
        Plugin.Config.InputToggleBindings.Value = JsonConvert.SerializeObject(inputToggle);
    }
}