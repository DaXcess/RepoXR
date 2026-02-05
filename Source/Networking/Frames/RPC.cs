using Photon.Pun;

namespace RepoXR.Networking.Frames;

[Frame(FrameHelper.FrameRPC)]
public class RPC : IFrame
{
    public ulong TypeHash;
    public ulong MethodHash;
    public object[] Arguments;

    public void Serialize(PhotonStream stream)
    {
        stream.SendNext(TypeHash);
        stream.SendNext(MethodHash);
        stream.SendNext(Arguments.Length);
        
        foreach (var arg in Arguments)
            stream.SendNext(arg);
    }

    public void Deserialize(PhotonStream stream)
    {
        TypeHash = (ulong)stream.ReceiveNext();
        MethodHash = (ulong)stream.ReceiveNext();

        var len = (int)stream.ReceiveNext();
        Arguments = new object[len];

        for (var i = 0; i < len; i++)
            Arguments[i] = stream.ReceiveNext();
    }
}