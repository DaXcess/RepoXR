using RepoXR.Networking.Frames;
using UnityEngine;

namespace RepoXR.Networking;

[DefaultExecutionOrder(100)]
public class NetworkPlayer : MonoBehaviour
{
    internal PlayerAvatar playerAvatar;
    
    private PlayerAvatarVisuals playerAvatarVisuals;
    private PlayerAvatarLeftArm playerLeftArm;
    private PlayerAvatarRightArm playerRightArm;

    private FlashlightController flashlight;
    private MapToolController mapTool;
    
    private Transform rigContainer;
    private Transform leftHandTarget;
    private Transform rightHandTarget;
    
    private Transform leftHandAnchor;
    private Transform rightHandAnchor;

    private Transform headlampTransform;
    
    private Vector3 leftHandPosition;
    private Vector3 rightHandPosition;

    private Quaternion leftHandRotation;
    private Quaternion rightHandRotation;

    public Transform PrimaryHand => isLeftHanded ? leftHandTarget : rightHandTarget;

    private bool networkReady;
    
    private bool isLeftHanded;
    private bool isMapLeftHanded;
    private bool isHeadlampEnabled;

    // Eye tracking
    public bool EyeTracking { get; private set; }
    public Vector3 EyeGazePoint { get; private set; }
    
    private void Start()
    {
        playerAvatarVisuals = playerAvatar.playerAvatarVisuals;
        playerLeftArm = playerAvatarVisuals.GetComponent<PlayerAvatarLeftArm>();
        playerRightArm = playerAvatarVisuals.GetComponent<PlayerAvatarRightArm>();

        rigContainer = new GameObject("VR Player Rig Container")
            {
                transform =
                {
                    parent = transform, localPosition = Vector3.zero,
                    localRotation = Quaternion.identity
                }
            }
            .transform;
        leftHandTarget = new GameObject("Left Hand") { transform = { parent = rigContainer } }.transform;
        rightHandTarget = new GameObject("Right Hand") { transform = { parent = rigContainer } }.transform;

        leftHandAnchor = new GameObject("Left Hand Anchor")
                { transform = { parent = playerLeftArm.leftArmTransform, localPosition = Vector3.forward * 0.513f } }
            .transform;
        rightHandAnchor = new GameObject("Right Hand Anchor")
            {
                transform =
                {
                    // ANIM ARM R SCALE is the one that scales, not rightArmTransform
                    parent = playerRightArm.rightArmTransform.Find("ANIM ARM R SCALE"),
                    localPosition = Vector3.forward * 0.513f
                }
            }
            .transform;

        // Headlamp

        headlampTransform = new GameObject("Headlamp Anchor")
            {
                transform =
                {
                    parent = playerAvatarVisuals.attachPointTopHeadMiddle, 
                    localPosition = new Vector3(isLeftHanded ? 0.21f : -0.21f, 0.1f, 0)
                }
            }
            .transform;
        
        // Re-parent tools and grabber

        var playerRoot = playerAvatar.transform.parent;
        
        flashlight = playerRoot.GetComponentInChildren<FlashlightController>(true);
        flashlight.transform.parent = transform;
        flashlight.transform.localScale = Vector3.one * flashlight.hiddenScale;
        flashlight.transform.localPosition = Vector3.zero;
        flashlight.transform.localRotation = Quaternion.identity;

        mapTool = playerRoot.GetComponentInChildren<MapToolController>(true);
        mapTool.transform.parent.parent = transform;
        mapTool.transform.parent.localPosition = Vector3.zero;
        mapTool.transform.parent.localRotation = Quaternion.identity;

        playerRightArm.grabberClawParent.SetParent(isLeftHanded ? leftHandAnchor : rightHandAnchor);
        playerRightArm.grabberClawParent.localPosition = Vector3.zero;

        playerRightArm.physGrabBeam.PhysGrabPointOrigin.SetParent(isLeftHanded ? leftHandAnchor : rightHandAnchor);
        playerRightArm.physGrabBeam.PhysGrabPointOrigin.localPosition = Vector3.zero;

        networkReady = true;
    }

