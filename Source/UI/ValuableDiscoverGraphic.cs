using RepoXR.Player;
using UnityEngine;

namespace RepoXR.UI;

// A rewrite of the original ValuableDiscoverGraphic that makes use of 3d models instead of a canvas renderer

public class ValuableDiscoverGraphic : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");

    public Renderer renderer;

    private global::ValuableDiscoverGraphic baseGraphic;

    internal PhysGrabObject target;

    private global::ValuableDiscoverGraphic.State state;
    private bool discovered;
    private float waitTimer;
    private float animLerp;

    private Vector3 targetCenter;
    private Vector3 targetSize;

    private void Awake()
    {
        baseGraphic = ValuableDiscover.instance.graphicPrefab.GetComponent<global::ValuableDiscoverGraphic>();
    }

    private void Start()
    {
        transform.localScale = Vector3.zero;

        waitTimer = state switch
        {
            global::ValuableDiscoverGraphic.State.Reminder => 0.5f,
            global::ValuableDiscoverGraphic.State.Bad => 3,
            _ => 1
        };
    }

    private void Update()
    {
        if (target)
        {
            var bounds = new Bounds(target.centerPoint, Vector3.zero);
            foreach (var meshRenderer in target.GetComponentsInChildren<MeshRenderer>())
                bounds.Encapsulate(meshRenderer.bounds);

            bounds.Expand(0.05f);

            targetCenter = bounds.center;
            targetSize = bounds.size;

            var lookingAt = VREyeTracking.LookingAt(bounds.center, 0.5f, 0.5f);
            if (lookingAt && !discovered)
            {
                if (state == global::ValuableDiscoverGraphic.State.Reminder)
                    baseGraphic.sound.Play(target.centerPoint, 0.3f);
                else
                    baseGraphic.sound.Play(target.centerPoint);

                renderer.enabled = true;
                discovered = true;
            }
        }
        else
            waitTimer = 0;

        transform.position = targetCenter;

        if (waitTimer > 0)
        {
            animLerp = Mathf.Clamp01(animLerp + baseGraphic.introSpeed * Time.deltaTime);
            transform.localScale =
                Vector3.LerpUnclamped(Vector3.zero, targetSize, baseGraphic.introCurve.Evaluate(animLerp));

            if (animLerp < 1)
                return;

            waitTimer -= Time.deltaTime;

            // Wait timer has ended, outro will now start (so reset animLerp)
            if (waitTimer <= 0)
                animLerp = 0;
        }
        else
        {
            animLerp = Mathf.Clamp01(animLerp + baseGraphic.outroSpeed * Time.deltaTime);
            transform.localScale =
                Vector3.LerpUnclamped(targetSize, Vector3.zero, baseGraphic.outroCurve.Evaluate(animLerp));

            if (animLerp >= 1)
                Destroy(gameObject);
        }
    }

    public void ReminderSetup()
    {
        state = global::ValuableDiscoverGraphic.State.Reminder;

        var baseColor = baseGraphic.ColorReminderMiddle;
        var edgeColor = baseGraphic.ColorReminderCorner;

        renderer.material.SetColor(BaseColor, baseColor);
        renderer.material.SetColor(EdgeColor, edgeColor);
    }

    public void BadSetup()
    {
        state = global::ValuableDiscoverGraphic.State.Bad;

        var baseColor = baseGraphic.ColorBadMiddle;
        var edgeColor = baseGraphic.ColorBadCorner;

        renderer.material.SetColor(BaseColor, baseColor);
        renderer.material.SetColor(EdgeColor, edgeColor);
    }

    public void CustomSetup(global::ValuableDiscoverGraphic.State newState, Color baseColor, Color edgeColor)
    {
        state = newState;

        renderer.material.SetColor(BaseColor, baseColor);
        renderer.material.SetColor(EdgeColor, edgeColor);
    }
}