using HarmonyLib;
using RepoXR.Input;
using RepoXR.Managers;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace RepoXR.Player;

public class VRInventory : MonoBehaviour
{
    public Transform visualsTransform;
    
    [SerializeField] protected VRInventorySlot[] slots;
    
    [SerializeField] protected Color holdColor;
    [SerializeField] protected Color hoverColor;
    [SerializeField] protected Color equippedColor;

    public VRRig rig;

    private bool holdingItem;
    private int hoveredSlot = -1;
    
    private void Update()
    {
        holdingItem = IsHoldingItem();
     
        HandleHoldItem();
        HandleSlotHover();
        HandleSlotInteract();
    }

    /// <summary>
    /// The code for handling when an item is held by the player's phys grabber
    /// </summary>
    private void HandleHoldItem()
    {
        visualsTransform.localScale = Vector3.Lerp(visualsTransform.localScale, Vector3.one * (holdingItem ? 3 : 1),
            8 * Time.deltaTime);
        visualsTransform.localPosition = Vector3.Lerp(visualsTransform.localPosition,
            holdingItem ? new Vector3(0, -0.5f, 1.2f) : Vector3.zero, 8 * Time.deltaTime);
        slots.Do(slot => slot.collider.transform.localScale = Vector3.Lerp(slot.collider.transform.localScale,
            Vector3.one * (holdingItem ? 2 : 1),
            8 * Time.deltaTime));

        hoveredSlot = -1;

        if (!holdingItem || !GetBeamIntersect(out var hit))
            return;

        for (var i = 0; i < slots.Length; i++)
            if (hit.collider == slots[i].collider)
                hoveredSlot = i;
        
        return;

        static bool GetBeamIntersect(out RaycastHit hit)
        {
            var beam = PhysGrabber.instance.physGrabBeamComponent;
            var positions = new Vector3[beam.CurveResolution];
            var isOverridden = PhysGrabber.instance.overrideGrabDistance > 0;

            beam.lineRenderer.GetPositions(positions);
            
            for (var i = 0; i < positions.Length - (isOverridden ? 3 : 1); i++)
            {
                var a = positions[i];
                var b = positions[i + 1];
                var distance = Vector3.Distance(a, b) + (isOverridden ? 1 : 0);

                if (Physics.Raycast(new Ray(a, b - a), out hit, distance, 1 << 27))
                    return true;
            }

            hit = default;
            return false;
        }
    }

    private void HandleSlotHover()
    {
        slots.Do(slot =>
        {
            if (!slot.spot || !slot.spot.isActiveAndEnabled)
            {
                slot.isHovered = false;
                slot.targetColor = Color.clear;
                
                return;
            }

            slot.isHolding = holdingItem;
            slot.isHovered = slot.slotIndex == hoveredSlot;
            
            if (slot.slotIndex == hoveredSlot)
                slot.targetColor = hoverColor;
            else if (holdingItem)
                slot.targetColor = holdColor;
            else if (slot.heldItem)
                slot.targetColor = equippedColor;
            else
                slot.targetColor = Color.clear;
        });
    }

    private void HandleSlotInteract()
    {
        if (SemiFunc.RunIsArena() || PlayerController.instance.InputDisableTimer > 0)
            return;
        
        slots.Do(slot =>
        {
            slot.isCollided = false;
            
            if (!slot.heldItem || !slot.spot.isActiveAndEnabled)
                return;

            slot.isCollided = Utils.Collide(slot.collider,
                VRSession.IsLeftHanded ? rig.leftHandCollider : rig.rightHandCollider);
            
            if (Actions.Instance["Grab"].WasPressedThisFrame() && slot.isCollided)
                slot.spot.HandleInput();
        });
    }
    
    public void TryEquipItem(ItemEquippable item)
    {
        if (!holdingItem || hoveredSlot == -1)
            return;

        var spot = slots[hoveredSlot].spot;
        if (spot.isActiveAndEnabled)
            spot.HandleInput();
    }

    public void EquipItem(ItemEquippable item)
    {
        var slot = slots[item.equippedSpot.inventorySpotIndex];

        if (GameDirector.instance.currentState != GameDirector.gameState.Main)
            item.transform.localScale = Vector3.one * 0.1667f * 2; // During state changes, the item is reduced in size by 50%, so double the current scale

        slot.heldItem = item;
        
        item.transform.parent = slot.transform;
        item.rb.interpolation = RigidbodyInterpolation.None;
        item.gameObject.SetLayerRecursively(6);

        // Prevent getting hurt by item in inventory
        if (item.TryGetComponent<ItemMelee>(out var melee))
            melee.hurtCollider.gameObject.SetActive(false);
        
        // Disable shadows
        item.GetComponentsInChildren<MeshRenderer>().Do(mesh => mesh.shadowCastingMode = ShadowCastingMode.Off);
    }

    public void UnequipItem(ItemEquippable item)
    {
        var slot = slots[item.equippedSpot.inventorySpotIndex];

        slot.heldItem = null;

        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.parent = GameObject.Find("Level Generator/Items")?.transform;
        item.rb.interpolation = RigidbodyInterpolation.Interpolate;
        item.gameObject.SetLayerRecursively(16);
        item.enabled = true;

        PhysGrabber.instance.OverrideGrab(item.physGrabObject, 0.25f);

        // Re-enable shadows
        item.GetComponentsInChildren<MeshRenderer>().Do(mesh => mesh.shadowCastingMode = ShadowCastingMode.On);
    }

    private static bool IsHoldingItem()
    {
        if (SemiFunc.RunIsArena() || PlayerController.instance.InputDisableTimer > 0)
            return false;

        if (!PhysGrabber.instance.grabbed || PhysGrabber.instance.grabbedPhysGrabObject is not { } heldObject)
            return false;

        return heldObject.GetComponent<ItemEquippable>();
    }
}