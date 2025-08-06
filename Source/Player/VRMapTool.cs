using System.Linq;
using RepoXR.Managers;
using UnityEngine;

namespace RepoXR.Player;

public class VRMapTool : MonoBehaviour
{
    public static VRMapTool instance;
    
    private MapToolController controller;

    private RenderTexture displayTexture;
    private Light light;

    private Transform displaySpring;

    private StatsUI statsUI;
    private RectTransform statsRect;

    public bool leftHanded;

    public static void Create()
    {
        var tool = FindObjectsOfType<MapToolController>().FirstOrDefault(t => t.PlayerAvatar.isLocal);
        if (instance != null || VRSession.Instance is not { } session || tool is null || !tool.PlayerAvatar.isLocal)
            return;

        tool.transform.parent.parent = session.Player.MapParent;
        tool.transform.parent.localPosition = Vector3.zero;
        tool.transform.parent.localRotation = Quaternion.identity;
        tool.gameObject.AddComponent<VRMapTool>();
    }
    
    private void Awake()
    {
        instance = this;
        controller = GetComponent<MapToolController>();
        displaySpring = controller.HideTransform.Find("Main Spring/Base Offset/Bob/Main Unit/Display Spring");
        
        var display = displaySpring.Find("display_1x1");
        displayTexture = (RenderTexture)display.GetComponent<MeshRenderer>().material.mainTexture;
        light = displaySpring.Find("Light").GetComponent<Light>();

        // FREE FIX SINCE THIS IS AN ISSUE IN THE BASE GAME AS WELL
        display.transform.localPosition = Vector3.back * 0.006f;

        statsUI = StatsUI.instance;
        statsRect = statsUI.GetComponent<RectTransform>();
    }

    private void OnDestroy()
    {
        instance = null!;
    }

    private void Update()
    {
        if (controller.Active)
        {
            light.intensity = Mathf.Lerp(light.intensity, 1, 4 * Time.deltaTime);
            
            VRSession.Instance.Player.DisableGrabRotate(0.1f);

            statsUI.Show();
        }
        else
        {
            displayTexture.Release();
            light.intensity = Mathf.Lerp(light.intensity, 0, 4 * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        var isAnimating = !((statsUI.showTimer > 0 && statsUI.hidePositionCurrent == statsUI.showPosition) ||
                            (statsUI.hideTimer > 0.1 && statsUI.hidePositionCurrent == statsUI.hidePosition));
        var animOffset = isAnimating
            ? (statsUI.showTimer > 0 ? 1 - statsUI.animationEval : statsUI.animationEval) * 0.25f
            : 0;
        var offset = (-(leftHanded ? .175f : .275f) + animOffset) * (leftHanded ? -1 : 1);

        statsRect.rotation = displaySpring.rotation * Quaternion.Euler(90, 0, 0);
        statsRect.position = displaySpring.TransformPoint(new Vector3(offset, 0, 0.2f));
        statsRect.localScale = transform.parent.localScale;
    }
}