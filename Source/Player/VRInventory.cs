using System.Collections.Generic;
using HarmonyLib;
using RepoXR.Input;
using RepoXR.Managers;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace RepoXR.Player;

/// <summary>
/// Body-anchored holster inventory.
///
/// The slots sit at fixed places around the player's waist (left hip, right hip, small of the back) on a heading
/// that only re-orients once you actually turn, so they stay put while you look around and can be reached for
/// without looking at them. Everything is driven off how close a *controller* is to a holster - never off aim
/// direction, the grab beam, or collider overlap.
/// </summary>
public class VRInventory : MonoBehaviour
{
    /// <summary>
    /// Where each holster sits relative to the player's waist, in body space (X right, Z forward). The first three
    /// are the layout proper; the rest only matter if the game ever hands us more than three inventory spots.
    /// </summary>
    /// <summary>
    /// Each holster's place in body space (X right, Z forward), whether it hangs off the back rather than the
    /// waist, and how forgiving its reach is relative to the configured radius.
    ///
    /// The back holster sits just right of the spine rather than out beside the shoulder, so it is found by putting
    /// a hand behind your back the way most VR games expect. That is a blind reach, so it gets a wider radius than
    /// the hips, which you can glance down at.
    /// </summary>
    private static readonly (Vector3 offset, bool back, float reach)[] HolsterLayout =
    [
        (new Vector3(-0.26f, 0f, 0.02f), false, 1f), // Left hip
        (new Vector3(0.26f, 0f, 0.02f), false, 1f), // Right hip
        (new Vector3(0.09f, 0f, -0.20f), true, 1.6f), // Behind the back, just right of the spine
        (new Vector3(-0.09f, 0f, -0.20f), true, 1.6f), // Behind the back, just left of the spine
        (new Vector3(-0.30f, 0f, 0.24f), false, 1f), // Front left
        (new Vector3(0.30f, 0f, 0.24f), false, 1f) // Front right
    ];

    /// <summary>
    /// Storing gets a slightly wider radius than retrieving, since your hand sits further from the holster when
    /// there's an object hanging off the end of the grab beam
    /// </summary>
    private const float StoreRadiusScale = 1.4f;

    /// <summary>
    /// How long a holster stays latched after your hand leaves it, so that letting go just outside still stores
    /// </summary>
    private const float HoverStickyTime = 0.3f;

    /// <summary>
    /// How long to wait for an equip to actually land before treating it as rejected. Generous on purpose: the
    /// equip animation itself takes ~0.4s, and a missing buzz is far less confusing than a wrong one.
    /// </summary>
    private const float EquipConfirmTime = 1.2f;

    /// <summary>
    /// How far you have to turn before the holsters re-orient to your new heading
    /// </summary>
    private const float BodyYawDeadzone = 45f;

    public Transform visualsTransform;

    [SerializeField] protected VRInventorySlot[] slots;

    [SerializeField] protected Color holdColor;
    [SerializeField] protected Color hoverColor;
    [SerializeField] protected Color equippedColor;

    public VRRig rig;

    private bool holdingItem;

    // Storing

    private int storeSlot = -1;
    private int stickySlot = -1;
    private float stickyTimer;

    private int pendingEquipSlot = -1;
    private float pendingEquipTimer;
    private HapticManager.Hand pendingEquipHand;

    // Retrieving

    private PhysGrabber? retrieveGrabber;
    private ItemEquippable? retrieveItem;
    private float retrieveTimer;

    private int dominantReachSlot = -1;
    private int offHandReachSlot = -1;

    // Body anchor

    private float bodyYaw;
    private bool bodyYawValid;

    private static Transform DominantHand => VRSession.Instance.Player.MainHand;
    private static Transform OffHand => VRSession.Instance.Player.SecondaryHand;

    private static float Radius => Plugin.Config.HolsterReach.Value;

