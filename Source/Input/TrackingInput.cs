using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace RepoXR.Input;

/// <summary>
/// Tracking input can be used to easily read out position and rotation data, even when a new scene is being loaded
/// </summary>
public class TrackingInput : MonoBehaviour
{
    public static TrackingInput Instance => _instance ?? InputManager.instance.gameObject.AddComponent<TrackingInput>();
    private static TrackingInput? _instance;
    
    public Transform HeadTransform { get; private set; }
    public Transform LeftHandTransform { get; private set; }
    public Transform RightHandTransform { get; private set; }
    
    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        CreateTrackingOrigins();
    }

    private void CreateTrackingOrigins()
    {
        HeadTransform = new GameObject("Tracking - Head") { transform = { parent = transform } }.transform;
        LeftHandTransform = new GameObject("Tracking - Left Hand") { transform = { parent = transform } }.transform;
        RightHandTransform = new GameObject("Tracking - Right Hand") { transform = { parent = transform } }.transform;

        var headPoseDriver = HeadTransform.gameObject.AddComponent<TrackedPoseDriver>();
        var leftPoseDriver = LeftHandTransform.gameObject.AddComponent<TrackedPoseDriver>();
        var rightPoseDriver = RightHandTransform.gameObject.AddComponent<TrackedPoseDriver>();

        headPoseDriver.positionAction = new InputAction(binding: "<XRHMD>/centerEyePosition");
        headPoseDriver.rotationAction = new InputAction(binding: "<XRHMD>/centerEyeRotation");
        headPoseDriver.trackingStateInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/trackingState"));

        leftPoseDriver.positionAction = new InputAction(binding: "<XRController>{LeftHand}/pointerPosition");
        leftPoseDriver.rotationAction = new InputAction(binding: "<XRController>{LeftHand}/pointerRotation");
        leftPoseDriver.trackingStateInput =
            new InputActionProperty(new InputAction(binding: "<XRController>{LeftHand}/trackingState"));

        rightPoseDriver.positionAction = new InputAction(binding: "<XRController>{RightHand}/pointerPosition");
        rightPoseDriver.rotationAction = new InputAction(binding: "<XRController>{RightHand}/pointerRotation");
        rightPoseDriver.trackingStateInput =
            new InputActionProperty(new InputAction(binding: "<XRController>{RightHand}/trackingState"));
        
        headPoseDriver.positionAction.Enable();
        headPoseDriver.rotationAction.Enable();
        headPoseDriver.trackingStateInput.action.Enable();
        
        leftPoseDriver.positionAction.Enable();
        leftPoseDriver.rotationAction.Enable();
        leftPoseDriver.trackingStateInput.action.Enable();
        
        rightPoseDriver.positionAction.Enable();
        rightPoseDriver.rotationAction.Enable();
        rightPoseDriver.trackingStateInput.action.Enable();
    }
}