using System.Linq;
using HarmonyLib;
using RepoXR.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace RepoXR.Player;

public class VRMapTool : MonoBehaviour
{
    public static VRMapTool instance;
    
    private MapToolController controller;

    private RenderTexture displayTexture;
    private new Light light;

    private Transform displaySpring;

    private StatsUI statsUI;
    private RectTransform statsRect;

    private LevelUI levelUI;
    private RectTransform levelRect;

    // Shadow stuff
    private bool shadowVisible = true;
    private MeshRenderer[] renderers;

    public bool leftHanded;

    public static void Create()
    {
        var tool = FindObjectsOfType<MapToolController>().FirstOrDefault(t => t.PlayerAvatar.isLocal);
        if (instance != null || VRSession.Instance is not { } session || tool is null || !tool.PlayerAvatar.isLocal)
            return;

        tool.transform.parent.parent = session.Player.MapParent;
        tool.transform.parent.localPosition = Vector3.zero;
        tool.transform.parent.localRotation = Quaternion.identity;

        var vrTool = tool.gameObject.AddComponent<VRMapTool>();
        vrTool.renderers = tool.VisualTransform.GetComponentsInChildren<MeshRenderer>()
            .Where(r => r.shadowCastingMode == ShadowCastingMode.On).ToArray();
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

        levelUI = LevelUI.instance;
        levelRect = levelUI.GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Force hide the UI on startup

        statsUI.AllChildrenSetActive(false);
        levelUI.AllChildrenSetActive(false);
    }

    private void OnDestroy()
    {
        instance = null!;
    }

    private void Update()
    {
        if (controller.Active)
        {
            if (!shadowVisible)
            {
                shadowVisible = true;

                renderers.Do(r => r.shadowCastingMode = ShadowCastingMode.On);
            }

            light.intensity = Mathf.Lerp(light.intensity, 1, 4 * Time.deltaTime);
            
            VRSession.Instance.Player.DisableGrabRotate(0.1f);

            statsUI.Show();
            levelUI.Show();
        }
        else
        {
            displayTexture.Release();
            light.intensity = Mathf.Lerp(light.intensity, 0, 4 * Time.deltaTime);

            if (light.intensity < 0.1f && shadowVisible)
            {
                shadowVisible = false;

                renderers.Do(r => r.shadowCastingMode = ShadowCastingMode.Off);
            }
        }
    }

    private void LateUpdate()
    {
        UpdateStatsUI();
        UpdateLevelUI();
    }

    private void UpdateStatsUI()
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

    private void UpdateLevelUI()
    {
        var isAnimating = !((levelUI.showTimer > 0 && levelUI.hidePositionCurrent == levelUI.showPosition) ||
                            (levelUI.hideTimer > 0.1 && levelUI.hidePositionCurrent == levelUI.hidePosition));
        var animOffset = isAnimating
            ? (levelUI.showTimer > 0 ? 1 - levelUI.animationEval : levelUI.animationEval) * 0.25f
            : 0;
        var offset = (.225f - animOffset) * (leftHanded ? -1 : 1);

        levelRect.rotation = displaySpring.rotation * Quaternion.Euler(90, 0, 0);
        levelRect.position = displaySpring.TransformPoint(new Vector3(offset, 0, 0.23f));
        levelRect.localScale = transform.parent.localScale;
    }
}