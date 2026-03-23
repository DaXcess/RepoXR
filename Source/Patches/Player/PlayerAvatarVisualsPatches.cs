using HarmonyLib;
using RepoXR.Managers;
using UnityEngine;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class PlayerAvatarVisualsPatches
{
    /// <summary>
    /// Set the color for the local VR rig based on the player's color
    /// TODO: PlayerMaterial.ColorSet might be a good candidate
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatarVisuals), nameof(PlayerAvatarVisuals.SetColor))]
    [HarmonyPostfix]
    private static void OnPlayerColorChanged(PlayerAvatarVisuals __instance, int _colorIndex, Color _setColor)
    {
        if (!__instance.playerAvatar.isLocal || VRSession.Instance is not { } session)
            return;
        
        session.Player.SetColor(_colorIndex, _setColor);
    }
}