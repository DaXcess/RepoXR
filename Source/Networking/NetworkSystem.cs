using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using RepoXR.Networking.Frames;
using RepoXR.Patches;
using UnityEngine;

namespace RepoXR.Networking;

public class NetworkSystem : MonoBehaviour
{
    private const long REPOXR_MAGIC = 0x5245504F5852;
    private const int PROTOCOL_VERSION = 1;

    public static NetworkSystem instance;

    private readonly List<IFrame> scheduledFrames = [];
    private readonly Dictionary<int, NetworkPlayer> networkPlayers = [];
    private readonly List<int> cachedPhotonIds = [];

    private void Awake()
    {
        if (instance)
        {
            // On new scene load, clear the network players cache
            instance.networkPlayers.Clear();

            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool GetNetworkPlayer(PlayerAvatar player, out NetworkPlayer networkPlayer)
    {
        return networkPlayers.TryGetValue(player.photonView.ControllerActorNr, out networkPlayer);
    }

    public bool IsVRPlayer(PlayerAvatar player)
    {
        return cachedPhotonIds.Contains(player.photonView.ControllerActorNr);
    }

    public bool IsVRView(PhotonView view)
    {
        return cachedPhotonIds.Contains(view.ControllerActorNr);
    }

    // Sending

    public void AnnounceVRPlayer()
    {
        scheduledFrames.Add(new Announcement());
    }

    public void SendRigData(Vector3 leftPosition, Vector3 rightPosition, Quaternion leftRotation,
        Quaternion rightRotation)
    {
        EnqueueFrame(
            new Rig
            {
                LeftPosition = leftPosition, RightPosition = rightPosition, LeftRotation = leftRotation,
                RightRotation = rightRotation
            });
    }

    public void UpdateMapToolState(bool hideFlashlight, bool leftHanded)
    {
        EnqueueFrame(new MapTool
        {
            HideFlashlight = hideFlashlight,
            LeftHanded = leftHanded
        });
    }

    public void UpdateHeadlamp(bool headLampEnabled)
    {
        EnqueueFrame(new Headlamp
        {
            HeadlampEnabled = headLampEnabled
        });
    }

    public void UpdateDominantHand(bool leftHanded)
    {
        EnqueueFrame(new DominantHand
        {
            LeftHanded = leftHanded
        });
    }

    public void UpdateEyeTracking(Vector3 gazePoint)
    {
        EnqueueFrame(new EyeGaze
        {
            GazePoint = gazePoint
        });
    }

    public void DisableEyeTracking()
    {
        UpdateEyeTracking(Vector3.down * 1000);
    }

    /// <summary>
    /// Enqueues a frame to be sent next serialization sequence. This function contains an optimization that removes
    /// duplicate frames to reduce network usage, which reduces server costs.
    /// </summary>
    private void EnqueueFrame<T>(T frame, bool removeDuplicate = true) where T : IFrame
    {
        if (removeDuplicate)
            scheduledFrames.ReplaceOrInsert(frame, f => f.GetType() == typeof(T));
        else
            scheduledFrames.Add(frame);
    }

    // Handling

    private void HandleFrame(PlayerAvatar player, IFrame frame)
    {
        try
        {
            switch (frame.FrameID)
            {
                case FrameHelper.FrameAnnouncement:
                {
                    if (networkPlayers.ContainsKey(player.photonView.ControllerActorNr))
                        return;

                    var networkPlayer =
                        new GameObject($"VR Player Rig - {player.playerName}").AddComponent<NetworkPlayer>();
                    networkPlayer.playerAvatar = player;

                    networkPlayers.Add(player.photonView.ControllerActorNr, networkPlayer);
                    cachedPhotonIds.Add(player.photonView.ControllerActorNr);

                    break;
                }

                case FrameHelper.FrameRig:
                {
                    var rigFrame = (Rig)frame;
                    if (!networkPlayers.TryGetValue(player.photonView.ControllerActorNr, out var networkPlayer))
                        return;

                    networkPlayer.HandleRigFrame(rigFrame);

                    break;
                }

                case FrameHelper.FrameMaptool:
                {
                    var mapFrame = (MapTool)frame;
                    if (!networkPlayers.TryGetValue(player.photonView.controllerActorNr, out var networkPlayer))
                        return;

                    networkPlayer.HandleMapFrame(mapFrame);

                    break;
                }

                case FrameHelper.FrameHeadlamp:
                {
                    var headlampFrame = (Headlamp)frame;
                    if (!networkPlayers.TryGetValue(player.photonView.controllerActorNr, out var networkPlayer))
                        return;

                    networkPlayer.HandleHeadlamp(headlampFrame.HeadlampEnabled);

                    break;
                }

                case FrameHelper.FrameDominantHand:
                {
                    var dominantHandFrame = (DominantHand)frame;
                    if (!networkPlayers.TryGetValue(player.photonView.controllerActorNr, out var networkPlayer))
                        return;

                    networkPlayer.UpdateDominantHand(dominantHandFrame.LeftHanded);

                    break;
                }

                case FrameHelper.FrameEyeGaze:
                {
                    var eyeGazeFrame = (EyeGaze)frame;
                    if (!networkPlayers.TryGetValue(player.photonView.controllerActorNr, out var networkPlayer))
                        return;

                    networkPlayer.UpdateEyeTracking(eyeGazeFrame.GazePoint);

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error while handling frame {frame.FrameID} ({frame.GetType().Name}): {ex.Message}");
            Logger.LogError(ex.StackTrace);
        }
    }

    // Internal stuff

    internal void ResetCache()
    {
        scheduledFrames.Clear();
        networkPlayers.Clear();
        cachedPhotonIds.Clear();
    }

    internal void OnPlayerLeave(int actorNumber)
    {
        if (networkPlayers.Remove(actorNumber, out var networkPlayer))
            Destroy(networkPlayer.gameObject);

        cachedPhotonIds.Remove(actorNumber);
    }

    internal void WriteAdditionalData(PhotonStream stream)
    {
        // Just don't send anything if we have nothing to say
        if (scheduledFrames.Count == 0)
            return;

        stream.SendNext(REPOXR_MAGIC);
        stream.SendNext(PROTOCOL_VERSION);

        stream.SendNext(scheduledFrames.Count);

        foreach (var frame in scheduledFrames)
        {
            stream.SendNext(frame.FrameID);
            frame.Serialize(stream);
        }

        scheduledFrames.Clear();
    }

    internal void ReadAdditionalData(PlayerAvatar playerAvatar, PhotonStream stream)
    {
        try
        {
            if ((long)stream.PeekNext() != REPOXR_MAGIC)
                return;

            stream.ReceiveNext();

            if ((int)stream.ReceiveNext() != PROTOCOL_VERSION)
                return;

            var frameCount = (int)stream.ReceiveNext();
            for (var i = 0; i < frameCount; i++)
            {
                var frameId = (int)stream.ReceiveNext();
                var frame = FrameHelper.CreateFrame(frameId);

                frame.Deserialize(stream);
                HandleFrame(playerAvatar, frame);
            }
        }
        catch
        {
            // no-op
        }
    }
}

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class NetworkingPatches
{
    // The reason that this code is injected on PhysGrabber is that it's the last observed component on the
    // player avatar controller's PhotonView, meaning no additional data is available on the PhotonStream.
    // If we started injecting data too early, it would cause vanilla clients to no longer be able
    // to understand our photon data, and that breaks multiplayer.

    /// <summary>
    /// Inject additional code when serializing/deserializing a network component
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.OnPhotonSerializeView))]
    [HarmonyPostfix]
    private static void OnAfterSerializeView(PhysGrabber __instance, PhotonStream stream)
    {
        if (stream.IsWriting)
            NetworkSystem.instance.WriteAdditionalData(stream);
        else
            NetworkSystem.instance.ReadAdditionalData(
                __instance.playerAvatar ?? __instance.GetComponent<PlayerAvatar>(), stream);
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnPlayerLeftRoom))]
    [HarmonyPostfix]
    private static void OnPlayerLeave(Photon.Realtime.Player otherPlayer)
    {
        NetworkSystem.instance.OnPlayerLeave(otherPlayer.ActorNumber);
    }

    /// <summary>
    /// When we enter the lobby, we clear the photon view cache
    /// </summary>
    [HarmonyPatch(typeof(MenuPageMain), nameof(MenuPageMain.Start))]
    [HarmonyPostfix]
    private static void OnMainMenuEntered()
    {
        NetworkSystem.instance.ResetCache();
    }
}