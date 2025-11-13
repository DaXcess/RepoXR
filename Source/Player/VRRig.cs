using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using RepoXR.Assets;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.Networking;
using RepoXR.Player.Camera;
using RepoXR.UI;
using RepoXR.UI.Expressions;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace RepoXR.Player;

public class VRRig : MonoBehaviour
{
    private static readonly int AlbedoColor = Shader.PropertyToID("_AlbedoColor");
    private static readonly int HurtColor = Shader.PropertyToID("_ColorOverlayAmount");
    private static readonly int HurtAmount = Shader.PropertyToID("_ColorOverlayAmount");
    
    public MeshRenderer[] meshes;

    public Transform head;
    public Transform leftArm;
    public Transform rightArm;
    public Transform leftArmCenter;
    public Transform rightArmCenter;
    public Transform leftArmTarget;
    public Transform rightArmTarget;
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public Transform leftHandTip;
    public Transform rightHandTip;

    public Transform headAnchor;
    public Transform planeOffsetTransform;
    public RectTransform infoHud;
    public Transform inventory;
    public Transform map;
    public Transform headLamp;

    public Collider leftHandCollider;
    public Collider rightHandCollider;
    public Collider mapPickupCollider;
    public Collider lampTriggerCollider;
    public Collider[] shoulderMapPickupColliders;

    public VRInventory inventoryController;

    public Vector3 headOffset;

    public Vector3 mapRightPosition;
    public Vector3 mapLeftPosition;

    private bool armsDetached;

    private Transform leftArmMesh;
    private Transform rightArmMesh;

    private PlayerAvatar playerAvatar;
    private PlayerAvatarVisuals playerAvatarVisuals;
    private PlayerAvatarRightArm playerAvatarRightArm;
    
    // Flashlight

    private FlashlightController flashlight;
    
    // Map tool stuff

    private MapToolController mapTool;
    private bool mapHeldLeftHand;
    private bool mapHeld;
    
    private void Awake()
    {
        leftArmMesh = leftArm.GetComponentInChildren<MeshRenderer>().transform;
        rightArmMesh = rightArm.GetComponentInChildren<MeshRenderer>().transform;
        
        // Load persisted data
        headlampEnabled = DataManager.instance.headlampEnabled;

        Plugin.Config.LeftHandDominant.SettingChanged += OnDominantHandChanged;
        Plugin.Config.DetachedArms.SettingChanged += OnDetachedArmsChanged;

        // Update on load
        OnDetachedArmsChanged(null!, null!);
    }

    private void OnDestroy()
    {
        Plugin.Config.LeftHandDominant.SettingChanged -= OnDominantHandChanged;
        Plugin.Config.DetachedArms.SettingChanged -= OnDetachedArmsChanged;
    }

    private IEnumerator Start()
    {
        playerAvatar = PlayerController.instance.playerAvatarScript;
        playerAvatarVisuals = playerAvatar.playerAvatarVisuals;
        playerAvatarRightArm = playerAvatarVisuals.GetComponentInChildren<PlayerAvatarRightArm>(true);

        // Set up grabber claw
        playerAvatarRightArm.grabberClawParent.gameObject.SetLayerRecursively(6);
        playerAvatarRightArm.grabberClawParent.GetComponentsInChildren<MeshRenderer>()
            .Do(mesh => mesh.shadowCastingMode = ShadowCastingMode.Off);

        // Everything else is only available after the first frame
        yield return null;

        // Grab flashlight reference
        flashlight = FlashlightController.Instance;

        // Map tool
        mapTool = FindObjectsOfType<MapToolController>().First(tool => tool.PlayerAvatar.isLocal);

        planeOffsetTransform.localPosition = Vector3.up * Plugin.Config.HUDPlaneOffset.Value;

        // Expression wheel
        Instantiate(AssetCollection.ExpressionWheel, CameraUtils.Instance.MainCamera.transform.parent)
            .GetComponent<ExpressionRadial>();
        
        // Update parents
        UpdateDominantTransforms();
    }

    private void LateUpdate()
    {
        transform.position = head.position + headOffset;
        transform.rotation = Quaternion.Lerp(transform.rotation,
            Quaternion.Euler(transform.eulerAngles.x, head.eulerAngles.y, transform.eulerAngles.z),
            10 * Time.deltaTime);

        headAnchor.position = head.position;
        headAnchor.rotation = head.rotation;
        
        UpdateArms();
        UpdateClaw();
        MapToolLogic();
        WallClipLogic();
        LookAtHUDLogic();
        HeadLampLogic();
    }