    private void Update()
    {
        TickTimers();

        UpdateBodyAnchor();

        holdingItem = FindHoldingGrabber() != null;

        UpdateStoreTarget();
        UpdateReachTargets();
        UpdateSlotVisuals();
    }

    private void LateUpdate()
    {
        // Re-applied after everything else has moved for the frame, so the holsters don't trail the player by a
        // frame. Deliberately does not touch the slots' own local positions, which VRInventorySlot.LateUpdate owns.
        UpdateBodyAnchor();
    }

    private void TickTimers()
    {
        if (stickyTimer > 0)
            stickyTimer -= Time.deltaTime;

        if (retrieveTimer > 0)
            retrieveTimer -= Time.deltaTime;

        if (pendingEquipTimer <= 0)
            return;

        pendingEquipTimer -= Time.deltaTime;

        // The equip never landed (spot cooldown, collision check, item state, ...). The base game fails these
        // silently, which in VR just reads as "the inventory didn't work", so at least buzz about it
        if (pendingEquipTimer <= 0)
        {
            HapticManager.Impulse(pendingEquipHand, HapticManager.Type.Impulse, 0.15f, 0.25f);
            pendingEquipSlot = -1;
        }
    }

    /// <summary>
    /// Park the holster rig at the player's waist, on a heading that lags behind where they are looking.
    ///
    /// The pose is rebuilt from scratch every frame rather than adjusted in place - an earlier version rotated the
    /// visuals relative to their own current rotation, which compounded every frame and span the holsters.
    /// </summary>
    private void UpdateBodyAnchor()
    {
        if (rig.head is not { } head)
            return;

        var headYaw = GetHeading(head);

        if (!bodyYawValid)
        {
            bodyYaw = headYaw;
            bodyYawValid = true;
        }

        var delta = Mathf.DeltaAngle(bodyYaw, headYaw);

        // Inside the deadzone the holsters do not move at all; outside it they ease along until comfortably back
        // within it, which is what makes their position learnable
        if (Mathf.Abs(delta) > BodyYawDeadzone)
            bodyYaw = Mathf.LerpAngle(bodyYaw, headYaw - Mathf.Sign(delta) * BodyYawDeadzone * 0.5f,
                8 * Time.deltaTime);

        var rotation = Quaternion.Euler(0, bodyYaw, 0);
        var waist = head.position + Vector3.up * Plugin.Config.HolsterHeight.Value;

        // The back holster is anchored to the head rather than the waist, so tuning how low the hips sit does not
        // drag the behind-the-back reach along with it
        var back = head.position + Vector3.up * Plugin.Config.BackHolsterHeight.Value;

        // Kept in step with the holsters because ItemUnequipNoTeleport measures the inventory's height against the
        // floor through it
        visualsTransform.SetPositionAndRotation(waist, rotation);
        visualsTransform.localScale = Vector3.one;

        for (var i = 0; i < slots.Length; i++)
        {
            var holster = Holster(i);

            slots[i].SetAnchor((holster.back ? back : waist) + rotation * holster.offset, rotation);
        }
    }

    /// <summary>
    /// Where a holster sits, what it hangs off, and how forgiving its reach is
    /// </summary>
    private (Vector3 offset, bool back, float reach) Holster(int index)
    {
        if (index < HolsterLayout.Length)
            return HolsterLayout[index];

        // More spots than we have a designed place for - ring the rest around the waist
        return (Quaternion.Euler(0, index * 360f / slots.Length, 0) * new Vector3(0, 0, -0.3f), false, 1f);
    }

    /// <summary>
    /// Which way the player is facing, from the head's forward flattened onto the floor plane.
    ///
    /// Reading eulerAngles.y directly is not safe here: looking straight up or down flips it by 180 degrees, which
    /// would whip the holsters around behind the player.
    /// </summary>
    private static float GetHeading(Transform head)
    {
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);

        // Looking (near enough) straight up or down - the top of the head still points somewhere useful
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(head.up * -Mathf.Sign(head.forward.y), Vector3.up);

