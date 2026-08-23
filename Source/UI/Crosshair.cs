using RepoXR.Managers;
using RepoXR.Player;
using UnityEngine;

namespace RepoXR.UI;

public class Crosshair : MonoBehaviour
{
    public static Crosshair instance;

    /// <summary>
    /// The off hand's own reticle, which only exists while two-handed grabbing is on
    /// </summary>
    public static Crosshair? offHandInstance;

    public const int LayerMask = 1 << 0 | 1 << 9 | 1 << 10 | 1 << 16 | 1 << 20 | 1 << 23;

    /// <summary>
    /// Whether this reticle belongs to the off hand rather than the dominant one
    /// </summary>
    public bool isOffHand;

    private new Transform camera;
    private Transform sprite;

    private void Awake()
    {
        // The off-hand copy must not claim the static out from under the dominant hand's reticle
        if (instance == null)
            instance = this;

        camera = Camera.main!.transform;
        sprite = GetComponentInChildren<Aim>(true).transform;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null!;

        if (offHandInstance == this)
            offHandInstance = null;
    }

    /// <summary>
    /// Give the off hand a reticle of its own, copied from the dominant hand's finished one
    /// </summary>
    public static void CreateOffHand()
    {
        if (offHandInstance != null || instance == null)
            return;

        var copy = Instantiate(instance.gameObject, instance.transform.parent);
        copy.name = "Crosshair (Off-Hand)";

        // The overcharge ring reads the dominant grabber's charge, so it would show the wrong value out here
        foreach (var overcharge in copy.GetComponentsInChildren<OverchargeRoundBarUI>(true))
            Destroy(overcharge.gameObject);

        var crosshair = copy.GetComponent<Crosshair>();
        crosshair.isOffHand = true;

        offHandInstance = crosshair;
    }

    public static void DestroyOffHand()
    {
        if (offHandInstance == null)
            return;

        Destroy(offHandInstance.gameObject);
        offHandInstance = null;
    }

    private void Update()
    {
        if (VRSession.Instance is not { } session)
            return;

        var hand = isOffHand ? session.Player.SecondaryHand : session.Player.MainHand;
        var grabber = isOffHand ? VROffHandGrabber.ActiveGrabber : PhysGrabber.instance;

        // An off-hand reticle is meaningless without an off-hand grabber behind it
        if (isOffHand && !grabber)
        {
            transform.position = Vector3.down * 3000;
            return;
        }

        if (!Physics.Raycast(new Ray(hand.position, hand.forward), out var hit, 10, LayerMask))
        {
            transform.position = Vector3.down * 3000;
            return;
        }

        // Hide the crosshair if we are holding an item (keep overcharge though)
        if (grabber && grabber!.grabbedObject)
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