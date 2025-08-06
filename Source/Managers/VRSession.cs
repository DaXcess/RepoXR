using RepoXR.Input;
using RepoXR.Player;
using RepoXR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;

namespace RepoXR.Managers;

public class VRSession : MonoBehaviour
{
    public static VRSession Instance { get; private set; }

    /// <summary>
    /// Whether the game has VR enabled. THis field will only be populated after RepoXR has loaded.
    /// </summary>
    public static bool InVR => Plugin.Flags.HasFlag(Flags.VR);

    /// <summary>
    /// Whether the player is using their left hand as dominant hand
    /// </summary>
    public static bool IsLeftHanded => Plugin.Config.LeftHandDominant.Value;

    public Camera MainCamera { get; private set; }
    public VRPlayer Player { get; private set; }
    public GameHud HUD { get; private set; }
    
    private void Awake()
    {
        Instance = this;
        
        if (InVR)
            InitializeVRSession();
    }

    private void OnDestroy()
    {
        Instance = null!;
    }

    private void InitializeVRSession()
    {
        // Disable base UI input system
        var input = GameObject.Find("EventSystem")?.GetComponent<InputSystemUIInputModule>();
        if (input != null)
            input.enabled = false;

        MainCamera = CameraUtils.Instance.MainCamera;
        MainCamera.targetTexture = null;
        MainCamera.depth = 0;

        // Setup camera tracking
        var cameraPoseDriver = MainCamera.gameObject.AddComponent<TrackedPoseDriver>();
        cameraPoseDriver.positionAction = Actions.Instance.HeadPosition;
        cameraPoseDriver.rotationAction = Actions.Instance.HeadRotation;
        cameraPoseDriver.trackingStateInput = new InputActionProperty(Actions.Instance.HeadTrackingState);
        
        // Setup "on top" camera
        var topCamera = MainCamera.transform.Find("Camera Top").GetComponent<Camera>();
        topCamera.depth = 1;
        topCamera.targetTexture = null;
        
        // Initialize VR Player
        Player = PlayerController.instance.gameObject.AddComponent<VRPlayer>();
        
        // Initialize VR HUD
        HUD = global::HUD.instance.gameObject.AddComponent<GameHud>();
        
        // Initialize Handheld Map (if it wasn't created yet)
        VRMapTool.Create();
    }
}