        if (forward.sqrMagnitude < 0.001f)
            return 0;

        return Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
    }

    /// <summary>
    /// Work out which holster the player is about to drop the item they are holding into
    /// </summary>
    private void UpdateStoreTarget()
    {
        var previous = storeSlot;
        var hand = HapticManager.Hand.Dominant;

        storeSlot = IsHolding(PhysGrabber.instance)
            ? FindHolster(DominantHand.position, StoreRadiusScale, true)
            : -1;

        if (storeSlot < 0 && VROffHandGrabber.ActiveGrabber is { } offHand && IsHolding(offHand))
        {
            storeSlot = FindHolster(OffHand.position, StoreRadiusScale, true);
            hand = HapticManager.Hand.Secondary;
        }

        if (storeSlot > -1)
        {
            stickySlot = storeSlot;
            stickyTimer = HoverStickyTime;
        }

        if (storeSlot > -1 && storeSlot != previous)
            HapticManager.Impulse(hand, HapticManager.Type.Impulse, 0.12f, 0.05f);
    }

    /// <summary>
    /// Work out which holster each hand is reaching into, and act on the grab press
    /// </summary>
    private void UpdateReachTargets()
    {
        if (SemiFunc.RunIsArena() || PlayerController.instance.InputDisableTimer > 0)
        {
            dominantReachSlot = offHandReachSlot = -1;
            return;
        }

        var dominant = FindHolster(DominantHand.position, 1f, false);
        var offHand = FindHolster(OffHand.position, 1f, false);

        // Small buzz on approach, so a holster can be found without looking down at it
        if (dominant > -1 && dominant != dominantReachSlot)
            HapticManager.Impulse(HapticManager.Hand.Dominant, HapticManager.Type.Impulse, 0.12f, 0.05f);

        if (offHand > -1 && offHand != offHandReachSlot)
            HapticManager.Impulse(HapticManager.Hand.Secondary, HapticManager.Type.Impulse, 0.12f, 0.05f);

        dominantReachSlot = dominant;
        offHandReachSlot = offHand;

        if (dominant > -1 && Actions.Instance["Grab"].WasPressedThisFrame())
            Retrieve(dominant, PhysGrabber.instance, HapticManager.Hand.Dominant);
        else if (offHand > -1 && (OffHandControls.Grab?.WasPressedThisFrame() ?? false))
            // Without two-handed grabbing there is no off-hand grabber to deliver the item to, so it goes to the
            // dominant hand instead - reaching with the off hand still works, it just hands the item over
            Retrieve(offHand, VROffHandGrabber.ActiveGrabber ?? PhysGrabber.instance, HapticManager.Hand.Secondary);
    }

    /// <summary>
    /// Find the holster nearest to a controller, within <paramref name="radius"/> metres of it.
    ///
    /// Straight point-to-point proximity - no raycasts, no collider overlap, no aim direction involved.
    /// </summary>
    private int FindHolster(Vector3 hand, float scale, bool wantEmpty)
    {
        var best = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];

            if (!slot.spot || !slot.spot.isActiveAndEnabled)
                continue;

            // Storing needs a free holster, retrieving needs an occupied one
            if (wantEmpty ? slot.heldItem : !slot.heldItem)
                continue;

            // Checked against each holster's own radius, but ranked on raw distance, so a generous holster cannot
            // steal a hand that is sitting squarely inside a tighter one
            var distance = Vector3.Distance(slot.transform.position, hand);
            if (distance > Radius * scale * Holster(i).reach || distance >= bestDistance)
                continue;

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    private void UpdateSlotVisuals()
    {
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];

            if (!slot.spot || !slot.spot.isActiveAndEnabled)
            {
                slot.isHovered = false;
                slot.isCollided = false;
                slot.targetColor = Color.clear;

                continue;
            }

            slot.isHolding = holdingItem;
            slot.isHovered = i == storeSlot;
            slot.isCollided = i == dominantReachSlot || i == offHandReachSlot;

            if (slot.isHovered || slot.isCollided)
                slot.targetColor = hoverColor;
            else if (holdingItem && !slot.heldItem)
                slot.targetColor = holdColor;
            else if (slot.heldItem)
                slot.targetColor = equippedColor;
            else
                slot.targetColor = Color.clear;
        }
    }

    private void Retrieve(int index, PhysGrabber grabber, HapticManager.Hand hand)
    {
        var slot = slots[index];

        if (!slot.heldItem || !slot.spot || !slot.spot.isActiveAndEnabled)
            return;

        // Remember which hand asked, so the item is delivered there instead of always to the dominant hand
        retrieveGrabber = grabber;
        retrieveItem = slot.heldItem;
        retrieveTimer = 1f;

        slot.spot.HandleInput();

        HapticManager.Impulse(hand, HapticManager.Type.Impulse, 0.3f, 0.08f);
    }

    /// <summary>
    /// Which grabber a retrieval should deliver its item to. Falls back to the dominant grabber when the item
    /// wasn't pulled out of a holster by hand (a forced unequip, for example).
    /// </summary>
    internal PhysGrabber RetrieveGrabberFor(ItemEquippable? item)
    {
        if (retrieveTimer > 0 && retrieveGrabber && (item == null || retrieveItem == item))
            return retrieveGrabber!;

        return PhysGrabber.instance;
    }

    /// <summary>
    /// Called when a grabber lets go of an equippable item, to store it in whichever holster it was released into
    /// </summary>
    public void TryEquipItem(PhysGrabber grabber, ItemEquippable item)
    {
        // Letting go with one hand while the other is still carrying it isn't a "put it away" gesture. This runs
        // from a ReleaseObject prefix, so the releasing grabber still reports the item as held - both hands being
        // on it here means the other hand keeps it.
        if (item.physGrabObject && VROffHandGrabber.HeldByBothHands(item.physGrabObject))
            return;

        // Resolved fresh rather than reusing the value from Update(): this runs from a ReleaseObject prefix, and
        // script execution order between this component and PhysGrabber is undefined
        var target = FindHolster(VROffHandGrabber.HandFor(grabber).position, StoreRadiusScale, true);

        // Letting go just outside a holster still counts, as long as your hand was in it a moment ago
        if (target < 0 && stickyTimer > 0 && stickySlot > -1 && !slots[stickySlot].heldItem)
            target = stickySlot;

        if (target < 0)
            return;

        var spot = slots[target].spot;
        if (!spot || !spot.isActiveAndEnabled || spot.currentState != InventorySpot.SpotState.Empty)
            return;

        var hand = VROffHandGrabber.Instance is { } offHand && grabber == offHand.Grabber
            ? HapticManager.Hand.Secondary
            : HapticManager.Hand.Dominant;

        pendingEquipSlot = target;
        pendingEquipTimer = EquipConfirmTime;
        pendingEquipHand = hand;

        stickyTimer = 0;

        // The dominant hand goes through the game's own input handler so that its cooldown and validation stay
        // intact. The off hand can't: InventorySpot re-derives the item from the *dominant* grabber, so it would
        // either store the wrong item or nothing at all - it gets told explicitly what to equip instead.
        if (grabber == PhysGrabber.instance)
            spot.HandleInput();
        else
            item.RequestEquip(spot.inventorySpotIndex);
    }

    public void EquipItem(ItemEquippable item)
    {
        var slotIndex = item.equippedSpot.inventorySpotIndex;
        var slot = slots[slotIndex];

        if (GameDirector.instance.currentState != GameDirector.gameState.Main)
            item.transform.localScale = Vector3.one * 0.1667f * 2; // During state changes, the item is reduced in size by 50%, so double the current scale

        slot.heldItem = item;

        item.transform.parent = slot.transform;
        item.rb.interpolation = RigidbodyInterpolation.None;
        item.gameObject.SetLayerRecursively(6);

        // Holsters sit against the player's body now, so a stowed item is physically inside them
        slot.hurtColliders = item.GetComponentsInChildren<HurtCollider>(true);
        SetStowed(item, true);

        // Disable shadows
        item.GetComponentsInChildren<MeshRenderer>().Do(mesh => mesh.shadowCastingMode = ShadowCastingMode.Off);

        // Confirm the equip that was requested, so it doesn't get reported as rejected
        if (pendingEquipSlot != slotIndex)
            return;

        pendingEquipSlot = -1;
        pendingEquipTimer = 0;

        HapticManager.Impulse(pendingEquipHand, HapticManager.Type.Impulse, 0.4f, 0.06f);
    }

    public void UnequipItem(ItemEquippable item)
    {
        var slot = slots[item.equippedSpot.inventorySpotIndex];

        slot.heldItem = null;
        slot.hurtColliders = [];

        SetStowed(item, false);

        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.parent = GameObject.Find("Level Generator/Items")?.transform;
        item.rb.interpolation = RigidbodyInterpolation.Interpolate;
        item.gameObject.SetLayerRecursively(16);
        item.enabled = true;

        RetrieveGrabberFor(item).OverrideGrab(item.physGrabObject, 0.25f);

        // Re-enable shadows
        item.GetComponentsInChildren<MeshRenderer>().Do(mesh => mesh.shadowCastingMode = ShadowCastingMode.On);
    }

    /// <summary>
    /// Stop a stowed item from shoving or hurting the player.
    ///
    /// The old slots floated out in front of the player, so an equipped item never physically overlapped them. The
    /// holsters sit on the body, so it now does: its colliders have to stop interacting with the player's, and its
    /// hurt boxes have to go away entirely - the base game only ever disabled the melee one, and ItemMelee's own
    /// state machine happily switches it back on while the item is stowed.
    /// </summary>
    private static void SetStowed(ItemEquippable item, bool stowed)
    {
        var players = GetPlayerColliders();

        foreach (var collider in item.GetComponentsInChildren<Collider>(true))
        {
            if (!collider)
                continue;

            foreach (var player in players)
                if (player)
                    Physics.IgnoreCollision(collider, player, stowed);
        }

        foreach (var hurt in item.GetComponentsInChildren<HurtCollider>(true))
            if (hurt)
                hurt.gameObject.SetActive(!stowed);
    }

    private static Collider[] GetPlayerColliders()
    {
        var colliders = new List<Collider>();

        if (PlayerController.instance is { col: { } col } && col)
            colliders.Add(col);

        if (PlayerCollision.instance is { } collision)
        {
            if (collision.StandCollision)
                colliders.AddRange(collision.StandCollision.GetComponentsInChildren<Collider>(true));

            if (collision.CrouchCollision)
                colliders.AddRange(collision.CrouchCollision.GetComponentsInChildren<Collider>(true));
        }

        return colliders.ToArray();
    }

    public void PingSlot(int index, float amount, float frequency, float time)
    {
        slots[index].Ping(amount, frequency, time);
    }

    public void HideSlots()
    {
        slots.Do(slot => slot.Hide());
    }

    private static bool IsHolding(PhysGrabber? grabber)
    {
        return grabber && grabber!.enabled && grabber.grabbed && grabber.grabbedPhysGrabObject is { } held &&
               held.GetComponent<ItemEquippable>();
    }

    /// <summary>
    /// The grabber currently holding an equippable item, if any. Checks the off-hand grabber too, so that items
    /// held in the off hand can be stored just like items held in the dominant hand.
    /// </summary>
    private static PhysGrabber? FindHoldingGrabber()
    {
        if (SemiFunc.RunIsArena() || PlayerController.instance.InputDisableTimer > 0)
            return null;

        if (IsHolding(PhysGrabber.instance))
            return PhysGrabber.instance;

        if (VROffHandGrabber.ActiveGrabber is { } offHand && IsHolding(offHand))
            return offHand;

        return null;
    }
}
