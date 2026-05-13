using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using MonoMod.Utils.Cil;
using RepoXR.Networking;
using SRE = System.Reflection.Emit;
using MethodBody = Mono.Cecil.Cil.MethodBody;

using static HarmonyLib.AccessTools;

namespace RepoXR.Patches;

internal static class HarmonyPatcher
{
    private static readonly Harmony VRPatcher = new("io.daxcess.repoxr");
    private static readonly Harmony UniversalPatcher = new("io.daxcess.repoxr-universal");

    private static bool ignoreErrors;

    public static void PatchUniversal()
    {
        Patch(UniversalPatcher, RepoXRPatchTarget.Universal);
    }

    public static void PatchVR()
    {
        Patch(VRPatcher, RepoXRPatchTarget.VROnly);
    }

    public static void PatchClass(Type type)
    {
        UniversalPatcher.CreateClassProcessor(type, true).Patch();
    }

    private static void Patch(Harmony patcher, RepoXRPatchTarget target)
    {
        GetTypesFromAssembly(Assembly.GetExecutingAssembly()).Do(type =>
        {
            try
            {
                var attribute =
                    (RepoXRPatchAttribute)Attribute.GetCustomAttribute(type, typeof(RepoXRPatchAttribute));

                if (attribute == null)
                    return;

                if (attribute.Dependency != null && !Compat.IsLoaded(attribute.Dependency))
                    return;

                if (attribute.Target != target)
                    return;

                Logger.LogDebug($"Applying patches from: {type.FullName}");

                patcher.CreateClassProcessor(type, true).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to apply patches from {type}: {e.Message}, {e.InnerException}");

                if (ignoreErrors)
                    return;

                var result = Native.ShellMessageBox(IntPtr.Zero, IntPtr.Zero,
                    $"One or more patches failed to apply correctly.\n\nThis version of RepoXR may not be compatible with this version of the game ({Plugin.GameVersion}),\nor other installed mods may be interfering with RepoXR.\n\nDo you want to continue loading the mod anyway? (NOT RECOMMENDED!)\n\nPressing 'No' will close the game.",
                    $"RepoXR v{Plugin.PLUGIN_VERSION} for R.E.P.O. {Plugin.SUPPORTED_GAME_VERSION}", 16 | 4 | 0x00010000);

                switch (result)
                {
                    // Yes
                    case 6:
                        ignoreErrors = true;
                        break;
                    // No
                    case 7:
                        Process.GetCurrentProcess().Kill();
                        break;
                }
            }
        });
    }

    internal static void PatchNetworkRPCs()
    {
        foreach (var type in GetTypesFromAssembly(Assembly.GetCallingAssembly()))
            ApplyNetworkRPCsForType(type);
    }

    private static void ApplyNetworkRPCsForType(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<XRRpcAttribute>() != null);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<XRRpcAttribute>();
            var processor = UniversalPatcher.CreateProcessor(method);

            // Apply RPC prefix patch
            processor.AddPrefix(new HarmonyMethod(Method(typeof(NetworkingPatches),
                attr.Self
                    ? nameof(NetworkingPatches.ExecuteRPCPrefixSelf)
                    : nameof(NetworkingPatches.ExecuteRPCPrefix))));
            processor.Patch();
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
internal class RepoXRPatchAttribute(RepoXRPatchTarget target = RepoXRPatchTarget.VROnly, string? dependency = null)
    : Attribute
{
    public RepoXRPatchTarget Target { get; } = target;
    public string? Dependency { get; } = dependency;
}

internal enum RepoXRPatchTarget
{
    Universal,
    VROnly
}

/// <summary>
/// To keep RepoXR compatible with older BepInEx versions, this patch will not be removed
///
/// Fixes a bug in older BepInEx versions
///
/// https://github.com/BepInEx/HarmonyX/blob/master/Harmony/Internal/Patching/ILManipulator.cs#L322
/// Licensed under MIT: https://github.com/BepInEx/HarmonyX/blob/master/LICENSE
/// </summary>
[RepoXRPatch(RepoXRPatchTarget.Universal)]
[HarmonyPriority(Priority.First)]
internal static class LeaveMyLeaveAlonePatch
{
    private static Type Type => TypeByName("HarmonyLib.Internal.Patching.ILManipulator");
    private static MethodInfo Method => Method(Type, "WriteTo");

    [UsedImplicitly]
    private static MethodBase TargetMethod() => Method;

