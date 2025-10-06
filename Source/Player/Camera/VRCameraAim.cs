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
    
    private Quaternion rotation;
    
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
        } else if (aimTargetLerp > 0)
        {
            cameraAim.ResetPlayerAim(mainCamera.rotation);
            aimTargetLerp = 0;
            aimTargetPriority = 999;
            aimTargetActive = false;
        }

        var targetRotation = GetLookRotation(aimTargetPosition);

        if (aimTargetLowImpact)
            targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
        
        rotation = Quaternion.LerpUnclamped(rotation, targetRotation, cameraAim.AimTargetCurve.Evaluate(aimTargetLerp));
        
        if (aimTargetSoftTimer > 0 && aimTargetTimer <= 0)
        {
            var targetStrength = playerAimingTimer <= 0 ? aimTargetSoftStrengthNoAim : aimTargetSoftStrength;

            aimTargetSoftStrengthCurrent = Mathf.Lerp(aimTargetSoftStrengthCurrent, targetStrength, 10 * Time.deltaTime);

            targetRotation = GetLookRotation(aimTargetSoftPosition);

            if (aimTargetSoftLowImpact)
                targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            
            rotation = Quaternion.Lerp(rotation, targetRotation, aimTargetSoftStrengthCurrent * Time.deltaTime);

            aimTargetSoftTimer -= Time.deltaTime;

            if (aimTargetSoftTimer <= 0)
            {
                aimTargetSoftObject = null;
                aimTargetSoftPriority = 999;
            }
        }

        if (!aimTargetActive && aimTargetSoftTimer <= 0)
            rotation = Quaternion.LerpUnclamped(rotation, Quaternion.Euler(0, rotation.eulerAngles.y, 0), 5 * Time.deltaTime);

        var lastRotation = transform.localRotation;
        
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0, transform.localEulerAngles.y, 0);
        
        var cameraPos = mainCamera.transform.position;
        
        transform.localRotation = Quaternion.Lerp(lastRotation, rotation, 33 * Time.deltaTime);
        transform.localPosition = cameraPos - mainCamera.transform.position;
        
        // Finally, reset the player aim
        
        cameraAim.ResetPlayerAim(mainCamera.rotation);
    }

    private Quaternion GetLookRotation(Vector3 position)
    {
        var desired = Quaternion.LookRotation(position - mainCamera.transform.position, Vector3.up);
        var camDelta = desired * Quaternion.Inverse(mainCamera.transform.rotation);

        return camDelta * transform.rotation;
    }

    /// <summary>
    /// Instantly append a set amount of degrees to the current aim on the Y axis
    /// </summary>
    public void TurnAimNow(float degrees)
    {
        var rot = Quaternion.Euler(transform.eulerAngles + Vector3.up * degrees);

        transform.localRotation = rot;
        rotation = rot;
    }

    /// <summary>
    /// Instantly change the aim rotation without any interpolation or smoothing
    /// </summary>
    public void ForceSetRotation(Vector3 newAngles)
    {
        var rot = Quaternion.Euler(newAngles);
        
        transform.localRotation = rot;
        rotation = rot;
    }

    /// <summary>
    /// Set spawn rotation, which takes into account the current Y rotation of the headset
    /// </summary>
    public void SetSpawnRotation(float yRot)
    {
        if (CameraNoPlayerTarget.instance)
            yRot = CameraNoPlayerTarget.instance.transform.eulerAngles.y;
        
        var angle = new Vector3(0, yRot - TrackingInput.Instance.HeadTransform.localEulerAngles.y, 0);
        
        ForceSetRotation(angle);
    }

    public void SetAimTarget(Vector3 position, float time, float speed, GameObject obj, int priority, bool lowImpact = false)
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
    [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.CameraAimSpawn))]
    [HarmonyPostfix]
    private static void OnCameraAimSpawn(float _rotation)
    {
        VRCameraAim.instance.SetSpawnRotation(_rotation);
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
}