    private void Update()
    {
        // We're most likely unloading the scene, so disable the VR player
        if (!playerAvatarVisuals || !mapTool)
        {
            enabled = false;
            return;
        }
        
        transform.position = playerAvatarVisuals.transform.position;

        leftHandTarget.position =
            Vector3.Lerp(leftHandTarget.position, leftHandPosition, 15 * Time.deltaTime);
        leftHandTarget.rotation =
            Quaternion.Slerp(leftHandTarget.rotation, leftHandRotation, 15 * Time.deltaTime);

        rightHandTarget.position =
            Vector3.Lerp(rightHandTarget.position, rightHandPosition, 15 * Time.deltaTime);
        rightHandTarget.rotation =
            Quaternion.Slerp(rightHandTarget.rotation, rightHandRotation, 15 * Time.deltaTime);

        if (!playerAvatar.isTumbling)
        {
            playerRightArm.rightArmTransform.LookAt(rightHandTarget.position);
            playerLeftArm.leftArmTransform.LookAt(leftHandTarget.position);
        }

        leftHandAnchor.rotation = leftHandTarget.rotation;
        rightHandAnchor.rotation = rightHandTarget.rotation;
    }

    private void LateUpdate()
    {
        // Update flashlight transform (only if headlamp is disabled)
        var anchor = isLeftHanded ? rightHandAnchor : leftHandAnchor;
     
        if (!isHeadlampEnabled)
        {
            flashlight.transform.position = anchor.position;
            flashlight.transform.rotation = anchor.rotation;
        }

        // Update map tool transform
        anchor = isMapLeftHanded ? leftHandAnchor : rightHandAnchor;

        mapTool.transform.parent.position = anchor.position;
        mapTool.transform.parent.rotation = anchor.rotation;
    }

    public void HandleRigFrame(Rig rigFrame)
    {
        leftHandPosition = rigFrame.LeftPosition;
        leftHandRotation = rigFrame.LeftRotation;

        rightHandPosition = rigFrame.RightPosition;
        rightHandRotation = rigFrame.RightRotation;
    }

    public void HandleMapFrame(MapTool mapFrame)
    {
        isMapLeftHanded = mapFrame.LeftHanded;
        
        if (!networkReady)
            return;
        
        flashlight.hideFlashlight = mapFrame.HideFlashlight;
    }

    public void HandleHeadlamp(bool headlampEnabled)
    {
        isHeadlampEnabled = headlampEnabled;

        if (!networkReady)
            return;
        
        flashlight.transform.SetParent(headlampEnabled ? headlampTransform : transform);
        flashlight.transform.localPosition = Vector3.zero;
        flashlight.transform.localRotation = Quaternion.identity;
    }

    public void UpdateDominantHand(bool leftHanded)
    {
        isLeftHanded = leftHanded;

        if (!networkReady)
            return;
        
        flashlight.transform.parent = isHeadlampEnabled ? headlampTransform : transform;
        flashlight.transform.localScale = Vector3.one * flashlight.hiddenScale;
        flashlight.transform.localPosition = Vector3.zero;
        flashlight.transform.localRotation = Quaternion.identity;

        headlampTransform.transform.localPosition = new Vector3(isLeftHanded ? 0.21f : -0.21f, 0.1f, 0);

        playerRightArm.grabberClawParent.SetParent(isLeftHanded ? leftHandAnchor : rightHandAnchor);
        playerRightArm.grabberClawParent.localPosition = Vector3.zero;

        playerRightArm.physGrabBeam.PhysGrabPointOrigin.SetParent(isLeftHanded ? leftHandAnchor : rightHandAnchor);
        playerRightArm.physGrabBeam.PhysGrabPointOrigin.localPosition = Vector3.zero;
    }

    public void UpdateEyeTracking(Vector3 gazePoint)
    {
        // (0, -1000, 0) is sent whenever eye tracking is disabled during a session
        if (gazePoint == Vector3.down * 1000)
        {
            EyeTracking = false;
            return;
        }

        EyeTracking = true;
        EyeGazePoint = gazePoint;
    }
}