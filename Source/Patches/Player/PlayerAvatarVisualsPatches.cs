using System.Linq;
using HarmonyLib;
using RepoXR.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerAvatarVisualsPatches
{
    /// <summary>
    /// Set the color for the local VR rig based on the player's color
    /// </summary>
    [HarmonyPatch(typeof(PlayerMaterial), nameof(PlayerMaterial.ColorSet))]
    [HarmonyPostfix]
    private static void OnPlayerMaterialColorChanged(PlayerMaterial __instance, int _albedoColor, int _emissionColor, int _colorIndex)
    {
        if (VRSession.Instance is not { } session)
            return;

        // Check if material belongs to our own player character
        if (!PlayerAvatar.instance.playerCosmetics.playerMaterials.Contains(__instance))
            return;

        if (__instance.tintType != PlayerMaterial.TintType.Albedo &&
            __instance.tintType != PlayerMaterial.TintType.Both)
            return;

        if (__instance.cosmeticType == SemiFunc.CosmeticType.ArmLeftMesh)
            session.Player.Rig.SetLeftArmColor(_albedoColor, MetaManager.instance.colors[_colorIndex].color);

        if (__instance.cosmeticType == SemiFunc.CosmeticType.ArmRightMesh)
            session.Player.Rig.SetRightArmColor(_albedoColor, MetaManager.instance.colors[_colorIndex].color);
    }

    /// <summary>
    /// Make sure the grabber, and it's orb are always visible (when they're used)
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatarVisuals), nameof(PlayerAvatarVisuals.ApplyLocalVisibilityBody))]
    [HarmonyPostfix]
    private static void KeepVisibleLocalPatch(PlayerAvatarVisuals __instance)
    {
        if (__instance.isMenuAvatar)
            return;

        var grabberMats = __instance.playerAvatarRightArm.grabberClawParent.GetComponentsInChildren<PlayerMaterial>(true);
        grabberMats.Where(mat => mat).Select(mat => mat.GetComponent<Renderer>())
            .Do(renderer =>
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.gameObject.layer = 6;
            });

        __instance.playerAvatarRightArm.grabberOrb.gameObject.SetActive(true);
    }

    /// <summary>
    /// Force materials parented to our VR rig to also be in the material list, for coloring to work properly
    /// </summary>
    [HarmonyPatch(typeof(PlayerCosmetics), nameof(PlayerCosmetics.PlayerMaterialSetup))]
    [HarmonyPostfix]
    private static void InsertGrabberPatch(PlayerCosmetics __instance)
    {
        if (!__instance.playerAvatarVisuals || !__instance.playerAvatarVisuals.playerAvatar ||
            !__instance.playerAvatarVisuals.playerAvatar.isLocal)
            return;

        if (VRSession.Instance is not { } instance)
            return;

        foreach (var mat in instance.Player.Rig.GetComponentsInChildren<PlayerMaterial>(true))
        {
            mat.Setup();
            __instance.playerMaterials.Add(mat);
        }
    }
}