using System.Collections;
using RepoXR.Input;
using RepoXR.Player.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RepoXR.UI;

public class MainMenu : MonoBehaviour
{
    private Camera mainCamera;
    private Canvas mainCanvas;

    private void Awake()
    {
        Actions.Instance["ResetHeight"].performed += OnResetHeight;

        if (RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu)
            StartCoroutine(LobbyLinkCopy());
    }

    private void OnDestroy()
    {
        Actions.Instance["ResetHeight"].performed -= OnResetHeight;
    }
    
    private IEnumerator Start()
    {
        yield return null;
        
        DisableEventSystem();
        SetupMainCamera();
        SetupMainCanvas();
        SetupControllers();
    }
    
    private static void DisableEventSystem()
    {
        var input = GameObject.Find("EventSystem")?.GetComponent<InputSystemUIInputModule>();
        if (input != null)
            input.enabled = false;
    }
    
    private void SetupMainCamera()
    {
        // Camera rendering setup
        mainCamera = CameraUtils.Instance.MainCamera;

        var topCamera = mainCamera.transform.Find("Camera Top").GetComponent<Camera>();
        topCamera.depth = 1;
        topCamera.targetTexture = null;
    }

    private void SetupMainCanvas()
    {
        mainCanvas = HUDCanvas.instance.GetComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.WorldSpace;
        mainCanvas.transform.position = new Vector3(-45, -0.75f, 6);
        mainCanvas.transform.eulerAngles = new Vector3(0, 50, 0);
        mainCanvas.transform.localScale = Vector3.one * 0.03f;
        mainCanvas.transform.Find("HUD").gameObject.AddComponent<RectMask2D>();
        
        Destroy(mainCanvas.GetComponent<GraphicRaycaster>());
        mainCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        
        // Remove game HUD elements
        mainCanvas.transform.Find("HUD/Game Hud").gameObject.SetActive(false);
        mainCanvas.transform.Find("HUD/Chat Local").gameObject.SetActive(false);
        
        // Set up chat
        if (RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu)
        {
            var chat = mainCanvas.transform.Find("HUD/Chat").GetComponent<RectTransform>();
            chat.SetParent(mainCanvas.transform.Find("HUD"), false);
        }
        else
            mainCanvas.transform.Find("HUD/Chat").gameObject.SetActive(false);

        // Move top menu selection outline
        var selection = FindObjectOfType<MenuSelectionBoxTop>(true);
        selection.transform.parent.parent = selection.transform.parent.parent.parent;
    }

    private void SetupControllers()
    {
        mainCamera.transform.parent.gameObject.AddComponent<XRRayInteractorManager>();
    }

    private static IEnumerator LobbyLinkCopy()
    {
        while (true)
        {
            yield return null;
            
            if ((!UnityEngine.Input.GetKey(KeyCode.LeftControl) && !UnityEngine.Input.GetKey(KeyCode.RightControl)) ||
                !UnityEngine.Input.GetKeyDown(KeyCode.C)) 
                continue;
            
            var lobby = SteamManager.instance.currentLobby;
            var link = $"steam://joinlobby/3241660/{lobby.Id}/{lobby.Owner.Id}";

            GUIUtility.systemCopyBuffer = link;
            
            Logger.LogInfo($"Copied lobby link: {link}");
        }
        
        // ReSharper disable once IteratorNeverReturns
    }
    
    private static void OnResetHeight(InputAction.CallbackContext obj)
    {
        VRCameraAim.instance.SetPlayerAim(0, true);
    }
}