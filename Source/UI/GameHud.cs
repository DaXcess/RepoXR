using System.Collections;
using System.Linq;
using RepoXR.Assets;
using RepoXR.Managers;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace RepoXR.UI;

public class GameHud : MonoBehaviour
{
    public Canvas OverlayCanvas { get; private set; }
    public Canvas SmoothedCanvas { get; private set; }

    private Transform smoothCanvasContainer;
    private new Transform camera;

    private PauseUI pause;

    private IEnumerator Start()
    {
        // Make sure all other Start() functions have been called
        yield return null;

        camera = CameraOverlay.instance.overlayCamera.transform;

        DisableEventSystem();
        SetupOverlayCanvas();
        SetupSmoothedCanvas();
        SetupPauseMenu();
        SetupInfoHUD();

        // Move the world space hud far away
        HUDCanvas.instance.transform.position = Vector3.down * 10000;
    }

    private void LateUpdate()
    {
        if (!camera) // Start has not yet finished
            return;

        // Keep the UI upright even during the short post-death "view from above" state
        var up = SpectateCamera.instance && SpectateCamera.instance.CheckState(SpectateCamera.State.Death)
            ? SpectateCamera.instance.transform.up
            : Vector3.up;

        var fwd = camera.position + Vector3.down * 0.15f + camera.forward * Plugin.Config.SmoothCanvasDistance.Value;
        var rot = Quaternion.LookRotation(camera.forward, up);

        SmoothedCanvas.transform.position = Vector3.Slerp(SmoothedCanvas.transform.position, fwd, 0.1f);
        SmoothedCanvas.transform.rotation = Quaternion.Slerp(SmoothedCanvas.transform.rotation, rot, 0.1f);
    }

    public void PauseGame()
    {
        pause.Show();
    }

    public void ResumeGame(bool instant = false)
    {
        pause.Hide(instant);
    }
    
    private static void DisableEventSystem()
    {
        var input = GameObject.Find("EventSystem")?.GetComponent<InputSystemUIInputModule>();
        if (input != null)
            input.enabled = false;
    }
    
    /// <summary>
    /// Creates the main overlay canvas, which is rendered in camera space
    /// </summary>
    private void SetupOverlayCanvas()
    {
        OverlayCanvas = GameObject.Find("VR Overlay Canvas").GetComponent<Canvas>();
        OverlayCanvas.worldCamera = CameraOverlay.instance.overlayCamera;
        OverlayCanvas.renderMode = RenderMode.ScreenSpaceCamera;

        // World space scale
        WorldSpaceUIParent.instance.transform.localScale = Vector3.one * 0.005f;

        // Put tumble UI on the overlay canvas, and replace the tumble lines with custom ones made to work better in VR
        var tumbleUi = TumbleUI.instance;
        var tumbleRect = tumbleUi.GetComponent<RectTransform>();

        tumbleRect.transform.parent = OverlayCanvas.transform;
        
        tumbleRect.anchorMin = Vector2.zero;
        tumbleRect.anchorMax = Vector2.one;
        tumbleRect.anchoredPosition = Vector2.zero;
        tumbleRect.sizeDelta = Vector2.zero;
        
        tumbleRect.transform.localPosition = Vector3.down * 130;
        tumbleRect.transform.localRotation = Quaternion.identity;
        
        foreach (Transform child in tumbleRect)
            Destroy(child.gameObject);

        var customTumbleUi = Instantiate(AssetCollection.VRTumble, tumbleRect).transform;

        tumbleUi.parts1[0] = customTumbleUi.Find("Top 1").gameObject;
        tumbleUi.parts1[1] = customTumbleUi.Find("Bottom 1").gameObject;
        tumbleUi.parts1[2] = customTumbleUi.Find("Left 1").gameObject;
        tumbleUi.parts1[3] = customTumbleUi.Find("Right 1").gameObject;
        tumbleUi.parts2[0] = customTumbleUi.Find("Top 2").gameObject;
        tumbleUi.parts2[1] = customTumbleUi.Find("Bottom 2").gameObject;
        tumbleUi.parts2[2] = customTumbleUi.Find("Left 2").gameObject;
        tumbleUi.parts2[3] = customTumbleUi.Find("Right 2").gameObject;

        tumbleUi.images1 = tumbleUi.parts1.Select(part => part.GetComponent<Image>()).ToArray();
        tumbleUi.images2 = tumbleUi.parts2.Select(part => part.GetComponent<Image>()).ToArray();
    }

