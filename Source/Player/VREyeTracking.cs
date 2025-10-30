using System;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RepoXR.Player;

public class VREyeTracking : MonoBehaviour
{
    private Transform debugCube;

    public bool Enabled => supported && Plugin.Config.EnableEyeTracking.Value;
    public Ray Gaze { get; private set; }

    private Vector3 gazePosition;
    private Quaternion gazeRotation;

    private bool supported;
    private float lastHardwareInput;

    private void Awake()
    {
        Actions.Instance.EyeGazePosition.performed += OnEyeGazePosition;
        Actions.Instance.EyeGazeRotation.performed += OnEyeGazeRotation;

        Plugin.Config.EnableEyeTracking.SettingChanged += OnEyeTrackingSettingChanged;

        // TODO: Remove once tested with real hardware
        debugCube = Instantiate(AssetCollection.Cube).transform;
        debugCube.GetComponent<MeshRenderer>().material.color = Color.blue;
        debugCube.GetComponent<Collider>().enabled = false;
        debugCube.position = Vector3.down * 1000;
        debugCube.gameObject.layer = 5;
        debugCube.localScale *= 2;
    }

    private void OnDestroy()
    {
        Actions.Instance.EyeGazePosition.performed -= OnEyeGazePosition;
        Actions.Instance.EyeGazeRotation.performed -= OnEyeGazeRotation;

        Plugin.Config.EnableEyeTracking.SettingChanged -= OnEyeTrackingSettingChanged;
    }

    private void OnEyeGazePosition(InputAction.CallbackContext ctx)
    {
        gazePosition = ctx.ReadValue<Vector3>();

        // Sometimes the OpenXR runtime misfires and triggers eye tracking callbacks even when it doesn't support it
        // In that case the data is always 0, so we can just discard the event if we didn't already have data before
        if (!supported && gazePosition == Vector3.zero)
            return;

        supported = true;
        lastHardwareInput = Time.realtimeSinceStartup;
    }

    private void OnEyeGazeRotation(InputAction.CallbackContext ctx)
    {
        gazeRotation = ctx.ReadValue<Quaternion>();

        // Sometimes the OpenXR runtime misfires and triggers eye tracking callbacks even when it doesn't support it
        // In that case the data is always 0, so we can just discard the event if we didn't already have data before
        if (!supported && gazeRotation == Quaternion.identity)
            return;

        supported = true;
        lastHardwareInput = Time.realtimeSinceStartup;
    }

    private static void OnEyeTrackingSettingChanged(object sender, EventArgs e)
    {
        if (!Plugin.Config.EnableEyeTracking.Value)
            NetworkSystem.instance.DisableEyeTracking();
    }

    private void Update()
    {
        if (!Enabled)
            return;

        // Assume eye tracking is no longer enabled if no data has been received for over 5 seconds
        if (Time.realtimeSinceStartup - lastHardwareInput > 5)
        {
            supported = false;
            NetworkSystem.instance.DisableEyeTracking();
            return;
        }

        var ray = new Ray(transform.parent.TransformPoint(gazePosition),
            transform.parent.TransformDirection(gazeRotation * Vector3.forward));

        var position = Physics.Raycast(ray, out var hit, 10, SemiFunc.LayerMaskGetShouldHits())
            ? hit.point
            : ray.origin + ray.direction * 10;

        Gaze = ray;

        NetworkSystem.instance.UpdateEyeTracking(position);

        debugCube.position = position;
        debugCube.rotation = Quaternion.LookRotation(ray.direction);
    }

    public static bool LookingAt(Vector3 position, float padWidth, float padHeight)
    {
        // Fall back if session is not initialized for some reason
        if (VRSession.Instance is not { } session)
            return SemiFunc.OnScreen(position, padWidth, padHeight);

        var eyeTracking = session.Player.EyeTracking.Enabled;

        var gaze = eyeTracking
            ? session.Player.EyeTracking.Gaze
            : new Ray(session.MainCamera.transform.position, session.MainCamera.transform.forward);
        var coneAngle = (eyeTracking ? 15f : 25f) + (padWidth + padHeight) * 2.5f;
        var direction = position - gaze.origin;
        var angle = Vector3.Angle(gaze.direction, direction.normalized);

        return angle <= coneAngle;
    }
}