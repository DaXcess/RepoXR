using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using RepoXR.Networking.Frames;
using RepoXR.Patches;
using UnityEngine;

namespace RepoXR.Networking;

public class NetworkSystem : MonoBehaviour
{
    private const long REPOXR_MAGIC = 0x5245504F5852;
    private const int PROTOCOL_VERSION = 2;

    private enum ControlCode : byte
    {
        Announce,
        Rpc,

        // ReSharper disable once InconsistentNaming
        __max
    }

    public static NetworkSystem instance;

    private readonly Dictionary<int, NetworkPlayer> networkPlayers = [];
    private readonly List<int> cachedPhotonIds = [];

    /// <summary>
    /// Dictionary mapping type hashcodes and method hashcodes to their respective <see cref="MethodInfo"/>
    /// </summary>
    private readonly Dictionary<ulong, Dictionary<ulong, MethodInfo>> rpcHashcodes = [];

    /// <summary>
    /// Dictionary mapping actor numbers and type hashcodes to their instances
    /// </summary>
    private readonly Dictionary<int, Dictionary<ulong, MonoBehaviour>> rpcInstances = [];
    
    /// <summary>
    /// Dictionary mapping type instances to their PhotonView
    /// </summary>
    private readonly Dictionary<MonoBehaviour, PhotonView> rpcViews = [];

