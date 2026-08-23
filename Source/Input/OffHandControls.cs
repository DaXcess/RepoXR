using System;
using System.Collections.Generic;
using System.Linq;
using RepoXR.Assets;
using RepoXR.Managers;
using UnityEngine.InputSystem;

namespace RepoXR.Input;

/// <summary>
/// Adds real, rebindable off-hand controls to the VR input asset and the controls menu.
///
/// The off hand originally had no actions of its own: grabbing was inferred by mirroring "Grab" onto the opposite
/// hand, and using a held item had no off-hand control at all, so the dominant hand's "Interact" drove whatever
/// either hand was holding. Both are now proper actions the player can see and bind.
///
/// Mirroring survives only as each action's *default*, so the controls still land somewhere sensible out of the
/// box. Anything the player binds themselves is left strictly alone.
/// </summary>
public static class OffHandControls
{
    private const string MapName = "VR Actions";

    private const string GrabName = "Off-Hand Grab";
    private const string InteractName = "Off-Hand Interact";

    /// <summary>
    /// Fixed base for binding ids. Unity matches saved binding overrides by id, so these have to be stable across
    /// launches or the player's rebind would be forgotten every time the game starts.
    /// </summary>
    private const string BindingIdBase = "5ac1f00d-0000-4000-8000-";

    /// <summary>
    /// Each off-hand action, and the dominant-hand action its default binding is mirrored from
    /// </summary>
    private static readonly (string name, string mirrorOf)[] Definitions =
    [
        (GrabName, "Grab"),
        (InteractName, "Interact")
    ];

    private static InputActionMap? Map => AssetCollection.VRInputs?.FindActionMap(MapName);

    /// <summary>
    /// The off hand's grab control, or null if it couldn't be registered
    /// </summary>
    public static InputAction? Grab => Map?.FindAction(GrabName);

    /// <summary>
    /// The off hand's "use the thing I'm holding" control, or null if it couldn't be registered
    /// </summary>
    public static InputAction? Interact => Map?.FindAction(InteractName);

    /// <summary>
    /// Register the actions and their controls-menu entries. Must run before the input asset is handed to
    /// PlayerInput, so that saved binding overrides can find them.
    /// </summary>
    public static void Register()
    {
        if (Map is not { } map)
        {
            Logger.LogError($"Couldn't find the \"{MapName}\" action map, the off hand will have no controls");
            return;
        }

        for (var slot = 0; slot < Definitions.Length; slot++)
            RegisterAction(map, Definitions[slot], slot);
    }

    private static void RegisterAction(InputActionMap map, (string name, string mirrorOf) definition, int slot)
    {
        if (map.FindAction(definition.name) != null)
            return;

        if (map.FindAction(definition.mirrorOf) is not { } source)
        {
            Logger.LogError(
                $"Couldn't find \"{definition.mirrorOf}\" to derive a default for \"{definition.name}\" from");
            return;
        }

        var wasEnabled = map.enabled;
        if (wasEnabled)
            map.Disable();

        var action = map.AddAction(definition.name, InputActionType.Button);

        // One binding per control scheme, so that GetBindingIndex(scheme) resolves the same way it does for every
        // other control. Schemes with nothing to mirror still get an (unbound) entry to keep that alignment.
        var schemes = AssetCollection.VRInputs.controlSchemes;

        for (var i = 0; i < schemes.Count; i++)
        {
            var group = schemes[i].bindingGroup;
            var index = source.GetBindingIndex(group);
            var path = index >= 0 ? MirrorHand(source.bindings[index].path) : null;

            action.AddBinding(new InputBinding(path ?? "", groups: group)
            {
                id = new Guid($"{BindingIdBase}{slot:x4}{i:x8}")
            });
        }

        if (wasEnabled)
            map.Enable();

        AssetCollection.RemappableControls.controls =
        [
            ..AssetCollection.RemappableControls.controls,
            new RemappableControl
            {
                controlName = definition.name,
                headerName = "",
                currentInput = InputActionReference.Create(action),
                bindingIndex = 0,
                toggleable = false,
                defaultToggle = false
            }
        ];

        Logger.LogInfo($"Registered \"{definition.name}\" across {schemes.Count} control scheme(s)");
    }

    /// <summary>
    /// Re-derive the default binding for every off-hand control the player hasn't bound themselves.
    ///
    /// Needed because the defaults depend on which hand is dominant, and that can change at any time. Bindings are
    /// edited rather than overridden, so a refreshed default still reads as "not customised" next time round and
    /// never gets written into the player's saved overrides.
    /// </summary>
    public static void RefreshDefaults()
    {
        if (Map is not { } map)
            return;

        var changes = new List<(InputAction action, int index, string path)>();

        foreach (var (name, mirrorOf) in Definitions)
        {
            if (map.FindAction(name) is not { } action || map.FindAction(mirrorOf) is not { } source)
                continue;

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];

                // The player bound this one themselves, leave it be
                if (!string.IsNullOrEmpty(binding.overridePath))
                    continue;

                var index = source.GetBindingIndex(binding.groups);
                if (index < 0)
                    continue;

                var path = MirrorHand(source.bindings[index].effectivePath);

                if (!string.IsNullOrEmpty(path) && path != binding.path)
                    changes.Add((action, i, path!));
            }
        }

        if (changes.Count == 0)
            return;

        // Bindings can't be edited while the map is live
        var wasEnabled = map.enabled;
        if (wasEnabled)
            map.Disable();

        foreach (var (action, index, path) in changes)
            action.ChangeBinding(index).WithPath(path);

        if (wasEnabled)
            map.Enable();

        Logger.LogDebug($"Refreshed {changes.Count} default off-hand binding(s)");
    }

    /// <summary>
    /// Warn about any off-hand control that shares a physical button with another action, since both would fire
    /// together and the result feels broken rather than merely unbound
    /// </summary>
    public static void WarnAboutConflicts()
    {
        foreach (var (name, _) in Definitions)
        {
            if (Map?.FindAction(name) is not { } action)
            {
                Logger.LogWarning($"\"{name}\" has no control, so the off hand can't use it. " +
                                  "Bind it under Settings > Controls.");
                continue;
            }

            if (FindConflict(action) is { } conflict)
                Logger.LogWarning($"\"{name}\" shares a control with \"{conflict}\", so both fire at once. " +
                                  "Rebind one of them under Settings > Controls.");
        }
    }

    /// <summary>
    /// Take a control on the dominant hand and return the same control on the off hand
    /// </summary>
    private static string? MirrorHand(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var dominant = VRSession.IsLeftHanded ? "LeftHand" : "RightHand";
        var offHand = VRSession.IsLeftHanded ? "RightHand" : "LeftHand";

        // Already on the off hand, nothing to mirror
        if (path!.Contains(offHand))
            return path;

        return path.Contains(dominant) ? path.Replace(dominant, offHand) : null;
    }

    /// <summary>
    /// The name of another action bound to the same control as this one, if there is one
    /// </summary>
    private static string? FindConflict(InputAction action)
    {
        if (AssetCollection.VRInputs is not { } asset)
            return null;

        var index = action.GetBindingIndex(VRInputSystem.Instance.CurrentControlScheme);
        if (index < 0)
            return null;

        var path = action.bindings[index].effectivePath;
        if (string.IsNullOrEmpty(path))
            return null;

        return (from map in asset.actionMaps
                from other in map.actions
                where other.id != action.id
                from binding in other.bindings
                where !string.IsNullOrEmpty(binding.effectivePath) &&
                      string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)
                select other.name).FirstOrDefault();
    }
}
