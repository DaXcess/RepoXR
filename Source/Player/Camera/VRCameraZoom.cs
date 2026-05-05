using HarmonyLib;
using RepoXR.Patches;
using UnityEngine;

namespace RepoXR.Player.Camera;

/// <summary>
/// This is a replacement for the original <see cref="CameraZoom"/> which doesn't actually zoom, but instead modifies
/// the position of the camera to recreate the effect
/// </summary>
public class VRCameraZoom : MonoBehaviour
{
    public static VRCameraZoom instance;
    
    private CameraZoom cameraZoom;
    private CameraPosition cameraPosition;
    
    private Transform? zoomTarget;
    private Vector3 targetPosition;
    private float zoomPadding;
    private float zoomTimer;
    private float zoomSpeedIn;
    private float zoomSpeedOut;
    private int zoomPriority = 999;
    private bool zoomActive;

    private float zoomLerp;
    private float zoomCurrent;
    private float zoomPrev;
    private float zoomNew;

    private void Awake()
    {
        instance = this;
        
        cameraZoom = GetComponentInChildren<CameraZoom>();
        cameraPosition = GetComponentInChildren<CameraPosition>();
    }

    private void Update()
    {
        if (SpectateCamera.instance || (SemiFunc.MenuLevel() && CameraNoPlayerTarget.instance))
            return;

        if (!LevelGenerator.Instance.Generated || PlayerController.instance.playerAvatarScript.isDisabled)
            return;
        
        if (zoomTimer > 0)
        {
            zoomTimer -= Time.deltaTime;
            zoomLerp += Time.deltaTime * zoomSpeedIn;
        }
        else if (zoomTimer <= 0)
        {
            if (zoomActive)
            {
                zoomActive = false;
                zoomTarget = null;
                zoomPriority = 999;
                zoomLerp = 0;
                zoomPrev = zoomCurrent;
                zoomNew = 0;

            }
            else if (zoomLerp >= 1)
                AudioManager.instance.AudioListener.TargetPositionTransform = CameraUtils.Instance.MainCamera.transform;

            zoomLerp += Time.deltaTime * zoomSpeedOut;
        }

        zoomLerp = Mathf.Clamp01(zoomLerp);
        zoomCurrent = Mathf.LerpUnclamped(zoomPrev, zoomNew, cameraZoom.OverrideZoomCurve.Evaluate(zoomLerp));
        transform.localPosition = Vector3.LerpUnclamped(Vector3.zero, targetPosition, zoomCurrent);
    }

    private Vector3 GetTargetVector(Vector3 position)
    {
        var camera = CameraUtils.Instance.MainCamera.transform;
        var unzoomedCameraCoords = cameraPosition.transform.localPosition + camera.localPosition;
        var direction = (position - unzoomedCameraCoords).normalized;
        
        if (zoomPadding < 0)
            return unzoomedCameraCoords - direction * zoomPadding - cameraPosition.transform.localPosition - camera.localPosition;
        
        return position - direction * zoomPadding - cameraPosition.transform.localPosition - camera.localPosition;
    }
    
    public void SetZoomTarget(float zoom, float time, float speedIn, float speedOut, Transform target, int priority)
    {
        if (priority > zoomPriority)
            return;

        if (target != zoomTarget)
        {
            zoomLerp = 0;
            zoomPrev = zoomCurrent;
        }

        zoomNew = 1;
        zoomPadding = zoom;
        zoomTarget = target;
        zoomTimer = time;
        zoomSpeedIn = speedIn;
        zoomSpeedOut = speedOut;
        zoomPriority = priority;
        zoomActive = true;

        targetPosition = GetTargetVector(target.position);

        // Prevent audio from being distorted by fast movement
        AudioManager.instance.AudioListener.TargetPositionTransform = PlayerAvatar.instance.RoomVolumeCheck.transform;
    }
}

[RepoXRPatch]
internal static class CameraZoomPatches
{
    [HarmonyPatch(typeof(CameraZoom), nameof(CameraZoom.Awake))]
    [HarmonyPostfix]
    private static void OnCameraZoomCreated(CameraZoom __instance)
    {
        // CameraZoom may be used on menu avatar cameras
        if (__instance.name != "Camera Controller")
            return;

        __instance.enabled = false;
        __instance.gameObject.AddComponent<VRCameraZoom>();
    }
}