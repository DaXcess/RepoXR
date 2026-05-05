using HarmonyLib;
using RepoXR.Rendering;
using UnityEngine;
using UnityEngine.XR;

namespace RepoXR.Patches.Rendering;

[RepoXRPatch]
internal static class PostProcessPatches
{
    /// <summary>
    /// Add a custom post-processing manager on the base game's post-processing volume
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(typeof(PostProcessing), nameof(PostProcessing.Awake))]
    [HarmonyPostfix]
    private static void OnPostProcessCreated(PostProcessing __instance)
    {
        __instance.gameObject.AddComponent<CustomPostProcessing>();
    }

    /// <summary>
    /// Patch for the bloom effect to also work properly in VR
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.BlindingLightBloomEffect))]
    [HarmonyPrefix]
    private static bool BlindingLightBloomVR(Transform _source, GameObject _obj)
    {
        if (SemiFunc.Photosensitivity())
            return false;

        if (Vector3.Distance(_source.position, PlayerController.instance.playerAvatarScript.transform.position) > 6)
            return false;

        if (!SemiFunc.OnScreen(_source.position, 0, 0.333f))
            return false;

        var screenPos = CameraUtils.Instance.MainCamera.WorldToScreenPoint(_source.position);
        if (screenPos.z < 0)
            return false;

        var screenCenter = new Vector2(XRSettings.eyeTextureWidth * 0.5f, XRSettings.eyeTextureHeight * 0.5f);
        var distanceFromCenter = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), screenCenter);
        var maxDistance = Mathf.Sqrt(screenCenter.x * screenCenter.x + screenCenter.y * screenCenter.y);
        var screenPositionFalloff = Mathf.Clamp01(1f - distanceFromCenter / maxDistance);

        var distance = Vector3.Distance(CameraUtils.Instance.MainCamera.transform.position, _source.position);
        var distanceFalloff = Mathf.Clamp01(1f - (distance - 1f) / 10f);

        var directionToPlayer = (CameraUtils.Instance.MainCamera.transform.position - _source.position).normalized;
        var dot = Vector3.Dot(_source.forward, directionToPlayer);
        var directionFalloff = Mathf.Clamp01(Mathf.InverseLerp(0.8f, 1f, dot));

        var finalIntensity = screenPositionFalloff * distanceFalloff * directionFalloff;
        if (finalIntensity <= 0)
            return false;

        var bloomIntensity = Mathf.Lerp(10f, 30f, finalIntensity);
        var bloomThreshold = Mathf.Lerp(0.9f, 0.15f, finalIntensity);
        PostProcessing.Instance.BloomOverride(bloomIntensity, bloomThreshold, 20f, 10f, 0.1f, _obj);

        return false;
    }
}