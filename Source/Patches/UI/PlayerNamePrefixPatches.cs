using System.Collections;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using RepoXR.Managers;
using UnityEngine;

namespace RepoXR.Patches.UI;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class PlayerNamePrefixPatches
{
    /// <summary>
    /// Check if we are in VR and share that info to other clients via Photon custom properties
    /// </summary>
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.Awake))]
    [HarmonyPostfix]
    private static void InsertPrefixPhononPatch(PlayerAvatar __instance)
    {
        if (!SemiFunc.IsMultiplayer() || !__instance.isLocal)
            return;

        var player = PhotonNetwork.LocalPlayer;
        player.CustomProperties["VRPlayer"] = VRSession.InVR;
        player.SetCustomProperties(player.CustomProperties);
    }

    /// <summary>
    /// Show a warning message if the host does not appear to have the VR mod installed
    /// </summary>
    [HarmonyPatch(typeof(MenuPageLobby), nameof(MenuPageLobby.Start))]
    [HarmonyPostfix]
    private static void LobbyMenuWarnHostNotVR(MenuPageLobby __instance)
    {
        // Ignore if *we* are the host, or if we're not in VR to begin with
        if (PhotonNetwork.IsMasterClient || !VRSession.InVR)
            return;

        __instance.StartCoroutine(CheckHostHasVR(__instance));
        return;

        static IEnumerator CheckHostHasVR(MenuPageLobby lobby)
        {
            yield return new Utils.WaitUntilTimeout(() => HostIsInVR(lobby), 1);

            if (!HostIsInVR(lobby))
            {
                Logger.LogWarning("Host does not have the VR mod, some features may not work in this lobby");
                MenuManager.instance.PagePopUp("Host missing VR mod", Color.yellow,
                    "The host does not appear to have the VR mod installed.\n\nSome VR features may not work, or have degraded functionality.",
                    "It is what it is",
                    true);
            }

            yield break;

            static bool HostIsInVR(MenuPageLobby lobby) => lobby.lobbyPlayers.Any(player =>
                player.photonView.Owner.IsMasterClient &&
                player.photonView.Owner.CustomProperties.ContainsKey("VRPlayer"));
        }
    }

    /// <summary>
    /// Give all VR players in the menu lobby a [VR] prefix
    /// </summary>
    [HarmonyPatch(typeof(MenuPageLobby), nameof(MenuPageLobby.Update))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void LobbyMenuShowPrefix(MenuPageLobby __instance)
    {
        foreach (var player in __instance.listObjects.Select(entry => entry.GetComponent<MenuPlayerListed>())
                     .Where(player => player.playerAvatar))
        {
            try
            {
                var inVR = (bool)player.playerAvatar.photonView.Owner.CustomProperties["VRPlayer"];

                player.playerName.richText = true;
                player.playerName.text =
                    $"{(inVR ? "<color=#ff7a00>[VR]</color> " : "")}<noparse>{player.playerAvatar.playerName}</noparse>";
            }
            catch
            {
                // you corrupted your properties?
            }
        }
    }

    // I don't really care to show the [VR] prefix in game as well, so I'll leave it at the lobby menu only
}