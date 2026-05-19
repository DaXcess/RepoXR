using System;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace RepoXR;

internal static class Mono
{
    [DllImport("mono-2.0-bdwgc", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mono_assembly_get_image(IntPtr assembly);

    [DllImport("mono-2.0-bdwgc", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_debug_open_image_from_memory(IntPtr image, IntPtr rawContents, int size);

    private static IntPtr GetMonoImage(Assembly assembly)
    {
        var fi = AccessTools.Field(AccessTools.TypeByName("System.Reflection.RuntimeAssembly"), "_mono_assembly");
        var ptr = (IntPtr)fi.GetValue(assembly);

        return mono_assembly_get_image(ptr);
    }

    public static void LoadSymbolsForAssembly(Assembly assembly, string symbolsUrl)
    {
        var symbols = new WebClient().DownloadData(symbolsUrl);
        var image = GetMonoImage(assembly);

        var symbolPtr = Marshal.AllocHGlobal(symbols.Length);
        Marshal.Copy(symbols, 0, symbolPtr, symbols.Length);

        mono_debug_open_image_from_memory(image, symbolPtr, symbols.Length);

        // From what I've been able to see mono will memcpy the symbol contents so we can just free the data here
        Marshal.FreeHGlobal(symbolPtr);
    }
}