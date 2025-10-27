using System;
using System.Collections;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Vector3 = UnityEngine.Vector3;

namespace RepoXR.Player;

public class VRPlayer : MonoBehaviour
{
    // Camera stuff
    private VRCameraPosition cameraPosition;
    private VRCameraAim cameraAim;  
    
    // Tracking stuff
    private Transform mainCamera;
    private Transform leftHand;
    private Transform rightHand;

    // Reference stuff
    private PlayerController localController;
    private VRRig localRig;
    private VREyeTracking eyeTracking;
    
    // Public accessors
    public Transform MainHand => VRSession.IsLeftHanded ? localRig.leftHandTip : localRig.rightHandTip;
    public Transform SecondaryHand => VRSession.IsLeftHanded ? localRig.rightHandTip : localRig.leftHandTip;
    public Transform MapParent => localRig.map;
    public VRRig Rig => localRig;
    public VREyeTracking EyeTracking => eyeTracking;
    
    // Public state
    public float disableRotateTimer;
    public bool physicalCrouch;
    
    // Private state
    private float movementRelative;
    private bool turnedLastInput;
    private Vector3 lastPosition;
    private bool wasPhysicalCrouch;

    private void Awake()
    {
        cameraPosition = VRCameraPosition.instance;
        cameraAim = VRCameraAim.instance;

        mainCamera = VRSession.Instance.MainCamera.transform;
        
        localController = PlayerController.instance;

        // This will make the player rotate towards where we are looking
        localController.cameraGameObject = mainCamera.gameObject;
        localController.cameraGameObjectLocal = mainCamera.gameObject;
        
        // Set up hands and stuff
        localRig = Instantiate(AssetCollection.VRRig).GetComponent<VRRig>();
        
        leftHand = new GameObject("Left Hand").transform;
        rightHand = new GameObject("Right Hand").transform;

        leftHand.transform.parent = rightHand.transform.parent = mainCamera.transform.parent;
        
        var leftHandTracker = leftHand.gameObject.AddComponent<TrackedPoseDriver>();
        var rightHandTracker = rightHand.gameObject.AddComponent<TrackedPoseDriver>();

        leftHandTracker.positionAction = Actions.Instance.LeftHandPosition;
        leftHandTracker.rotationAction = Actions.Instance.LeftHandRotation;
        leftHandTracker.trackingStateInput = new InputActionProperty(Actions.Instance.LeftHandTrackingState);

        rightHandTracker.positionAction = Actions.Instance.RightHandPosition;
        rightHandTracker.rotationAction = Actions.Instance.RightHandRotation;
        rightHandTracker.trackingStateInput = new InputActionProperty(Actions.Instance.RightHandTrackingState);
        
        localRig.head = mainCamera;
        localRig.leftArmTarget = leftHand;
        localRig.rightArmTarget = rightHand;

        eyeTracking = mainCamera.gameObject.AddComponent<VREyeTracking>();

        Actions.Instance["ResetHeight"].performed += OnResetHeight;
    }

    private void OnDestroy()
    {
        Actions.Instance["ResetHeight"].performed -= OnResetHeight;
    }

    private IEnumerator Start()
    {
        yield return null;
        
        ResetHeight();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleCrouching();
    }

    private void Update()
    {
        HandleTurning();
        
        // Timers
        if (disableRotateTimer > 0)
            disableRotateTimer -= Time.deltaTime;
    }

    public void DisableGrabRotate(float time)
    {
        disableRotateTimer = time;
    }
    
    public void SetColor(int colorIndex, Color color = default)
    {
        var customColor = colorIndex == -1;
        if (!customColor)
            color = AssetManager.instance.playerColors[colorIndex];

        localRig.SetColor(color);
    }

    private void ResetHeight()
    {
        const float targetHeight = 1.5f;

        // The playerOffset field is actually smoothed, making the reset height sequence look a little bit nicer
        cameraPosition.original.playerOffset = new Vector3(0, targetHeight - mainCamera.transform.localPosition.y, 0);
    }