    /// <summary>
    /// Update transform parents and positions based on the current dominant hand
    /// </summary>
    private void UpdateDominantTransforms()
    {
        playerAvatarRightArm.grabberClawParent.SetParent(VRSession.Instance.Player.MainHand);
        playerAvatarRightArm.grabberClawParent.localPosition = Vector3.zero;

        var beamOrigin = PhysGrabber.instance.physGrabBeamComponent.PhysGrabPointOrigin;
        beamOrigin.SetParent(VRSession.Instance.Player.MainHand);
        beamOrigin.localPosition = Vector3.zero;
        
        flashlight.transform.parent = headlampEnabled ? headLamp : VRSession.Instance.Player.SecondaryHand;
        flashlight.transform.localPosition = Vector3.zero;
        flashlight.transform.localRotation = Quaternion.identity;

        lampTriggerCollider.transform.localPosition = new Vector3(VRSession.IsLeftHanded ? 0.2f : -0.2f,
            lampTriggerCollider.transform.localPosition.y, lampTriggerCollider.transform.localPosition.z);
        
        NetworkSystem.instance.UpdateDominantHand(Plugin.Config.LeftHandDominant.Value);
    }

    private void UpdateArms()
    {
        if (armsDetached)
            UpdateArmsDetached();
        else
            UpdateArmsAttached();

        // Synchronize multiplayer rig
        if (SemiFunc.IsMultiplayer())
            NetworkSystem.instance.SendRigData(leftHandTip.position, rightHandTip.position, leftHandTip.rotation,
                rightHandTip.rotation);
    }
    
    private void UpdateArmsAttached()
    {
        leftArm.localPosition = new Vector3(leftArm.localPosition.x, leftArm.localPosition.y, 0);
        rightArm.localPosition = new Vector3(rightArm.localPosition.x, rightArm.localPosition.y, 0);
            
        leftArm.LookAt(leftArmTarget.position);
        rightArm.LookAt(rightArmTarget.position);

        // I KNOW THAT THIS IS NOT *THE* WAY TO DO THIS, THEY DON'T CALL IT *INVERSE* KINEMATICS FOR NOTHING, AND I AM DOING QUITE THE OPPOSITE
        var maxDistanceLeft = leftHandAnchor.localPosition.z;
        var maxDistanceRight = rightHandAnchor.localPosition.z;

        if (Vector3.Distance(leftArm.position, leftArmTarget.position) is var leftDistance &&
            leftDistance < maxDistanceLeft)
        {
            leftArm.localPosition += Vector3.back * (maxDistanceLeft - leftDistance);
            leftArm.LookAt(leftArmTarget.position);
        }

        if (Vector3.Distance(rightArm.position, rightArmTarget.position) is var rightDistance &&
            rightDistance < maxDistanceRight)
        {
            rightArm.localPosition += Vector3.back * (maxDistanceRight - rightDistance);
            rightArm.LookAt(rightArmTarget.position);
        }
        
        leftArmMesh.localEulerAngles = Vector3.up * 90;
        rightArmMesh.localEulerAngles = Vector3.down * 90;
        
        leftArmMesh.Rotate(Vector3.left, leftArmTarget.localEulerAngles.z);
        rightArmMesh.Rotate(Vector3.right, rightArmTarget.localEulerAngles.z);

        leftHandTip.rotation = leftArmTarget.rotation;
        rightHandTip.rotation = rightArmTarget.rotation;
    }

    private void UpdateArmsDetached()
    {
        leftArm.position =  leftArmTarget.position - (leftArmCenter.position - leftArm.position);
        leftArm.rotation = leftArmTarget.rotation;

        rightArm.position = rightArmTarget.position - (rightArmCenter.position - rightArm.position);
        rightArm.rotation = rightArmTarget.rotation;
    }

    private void UpdateClaw()
    {
        // We most likely are changing scenes
        if (!playerAvatarVisuals || !playerAvatar || !playerAvatarRightArm)
            return;
        
        if (playerAvatarVisuals.isMenuAvatar || (playerAvatar.isActiveAndEnabled &&
                                                 playerAvatarRightArm.playerAvatar.playerHealth.hurtFreeze))
            return;
        
        playerAvatarRightArm.deltaTime = playerAvatarVisuals.deltaTime;
        playerAvatarRightArm.GrabberLogic();
    }

    private Vector3 MapPrimaryPosition => VRSession.IsLeftHanded ? mapLeftPosition : mapRightPosition;
    private Vector3 MapSecondaryPosition => VRSession.IsLeftHanded ? mapRightPosition : mapLeftPosition;

    private bool mapHovered;

