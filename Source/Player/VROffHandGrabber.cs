using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RepoXR.Input;
using RepoXR.Managers;
using RepoXR.UI;
using UnityEngine;

namespace RepoXR.Player;

/// <summary>
/// Drives a second, off-hand <see cref="PhysGrabber"/> so both hands can grab objects independently.
///
/// Only spun up in singleplayer/offline sessions (<see cref="SemiFunc.IsMultiplayer"/> is false), since
/// PhysGrabber's grab state is broadcast over Photon RPCs tied to the player's single PhotonView. In singleplayer
/// the game calls the *_RPC methods directly instead of dispatching them, so a second grabber sharing that view is
/// harmless; in a real session both grabbers would resolve to the same view and stomp on each other's grab state.
///
/// The second grabber is built onto a bare GameObject of our own rather than by cloning the player's. An earlier
/// version did clone it, and because the grabber shares its GameObject and neighbourhood with other player
/// components, waking those duplicates up tore down the originals - the arms, flashlight and map tool all went null
/// the instant the clone appeared. Everything this class creates now lives under one object it owns outright, so
/// nothing of the player's is duplicated and teardown is total.
/// </summary>
public class VROffHandGrabber : MonoBehaviour
{
    public static VROffHandGrabber? Instance { get; private set; }

    /// <summary>
    /// Whether an off-hand grabber is allowed to exist right now
    /// </summary>
    public static bool Available => VRSession.InVR && Plugin.Config.TwoHandedGrab.Value && !SemiFunc.IsMultiplayer();

    /// <summary>
    /// The off-hand grabber, if one is currently active
    /// </summary>
    public static PhysGrabber? ActiveGrabber =>
        Instance is { Grabber: { enabled: true } grabber } ? grabber : null;

    public PhysGrabber Grabber { get; private set; } = null!;

    /// <summary>
    /// The player's normal, dominant-hand grabber
    /// </summary>
    public PhysGrabber Primary => primaryGrabber;

    public bool GrabHeld { get; private set; }
    public bool GrabDown { get; private set; }

    private PhysGrabber primaryGrabber = null!;
    private List<GameObject> owned = [];
    private int lastPollFrame = -1;

    private PhysGrabObject? offHandHeld;
    private PhysGrabObject? primaryHeld;

    /// <summary>
    /// The controller that drives a given grabber. Use this anywhere a hand transform is needed and the grabber is
    /// known - resolving to MainHand unconditionally is what made the off hand steer carts from the wrong place.
    /// </summary>
    public static Transform HandFor(PhysGrabber? grabber)
    {
        var player = VRSession.Instance.Player;

        return Instance is { } offHand && grabber && grabber == offHand.Grabber
            ? player.SecondaryHand
            : player.MainHand;
    }

    /// <summary>
    /// The controller most likely doing the grabbing, for the cases where the game only tells us which player is
    /// involved and not which grabber. Resolves to the off hand only while it is the sole hand holding anything.
    /// </summary>
    public static Transform ActiveHand
    {
        get
        {
            var player = VRSession.Instance.Player;

            if (ActiveGrabber is not { grabbed: true })
                return player.MainHand;

            return PhysGrabber.instance && PhysGrabber.instance.grabbed ? player.MainHand : player.SecondaryHand;
        }
    }

    /// <summary>
    /// Whether the off hand is holding this object and the dominant hand isn't
    /// </summary>
    public static bool HeldByOffHandOnly(PhysGrabObject item)
    {
        return Holds(ActiveGrabber, item) && !Holds(PhysGrabber.instance, item);
    }

    /// <summary>
    /// Whether both hands are carrying this object at once
    /// </summary>
    public static bool HeldByBothHands(PhysGrabObject item)
    {
        return Holds(ActiveGrabber, item) && Holds(PhysGrabber.instance, item);
    }

    private static bool Holds(PhysGrabber? grabber, PhysGrabObject item)
    {
        return grabber && grabber!.grabbed && grabber.grabbedPhysGrabObject == item;
    }

    /// <summary>
    /// Create the off-hand grabber, if the current session allows for one
    /// </summary>
    public static void Create()
    {
        if (Instance != null || !Available || VRSession.Instance is not { } session)
            return;

        if (PlayerController.instance is not { } controller ||
            controller.playerAvatarScript is not { physGrabber: { } primary })
            return;

        try
        {
            Build(session, primary);
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to create the off-hand grabber, turning two-handed grabbing off: {exception}");

            Teardown();

            Plugin.Config.TwoHandedGrab.Value = false;
        }
    }

