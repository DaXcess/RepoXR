using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Photon.Pun;
using RepoXR.Managers;
using RepoXR.Networking;
using UnityEngine;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class InventoryPatches
{
    private static bool ItemIsMine(ItemEquippable item)
    {
        return !SemiFunc.IsMultiplayer() || item.ownerPlayerId == PlayerAvatar.instance.photonView.ViewID;
    }
    
    /// <summary>
    /// Do not allow the item to shrink below 50% if we are the ones holding it
    /// </summary>
    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.AnimateEquip), MethodType.Enumerator)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> MinimumScaleEquip(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.01f))
            .Advance(-2)
            .RemoveInstructions(4)
            .Insert(
                new CodeInstruction(OpCodes.Ldloc_1), // `this` is `ldloc.1` because we're in an enumerator, I don't make the rules
                new CodeInstruction(OpCodes.Call, ((Func<ItemEquippable, Vector3>)MinimumScale).Method)
            )
            .InstructionEnumeration();

        static Vector3 MinimumScale(ItemEquippable item)
        {
            return ItemIsMine(item) ? Vector3.one * 0.1667f : item.transform.localScale * 0.01f;
        }
    }

    /// <summary>
    /// Immediately force grab item when unequipping
    /// </summary>
    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.AnimateUnequip), MethodType.Enumerator)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UnequipGrabImmediately(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Callvirt, PropertySetter(typeof(Transform), nameof(Transform.localScale))))
            .Advance(1)
            .Insert(
                new CodeInstruction(OpCodes.Ldloc_1), // `this` is `ldloc.1` because we're in an enumerator, I don't make the rules
                new CodeInstruction(OpCodes.Callvirt, Method(typeof(ItemEquippable), nameof(ItemEquippable.ForceGrab)))
            )
            .InstructionEnumeration();
    }

    /// <summary>
    /// Fix item magnitude if the scale is much larger than normal
    /// </summary>
    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.StateEquipped))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> StateEquippedPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 0.1f))
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .Insert(new CodeInstruction(OpCodes.Call, ((Func<ItemEquippable, float>)MinimumMagnitude).Method))
            .InstructionEnumeration();

        static float MinimumMagnitude(ItemEquippable item)
        {
            return ItemIsMine(item) ? 0.9f : 0.1f;
        }
    }

    /// <summary>
    /// When unequipping an item, disable teleportation since we grab items straight from out inventory
    /// </summary>
    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.RayHitTestNew))]
    [HarmonyPrefix]
    private static bool ItemUnequipNoTeleport(ItemEquippable __instance, float distance)
    {
        if (VRSession.Instance is not { } session)
            return true;

        // PlayerAvatar position is at the feet, so if the visuals are below that, they're underneath the floor
        // So when an item would be unequipped below the floor, we just re-enable the original functionality
        if (session.Player.Rig.inventoryController.visualsTransform.position.y <
            PlayerAvatar.instance.transform.position.y + 0.1f)
        {
            var mask = SemiFunc.LayerMaskGetVisionObstruct() & ~LayerMask.GetMask("Ignore Raycast", "CollisionCheck");
            if (Physics.Raycast(Camera.main!.transform.position, Camera.main.transform.forward, out var hit, distance,
                    mask))
            {
                __instance.teleportPosition = hit.point;
                return false;
            }

            __instance.teleportPosition = Camera.main.transform.position + Camera.main.transform.forward * 0.2f;
        }
        
        __instance.teleportPosition = __instance.transform.position;

        return false;
    }
    
    /// <summary>
    /// Prevent items from being "hidden" when equipped in an inventory
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.OverrideTimersTick))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisableItemHiding(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 3000f))
            .Advance(-4);

        var jmp = matcher.Instruction;

        matcher.Advance(1).Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, ((Func<PhysGrabObject, bool>)ShouldTeleport).Method),
            jmp
        );

        return matcher.InstructionEnumeration();

        static bool ShouldTeleport(PhysGrabObject @object)
        {
            if (!@object.TryGetComponent<ItemEquippable>(out var item))
                return true;

            return !ItemIsMine(item);
        }
    }

    /// <summary>
    /// Prevent items from being "hidden" when equipped in an inventory
    /// </summary>
    [HarmonyPatch(typeof(PhysGrabObject), nameof(PhysGrabObject.OverrideDeactivate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> DisableItemOverrideHiding(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call, Method(typeof(SemiFunc), nameof(SemiFunc.IsMasterClientOrSingleplayer))))
            .Set(OpCodes.Call, ((Func<PhysGrabObject, bool>)ShouldMoveItem).Method)
            .Insert(new CodeInstruction(OpCodes.Ldarg_0))
            .InstructionEnumeration();

        static bool ShouldMoveItem(PhysGrabObject @object)
        {
            var result = SemiFunc.IsMasterClientOrSingleplayer();

            if (!@object.TryGetComponent<ItemEquippable>(out var item))
                return result;

            return result && !ItemIsMine(item);
        }
    }

    [HarmonyPatch(typeof(InventorySpot), nameof(InventorySpot.EquipItem))]
    [HarmonyPrefix]
    private static void OnItemEquip(InventorySpot __instance, ItemEquippable item)
    {
        if (__instance.currentState != InventorySpot.SpotState.Empty)
            return;
    
        VRSession.Instance.Player.Rig.inventoryController.EquipItem(item);
    }

    [HarmonyPatch(typeof(InventorySpot), nameof(InventorySpot.UnequipItem))]
    [HarmonyPrefix]
    private static void OnItemUnequip(InventorySpot __instance)
    {
        if (__instance.currentState != InventorySpot.SpotState.Occupied)
            return;

        if (VRSession.Instance is not { } session)
            return;
    
        session.Player.Rig.inventoryController.UnequipItem(__instance.CurrentItem);
    }

    /// <summary>
    /// Hide the battery UI if it's equipped
    /// </summary>
    [HarmonyPatch(typeof(ItemBattery), nameof(ItemBattery.OverrideBatteryShow))]
    [HarmonyPrefix]
    private static bool HideBatteryIfEquipped(ItemBattery __instance)
    {
        if (!__instance || !__instance.itemEquippable)
            return true;
        
        return __instance.itemEquippable.currentState != ItemEquippable.ItemState.Equipped;
    }
}

[RepoXRPatch(RepoXRPatchTarget.Universal)]
internal static class UniversalInventoryPatches
{
    /// <summary>
    /// Prevent melee weapons from hurting VR players when in their inventory
    /// </summary>
    [HarmonyPatch(typeof(ItemMelee), nameof(ItemMelee.FixedUpdate))]
    [HarmonyPrefix]
    private static bool DontMeleeWhenEquipped(ItemMelee __instance)
    {
        var isEquipped =
            __instance.itemEquippable.currentState is ItemEquippable.ItemState.Equipped
                or ItemEquippable.ItemState.Equipping;

        if (!isEquipped) // Return early, as `ownerPlayerId` isn't set here yet
            return true;

        var isVRPlayer = (SemiFunc.IsMultiplayer() && PhotonView.Find(__instance.itemEquippable.ownerPlayerId)
            .GetComponent<PlayerAvatar>().IsVRPlayer()) || (!SemiFunc.IsMultiplayer() && VRSession.InVR);

        return !isVRPlayer;
    }
}