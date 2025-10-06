using System.Collections.Generic;
using RepoXR.Assets;
using RepoXR.UI.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Input;

public class RebindManager : MonoBehaviour
{    
    /// <summary>
    /// Due to many VR controllers also reporting "touched" state, we have to disable them as they interfere with
    /// "pressed" bindings (some touched bindings are allowed, since they don't have a corresponding "pressed" binding)
    /// </summary>
    private readonly string[] DISALLOWED_BINDINGS =
    [
        "<WMRSpatialController>/touchpadTouched",
        "<OculusTouchController>/primaryTouched",
        "<OculusTouchController>/secondaryTouched",
        "<OculusTouchController>/triggerTouched",
        "<OculusTouchController>/thumbstickTouched",
        "<ViveController>/trackpadTouched",
        "<ValveIndexController>/systemTouched",
        "<ValveIndexController>/primaryTouched",
        "<ValveIndexController>/secondaryTouched",
        "<ValveIndexController>/gripForce",
        "<ValveIndexController>/triggerTouched",
        "<ValveIndexController>/thumbstickTouched",
        "<ValveIndexController>/trackpadTouched",
        "<ValveIndexController>/trackpadForce",
        "<QuestProTouchController>/primaryTouched",
        "<QuestProTouchController>/secondaryTouched",
        "<QuestProTouchController>/triggerTouched",
        "<QuestProTouchController>/thumbstickTouched",
        "<QuestProTouchController>/triggerCurl",
        "<QuestProTouchController>/triggerSlide",
        "<QuestProTouchController>/triggerProximity",
        "<QuestProTouchController>/thumbProximity",
        "*/isTracked",
    ];   
    
    public static RebindManager Instance { get; private set; }

    private PlayerInput playerInput;
    private InputActionRebindingExtensions.RebindingOperation? currentOperation;
    private ControlOption currentOption;
    private readonly List<ControlOption> options = [];

    private float lastRebindTime;
    
    private void Awake()
    {
        Instance = this;
        playerInput = VRInputSystem.Instance.GetPlayerInput();
        
        DestroyOldUI();
        CreateUI();

        playerInput.onControlsChanged += OnControlsChanged;
        
        ReloadBindings();
    }

    private void OnDestroy()
    {
        playerInput.onControlsChanged -= OnControlsChanged;
    }

    /// <summary>
    /// If the current control scheme changes (somehow), reload the bindings for the new scheme
    /// </summary>
    private void OnControlsChanged(PlayerInput input)
    {
        Logger.LogDebug($"New control scheme: {input.currentControlScheme}");
        // TODO: Change some text somewhere
        
        ReloadBindings();
    }

    /// <summary>
    /// Destroy all built-in rebind buttons
    /// </summary>
    private void DestroyOldUI()
    {
        var container = transform.Find("Scroll Box/Mask/Scroller");
        var objectsToDestroy = new List<GameObject>();
        
        for (var i = 3; i < container.childCount; i++)
            objectsToDestroy.Add(container.GetChild(i).gameObject);
        
        // Need to destroy immediately because the scroll box updates the height this frame
        objectsToDestroy.ForEach(DestroyImmediate);
    }

    /// <summary>
    /// Create new rebind buttons
    /// </summary>
    private void CreateUI()
    {        
        var container = transform.Find("Scroll Box/Mask/Scroller");
        var yPos = -40;
        
        foreach (var control in AssetCollection.RemappableControls.controls)
        {
            if (!string.IsNullOrEmpty(control.headerName))
            {
                yPos -= 10;

                var header = Instantiate(AssetCollection.RebindHeader, container).GetComponent<RectTransform>();
                header.localPosition = new Vector3(58.8f, yPos, 0);
                header.GetComponentInChildren<TextMeshProUGUI>().text = control.headerName;

                yPos -= 58;
            }

            var bindingComponent =
                Instantiate(control.toggleable ? AssetCollection.RebindButtonToggle : AssetCollection.RebindButton,
                    container).GetComponent<RectTransform>();
            var bindingOption = bindingComponent.GetComponent<ControlOption>();

            bindingComponent.localPosition = new Vector3(0, yPos, 0);
            bindingOption.Setup(this, control);
            
            options.Add(bindingOption);

            yPos -= control.toggleable ? 65 : 42;
        }
    }

    /// <summary>
    /// Initiate the interactive rebinding of a control binding
    /// </summary>
    public void StartRebind(ControlOption option, int bindingIndex)
    {        
        // Prevent accidentally re-triggering the rebind process
        if (Time.realtimeSinceStartup - lastRebindTime < 0.5f)
            return;
        
        // Don't allow rebinding until a controller scheme is known
        if (string.IsNullOrEmpty(playerInput.currentControlScheme))
            return;

        if (currentOperation != null)
        {
            currentOperation.Dispose();
            if (currentOption != null)
                currentOption.ReloadBinding();
        }
        
        option.StartRebindTimer();
        
        playerInput.DeactivateInput();
        currentOption = option;
        currentOperation = option.action.PerformInteractiveRebinding(bindingIndex)
            .OnMatchWaitForAnother(0.1f).WithControlsHavingToMatchPath("<XRController>").WithTimeout(5)
            .OnComplete(_ => CompleteRebind(option)).OnCancel(_ => CompleteRebind(option));

        foreach (var exclude in DISALLOWED_BINDINGS)
            currentOperation.WithControlsExcluding(exclude);

        currentOperation.Start();
    }

    /// <summary>
    /// Save the current binding overrides to the configuration
    /// </summary>
    public void SaveBindings()
    {
        Plugin.Config.ControllerBindingsOverride.Value = playerInput.actions.SaveBindingOverridesAsJson();
    }

    /// <summary>
    /// Finalize the rebinding operation, and update the binding configuration
    /// </summary>
    private void CompleteRebind(ControlOption option)
    {
        currentOperation?.Dispose();
        currentOperation = null;
        
        playerInput.ActivateInput();
        
        option.ReloadBinding();
        SaveBindings();

        lastRebindTime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// Reset all controls back to their default values
    /// </summary>
    public void ResetControls()
    {
        playerInput.actions.RemoveAllBindingOverrides();
        
        SaveBindings();
        ReloadBindings();
    }
    
    private void ReloadBindings()
    {
        foreach (var option in options)
            option.ReloadBinding();
        
        // Reload chat binding in the lobby menu
        if (MenuPageLobby.instance)
            MenuPageLobby.instance.UpdateChatPrompt();
    }
}