    private void MapToolLogic()
    {
        if (!mapTool)
            return;

        // Move map tool anchor to the left if we're holding an item
        map.transform.localPosition = Vector3.Lerp(map.transform.localPosition,
            PhysGrabber.instance.grabbed ? MapSecondaryPosition : MapPrimaryPosition, 8 * Time.deltaTime);

        mapTool.transform.parent.localPosition =
            Vector3.Lerp(mapTool.transform.parent.localPosition, Vector3.zero, 5 * Time.deltaTime);
        mapTool.transform.parent.localRotation = Quaternion.Slerp(mapTool.transform.parent.localRotation,
            Quaternion.identity, 5 * Time.deltaTime);

        // If the map tool was disabled for any reason, reparent back to hotbar
        if (!mapTool.Active && mapHeld)
        {
            mapHeld = false;
            mapHeldLeftHand = false;
            mapTool.transform.parent.parent = map;
            playerAvatar.physGrabber.enabled = true;
        }

        mapHeld = mapTool.Active;

        // Check for states that don't allow the map to be used
        if (playerAvatar.isDisabled || playerAvatar.isTumbling || VRCameraAim.instance.IsActive || SemiFunc.MenuLevel())
        {
            mapTool.Active = false;
            return;
        }

        var rightHandHovered = Utils.Collide(rightHandCollider, [mapPickupCollider, ..shoulderMapPickupColliders]);
        var leftHandHovered = Utils.Collide(leftHandCollider, [mapPickupCollider, ..shoulderMapPickupColliders]);

        // Haptic touch logic
        if (!mapTool.Active && !mapHovered && leftHandHovered)
        {
            mapHovered = true;

            HapticManager.Impulse(HapticManager.Hand.Left, HapticManager.Type.Impulse);
        }
        else if (!mapTool.Active && !mapHovered && rightHandHovered)
        {
            mapHovered = true;

            HapticManager.Impulse(HapticManager.Hand.Right, HapticManager.Type.Impulse);
        }
        else if (mapTool.Active || (!leftHandHovered && !rightHandHovered))
            mapHovered = false;

        // Flashlight hide logic (before picking up)
        if (!mapTool.Active &&
            Utils.Collide(VRSession.IsLeftHanded ? rightHandCollider : leftHandCollider,
                [mapPickupCollider, ..shoulderMapPickupColliders]) && !PlayerController.instance.sprinting)
            flashlight.hideFlashlight = !headlampEnabled;
        else if (!mapTool.Active)
            flashlight.hideFlashlight = false;

        // Right hand pickup logic
        if (!mapTool.Active && Actions.Instance["MapGrabRight"].WasPressedThisFrame() &&
            Utils.Collide(rightHandCollider, [mapPickupCollider, ..shoulderMapPickupColliders]) &&
            !PlayerController.instance.sprinting)
            if (mapTool.HideLerp >= 1)
            {
                mapTool.transform.parent.parent = rightHandTip;
                mapTool.Active = true;
                VRMapTool.instance.leftHanded = false;
                flashlight.hideFlashlight = !headlampEnabled && VRSession.IsLeftHanded;

                // Prevent picking up items while the map is opened
                if (!VRSession.IsLeftHanded)
                {
                    playerAvatar.physGrabber.ReleaseObject(-1);
                    playerAvatar.physGrabber.enabled = false;
                }
            }

        // Left hand pickup logic
        if (!mapTool.Active && Actions.Instance["MapGrabLeft"].WasPressedThisFrame() &&
            Utils.Collide(leftHandCollider, [mapPickupCollider, ..shoulderMapPickupColliders]) &&
            !PlayerController.instance.sprinting)
            if (mapTool.HideLerp >= 1)
            {
                mapTool.transform.parent.parent = leftHandTip;
                mapTool.Active = true;
                VRMapTool.instance.leftHanded = true;
                mapHeldLeftHand = true;
                flashlight.hideFlashlight = !headlampEnabled && !VRSession.IsLeftHanded;

                // Prevent picking up items while the map is opened
                if (VRSession.IsLeftHanded)
                {
                    playerAvatar.physGrabber.ReleaseObject(-1);
                    playerAvatar.physGrabber.enabled = false;
                }
            }

        // Disable map when sprinting
        if (PlayerController.instance.sprinting)
            mapTool.Active = false;

        // Right hand "let-go" logic
        if (mapTool.Active && !Actions.Instance["MapGrabRight"].IsPressed() && !mapHeldLeftHand &&
            mapTool.HideLerp <= 0)
            mapTool.Active = false;

        // Left hand "let-go" logic
        if (mapTool.Active && !Actions.Instance["MapGrabLeft"].IsPressed() && mapHeldLeftHand && mapTool.HideLerp <= 0)
            mapTool.Active = false;

        NetworkSystem.instance.UpdateMapToolState(flashlight.hideFlashlight, mapHeldLeftHand);
    }

