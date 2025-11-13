using System.Collections;
using RepoXR.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.UI.Controls;

public class ControlOption : MonoBehaviour
{
    [SerializeField] protected TextMeshProUGUI controlName;
    [SerializeField] protected TextMeshProUGUI controlText;

    public InputAction? action => control?.currentInput.action;

    private PlayerInput playerInput;
    private RebindManager manager;
    private RemappableControl control;
    
    private Coroutine? rebindTimerCoroutine;
    private int bindingIndex;

    private void Awake()
    {
        playerInput = VRInputSystem.Instance.GetPlayerInput();
    }

    public void StartRebind()
    {
        manager.StartRebind(this, bindingIndex);
    }

    public void DeleteBinding()
    {
        // Don't allow to unbind while no controller scheme is known
        if (string.IsNullOrEmpty(playerInput.currentControlScheme))
            return;

        action.ApplyBindingOverride(bindingIndex, "");

        ReloadBinding();
        manager.SaveBindings();
    }

    public void SetBindToggle(bool toggle)
    {
        VRInputSystem.Instance.InputToggleRebind(action!.name, toggle);
    }

    public void FetchToggle()
    {
        if (action == null)
            return;
        
        var menuTwoOptions = GetComponentInChildren<MenuTwoOptions>();
        menuTwoOptions.startSettingFetch = VRInputSystem.Instance.InputToggleGet(action.name);
    }

    public void Setup(RebindManager rebindManager, RemappableControl remappableControl)
    {
        manager = rebindManager;
        control = remappableControl;

        controlName.text = control.controlName;
    }

    public void ReloadBinding()
    {
        if (rebindTimerCoroutine != null)
            StopCoroutine(rebindTimerCoroutine);

        bindingIndex = Mathf.Max(control.bindingIndex, 0) +
                       Mathf.Max(
                           control.currentInput.action.GetBindingIndex(playerInput.currentControlScheme), 0);
        
        if (string.IsNullOrEmpty(playerInput.currentControlScheme))
        {
            controlText.text = "<sprite=37> DISCONNECTED";

            return;
        }

        var binding = control.currentInput.action.bindings[bindingIndex].effectivePath;

        if (string.IsNullOrEmpty(binding))
        {
            controlText.text = "NOT BOUND";

            return;
        }

        controlText.text = Utils.GetControlSpriteString(binding);
    }

    public void StartRebindTimer()
    {
        rebindTimerCoroutine = StartCoroutine(RebindTimer());
    }

    private IEnumerator RebindTimer()
    {
        for (var i = 5; i >= 0; i--)
        {
            controlText.text = $"[ .{i}. ]";

            yield return new WaitForSeconds(0.5f);

            controlText.text = $"[. {i} .]";

            yield return new WaitForSeconds(0.5f);
        }
    }
}