    [UsedImplicitly]
    private static bool Prefix(object __instance, MethodBody body, MethodBase original)
    {
        // Clean up the body of the target method
        body.Instructions.Clear();
        body.ExceptionHandlers.Clear();

        var il = new CecilILGenerator(body.GetILProcessor());
        var cil = il.GetProxy();

        // Define an "empty" label
        // In Harmony, the first label can point to the end of the method
        // Apparently, some transpilers naively call new Label() to define a label and thus end up
        // using the first label without knowing it
        // By defining the first label we'll ensure label count is correct
        il.DefineLabel();
        
        // Step 1: Apply transpilers
        // We don't remove trailing `ret`s because we need to do so only if prefixes/postfixes are present
        var newInstructions = ApplyTranspilers(__instance, cil, original, vDef => il.GetLocal(vDef), il.DefineLabel);
        
        // Step 2: Emit code
        foreach (var cur in newInstructions)
        {
            cur.labels.ForEach(l => il.MarkLabel(l));
            cur.blocks.ForEach(b => il.MarkBlockBefore(b));
            
            // We need to handle exception handler opcodes specially because ILProcessor emits them automatically
            // Case: endfilter/endfinally and end of exception marker => ILProcessor will generate the correct end
            if ((cur.opcode == SRE.OpCodes.Endfilter || cur.opcode == SRE.OpCodes.Endfinally) && cur.blocks.Count > 0)
                goto mark_block;
            // Other cases are either intentional leave or invalid IL => let them be processed and let JIT generate correct exception
            
            // We don't replace `ret`s yet because we might not need to
            // We do that only if we add prefixes/postfixes
            // We also don't need to care for long/short forms thanks to Cecil/MonoMod
            
            // Temporary fix: CecilILGenerator doesn't properly handle ldarg
            switch (cur.opcode.OperandType)
            {
                case SRE.OperandType.InlineNone:
                    il.Emit(cur.opcode);
                    break;
                case SRE.OperandType.InlineSig:
                    throw new NotSupportedException(
                        "Emitting opcodes with CallSites is currently not fully implemented");
                default:
                    if (cur.operand == null)
                        throw new ArgumentNullException(nameof(cur.operand), $"Invalid argument for {cur}");
                    
                    il.Emit(cur.opcode, cur.operand);
                    break;
            }
            
            mark_block:
            cur.blocks.ForEach(b => il.MarkBlockAfter(b));
        }
        
        // Special Harmony interop case: if no instructions exist, at least emit a quick return to attempt to get a valid method
        // Vanilla Harmony (almost) always emits a `ret` which allows for skipping original method by writing an empty transpiler
        if (body.Instructions.Count == 0)
            il.Emit(SRE.OpCodes.Ret);

        // Note: We lose all unassigned labels here along with any way to log them
        // On the contrary, we gain better logging anyway down the line by using Cecil

        return false;
    }

    private static IEnumerable<CodeInstruction> ApplyTranspilers(object instance, SRE.ILGenerator il,
        MethodBase original, Func<VariableDefinition, SRE.LocalBuilder> getLocal, Func<SRE.Label> defineLabel)
    {
        return (IEnumerable<CodeInstruction>)Method(Type, "ApplyTranspilers")
            .Invoke(instance, [il, original, getLocal, defineLabel]);
    }

    extension(CecilILGenerator il)
    {
        private SRE.LocalBuilder GetLocal(VariableDefinition varDef)
        {
            var type = TypeByName("HarmonyLib.Internal.Util.EmitterExtensions");
            var method = Method(type, "GetLocal");

            return (SRE.LocalBuilder)method.Invoke(null, [il, varDef]);
        }

        private void MarkBlockBefore(ExceptionBlock block)
        {
            var type = TypeByName("HarmonyLib.Internal.Util.EmitterExtensions");
            var method = Method(type, "MarkBlockBefore");

            method.Invoke(null, [il, block]);
        }

        private void MarkBlockAfter(ExceptionBlock block)
        {
            var type = TypeByName("HarmonyLib.Internal.Util.EmitterExtensions");
            var method = Method(type, "MarkBlockAfter");

            method.Invoke(null, [il, block]);
        }

        private void Emit(SRE.OpCode opcode, object operand)
        {
            var type = TypeByName("HarmonyLib.Internal.Util.EmitterExtensions");
            var method = Method(type, "Emit");

            method.Invoke(null, [il, opcode, operand]);
        }
    }
}