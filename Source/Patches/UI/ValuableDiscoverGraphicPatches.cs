using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Player;
using UnityEngine;

namespace RepoXR.Patches.UI;

[RepoXRPatch]
internal static class ValuableDiscoverGraphicPatches
{
    [HarmonyPatch(typeof(ValuableDiscoverGraphic), nameof(ValuableDiscoverGraphic.Start))]
    [HarmonyPostfix]
    private static void OnValuableDiscovered(ValuableDiscoverGraphic __instance)
    {
        // Create canvas for rendering in world space
        var canvas = new GameObject("World Space Valuable Graphic") { layer = 5 }.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Move graphic to canvas
        __instance.transform.SetParent(canvas.transform, false);
        __instance.transform.localPosition = Vector3.zero;
        __instance.transform.localEulerAngles = Vector3.zero;
        __instance.transform.localScale = Vector3.one;

        __instance.canvasRect = canvas.GetComponent<RectTransform>();

        // Anchor images
        var container = __instance.middle.parent.GetComponent<RectTransform>();
        var middle = __instance.middle.GetComponent<RectTransform>();
        var topLeft = __instance.topLeft.GetComponent<RectTransform>();
        var topRight = __instance.topRight.GetComponent<RectTransform>();
        var bottomLeft = __instance.botLeft.GetComponent<RectTransform>();
        var bottomRight = __instance.botRight.GetComponent<RectTransform>();

        const float offset = 0.024f;
        
        container.anchorMax = new Vector2(1, 1);
        container.anchorMin = new Vector2(0, 0);
        container.offsetMax = Vector2.zero;
        container.offsetMin = Vector2.zero;
        container.anchoredPosition = Vector2.zero;
        
        middle.anchorMax = new Vector2(1, 1);
        middle.anchorMin = new Vector2(0, 0);
        middle.offsetMin = new Vector2(0, 0);
        middle.offsetMax = new Vector2(0, 0);
        middle.anchoredPosition = Vector2.zero;
        middle.sizeDelta = Vector2.zero;

        topLeft.anchorMax = new Vector2(0, 1);
        topLeft.anchorMin = new Vector2(0, 1);
        topLeft.anchoredPosition = new Vector2(offset, -offset);
        topLeft.localScale = Vector3.one * 0.0006f;

        topRight.anchorMax = new Vector2(1, 1);
        topRight.anchorMin = new Vector2(1, 1);
        topRight.anchoredPosition = new Vector2(-offset, -offset);
        topRight.localScale = Vector3.one * 0.0006f;

        bottomLeft.anchorMax = new Vector2(0, 0);
        bottomLeft.anchorMin = new Vector2(0, 0);
        bottomLeft.anchoredPosition = new Vector2(offset, offset);
        bottomLeft.localScale = Vector3.one * 0.0006f;

        bottomRight.anchorMax = new Vector2(1, 0);
        bottomRight.anchorMin = new Vector2(1, 0);
        bottomRight.anchoredPosition = new Vector2(-offset, offset);
        bottomRight.localScale = Vector3.one * 0.0006f;
    }

    /// <summary>
    /// A replacement for the original Update method that makes the graphic show in world space
    /// </summary>
    [HarmonyPatch(typeof(ValuableDiscoverGraphic), nameof(ValuableDiscoverGraphic.Update))]
    [HarmonyPrefix]
    private static bool ValuableDiscoverGraphicUpdate(ValuableDiscoverGraphic __instance)
    {
        var mainCamera = VRSession.Instance.MainCamera.transform;
        var canvas = __instance.transform.parent.GetComponent<RectTransform>();
        
        if (__instance.target)
        {
            var bounds = new Bounds(__instance.target.centerPoint, Vector3.zero);

            foreach (var meshRenderer in __instance.target.GetComponentsInChildren<MeshRenderer>())
                bounds.Encapsulate(meshRenderer.bounds);
            
            canvas.position = bounds.center;
            canvas.sizeDelta = new Vector2(bounds.size.x + 0.05f, bounds.size.y + 0.05f);
            canvas.LookAt(mainCamera.position);
            canvas.eulerAngles = new Vector3(0, canvas.eulerAngles.y, 0);

            if (VREyeTracking.LookingAt(bounds.center, 0.5f, 0.5f))
            {
                if (__instance.first)
                {
                    if (__instance.state == ValuableDiscoverGraphic.State.Reminder)
                        __instance.sound.Play(__instance.target.centerPoint, 0.3f);
                    else
                        __instance.sound.Play(__instance.target.centerPoint);
                    
                    __instance.middle.gameObject.SetActive(true);
                    __instance.topLeft.gameObject.SetActive(true);
                    __instance.topRight.gameObject.SetActive(true);
                    __instance.botLeft.gameObject.SetActive(true);
                    __instance.botRight.gameObject.SetActive(true);

                    __instance.first = false;
                }
            }
        }
        else
            __instance.waitTimer = 0;

        if (__instance.waitTimer > 0)
        {
            __instance.animLerp = Mathf.Clamp01(__instance.animLerp + __instance.introSpeed * Time.deltaTime);
            canvas.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one,
                __instance.introCurve.Evaluate(__instance.animLerp));

            if (__instance.animLerp >= 1)
            {
                __instance.waitTimer -= Time.deltaTime;
                if (__instance.waitTimer <= 0)
                {
                    __instance.animLerp = 0;
                    return false;
                }
            }
        }
        else
        {
            __instance.animLerp = Mathf.Clamp01(__instance.animLerp + __instance.outroSpeed * Time.deltaTime);
            canvas.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.zero,
                __instance.outroCurve.Evaluate(__instance.animLerp));

            if (__instance.animLerp >= 1)
                Object.Destroy(__instance.transform.parent.gameObject);
        }
        
        return false;
    }
}