    private static void Build(VRSession session, PhysGrabber primary)
    {
        Logger.LogDebug($"Off-hand grabber: primary grabber lives at {GetPath(primary.transform)}");

        if (primary.physGrabBeamComponent)
            Logger.LogDebug($"Off-hand grabber: beam lives at {GetPath(primary.physGrabBeamComponent.transform)}");

        // Built inactive so that every reference can be fixed up before any Awake runs
        var host = new GameObject("RepoXR Off-Hand Grabber") { layer = primary.gameObject.layer };
        host.SetActive(false);
        host.transform.SetParent(primary.transform.parent, false);
        host.transform.SetPositionAndRotation(primary.transform.position, primary.transform.rotation);

        var grabber = host.AddComponent<PhysGrabber>();

        CopyFields(primary, grabber);

        grabber.playerAvatar = primary.playerAvatar;
        grabber.photonView = primary.photonView;
        grabber.playerCamera = primary.playerCamera;
        grabber.isLocal = true;

        // Each copy is made as a *sibling of its original*, keeping the exact parent it had on the dominant hand.
        // Parenting them under our host instead put them in a different frame, and PhysGrabber does enough
        // parent-relative maths on its grab points that the held object then tracked the body rather than the hand.
        var beamRoot = primary.physGrabBeamComponent ? primary.physGrabBeamComponent.transform : null;
        Transform? beamClone = null;

        // Every (original, copy) pair we make, so that any other field still pointing into the original can be
        // redirected into our copy afterwards
        var cloned = new List<(Transform original, Transform copy)>();

        // Copies live outside our own hierarchy now, so they have to be cleaned up by hand on teardown
        var owned = new List<GameObject>();

        if (primary.physGrabBeamComponent is { } sourceBeam)
        {
            var copy = Instantiate(sourceBeam.gameObject, sourceBeam.transform.parent, true);
            copy.name = "PhysGrabBeam (Off-Hand)";

            beamClone = copy.transform;
            cloned.Add((sourceBeam.transform, beamClone));
            owned.Add(copy);

            grabber.physGrabBeamComponent = copy.GetComponent<PhysGrabBeam>();
            grabber.physGrabBeamScript = grabber.physGrabBeamComponent;
            grabber.physGrabBeam = copy;

            Logger.LogDebug($"Off-hand grabber: beam copy parented to {GetPath(copy.transform.parent)}");
        }

        grabber.physGrabPoint = Detach(primary.physGrabPoint, nameof(primary.physGrabPoint));
        grabber.physGrabPointPuller = Detach(primary.physGrabPointPuller, nameof(primary.physGrabPointPuller));
        grabber.physGrabPointPlane = Detach(primary.physGrabPointPlane, nameof(primary.physGrabPointPlane));

        // Anything else the grabber points at that lives inside something we copied (the grab point visuals, the
        // rotation and grid transforms, ...) has to follow, otherwise both grabbers drive the dominant hand's
        // visuals and they fight over them
        RemapFields(grabber, cloned);

        if (grabber.physGrabBeamComponent is { } beam)
        {
            beam.playerAvatar = primary.playerAvatar;
            beam.PhysGrabPoint = grabber.physGrabPoint;
            beam.PhysGrabPointPuller = grabber.physGrabPointPuller;
            beam.PhysGrabPointOrigin = session.Player.SecondaryHand;
            beam.PhysGrabPointOriginLocal = session.Player.SecondaryHand;
        }

        var offHand = host.AddComponent<VROffHandGrabber>();
        offHand.Grabber = grabber;
        offHand.primaryGrabber = primary;
        offHand.owned = owned;

        Instance = offHand;

        host.SetActive(true);

        // PhysGrabber.Start() claims the shared static as soon as it wakes; hand it straight back
        PhysGrabber.instance = primary;

        offHand.StartCoroutine(offHand.VerifyPlayerIntact(session));

        Crosshair.CreateOffHand();

        OffHandControls.WarnAboutConflicts();

        Logger.LogInfo("Created off-hand grabber");

        return;

        Transform Detach(Transform original, string field)
        {
            if (!original)
                return original;

            Logger.LogDebug($"Off-hand grabber: {field} lives at {GetPath(original)}");

            // Lives under the beam we just copied, so an equivalent already exists inside that copy
            if (beamRoot && beamClone && original.IsChildOf(beamRoot!))
                return Remap(beamRoot!, beamClone!, original);

            var copy = Instantiate(original.gameObject, original.parent, true);
            copy.name = $"{original.name} (Off-Hand)";

            cloned.Add((original, copy.transform));
            owned.Add(copy);

            return copy.transform;
        }
    }

