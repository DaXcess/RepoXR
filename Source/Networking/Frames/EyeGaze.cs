using Photon.Pun;
using UnityEngine;

namespace RepoXR.Networking.Frames;

[Frame(FrameHelper.FrameEyeGaze)]
public class EyeGaze : IFrame
{
    public Vector3 GazePoint;

    public void Serialize(PhotonStream stream)
    {
        stream.SendNext(GazePoint);
    }

    public void Deserialize(PhotonStream stream)
    {
        GazePoint = (Vector3)stream.ReceiveNext();
    }
}