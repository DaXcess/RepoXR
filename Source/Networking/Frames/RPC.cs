namespace RepoXR.Networking.Frames;

public struct RPCFrame
{
    public ulong typeHash;
    public ulong methodHash;
    public object[] arguments;

    public static RPCFrame CreateAnnouncement() => new()
    {
        typeHash = 0,
        methodHash = 0,
        arguments = []
    };

    public bool Matches(RPCFrame other)
    {
        return other.typeHash == typeHash && other.methodHash == methodHash;
    }
}