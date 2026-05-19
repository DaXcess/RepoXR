using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Patches;
using UnityEngine;

namespace RepoXR.Player.Camera;

public class VRCameraPosition : MonoBehaviour
{
    public static VRCameraPosition instance;

    public CameraPosition original;
    public Vector3 additionalOffset;

    private Vector3 currentPosition;

    // Position overriding
    private Transform mainCamera;

    internal bool overridePositionActive;
    private float overridePositionTimer;
    private Vector3 overridePositionTarget;

    private void Awake()
    {
        instance = this;
        original = GetComponent<CameraPosition>();
        mainCamera = UnityEngine.Camera.main!.transform;
    }

    private void Update()
    {
        var smoothing = original.positionSmooth;
        if (original.tumbleSetTimer > 0f)
        {
            smoothing *= 0.5f;
            original.tumbleSetTimer -= Time.deltaTime;
        }

        if (overridePositionTimer > 0)
        {
            overridePositionTimer -= Time.deltaTime;
            MoveCameraToPosition(overridePositionTarget);

            return;
        }

        var targetPosition = original.playerTransform.localPosition + original.playerOffset;
        
        if (SemiFunc.MenuLevel() && CameraNoPlayerTarget.instance)
            targetPosition = CameraNoPlayerTarget.instance.transform.position;

        currentPosition = Vector3.Slerp(currentPosition, targetPosition, smoothing * Time.deltaTime);

        if (overridePositionActive)
            PositionOverrideToggled(targetPosition, false);

        transform.localPosition = currentPosition + additionalOffset;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, smoothing * Time.deltaTime);
        
        if (SemiFunc.MenuLevel())
            transform.localPosition = targetPosition;
    }

    public void OverridePosition(Vector3 pos, float time = 0.1f)
    {
        if (time <= 0)
        {
            overridePositionTimer = time;
            PositionOverrideToggled(original.playerTransform.position + original.playerOffset, false);
            return;
        }

        if (!overridePositionActive)
            PositionOverrideToggled(pos, true);

        if (Vector3.Distance(pos, transform.position) > 5)
        {
            PlayerController.instance.playerAvatarScript.localCamera.Teleported();
            AudioManager.instance.RestartAudioLoopDistances();
            LevelGenerator.Instance.RestartParticleDistances();
        }

        overridePositionTarget = pos;
        overridePositionTimer = time;
        overridePositionActive = true;
    }

    private void PositionOverrideToggled(Vector3 pos, bool state)
    {
        if (state)
        {
            SemiFunc.LightManagerSetCullTargetTransform(transform);

            if (PlayerAvatar.instance?.physGrabber is { } grabber)
            {
                var visuals = PlayerAvatar.instance.playerAvatarVisuals;
                visuals.playerAvatarRightArm.grabberClawParent.SetParent(visuals.playerAvatarRightArm.grabberTransform);
                visuals.playerAvatarRightArm.grabberClawParent.localPosition = Vector3.zero;

                grabber.physGrabPointPlane.SetParent(PlayerAvatar.instance.playerAvatarVisuals.playerAvatarRightArm.grabberTransformTarget, true);
                grabber.physGrabPointPlane.localPosition = Vector3.forward * grabber.physGrabPointPlane.localPosition.z;
                grabber.physGrabPointPlane.localRotation = Quaternion.identity;
                grabber.physGrabBeamComponent?.PhysGrabPointOrigin =
                    grabber.physGrabBeamComponent.PhysGrabPointOriginClient;
            }

            // Set camera layer mask to render normally in the world
            if (PlayerAvatar.instance?.flashlightController is { } flashlight)
            {
                flashlight.toolBackAway.enabled = false;

                var targetLayer = LayerMask.NameToLayer("Triggers");
                foreach (var child in flashlight.GetComponentsInChildren<Transform>())
                {
                    if (child.gameObject != flashlight.meshShadows.gameObject)
                        child.gameObject.layer = targetLayer;
                }
            }

            MoveCameraToPosition(pos);

            VRSession.Instance.Player.Rig.SetVisible(false);
        }
        else
        {
            if (PlayerAvatar.instance is { } player)
            {
                SemiFunc.LightManagerSetCullTargetTransform(player.transform);
                VRSession.Instance.Player.Rig.SetVisible(!player.deadSet);
            }

            if (Vector3.Distance(pos, transform.position) > 5)
            {
                PlayerController.instance.playerAvatarScript.localCamera.Teleported();
                AudioManager.instance.RestartAudioLoopDistances();
                LevelGenerator.Instance.RestartParticleDistances();
            }

            overridePositionActive = false;

            if (PlayerAvatar.instance?.physGrabber is { } grabber)
            {
                // Setting to CameraAim is fine, we override its full position in the grabber patch
                grabber.physGrabPointPlane.SetParent(CameraAim.Instance.transform, true);
                grabber.physGrabPointPlane.localPosition = Vector3.forward * grabber.physGrabPointPlane.localPosition.z;
                grabber.physGrabPointPlane.localRotation = Quaternion.identity;
            }

            // Reset camera layer mask to render on top again
            if (PlayerAvatar.instance?.flashlightController is { } flashlight)
            {
                flashlight.toolBackAway.enabled = false;

                var targetLayer = LayerMask.NameToLayer("TopLayerOnly");
                foreach (var child in flashlight.GetComponentsInChildren<Transform>())
                {
                    if (child.gameObject != flashlight.meshShadows.gameObject)
                        child.gameObject.layer = targetLayer;
                }
            }

            // Reset some rig transforms
            VRSession.Instance.Player.Rig.UpdateDominantTransforms();
        }
    }

    /// <summary>
    /// Helper function that moves the main camera to <param name="pos" /> accounting for any non-zero local positions
    /// </summary>
    private void MoveCameraToPosition(Vector3 pos)
    {
        var camLocal = mainCamera.localToWorldMatrix;
        var curWorld = transform.localToWorldMatrix;
        var rel = curWorld.inverse * camLocal;
        var targetWorld = Matrix4x4.TRS(pos, mainCamera.rotation, mainCamera.lossyScale);
        var thisWorld = targetWorld * rel.inverse;

        transform.SetPositionAndRotation(thisWorld.GetColumn(3), thisWorld.rotation);
    }
}

[RepoXRPatch]
internal static class CameraPositionPatches
{
    /// <summary>
    /// Attach a <see cref="VRCameraPosition"/> to any <see cref="CameraPosition"/> game object
    /// </summary>
    [HarmonyPatch(typeof(CameraPosition), nameof(CameraPosition.Awake))]
    [HarmonyPostfix]
    private static void OnCreateCameraPosition(CameraPosition __instance)
    {
        __instance.gameObject.AddComponent<VRCameraPosition>();
    }

    [HarmonyPatch(typeof(CameraPosition), nameof(CameraPosition.OverridePosition))]
    [HarmonyPrefix]
    private static bool OnOverrideCameraPosition(CameraPosition __instance, Vector3 _position, float _time)
    {
        __instance.GetComponent<VRCameraPosition>().OverridePosition(_position, _time);

        return false;
    }

    /// <summary>
    /// Disable the base functionality as we'll reimplement it in <see cref="VRCameraPosition"/>
    /// </summary>
    [HarmonyPatch(typeof(CameraPosition), nameof(CameraPosition.Update))]
    [HarmonyPrefix]
    private static bool DisableCameraPosition()
    {
        return false;
    }
}