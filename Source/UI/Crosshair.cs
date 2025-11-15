using RepoXR.Managers;
using UnityEngine;

namespace RepoXR.UI;

public class Crosshair : MonoBehaviour
{
    public static Crosshair instance;
    
    public const int LayerMask = 1 << 0 | 1 << 9 | 1 << 10 | 1 << 16 | 1 << 20 | 1 << 23;
    
    private new Transform camera;
    private Transform sprite;

    private void Awake()
    {
        instance = this;

        camera = Camera.main!.transform;
        sprite = GetComponentInChildren<Aim>(true).transform;
    }

    private void OnDestroy()
    {
        instance = null!;
    }

    private void Update()
    {
        if (VRSession.Instance is not { } session)
            return;

        if (!Physics.Raycast(new Ray(session.Player.MainHand.position, session.Player.MainHand.forward), out var hit,
                10, LayerMask))
        {
            transform.position = Vector3.down * 3000;
            return;
        }

        // Hide the crosshair if we are holding an item (keep overcharge though)
        if (PhysGrabber.instance.grabbedObject)
        {
            sprite.position = Vector3.down * 3000;
            return;
        }

        // Look straight at camera if we're pointing to an enemy
        if (hit.collider.CompareTag("Phys Grab Object") && hit.collider.GetComponentInParent<EnemyRigidbody>())
        {
            transform.SetPositionAndRotation(hit.point,
                Quaternion.LookRotation(hit.point - camera.position) * Quaternion.Euler(0, 90, 90));

            return;
        }

        var upness = Mathf.Abs(Mathf.Max(0, Vector3.Dot(hit.normal, Vector3.up) - 0.5f)) / 0.5f;
        var toCamera = camera.position - hit.point;
        var projectedToCamera = Vector3.ProjectOnPlane(toCamera, hit.normal).normalized;
        var forward = Quaternion.AngleAxis(90, hit.normal) * projectedToCamera;
        var calculatedRotation = Quaternion.LookRotation(forward, hit.normal);
        var finalRotation =
            Quaternion.Lerp(Quaternion.Euler(0, calculatedRotation.eulerAngles.y, calculatedRotation.eulerAngles.z),
                calculatedRotation, upness);

        transform.SetPositionAndRotation(hit.point, finalRotation);
        sprite.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.one, hit.distance * 0.33f);
        sprite.localPosition = Vector3.zero;
    }
}