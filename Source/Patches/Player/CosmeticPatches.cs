using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RepoXR.Managers;
using RepoXR.Player.Camera;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches.Player;

[RepoXRPatch]
internal static class CosmeticPatches
{
    /// <summary>
    /// Force look at the machine briefly when we get picked up by it
    /// </summary>
    [HarmonyPatch(typeof(CosmeticShopMachine), nameof(CosmeticShopMachine.InteractingPlayerLogic))]
    [HarmonyPostfix]
    private static void LookAtMachinePatch(CosmeticShopMachine __instance)
    {
        if (!__instance.interactingPlayer || __instance.interactingPlayerStuck ||
            !__instance.interactingPlayer.isLocal) return;

        if (__instance.stateCurrent != CosmeticShopMachine.State.TokenOutro) return;

        VRCameraAim.instance.SetAimTarget(__instance.playerCameraTarget.position, 0.5f, 5f, __instance.gameObject, 50,
            true);
    }

    /// <summary>
    /// Prevent the token animation from being scaled and rotated improperly
    /// </summary>
    [HarmonyPatch(typeof(CosmeticTokenUIElement), nameof(CosmeticTokenUIElement.OnDestroy))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TokenScalePatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Call, PropertyGetter(typeof(Quaternion), nameof(Quaternion.identity))))
            .Advance(-2)
            .RemoveInstructions(3)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Call, PropertyGetter(typeof(Component), nameof(Component.transform))),
                new CodeInstruction(OpCodes.Callvirt, PropertyGetter(typeof(Transform), nameof(Transform.parent)))
            )
            .SetOperandAndAdvance(Method(typeof(GameObject), nameof(GameObject.Instantiate),
                [typeof(GameObject), typeof(Transform)]))
            .InstructionEnumeration();
    }

    /// <summary>
    /// Instantiate arm cosmetics on the VR arms as well
    /// </summary>
    [HarmonyPatch(typeof(PlayerCosmetics), nameof(PlayerCosmetics.SetupCosmeticsLogic))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> InstantiateCosmeticVRPatch(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchForward(true,
                new CodeMatch(OpCodes.Call,
                    Method(typeof(PlayerCosmetics), nameof(PlayerCosmetics.InstantiateCosmetic))))
            .Advance(1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0), // this
                new CodeInstruction(OpCodes.Ldloc_S, Plugin.DebugBuild ? 24 : 14), // locals0
                new CodeInstruction(OpCodes.Ldfld,
                    Field(Inner(typeof(PlayerCosmetics), "<>c__DisplayClass57_0"),
                        "_cosmeticAsset")), // _cosmeticAsset,
                new CodeInstruction(OpCodes.Ldloc_1), // _newEquipped
                new CodeInstruction(OpCodes.Call,
                    ((Action<PlayerCosmetics, CosmeticAsset, List<Cosmetic>>)InstantiateCosmeticVR).Method)
            )
            .InstructionEnumeration();
    }

    private static void InstantiateCosmeticVR(PlayerCosmetics cosmetics, CosmeticAsset cosmeticAsset, List<Cosmetic> newEquipped)
    {
        if (!cosmeticAsset || !cosmeticAsset.prefab.IsValid())
            return;

        if (cosmetics.playerAvatarVisuals is { playerAvatar.isLocal: false } or { isMenuAvatar: true })
            return;

        if (VRSession.Instance is not { } instance)
            return;

        var parent = cosmetics.cosmeticParents.Find(x => x.cosmeticType == cosmeticAsset.type);
        if (parent == null)
            return;

        var transform = cosmeticAsset.type switch
        {
            SemiFunc.CosmeticType.ArmLeft or SemiFunc.CosmeticType.ArmLeftOverlay or SemiFunc.CosmeticType.ArmLeftMesh
                => instance.Player.Rig.leftArmMesh.transform,
            SemiFunc.CosmeticType.ArmRight or SemiFunc.CosmeticType.ArmRightOverlay
                or SemiFunc.CosmeticType.ArmRightMesh => instance.Player.Rig.rightArmMesh.transform,
            _ => null
        };

        if (transform == null)
            return;

        var cosmeticInstance = Object.Instantiate(cosmeticAsset.prefab.Prefab, transform.parent);
        var cosmeticComponent = cosmeticInstance.GetComponent<Cosmetic>();

        cosmeticComponent.cosmeticAsset = cosmeticAsset;
        cosmeticComponent.cosmeticTypeAsset = MetaManager.instance.cosmeticTypeAssets[(int)cosmeticAsset.type];
        cosmeticComponent.playerCosmetics = cosmetics;
        cosmeticComponent.cosmeticParent = new PlayerCosmetics.CosmeticParent
        {
            parent = transform.parent,
            cosmeticType = cosmeticAsset.type,
            resetTransform = parent.resetTransform,
            springImpulse = parent.springImpulse,
            baseMeshes = [transform],
            baseMeshParents = [transform.parent]
        };
        cosmeticComponent.type = cosmeticAsset.type;
        cosmeticComponent.rarity = cosmeticAsset.rarity;

        if (parent.resetTransform)
        {
            cosmeticInstance.transform.localPosition = Vector3.zero;
            cosmeticInstance.transform.localRotation = Quaternion.identity;
            cosmeticInstance.transform.localScale = Vector3.one;
        }

        // Same layer as arms
        cosmeticInstance.SetLayerRecursively(6);

        cosmeticComponent.Setup();

        // Disable colliders and shadows
        cosmeticInstance.GetComponentsInChildren<Collider>(true).Do(collider => collider.enabled = false);
        cosmeticInstance.GetComponentsInChildren<MeshRenderer>(true)
            .Do(renderer => renderer.shadowCastingMode = ShadowCastingMode.Off);

        newEquipped.Add(cosmeticComponent);
    }
}