    /// <summary>
    /// Creates the canvas that is displayed in front of the eyes, but far enough away so it can be smoothed
    /// </summary>
    private void SetupSmoothedCanvas()
    {
        smoothCanvasContainer = new GameObject("VR Smoothed Canvas - Container")
            { layer = 5, transform = { parent = camera.parent.parent.parent } }.transform;
        SmoothedCanvas = new GameObject("VR Smoothed Canvas")
            {
                layer = 5,
                transform =
                {
                    parent = smoothCanvasContainer,
                    localScale = Vector3.one * 0.003f,
                    localPosition = Vector3.zero,
                    localEulerAngles = Vector3.zero
                }
            }
            .AddComponent<Canvas>();
        SmoothedCanvas.renderMode = RenderMode.WorldSpace;
        SmoothedCanvas.gameObject.AddComponent<RectMask2D>();

        var rect = SmoothedCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(712, 400);

        // Dump all the game hud onto this smoothed canvas
        var gameHud = HUDCanvas.instance.transform.Find("HUD/Game Hud");
        var chatLocal = HUDCanvas.instance.transform.Find("HUD/Chat Local");
        var debugConsole = GameObject.Find("UI/UI/Canvas/DebugConsole")?.transform;

        gameHud.SetParent(rect, false);
        chatLocal.SetParent(rect, false);
        debugConsole?.SetParent(rect, false);
    }

    /// <summary>
    /// Create and set up the pause menu canvas
    /// </summary>
    private void SetupPauseMenu()
    {
        var canvas = new GameObject("VR Pause Menu / Chat")
        {
            transform =
            {
                parent = camera.transform.parent.parent, 
                localPosition = Vector3.down * 3000, // Move very far away initially to prevent UI flashes during loading
                localRotation = Quaternion.identity,
                localScale = Vector3.one * 0.01f
            },
            layer = 5
        }.AddComponent<Canvas>();
        var menu = MenuHolder.instance.transform;
        var rect = menu.GetComponent<RectTransform>();

        canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;

        menu.SetParent(canvas.transform, false);
        menu.localPosition = Vector3.zero;
        menu.localRotation = Quaternion.identity;
        menu.localScale = Vector3.one;
        menu.gameObject.AddComponent<RectMask2D>();

        var pixelOffset = -(rect.sizeDelta * 0.5f) + new Vector2(50, 0);
        
        pause = canvas.gameObject.AddComponent<PauseUI>();
        pause.positionOffset = new Vector3(pixelOffset.x * 0.01f, pixelOffset.y * 0.01f, 0);
        
        // Put chat on the pause canvas (or disable if singleplayer)

        var chatHolder = new GameObject("Chat Holder") { transform = { parent = canvas.transform } }
            .AddComponent<RectTransform>();

        chatHolder.sizeDelta = new Vector2(720, 450);
        chatHolder.pivot = Vector2.zero;
        chatHolder.localPosition = Vector3.zero;
        chatHolder.localRotation = Quaternion.identity;
        chatHolder.localScale = Vector3.one;
        chatHolder.gameObject.AddComponent<RectMask2D>();
        
        var chat = HUDCanvas.instance.transform.Find("HUD/Chat");
        
        if (SemiFunc.IsMultiplayer())
            chat.SetParent(chatHolder, false);
        else
            chat.gameObject.SetActive(false);
    }

    /// <summary>
    /// Move a bunch of UI elements to the rig's info HUD
    /// </summary>
    private void SetupInfoHUD()
    {
        var goal = GoalUI.instance.GetComponent<SemiUI>();
        var haul = HaulUI.instance.GetComponent<SemiUI>();
        var health = HealthUI.instance.GetComponent<SemiUI>();
        var energy = EnergyUI.instance.GetComponent<SemiUI>();
        var upgrades = StatsUI.instance.GetComponent<SemiUI>();

        goal.transform.SetParent(VRSession.Instance.Player.Rig.infoHud, false);
        haul.transform.SetParent(VRSession.Instance.Player.Rig.infoHud, false);
        health.transform.SetParent(VRSession.Instance.Player.Rig.infoHud, false);
        energy.transform.SetParent(VRSession.Instance.Player.Rig.infoHud, false);
        upgrades.transform.SetParent(VRSession.Instance.Player.Rig.infoHud, false);
        
        goal.DisableScanlines();
        haul.DisableScanlines();
        health.DisableScanlines();
        energy.DisableScanlines();
        upgrades.DisableScanlines();
        
        goal.SetUIAnchoredPosition(new Vector2(0, -30));
        haul.SetUIAnchoredPosition(Vector2.zero);
        health.SetUIAnchoredPosition(new Vector2(30, 0));
        energy.SetUIAnchoredPosition(new Vector2(30, -30));
        upgrades.SetUIAnchoredPosition(new Vector2(50, 50));
    }
}