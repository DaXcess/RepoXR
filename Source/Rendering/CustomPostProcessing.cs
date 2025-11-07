using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace RepoXR.Rendering;

public class CustomPostProcessing : MonoBehaviour
{
    private PostProcessing postProcessing;

    private CustomVignette vignette;

    private void Start()
    {
        postProcessing = GetComponent<PostProcessing>();

        vignette = ScriptableObject.CreateInstance<CustomVignette>();
        vignette.color.overrideState = true;
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.enabled.value = true;
        
        postProcessing.volume.profile.AddSettings(vignette);
        
        // Disable original shaders
        postProcessing.vignette.enabled.value = false;

        // Disable ambient occlusion (big performance boost)
        postProcessing.GetComponent<PostProcessVolume>().profile.GetSetting<AmbientOcclusion>().enabled.value = false;
    }

    private void Update()
    {
        vignette.color.value = postProcessing.vignette.color.value;
        vignette.intensity.value = postProcessing.vignette.intensity.value;
        vignette.smoothness.value = postProcessing.vignette.smoothness.value;
    }
}