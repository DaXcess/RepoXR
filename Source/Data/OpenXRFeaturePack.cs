using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;

namespace RepoXR.Data;

public class OpenXRFeaturePack : ScriptableObject
{
    [SerializeReference] private List<OpenXRFeature> features = [];

    public IReadOnlyList<OpenXRFeature> Features => features;
}