    /// <summary>
    /// List of RPC frames to be sent during next view serialization
    /// </summary>
    private readonly List<RPCFrame> rpcBuffer = [];

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
        PrecomputeRpcHashes();
    }

    /// <summary>
    /// Discover all RPCs and register them by their hashes
    /// </summary>
    private void PrecomputeRpcHashes()
    {
        // In the case that we ever need to call this initializer more than once, clear out the current hashes
        rpcHashcodes.Clear();
        
        // TODO: Should we scan all assemblies instead of only our own?
        foreach (var type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
        {
            if (type == null)
                continue;

            var typeHash = type.GetNetworkHash();

            // RPC methods are never static
            foreach (var method in
                     type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .Where(m => m.GetCustomAttribute<XRRpcAttribute>() != null))
            {
                if (method.GetParameters().Length > 6)
                {
                    Logger.LogWarning($"RPC {type.FullName}::{method.Name} contains too many parameters and will be ignored");
                    continue;
                }

                var methodHash = method.GetNetworkHash();
                
                if (!rpcHashcodes.ContainsKey(typeHash))
                    rpcHashcodes.Add(typeHash, new Dictionary<ulong, MethodInfo>());

                rpcHashcodes[typeHash][methodHash] = method;
            }
        }
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
        EnqueueRPC(RPCFrame.CreateAnnouncement());
    }

    /// <summary>
    /// Enqueues an RPC frame to be sent next serialization sequence. This function contains an optimization that
    /// removes duplicate RPCs to reduce network usage, which reduces server costs.
    /// </summary>
    private void EnqueueRPC(RPCFrame rpc, bool deduplicate = true)
    {
        if (deduplicate)
            rpcBuffer.ReplaceOrInsert(rpc, f => f.Matches(rpc));
        else
            rpcBuffer.Add(rpc);
    }

    // RPC stuff

    /// <summary>
    /// Register a component instance and photon view with the RPC networking system
    /// </summary>
    public void RegisterRPCBehaviour<T>(T behaviour, PhotonView view) where T : MonoBehaviour
    {
        var typeHash = typeof(T).GetNetworkHash();

        // Ignore if this isn't a type known to have RPCs
        if (!rpcHashcodes.ContainsKey(typeHash))
            throw new InvalidOperationException($"Type {typeof(T)} has not been registered during RPC discovery");

        if (!rpcInstances.ContainsKey(view.controllerActorNr))
            rpcInstances[view.controllerActorNr] = new Dictionary<ulong, MonoBehaviour>();
            
        rpcViews[behaviour] = view;
        rpcInstances[view.controllerActorNr][typeHash] = behaviour;
    }

    /// <summary>
    /// This method is called before the code of an RPC method is executed
    /// </summary>
    /// <returns>Whether the RPC handler should be executed</returns>
    internal bool ExecuteRPC(MonoBehaviour behaviour, MethodBase method, object[] args, bool self)
    {
        // If we're not in a multiplayer game, just either ignore the RPC or execute the handler if it's marked as self
        if (!SemiFunc.IsMultiplayer())
            return self;

        var typeHash = behaviour.GetType().GetNetworkHash();
        var methodHash = method.GetNetworkHash();

        // Try get RPC PhotonView, erroring out if it's unknown
        if (!rpcViews.TryGetValue(behaviour, out var view))
            throw new InvalidOperationException($"Attempt to execute RPC {method} while not initialized");

        // If we do not own the view we can just execute the RPC handler
        if (!view.IsMine)
            return true;

        // Dispatch RPC
        EnqueueRPC(new RPCFrame
        {
            typeHash = typeHash,
            methodHash = methodHash,
            arguments = args
        });

        // If the RPC is marked as "self" the RPC handler should also be run on the sender
        return self;
    }

    private void HandleRPC(PlayerAvatar player, ulong typeHash, ulong methodHash, object[] args)
    {
        // Special case: TypeHash == 0 and MethodHash == 0 is an announcement frame
        if (typeHash == 0 && methodHash == 0)
        {
            if (networkPlayers.ContainsKey(player.photonView.ControllerActorNr))
                return;

            var networkPlayer =
                new GameObject($"VR Player Rig - {player.playerName}").AddComponent<NetworkPlayer>();
            networkPlayer.playerAvatar = player;

            networkPlayers.Add(player.photonView.ControllerActorNr, networkPlayer);
            cachedPhotonIds.Add(player.photonView.ControllerActorNr);

            return;
        }

        if (!rpcHashcodes.TryGetValue(typeHash, out var methodHashes))
            return;

        if (!methodHashes.TryGetValue(methodHash, out var method))
            return;

        if (!rpcInstances.TryGetValue(player.photonView.controllerActorNr, out var instances))
            return;

        if (!instances.TryGetValue(typeHash, out var inst))
            return;

        method.Invoke(inst, args);
    }

    // Internal stuff

    internal void ResetCache()
    {
        rpcBuffer.Clear();
        networkPlayers.Clear();
        cachedPhotonIds.Clear();
    }

    internal void OnPlayerLeave(int actorNumber)
    {
        if (networkPlayers.Remove(actorNumber, out var networkPlayer))
            Destroy(networkPlayer.gameObject);

        cachedPhotonIds.Remove(actorNumber);

        // Clear view mappings for instances that have been destroyed
        rpcViews.Keys.Where(k => k == null).ToList().Do(b => rpcViews.Remove(b));
        rpcInstances.Remove(actorNumber);
    }

    internal void WriteAdditionalData(PhotonStream stream)
    {
        // Just don't send anything if we have nothing to say
        if (rpcBuffer.Count == 0)
            return;

        stream.SendNext(REPOXR_MAGIC);
        stream.SendNext(PROTOCOL_VERSION);

        stream.SendNext(rpcBuffer.Count);

        foreach (var rpc in rpcBuffer)
        {
            stream.SendNext(unchecked((long)rpc.typeHash));
            stream.SendNext(unchecked((long)rpc.methodHash));
            stream.SendNext(rpc.arguments.Length);

            foreach (var arg in rpc.arguments)
                stream.SendNext(arg);
        }

        rpcBuffer.Clear();
    }

    internal void ReadAdditionalData(PlayerAvatar playerAvatar, PhotonStream stream)
    {
        try
        {
            if (stream.currentItem >= stream.Count)
                return;

            if ((long)stream.PeekNext() != REPOXR_MAGIC)
                return;

            stream.ReceiveNext();

            if ((int)stream.ReceiveNext() != PROTOCOL_VERSION)
                return;

            var rpcCount = (int)stream.ReceiveNext();
            if (rpcCount > 32)
                throw new InvalidOperationException("Too many RPCs during a single read");

            for (var i = 0; i < rpcCount; i++)
            {
                var typeHash = unchecked((ulong)(long)stream.ReceiveNext());
                var methodHash = unchecked((ulong)(long)stream.ReceiveNext());
                var args = new object[(int)stream.ReceiveNext()];

                for (var j = 0; j < args.Length; j++)
                    args[j] = stream.ReceiveNext();

                try
                {
                    HandleRPC(playerAvatar, typeHash, methodHash, args);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Exception during handling of RPC frame: {ex}");
                }
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
    // The reason that this code is injected on PlayerLocalCamera is that it's still enabled
    // even after the player dies. Previous versions of this code would make the NetworkSystem
    // stop functioning after the player died, which was fine before, but now the game
    // has features that VR needs some special interactions with even after death

    /// <summary>
    /// Inject additional code when serializing/deserializing a network component
    /// </summary>
    [HarmonyPatch(typeof(PlayerLocalCamera), nameof(PlayerLocalCamera.OnPhotonSerializeView))]
    [HarmonyPostfix]
    private static void OnAfterSerializeView(PlayerLocalCamera __instance, PhotonStream stream, PhotonMessageInfo info)
    {
        if (!SemiFunc.MasterAndOwnerOnlyRPC(info, __instance.photonView))
            return;

        if (stream.IsWriting)
            NetworkSystem.instance.WriteAdditionalData(stream);
        else
            NetworkSystem.instance.ReadAdditionalData(
                __instance.playerAvatar ?? __instance.transform.parent.GetComponentInChildren<PlayerAvatar>(true),
                stream);
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

    internal static bool ExecuteRPCPrefix(object __instance, MethodBase __originalMethod, object[] __args)
    {
        return NetworkSystem.instance.ExecuteRPC((MonoBehaviour)__instance, __originalMethod, __args, false);
    }

    internal static bool ExecuteRPCPrefixSelf(object __instance, MethodBase __originalMethod, object[] __args)
    {
        return NetworkSystem.instance.ExecuteRPC((MonoBehaviour)__instance, __originalMethod, __args, true);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class XRRpcAttribute(bool self = false) : Attribute
{
    public bool Self => self;
}