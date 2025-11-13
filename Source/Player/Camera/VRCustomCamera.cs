using System;
using RepoXR.Input;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace RepoXR.Player.Camera;

/// <summary>
/// Contrary to the name, this script actually deals with non-vr cameras (but they only exist if VR is enabled so lalalala)
/// </summary>
[DefaultExecutionOrder(100)]
public class VRCustomCamera : MonoBehaviour
{
    public static VRCustomCamera instance;

    private RenderTexture targetTexture;

    [SerializeField] protected UnityEngine.Camera mainCamera;
    [SerializeField] protected UnityEngine.Camera topCamera;
    [SerializeField] protected UnityEngine.Camera uiCamera;

    [SerializeField] protected Image overlayImage;

    private Transform gameplayCamera;

    private int lastWidth;
    private int lastHeight;

    private float frameTimer;

    private void Awake()
    {
        instance = this;

        var fov = Plugin.Config.CustomCameraFOV.Value;

        mainCamera.fieldOfView = fov;
        topCamera.fieldOfView = fov;
        uiCamera.fieldOfView = fov;

        gameplayCamera = UnityEngine.Camera.main!.transform;

        transform.localPosition = TrackingInput.Instance.HeadTransform.localPosition;
        transform.localRotation = TrackingInput.Instance.HeadTransform.localRotation;

        Plugin.Config.CustomCameraFOV.SettingChanged += OnFOVChanged;

        Application.onBeforeRender += OnBeforeRender;

        UpdateRenderTexture();
    }

    private void OnDestroy()
    {
        instance = null!;

        Plugin.Config.CustomCameraFOV.SettingChanged -= OnFOVChanged;

        Application.onBeforeRender -= OnBeforeRender;
    }

    private void OnBeforeRender()
    {
        transform.localPosition = gameplayCamera.localPosition;
    }

    private void OnFOVChanged(object sender, EventArgs e)
    {
        var fov = Plugin.Config.CustomCameraFOV.Value;

        mainCamera.fieldOfView = fov;
        topCamera.fieldOfView = fov;
        uiCamera.fieldOfView = fov;
    }

    private void Update()
    {
        var smoothing = Plugin.Config.CustomCameraSmoothing.Value;
        var strength = smoothing == 0 ? 1 / Time.deltaTime : Mathf.Lerp(15, 3, smoothing);

        transform.localPosition = gameplayCamera.localPosition;
        transform.localRotation =
            Quaternion.Slerp(transform.localRotation, gameplayCamera.localRotation, strength * Time.deltaTime);

        mainCamera.backgroundColor = RenderSettings.fogColor;

        // Some weird fog thing, I don't know why but this is needed
        RenderSettings.fogDensity =
            SemiFunc.MenuLevel() || SemiFunc.RunIsShop() || SemiFunc.RunIsLobby() ? 0.015f : 0.15f;
    }

    private void LateUpdate()
    {
        transform.localPosition = gameplayCamera.localPosition;

        // Since we override the FadeOverlay image color in a LateUpdate, we need to read it back in a late update as well
        // Also this script needs to execute *after* the override, hence the [DefaultExecutionOrder(100)]
        overlayImage.color = FadeOverlay.Instance.Image.color;

        if (lastWidth != Screen.width || lastHeight != Screen.height)
            UpdateRenderTexture();

        frameTimer += Time.unscaledDeltaTime;

        var interval = 1f / Plugin.Config.CustomCameraFramerate.Value;
        if (frameTimer < interval)
            return;

        frameTimer -= interval;

        mainCamera.Render();
        topCamera.Render();
        uiCamera.Render();
    }

    private void UpdateRenderTexture()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        if (targetTexture != null)
            targetTexture.Release();

        targetTexture = new RenderTexture(lastWidth, lastHeight, GraphicsFormat.R8G8B8A8_UNorm,
            GraphicsFormat.D32_SFloat_S8_UInt)
        {
            name = "Custom Camera RT",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
        };

        mainCamera.targetTexture = targetTexture;
        topCamera.targetTexture = targetTexture;
        uiCamera.targetTexture = targetTexture;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (targetTexture != null)
            Graphics.Blit(targetTexture, destination);
        else
            Graphics.Blit(Texture2D.blackTexture, destination);
    }
}

/// <summary>
/// A custom tumble UI that behaves the same as the base game tumble UI, but is exclusively used for the custom camera
/// </summary>
public class CustomTumbleUI : TumbleUI
{
    public new static CustomTumbleUI? instance;
    
    private new void Awake()
    {
        instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        canExitSound = new Sound { Sounds = [] };
    }

    private void OnDestroy()
    {
        instance = null;
    }
}