    /// <summary>
    /// Redirect every grabber field that still points somewhere inside an original we copied at the corresponding
    /// object inside our copy
    /// </summary>
    private static void RemapFields(PhysGrabber target, List<(Transform original, Transform copy)> cloned)
    {
        foreach (var field in typeof(PhysGrabber).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsInitOnly || field.IsLiteral)
                continue;

            try
            {
                var current = field.GetValue(target) switch
                {
                    Transform transform => transform,
                    GameObject gameObject => gameObject.transform,
                    Component component => component.transform,
                    _ => null
                };

                if (!current)
                    continue;

                foreach (var (original, copy) in cloned)
                {
                    // Already ours, or unrelated
                    if (!current!.IsChildOf(original))
                        continue;

                    var mapped = Remap(original, copy, current);

                    object? value = field.FieldType == typeof(Transform) ? mapped :
                        field.FieldType == typeof(GameObject) ? mapped.gameObject :
                        mapped.GetComponent(field.FieldType);

                    if (value != null)
                        field.SetValue(target, value);

                    break;
                }
            }
            catch (Exception exception)
            {
                Logger.LogDebug($"Off-hand grabber: couldn't remap field {field.Name} ({exception.GetType().Name})");
            }
        }
    }

    /// <summary>
    /// Copy the primary grabber's serialized state onto the off-hand one.
    ///
    /// Collections are rebuilt rather than shared, and anything describing what is currently being held is cleared,
    /// so the off hand starts empty instead of believing it is already holding the dominant hand's object.
    /// </summary>
    private static void CopyFields(PhysGrabber source, PhysGrabber target)
    {
        foreach (var field in typeof(PhysGrabber).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsInitOnly || field.IsLiteral)
                continue;

            try
            {
                field.SetValue(target, field.GetValue(source));
            }
            catch (Exception exception)
            {
                Logger.LogDebug($"Off-hand grabber: skipped field {field.Name} ({exception.GetType().Name})");
            }
        }

        target.physGrabPointVisualGridObjects = [];
        target.discoverSkipList = [];

        target.grabbed = false;
        target.grabbedPhysGrabObject = null;
        target.grabbedObject = null;
        target.grabbedObjectTransform = null;
        target.grabbedStaticGrabObject = null;
        target.grabbedPhysGrabObjectCollider = null;
    }

    /// <summary>
    /// A second grabber has broken the player's rig before, so check a frame later that everything critical is
    /// still alive. If it isn't, back out and turn the setting off rather than leaving an unplayable session.
    /// </summary>
    private IEnumerator VerifyPlayerIntact(VRSession session)
    {
        yield return null;

        var rig = session.Player.Rig;

        var intact = rig && rig.head && rig.leftHandTip && rig.rightHandTip &&
                     PlayerController.instance && PlayerController.instance.playerAvatarScript &&
                     PlayerController.instance.playerAvatarScript.playerAvatarVisuals &&
                     FlashlightController.Instance;

        if (intact)
            yield break;

        Logger.LogError(
            "Off-hand grabber: the player rig was damaged by creating a second grabber, tearing it back down and " +
            "turning two-handed grabbing off. Please report this along with your log.");

        Teardown();

        Plugin.Config.TwoHandedGrab.Value = false;
    }

    /// <summary>
    /// Tear down the off-hand grabber, releasing whatever it was holding
    /// </summary>
    public static void Teardown()
    {
        if (Instance == null)
            return;

        var instance = Instance;
        Instance = null;

        Crosshair.DestroyOffHand();

        // Drop whatever the off hand was holding rather than leaving the object stuck to a destroyed grabber
        if (instance.Grabber && instance.Grabber.grabbed)
            instance.Grabber.ReleaseObject(-1);

        instance.ClearGrabberLists();

        if (PhysGrabber.instance == instance.Grabber && instance.primaryGrabber)
            PhysGrabber.instance = instance.primaryGrabber;

        Destroy(instance.gameObject);
    }

    /// <summary>
    /// Make sure the off-hand grabber matches the current session and configuration
    /// </summary>
    public static void Refresh()
    {
        if (Available)
            Create();
        else
            Teardown();
    }

    /// <summary>
    /// Resolve the transform inside <paramref name="cloneRoot"/> that corresponds to <paramref name="original"/>
    /// under <paramref name="root"/>. Instantiate preserves hierarchy exactly, so sibling indices map one to one.
    /// </summary>
    private static Transform Remap(Transform root, Transform cloneRoot, Transform original)
    {
        var path = new List<int>();

        for (var transform = original; transform != root; transform = transform.parent)
            path.Add(transform.GetSiblingIndex());

        path.Reverse();

        return path.Aggregate(cloneRoot, (current, index) => current.GetChild(index));
    }

    private static string GetPath(Transform transform)
    {
        var path = transform.name;

        for (var parent = transform.parent; parent; parent = parent.parent)
            path = $"{parent.name}/{path}";

        return path;
    }

    /// <summary>
    /// Temporarily suspend the off-hand grabber, used while the off hand is busy with something else (like the map)
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (!enabled && Grabber && Grabber.grabbed)
            Grabber.ReleaseObject(-1);

        if (Grabber)
            Grabber.enabled = enabled;

        if (Grabber && Grabber.physGrabBeamComponent)
            Grabber.physGrabBeamComponent.gameObject.SetActive(enabled);
    }

    /// <summary>
    /// Keep each held object's list of grabbers matching which hands are actually holding it.
    ///
    /// PhysGrabObject learns who is grabbing it through RPCs keyed on the grabbing player's PhotonView
    /// (GrabPlayerAddRPC, GrabStartedRPC, GrabLinkRPC all take a view id). Both of our grabbers share the player's
    /// single view on purpose, so when the second hand grabs, the game resolves that view, finds the grabber
    /// already listed, and playerGrabbing stays one entry long - the object never learns there is a second hand on
    /// it, and just follows whichever registered first.
    ///
    /// The list holds PhysGrabber instances though, and the physics reads it directly, so putting the real second
    /// grabber into it is enough to make the object respond to both grab points the way it does for two players.
    /// </summary>
    private void ReconcileGrabberLists()
    {
        offHandHeld = Reconcile(Grabber, offHandHeld);
        primaryHeld = Reconcile(primaryGrabber, primaryHeld);
    }

    private static PhysGrabObject? Reconcile(PhysGrabber grabber, PhysGrabObject? previous)
    {
        var current = grabber && grabber.enabled && grabber.grabbed ? grabber.grabbedPhysGrabObject : null;

        // Let go of whatever this hand was holding before
        if (previous && previous != current)
            previous!.playerGrabbing.Remove(grabber);

        if (current && !current!.playerGrabbing.Contains(grabber))
            current.playerGrabbing.Add(grabber);

        return current;
    }

    /// <summary>
    /// Take this grabber back out of whatever it was holding, so a destroyed grabber is never left in an object's
    /// list where the physics would keep pulling towards it
    /// </summary>
    private void ClearGrabberLists()
    {
        if (offHandHeld && Grabber)
            offHandHeld!.playerGrabbing.Remove(Grabber);

        offHandHeld = null;
        primaryHeld = null;
    }

    /// <summary>
    /// Keep our host object sitting exactly where the dominant grabber's object sits.
    ///
    /// The host is created once and parented alongside the primary grabber, but the primary's own transform is
    /// driven every frame - so without this we sit at a stale pose forever. Anything PhysGrabber computes relative
    /// to its own transform (its grab point and puller among them) is then frozen in that stale frame, which leaves
    /// a grabbed object springing towards a fixed point in the world instead of towards the hand: held, but immovable.
    ///
    /// Driven from the patch prefixes rather than from Update so that it always lands before the grabber runs.
    /// </summary>
    internal void SyncTransform()
    {
        if (!primaryGrabber)
            return;

        var source = primaryGrabber.transform;

        if (source.parent == transform.parent)
        {
            transform.localPosition = source.localPosition;
            transform.localRotation = source.localRotation;
            transform.localScale = source.localScale;

            return;
        }

        transform.SetPositionAndRotation(source.position, source.rotation);
    }

    /// <summary>
    /// Sample the off-hand grab control.
    ///
    /// Driven from the off-hand PhysGrabber.Update prefix rather than from our own Update, since script execution
    /// order between two components on the same GameObject is undefined and the grabber must never read a stale
    /// "was pressed this frame" edge.
    /// </summary>
    internal void PollInput()
    {
        if (lastPollFrame == Time.frameCount)
            return;

        lastPollFrame = Time.frameCount;

        var grab = OffHandControls.Grab;

        GrabHeld = grab?.IsPressed() ?? false;
        GrabDown = grab?.WasPressedThisFrame() ?? false;
    }

    private void Update()
    {
        // If someone joins mid-session, or two-handed grabbing gets turned off, tear ourselves down
        if (!Available)
        {
            Teardown();
            return;
        }

        // The dominant hand's crosshair may not have existed yet when we were built, so the off-hand reticle is
        // created here rather than only once at startup
        if (Crosshair.offHandInstance == null)
            Crosshair.CreateOffHand();

        // The grabber may re-derive these itself on its own Start(); keep them pinned to be safe
        Grabber.playerAvatar = primaryGrabber.playerAvatar;
        Grabber.photonView = primaryGrabber.photonView;
        Grabber.isLocal = true;

        // Guard against the off hand stealing the shared static "local grabber" reference
        PhysGrabber.instance = primaryGrabber;

        ReconcileGrabberLists();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (PhysGrabber.instance == Grabber && primaryGrabber)
            PhysGrabber.instance = primaryGrabber;

        ClearGrabberLists();

        // The beam and grab point copies sit next to the originals rather than under us, so they don't get
        // destroyed along with this object
        foreach (var copy in owned)
            if (copy)
                Destroy(copy);

        owned.Clear();
    }
}
