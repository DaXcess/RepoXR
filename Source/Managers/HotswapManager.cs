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
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PagePopUp("VR Startup Failed", Color.red,
                "RepoXR tried to launch the game in VR, however an error occured during initialization.\n\nYou can disable VR in the settings if you are not planning to play in VR.",
                "Alright fam",
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