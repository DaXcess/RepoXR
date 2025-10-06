using RepoXR.Assets;
using UnityEngine.InputSystem;

namespace RepoXR.Input;

public class Actions
{
    public static Actions Instance { get; private set; } = new();
    
    public InputAction HeadPosition { get; private set; }
    public InputAction HeadRotation { get; private set; }
    public InputAction HeadTrackingState { get; private set; }
    
    public InputAction LeftHandPosition { get; private set; }
    public InputAction LeftHandRotation { get; private set; }
    public InputAction LeftHandTrackingState { get; private set; }
    
    public InputAction RightHandPosition { get; private set; }
    public InputAction RightHandRotation { get; private set; }
    public InputAction RightHandTrackingState { get; private set; }
    
    public InputAction EyeGazePosition { get; private set; }
    public InputAction EyeGazeRotation { get; private set; }

    private Actions()
    {
        HeadPosition = AssetCollection.DefaultXRActions.FindAction("Head/Position");
        HeadRotation = AssetCollection.DefaultXRActions.FindAction("Head/Rotation");
        HeadTrackingState = AssetCollection.DefaultXRActions.FindAction("Head/Tracking State");

        LeftHandPosition = AssetCollection.DefaultXRActions.FindAction("Left/Position");
        LeftHandRotation = AssetCollection.DefaultXRActions.FindAction("Left/Rotation");
        LeftHandTrackingState = AssetCollection.DefaultXRActions.FindAction("Left/Tracking State");

        RightHandPosition = AssetCollection.DefaultXRActions.FindAction("Right/Position");
        RightHandRotation = AssetCollection.DefaultXRActions.FindAction("Right/Rotation");
        RightHandTrackingState = AssetCollection.DefaultXRActions.FindAction("Right/Tracking State");

        EyeGazePosition = AssetCollection.DefaultXRActions.FindAction("Eye Gaze/Position");
        EyeGazeRotation = AssetCollection.DefaultXRActions.FindAction("Eye Gaze/Rotation");

        AssetCollection.DefaultXRActions.Enable();
    }

    public InputAction this[string name] => VRInputSystem.Instance.Actions[name];
}