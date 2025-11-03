using HarmonyLib;
using RepoXR.Input;
using RepoXR.Patches;
using UnityEngine;

namespace RepoXR.Player.Camera;

// KNOWN ISSUE: When the player is not near their play area center, the VR aim script will pivot around a far away point
//              instead of at the camera itself. This is a known issue, no clue how to fix it yet.

public class VRCameraAim : MonoBehaviour
{
    public static VRCameraAim instance;

    private CameraAim cameraAim;
    private Transform mainCamera;

    public Quaternion rotationXZ = Quaternion.identity;
    public Quaternion rotationY = Quaternion.identity;

    // Aim fields
    private bool aimTargetActive;
    private GameObject? aimTargetObject;
    private Vector3 aimTargetPosition;
    private float aimTargetTimer;
    private float aimTargetSpeed;
    private int aimTargetPriority = 999;
    private bool aimTargetLowImpact;

    private float aimTargetLerp;

    // Soft aim fields
    private GameObject? aimTargetSoftObject;
    private Vector3 aimTargetSoftPosition;
    private float aimTargetSoftTimer;
    private float aimTargetSoftStrength;
    private float aimTargetSoftStrengthNoAim;
    private int aimTargetSoftPriority = 999;
    private bool aimTargetSoftLowImpact;

    private float aimTargetSoftStrengthCurrent;

    private Quaternion lastCameraRotation;
    private float playerAimingTimer;

    public bool IsActive => aimTargetActive;

    private void Awake()
    {
        instance = this;

        cameraAim = GetComponent<CameraAim>();
        mainCamera = GetComponentInChildren<UnityEngine.Camera>().transform;
    }

    private void Update()
    {
        // Detect head movement

        if (lastCameraRotation == Quaternion.identity)
            lastCameraRotation = mainCamera.localRotation;

        var delta = Quaternion.Angle(lastCameraRotation, mainCamera.localRotation);
        if (delta > 1)
            playerAimingTimer = 0.1f;

        lastCameraRotation = mainCamera.localRotation;

        // Perform forced rotations

        if (playerAimingTimer > 0)
            playerAimingTimer -= Time.deltaTime;

        if (aimTargetTimer > 0)
        {
            aimTargetTimer -= Time.deltaTime;
            aimTargetLerp += Time.deltaTime * aimTargetSpeed;
            aimTargetLerp = Mathf.Clamp01(aimTargetLerp);
        }
        else if (aimTargetLerp > 0)
        {
            cameraAim.SetPlayerAim(mainCamera.rotation, false);
            aimTargetLerp = 0;
            aimTargetPriority = 999;
            aimTargetActive = false;
        }

        var (targetY, targetXZ) = GetLookRotation(aimTargetPosition);

        if (aimTargetLowImpact)
            targetXZ = Quaternion.identity;

        rotationXZ = Quaternion.LerpUnclamped(rotationXZ, targetXZ, cameraAim.AimTargetCurve.Evaluate(aimTargetLerp));
        rotationY = Quaternion.LerpUnclamped(rotationY, targetY, cameraAim.AimTargetCurve.Evaluate(aimTargetLerp));

        if (aimTargetSoftTimer > 0 && aimTargetTimer <= 0)
        {
            var targetStrength = playerAimingTimer <= 0 ? aimTargetSoftStrengthNoAim : aimTargetSoftStrength;

            aimTargetSoftStrengthCurrent =
                Mathf.Lerp(aimTargetSoftStrengthCurrent, targetStrength, 10 * Time.deltaTime);

            (targetY, targetXZ) = GetLookRotation(aimTargetSoftPosition);

            if (aimTargetSoftLowImpact)
                targetXZ = Quaternion.identity;

            rotationXZ = Quaternion.Lerp(rotationXZ, targetXZ, aimTargetSoftStrengthCurrent * Time.deltaTime);
            rotationY = Quaternion.Lerp(rotationY, targetY, aimTargetSoftStrengthCurrent * Time.deltaTime);

            aimTargetSoftTimer -= Time.deltaTime;

            if (aimTargetSoftTimer <= 0)
            {
                aimTargetSoftObject = null;
                aimTargetSoftPriority = 999;
            }
        }

        if (!aimTargetActive && aimTargetSoftTimer <= 0)
            rotationXZ = Quaternion.Lerp(rotationXZ, Quaternion.identity, 5 * Time.deltaTime);

        if (SpectateCamera.instance && SpectateCamera.instance.CheckState(SpectateCamera.State.Death))
            transform.localRotation = Quaternion.identity;
        else
            transform.localRotation = Quaternion.Lerp(transform.localRotation, rotationY, 33 * Time.deltaTime);

        // Finally, reset the player aim

        cameraAim.SetPlayerAim(mainCamera.rotation, false);
    }

