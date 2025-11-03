using RepoXR.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace RepoXR.Player.Camera;

public class VRCameraTracker : TrackedPoseDriver
{
    public override void Awake()
    {
        base.Awake();

        positionAction = Actions.Instance.HeadPosition;
        rotationAction = Actions.Instance.HeadRotation;
        trackingStateInput = new InputActionProperty(Actions.Instance.HeadTrackingState);
    }

    public override void SetLocalTransform(Vector3 newPosition, Quaternion newRotation)
    {
        var rotation = newRotation;

        if (VRCameraAim.instance is { } aim)
            rotation = aim.rotationXZ * rotation;

        base.SetLocalTransform(newPosition, rotation);
    }
}