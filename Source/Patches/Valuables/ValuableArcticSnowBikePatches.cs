using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Valuables;

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class ValuableArcticSnowBikePatches
{
    /// <summary>
    /// Steer the arctic bike using your hand instead of head
    /// </summary>
    [HarmonyPatch(typeof(ValuableArcticSnowBike), nameof(ValuableArcticSnowBike.UpdateSteering))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> BikeHandSteeringPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, Field(typeof(PlayerAvatar), nameof(PlayerAvatar.localCamera))))
            .Set(OpCodes.Call, ((Func<PlayerAvatar, Transform>)GetSourceTransform).Method)
            .InstructionEnumeration();

        static Transform GetSourceTransform(PlayerAvatar player)
        {
            if (player.isLocal)
                return VRSession.InVR ? VRSession.Instance.Player.MainHand : player.localCamera.transform;

            if (!NetworkSystem.instance)
                return player.localCamera.transform;

            return NetworkSystem.instance.GetNetworkPlayer(player, out var networkPlayer)
                ? networkPlayer.PrimaryHand
                : player.localCamera.transform;
        }
    }
}