    private void HandleMovement()
    {
        // No tracking data yet
        if (mainCamera.transform.localPosition == Vector3.zero)
            return;

        // If this is our first frame with position data, just reset the position immediately
        if (lastPosition == Vector3.zero)
            lastPosition = mainCamera.transform.localPosition;

        var headPosition = mainCamera.transform.localPosition;
        var movement = new Vector3(headPosition.x - lastPosition.x, 0, headPosition.z - lastPosition.z);

        cameraPosition.additionalOffset = -(mainCamera.transform.parent.rotation *
                                            new Vector3(mainCamera.localPosition.x, 0,
                                                mainCamera.localPosition.z)); // Will make the game run like 3-DoF
        PlayerController.instance.rb.MovePosition(PlayerController.instance.rb.transform.position +
                                                  mainCamera.transform.parent.rotation *
                                                  movement); // Will make the game run like 6-DoF again

        movementRelative += movement.sqrMagnitude;

        if (movementRelative > 0.00025f)
        {
            PlayerController.instance.movingResetTimer = 0.1f;
            PlayerController.instance.moving = true;
        }

        movementRelative = Mathf.Lerp(movementRelative, 0, 5 * Time.fixedDeltaTime);

        lastPosition = headPosition;
    }

    private void HandleCrouching()
    {
        var offset = 1.5f - cameraPosition.original.playerOffset.y;
        var diff = mainCamera.transform.localPosition.y - offset;
        
        if (diff > 0.5f) // Check if we're getting too tall
            ResetHeight();
        else if (diff < -offset + 0.05f) // Check if going through floor (with 5cm offset)
            ResetHeight();

        if (diff <= -0.6f)
            physicalCrouch = true;
        else if (diff > -0.6f)
            physicalCrouch = false;

        // Ignore if room scale crouch is disabled
        if (!Plugin.Config.RoomscaleCrouch.Value)
        {
            physicalCrouch = false;
            
            if (diff < -.5f)
                ResetHeight();

            return;
        }

        if (physicalCrouch != wasPhysicalCrouch)
        {
            wasPhysicalCrouch = physicalCrouch;

            // If we were previously crouching but not anymore, set the player to uncrouch
            if (!physicalCrouch)
            {
                PlayerController.instance.toggleCrouch = false;
                CameraCrouchPosition.instance.Lerp = 0; // Prevent animation when physically uncrouching
            }
        }

        // If we're physically crouching, set the player to crouch
        if (physicalCrouch)
            PlayerController.instance.toggleCrouch = true;
    }
    
    private void HandleTurning()
    {
        // Block turning if we are rotating an object or being blocked by a menu
        if (PlayerAvatar.instance.physGrabber.isRotating || PlayerController.instance.InputDisableTimer > 0)
            return;
        
        var value = Actions.Instance["Turn"].ReadValue<float>();

        switch (Plugin.Config.TurnProvider.Value)
        {
            case Config.TurnProviderOption.Snap:
                var should = Mathf.Abs(value) > 0.75f;
                var snapSize = Plugin.Config.SnapTurnSize.Value;
                
                // Funny hourglass makes turning slower :)
                if (PlayerController.instance.overrideTimeScaleTimer > 0)
                    snapSize *= PlayerController.instance.overrideTimeScaleMultiplier;
                
                if (!turnedLastInput && should)
                    if (value > 0)
                        cameraAim.TurnAimNow(snapSize);
                    else
                        cameraAim.TurnAimNow(-snapSize);

                turnedLastInput = should;
                
                break;
            
            case Config.TurnProviderOption.Smooth:
                if (!Plugin.Config.DynamicSmoothSpeed.Value)
                    value = value == 0 ? 0 : Math.Sign(value);

                if (PlayerController.instance.overrideTimeScaleTimer > 0)
                    value *= PlayerController.instance.overrideTimeScaleMultiplier;
                
                cameraAim.TurnAimNow(180 * Time.deltaTime * Plugin.Config.SmoothTurnSpeedModifier.Value * value);
                break;
            
            case Config.TurnProviderOption.Disabled:
            default:
                break;
        }
    }

    private void OnResetHeight(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;
        
        ResetHeight();
    }
}
