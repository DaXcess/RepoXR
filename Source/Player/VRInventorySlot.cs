using System.Collections;
using UnityEngine;

namespace RepoXR.Player;

public class VRInventorySlot : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    
    public int slotIndex = -1;
    public LineRenderer lineRenderer;
    public new Collider collider;
    public InventorySpot spot;

    public bool isHolding;
    public bool isHovered;
    public bool isCollided;
    
    public Color targetColor = Color.clear;
    public ItemEquippable? heldItem;

    /// <summary>
    /// Hurt boxes belonging to the stowed item, kept switched off for as long as it's in the holster
    /// </summary>
    public HurtCollider[] hurtColliders = [];

    private Vector3 anchorPosition;
    private Quaternion anchorRotation;
    private bool anchored;

    /// <summary>
    /// Place this holster at a world pose.
    ///
    /// World rather than local on purpose: the slots sit under prefab parents whose own offsets and rotations this
    /// code has no business assuming anything about, and setting local positions against them put every holster in
    /// the wrong place (and two of them on top of each other). The pose is applied here in LateUpdate rather than by
    /// the caller so that it always lands before the line renderer below is rebuilt from it.
    /// </summary>
    public void SetAnchor(Vector3 position, Quaternion rotation)
    {
        anchorPosition = position;
        anchorRotation = rotation;
        anchored = true;
    }
    
    private float heightOffset;
    private Color currentColor = Color.clear;

    private float pingTime;
    private float pingAmount;
    private float pingFrequency;
    private float pingDuration;
    private float pingValue;

    private IEnumerator Start()
    {
        yield return null;
        
        spot = Inventory.instance.inventorySpots[slotIndex];
    }

    private void Update()
    {
        var lineColor = currentColor = Color.Lerp(currentColor, targetColor, 5 * Time.deltaTime);
        heightOffset = Mathf.Lerp(heightOffset, isHovered ? 0.02f : 0, 5 * Time.deltaTime);

        if (pingTime < pingDuration)
            lineColor = Color.Lerp(currentColor, Color.white, Mathf.Abs(pingValue / pingAmount));

        lineRenderer.widthMultiplier =
            Mathf.Lerp(lineRenderer.widthMultiplier, isHolding ? 0.2f : 0.1f, 5 * Time.deltaTime);

        lineRenderer.material.mainTextureOffset += Vector2.right * (Time.deltaTime * (isHolding ? 2f : 0.4f));
        lineRenderer.material.SetColor(ColorId,
            new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * 0.6f));
        lineRenderer.material.SetColor(EmissionColorId, lineColor);

        transform.localScale =
            Vector3.Lerp(transform.localScale, Vector3.one * (isCollided ? 1.3f : 1), 10 * Time.deltaTime);

        if (heldItem != null)
        {
            heldItem.transform.localPosition =
                Vector3.Lerp(heldItem.transform.localPosition, Vector3.zero, 5 * Time.deltaTime);
            heldItem.transform.localRotation =
                Quaternion.Slerp(heldItem.transform.localRotation, Quaternion.identity, 5 * Time.deltaTime);

            // The item's own state machine can switch its hurt boxes back on while it's sitting against the
            // player's body, so they get held down rather than just disabled once on equip
            foreach (var hurt in hurtColliders)
                if (hurt && hurt.gameObject.activeSelf)
                    hurt.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        var height = heightOffset;

        if (pingTime < pingDuration)
        {
            pingValue = CalculateSpringOffset(pingTime, pingAmount, pingFrequency, pingDuration);
            height += pingValue * 0.0025f;
            pingTime += Time.deltaTime;
        }

        if (anchored)
            transform.SetPositionAndRotation(anchorPosition + anchorRotation * (Vector3.up * (pingValue * 0.0025f)),
                anchorRotation);
        else
            transform.localPosition =
                new Vector3(transform.localPosition.x, pingValue * 0.0025f, transform.localPosition.z);

        // Holster mouth, lying flat in the slot's XZ plane. Sized to be a believable pouch you can see out of the
        // corner of your eye and put a controller into, rather than the small marker this used to be.
        const float halfWidth = 0.075f;
        const float halfDepth = 0.11f;

        lineRenderer.positionCount = 4;
        lineRenderer.SetPositions([
            transform.TransformPoint(new Vector3(-halfWidth, height, -halfDepth)),
            transform.TransformPoint(new Vector3(halfWidth, height, -halfDepth)),
            transform.TransformPoint(new Vector3(halfWidth, height, halfDepth)),
            transform.TransformPoint(new Vector3(-halfWidth, height, halfDepth))
        ]);
        
        if (spot != null)
        {
            spot.batteryVisualLogic.transform.position = transform.TransformPoint(Vector3.back * 0.05f);
            spot.batteryVisualLogic.transform.rotation = transform.rotation * Quaternion.Euler(90, 0, 0);
        }

        if (heldItem != null)
            heldItem.rb.interpolation = RigidbodyInterpolation.None;
    }

    public void Ping(float amount, float frequency, float time)
    {
        pingTime = 0;
        pingAmount = amount;
        pingFrequency = frequency;
        pingDuration = time;
    }

    /// <summary>
    /// Hide the slot
    /// </summary>
    public void Hide()
    {
        if (spot != null)
            spot.batteryVisualLogic.transform.position = Vector3.down * 3000;
    }

    private static float CalculateSpringOffset(float currentTime, float amount, float frequency, float duration)
    {
        var t = currentTime / duration;
        var dampedFreq = frequency * (1f - t);
        return amount * Mathf.Sin(dampedFreq * t * Mathf.PI * 2f) * (1f - t);
    }
}