    /// <summary>
    /// Detects clipping through walls with the VR rig arms and disables grabbing and the cursor
    /// </summary>
    private void WallClipLogic()
    {
        var camera = CameraUtils.Instance.MainCamera.transform;
        var direction = VRSession.Instance.Player.MainHand.position - camera.position;

        if (Physics.Raycast(new Ray(camera.position, direction), out _,
                Vector3.Distance(camera.position, VRSession.Instance.Player.MainHand.position), Crosshair.LayerMask))
        {
            // HIT!
            Crosshair.instance.gameObject.SetActive(false);
            PhysGrabber.instance.grabDisableTimer = 0.1f;
        }
        else
        {
            // Not hit!

            Crosshair.instance.gameObject.SetActive(true);
        }
    }

    private bool lookingAtHud;

    /// <summary>
    /// Detect how much the camera is looking downwards and make the info HUD more accessible if looking at it
    /// </summary>
    private void LookAtHUDLogic()
    {
        if (head.localEulerAngles.x is < 180 and > 30 && !lookingAtHud)
            lookingAtHud = true;
        else if (head.localEulerAngles.x is < 20 or > 180 && lookingAtHud)
            lookingAtHud = false;

        planeOffsetTransform.transform.localPosition = Vector3.Lerp(planeOffsetTransform.transform.localPosition,
            Vector3.up * (lookingAtHud ? Plugin.Config.HUDGazePlaneOffset.Value : Plugin.Config.HUDPlaneOffset.Value),
            8 * Time.deltaTime);
    }

    private bool headlampHovered;
    private bool headlampEnabled;
    
    private void HeadLampLogic()
    {
        // Disable in shop and lobby
        if (RunManager.instance.levelCurrent == RunManager.instance.levelLobby ||
            RunManager.instance.levelCurrent == RunManager.instance.levelShop)
            return;
        
        var collided = Utils.Collide(VRSession.IsLeftHanded ? rightHandCollider : leftHandCollider, lampTriggerCollider);
        if (collided && !headlampHovered)
            HapticManager.Impulse(HapticManager.Hand.Secondary, HapticManager.Type.Impulse);

        if (collided && Actions.Instance[VRSession.IsLeftHanded ? "MapGrabRight" : "MapGrabLeft"]
                .WasPressedThisFrame())
        {
            // Reparent flashlight onto new parent
            flashlight.transform.SetParent(
                headlampEnabled ? (VRSession.IsLeftHanded ? rightHandTip : leftHandTip) : headLamp,
                false);
            flashlight.lightOnAudio.Play(flashlight.transform.position);

            headlampEnabled = !headlampEnabled;

            DataManager.instance.headlampEnabled = headlampEnabled;
            NetworkSystem.instance.UpdateHeadlamp(headlampEnabled);
        }

        headlampHovered = collided;
    }

    private void DetachArms()
    {
        armsDetached = true;

        leftArm.localScale = rightArm.localScale = Vector3.one * 0.3f;
        leftHandCollider.transform.localScale = rightHandCollider.transform.localScale = Vector3.one * 3f;

        leftHandTip.localRotation = Quaternion.identity;
        rightHandTip.localRotation = Quaternion.identity;
    }

    private void AttachArms()
    {
        armsDetached = false;

        leftArm.localScale = rightArm.localScale = Vector3.one;
        leftHandCollider.transform.localScale = rightHandCollider.transform.localScale = Vector3.one;
        leftArm.localPosition = rightArm.localPosition = Vector3.zero;
    }
    
    public void SetVisible(bool visible)
    {
        foreach (var mesh in meshes)
            mesh.enabled = visible;

        infoHud.gameObject.SetActive(visible);
        map.gameObject.SetActive(visible);
        inventory.gameObject.SetActive(visible);
    }
    
    public void SetColor(Color color)
    {
        foreach (var mesh in meshes)
            mesh.sharedMaterial.SetColor(AlbedoColor, color);
    }

    public void SetHurtColor(Color color)
    {
        foreach (var mesh in meshes)
            mesh.sharedMaterial.SetColor(HurtColor, color);
    }

    public void SetHurtAmount(float amount)
    {
        foreach (var mesh in meshes)
            mesh.sharedMaterial.SetFloat(HurtAmount, amount);
    }
    
    // Event handlers

    private void OnDominantHandChanged(object sender, EventArgs args)
    {
        UpdateDominantTransforms();
    }

    private void OnDetachedArmsChanged(object sender, EventArgs args)
    {
        if (Plugin.Config.DetachedArms.Value)
            DetachArms();
        else
            AttachArms();
    }
}