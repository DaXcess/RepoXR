using BepInEx.Bootstrap;

namespace RepoXR;

public static class Compat
{
    public const string UnityExplorer = "com.sinai.unityexplorer";
    public const string CustomDiscoverStateLib = "Kistras-CustomDiscoverStateLib";
    
    public static bool IsLoaded(string modId)
    {
        return Chainloader.PluginInfos.ContainsKey(modId);
    }
}