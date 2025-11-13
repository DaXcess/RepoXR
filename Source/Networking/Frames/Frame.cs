using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace RepoXR.Networking.Frames;

public static class FrameHelper
{
    public const int FrameAnnouncement = 1;
    public const int FrameRig = 2;
    public const int FrameMaptool = 3;
    public const int FrameHeadlamp = 4;
    public const int FrameDominantHand = 5;
    public const int FrameEyeGaze = 6;
    
    private static Dictionary<int, Type> cachedTypes = [];

    static FrameHelper()
    {
        Assembly.GetExecutingAssembly().GetTypes().Do(type =>
        {
            if (type.GetCustomAttribute<FrameAttribute>() is not { } frame)
                return;
            
            cachedTypes.Add(frame.FrameID, type);
        });
    }

    public static Type GetFrameType(int frameId)
    {
        return cachedTypes[frameId];
    }

    public static IFrame CreateFrame(int frameId)
    {
        return (IFrame)Activator.CreateInstance(GetFrameType(frameId));
    }

    public static int GetFrameID(IFrame frame)
    {
        return cachedTypes.First(types => types.Value == frame.GetType()).Key;
    }
}

public interface IFrame
{
    public int FrameID => FrameHelper.GetFrameID(this);

    public void Serialize(PhotonStream stream);
    public void Deserialize(PhotonStream stream);
}

[AttributeUsage(AttributeTargets.Class)]
public class FrameAttribute(int frameId) : Attribute
{
    public int FrameID => frameId;
}