    private (Quaternion, Quaternion) GetLookRotation(Vector3 position)
    {
        var finalWorldRot = Quaternion.LookRotation(position - mainCamera.position, Vector3.up);
        var localRot = finalWorldRot * Quaternion.Inverse(TrackingInput.Instance.HeadTransform.rotation);
        var localFwd = localRot * Vector3.forward;
        var localYawFwd = new Vector3(localFwd.x, 0, localFwd.z).normalized;

        var qY = Quaternion.LookRotation(localYawFwd, Vector3.up);
        var qXZ = Quaternion.Inverse(qY) * localRot;

        return (qY, qXZ);
    }

    /// <summary>
    /// Instantly append a set amount of degrees to the current aim on the Y axis
    /// </summary>
    public void TurnAimNow(float degrees)
    {
        var rot = Quaternion.Euler(rotationY.eulerAngles + Vector3.up * degrees);

        transform.localRotation = rot;
        rotationY = rot;
    }

    /// <summary>
    /// Instantly change the aim rotation without any interpolation or smoothing
    /// </summary>
    public void SetAimNow(float degrees)
    {
        var rot = Quaternion.Euler(0, degrees, 0);

        transform.localRotation = rot;
        rotationY = rot;
    }

    private bool setInitialAim;

    /// <summary>
    /// Set current aim rotation, which takes into account the current Y rotation of the headset
    /// </summary>
    public void SetPlayerAim(float yRot, bool forceInitial = false)
    {
        if (CameraNoPlayerTarget.instance && (!setInitialAim || forceInitial))
        {
            yRot = CameraNoPlayerTarget.instance.transform.eulerAngles.y;
            setInitialAim = true;
        }

        SetAimNow(yRot - TrackingInput.Instance.HeadTransform.localEulerAngles.y);
    }

    public void SetAimTarget(Vector3 position, float time, float speed, GameObject obj, int priority,
        bool lowImpact = false)
    {
        if (priority > aimTargetPriority)
            return;

        if (obj != aimTargetObject && aimTargetLerp != 0)
            return;

        aimTargetActive = true;
        aimTargetObject = obj;
        aimTargetPosition = position;
        aimTargetTimer = time;
        aimTargetSpeed = speed;
        aimTargetPriority = priority;
        aimTargetLowImpact = lowImpact;
    }

    public void SetAimTargetSoft(Vector3 position, float time, float strength, float strengthNoAim, GameObject obj,
        int priority, bool lowImpact = false)
    {
        if (priority > aimTargetSoftPriority)
            return;

        if (aimTargetSoftObject && obj != aimTargetSoftObject)
            return;

        if (obj != aimTargetSoftObject)
            playerAimingTimer = 0;

        aimTargetSoftPosition = position;
        aimTargetSoftTimer = time;
        aimTargetSoftStrength = strength;
        aimTargetSoftStrengthNoAim = strengthNoAim;
        aimTargetSoftObject = obj;
        aimTargetSoftPriority = priority;
        aimTargetSoftLowImpact = lowImpact;
    }
}

[RepoXRPatch]
internal static class CameraAimPatches
{
    /// <summary>
    /// Attach a <see cref="VRCameraAim"/> script to all <see cref="CameraAim"/> objects
    /// </summary>
    [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.Awake))]
    [HarmonyPostfix]
    private static void OnCameraAimAwake(CameraAim __instance)
    {
        __instance.gameObject.AddComponent<VRCameraAim>();
    }

    /// <summary>
    /// Set initial rotation on game start
    /// </summary>
    [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.SetPlayerAim))]
    [HarmonyPostfix]
    private static void OnCameraAimSpawn(ref Quaternion _rotation, bool _setRotation)
    {
        if (_setRotation)
            VRCameraAim.instance.SetPlayerAim(_rotation.eulerAngles.y);
    }
    
    /// <summary>
    /// Disable the game's built in <see cref="CameraAim"/> functionality, as we'll implement that manually in VR 
    /// </summary>
    [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.Update))]
    [HarmonyPrefix]
    private static bool DisableCameraAim(CameraAim __instance)
    {
        return false;
    }

    /// <summary>
    /// Disable this method as it modifies the camera aim transform
    /// </summary>
    [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.OverridePlayerAimDisable))]
    [HarmonyPrefix]
    private static bool DisableAimDisableOverride()
    {
        return false;
    }
}
