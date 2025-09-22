using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Managers;

public class HotswapManager : MonoBehaviour
{
    private readonly InputAction swapAction = new(binding: "<Keyboard>/F8");
    
    private void Awake()
    {
        swapAction.performed += SwapActionPerformed;
    }

    private void OnDestroy()
    {
        swapAction.performed -= SwapActionPerformed;
    }

    private static void SwapActionPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (VRSession.InVR)
            HotswapDisableVR();
        else
            HotswapEnableVR();
    }

    private static void HotswapDisableVR()
    {
        Plugin.ToggleVR();
        
        GameDirector.instance.OutroStart(); // Reload scene
    }

    private static void HotswapEnableVR()
    {
        Plugin.ToggleVR();
        
        if (VRSession.InVR)
            GameDirector.instance.OutroStart(); // Reload scene
        else
            UniversalEntrypoint.ShowVRFailedWarning(true);
    }
}