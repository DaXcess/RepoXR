using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RepoXR.Managers;

public class HotswapManager : MonoBehaviour
{
    private readonly InputAction swapAction = new(binding: "<Keyboard>/F8");
    
    private void Awake()
    {
        swapAction.performed += SwapActionPerformed;
        swapAction.Enable();
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

        RestartScene();
    }

    private static void HotswapEnableVR()
    {
        Plugin.ToggleVR();

        if (VRSession.InVR)
            RestartScene();
        else
        {
            // Close existing popup if one is open
            if (MenuPagePopUp.instance != null)
                MenuPagePopUp.instance.ButtonEvent();

            MenuManager.instance.PagePopUp("VR Startup Failed", Color.red,
                "RepoXR tried to swap the game to VR, however an error occured during initialization.\n\nYou can update your settings and press F8 to try again.",
                "Darn it",
                true);
        }
    }

    private static void RestartScene()
    {
        if (SemiFunc.IsMultiplayer() && !PhotonNetwork.IsMasterClient)
            // RestartScene is not allowed when not the host, so we just re-join the lobby
            SceneManager.LoadSceneAsync("LobbyJoin");
        else
            RunManager.instance.RestartScene();
    }
}