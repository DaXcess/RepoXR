using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

namespace RepoXR.Preload;

public static class Preload
{
    public static IEnumerable<string> TargetDLLs { get; } = [];

    private const string VR_MANIFEST = """
                                       {
                                         "name": "OpenXR XR Plugin",
                                         "version": "1.10.0",
                                         "libraryName": "UnityOpenXR",
                                         "displays": [
                                           {
                                             "id": "OpenXR Display"
                                           }
                                         ],
                                         "inputs": [
                                           {
                                             "id": "OpenXR Input"
                                           }
                                         ]
                                       }
                                       """;

    private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("RepoXR.Preload");

    public static void Initialize()
    {
        SetupRuntimeAssets();
        DiscoverGameVersion();

        Logger.LogInfo("We're done here. Goodbye!");
    }

    /// <summary>
    /// Place required runtime libraries and configuration in the game files to allow VR to be started
    /// </summary>
    private static void SetupRuntimeAssets()
    {
        Logger.LogInfo("Setting up VR runtime assets");

        var root = Path.Combine(Paths.GameRootPath, "REPO_Data");
        var subsystems = Path.Combine(root, "UnitySubsystems");
        if (!Directory.Exists(subsystems))
            Directory.CreateDirectory(subsystems);

        var openXr = Path.Combine(subsystems, "UnityOpenXR");
        if (!Directory.Exists(openXr))
            Directory.CreateDirectory(openXr);

        var manifest = Path.Combine(openXr, "UnitySubsystemsManifest.json");
        if (!File.Exists(manifest))
            File.WriteAllText(manifest, VR_MANIFEST);

        var plugins = Path.Combine(root, "Plugins");
        var oxrPluginTarget = Path.Combine(plugins, "UnityOpenXR.dll");
        var oxrLoaderTarget = Path.Combine(plugins, "openxr_loader.dll");

        var current = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var oxrPlugin = Path.Combine(current, "RuntimeDeps/UnityOpenXR.dll");
        var oxrLoader = Path.Combine(current, "RuntimeDeps/openxr_loader.dll");
        
        File.Copy(oxrPlugin, oxrPluginTarget, true);
        File.Copy(oxrLoader, oxrLoaderTarget, true);
    }

    /// <summary>
    /// Load the current version of R.E.P.O. into environment variables
    /// </summary>
    private static void DiscoverGameVersion()
    {
        const int chunkSize = 4096;
        const string searchString = "Version - ";

        using var stream = File.OpenRead(Path.Combine(Paths.GameRootPath, "REPO_Data/sharedassets0.assets"));

        var offset = FindOffset(stream) - 4;
        if (offset < 0)
            throw new ApplicationException("Unknown application version");

        stream.Seek(offset, SeekOrigin.Begin);

        using var reader = new BinaryReader(stream, Encoding.ASCII);

        var stringLength = reader.ReadInt32();
        var name = Encoding.ASCII.GetString(reader.ReadBytes(stringLength));

        if (!name.StartsWith("Version - "))
            throw new ApplicationException("Unknown application version");

        // Align
        stream.Position = checked(stream.Position + 3L) & -4L;

        stringLength = reader.ReadInt32();

        var version = Encoding.ASCII.GetString(reader.ReadBytes(stringLength));
        var branch = name.Split(" - ")[1]!;

        Logger.LogInfo($"Version: {branch} {version}");

        Environment.SetEnvironmentVariable("REPO_VERSION", version);
        Environment.SetEnvironmentVariable("REPO_BRANCH", branch);

        return;

        static int FindOffset(FileStream stream)
        {
            var pattern = Encoding.ASCII.GetBytes(searchString);
            var length = stream.Length;
            var position = length;

            Span<byte> buffer = stackalloc byte[chunkSize + 64];
            Span<byte> overlap = stackalloc byte[64];

            var overlapBytes = 0;

            while (position > 0)
            {
                var readSize = (int)Math.Min(chunkSize, position);
                position -= readSize;

                stream.Seek(position, SeekOrigin.Begin);

                var chunk = buffer[..readSize];
                _ = stream.Read(chunk);

                if (overlapBytes > 0)
                {
                    overlap[..overlapBytes].CopyTo(buffer[readSize..]);
                    chunk = buffer[..(readSize + overlapBytes)];
                }

                var index = LastIndexOf(chunk, pattern);
                if (index >= 0)
                    return (int)position + index;

                overlapBytes = Math.Min(pattern.Length - 1, readSize);
                chunk[..overlapBytes].CopyTo(overlap);
            }

            return -1;
        }

        static int LastIndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        {
            if (needle.Length == 0)
                return 0;

            for (var i = haystack.Length - needle.Length;
                 i >= 0;)
            {
                while (i >= 0 && haystack[i + needle.Length - 1] != needle[^1])
                    i--;

                if (i < 0)
                    break;

                if (haystack.Slice(i, needle.Length)
                    .SequenceEqual(needle))
                {
                    return i;
                }

                i--;
            }

            return -1;
        }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        // No-op
    }
}
