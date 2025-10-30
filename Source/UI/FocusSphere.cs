using UnityEngine;

namespace RepoXR.UI;

public class FocusSphere : MonoBehaviour
{
    private static readonly int FadeStart = Shader.PropertyToID("_FadeStart");
    private static readonly int FadeEnd = Shader.PropertyToID("_FadeEnd");

    [SerializeField] protected Renderer renderer;
    [SerializeField] protected AnimationCurve animIn;
    [SerializeField] protected AnimationCurve animOut;

    private Transform camera;

    private Transform? lookAtTarget;
    private float lookAtTimer;
    private float lookAtSpeed;
    private float lookAtStrength;

    private float strengthLerp;

    private void Awake()
    {
        renderer.material.SetFloat(FadeStart, 0f);
        renderer.material.SetFloat(FadeEnd, 0f);

        camera = Camera.main!.transform;
    }

    private void Update()
    {
        transform.localPosition = camera.localPosition;

        var hasTarget = lookAtTarget != null;
        var visible = hasTarget && lookAtTimer > 0;

        strengthLerp = Mathf.Clamp01(strengthLerp + (visible ? lookAtSpeed : -lookAtSpeed) * Time.deltaTime);

        if (lookAtTimer > 0)
        {
            lookAtTimer = Mathf.Max(0, lookAtTimer - Time.deltaTime);

            renderer.material.SetFloat(FadeStart, animIn.Evaluate(strengthLerp) * lookAtStrength * 0.92f);
            renderer.material.SetFloat(FadeEnd, animIn.Evaluate(strengthLerp) * lookAtStrength);
        } else if (strengthLerp > 0)
        {
            renderer.material.SetFloat(FadeStart, animOut.Evaluate(strengthLerp) * lookAtStrength * 0.92f);
            renderer.material.SetFloat(FadeEnd, animOut.Evaluate(strengthLerp) * lookAtStrength);
        }

        if (hasTarget && strengthLerp > 0)
            transform.LookAt(lookAtTarget);
    }

    public void SetLookAtTarget(Transform target, float time, float speed, float strength)
    {
        strength = Mathf.Clamp01(strength);

        lookAtTarget = target;
        lookAtTimer = time;
        lookAtSpeed = speed;
        lookAtStrength = strength;
    }
}