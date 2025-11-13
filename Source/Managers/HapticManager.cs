using System;
using HarmonyLib;
using RepoXR.Data;
using UnityEngine;
using UnityEngine.XR;

namespace RepoXR.Managers;

public class HapticManager : MonoBehaviour
{
    private static HapticManager instance;
    
    private AnimationCurveData? currentCurve;
    private Hand currentHand;
    private float currentAmplitude;
    private float curveTimer;
    private float curveSpeed;
    
    private int currentPriority = -999;
    private float priorityTimer;
    
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Destroy object if we toggled out of VR
        if (SemiFunc.FPSImpulse1() && !VRSession.InVR)
        {
            Destroy(gameObject);
            return;
        }
        
        if (priorityTimer > 0)
        {
            priorityTimer -= Time.deltaTime;

            if (priorityTimer <= 0)
                currentPriority = -999;
        }

        if (currentCurve is null || currentCurve.curve.length == 0)
            return;

        Execute(currentHand, Type.Impulse, currentAmplitude * currentCurve.Evaluate(curveTimer), 0.1f);

        var maxTime = currentCurve.curve[currentCurve.curve.length - 1].time;
        curveTimer += Time.deltaTime * curveSpeed;

        if (curveTimer < maxTime)
            return;

        currentPriority = -999;
        currentCurve = null;
    }

    private static void Execute(Hand hand, Type type, float amplitude, float duration)
    {
        if ((int)Plugin.Config.HapticFeedback.Value <= (int)type)
            return;

        if (amplitude < 0 || duration < 0)
            return;
        
        InputDevice[] devices = hand == Hand.Both
            ? [InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), InputDevices.GetDeviceAtXRNode(XRNode.RightHand)]
            :
            [
                InputDevices.GetDeviceAtXRNode(hand switch
                {
                    Hand.Dominant => VRSession.IsLeftHanded ? XRNode.LeftHand : XRNode.RightHand,
                    Hand.Secondary => VRSession.IsLeftHanded ? XRNode.RightHand : XRNode.LeftHand,
                    Hand.Left => XRNode.LeftHand,
                    Hand.Right => XRNode.RightHand,
                    _ => throw new ArgumentOutOfRangeException(nameof(hand), hand, null)
                })
            ];

        devices.Do(device =>
        {
            if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, amplitude, duration);
        });
    }

    public static void PulseCurve(Hand hand, AnimationCurveData curve, float amplitude = 0.1f, float speed = 1,
        int priority = 1)
    {        
        if (priority < instance.currentPriority)
            return;

        instance.currentHand = hand;
        instance.currentCurve = curve;
        instance.currentAmplitude = amplitude;
        instance.curveTimer = 0;
        instance.curveSpeed = speed;
        instance.currentPriority = priority;
    }

    public static void Impulse(Hand hand, Type type, float amplitude = 0.1f, float duration = 0.1f, int priority = 1)
    {
        if (priority < instance.currentPriority)
            return;

        instance.currentPriority = priority;
        instance.priorityTimer = duration;
        
        Execute(hand, type, amplitude, duration);
    }

    public enum Type
    {
        Impulse,
        Continuous
    }

    public enum Hand
    {
        Left,
        Right,
        Both,
        
        Dominant,
        Secondary
    }
}