using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset<CustomRenderPipeline>
{
    [SerializeField]
    bool useGPUInstacing = true, useSRPBatcher = true,
        useLightsPerObject = false, useDynamicBatching = false;

    [SerializeField]
    CameraBufferSettings cameraBuffer = new CameraBufferSettings
    {
        allowHDR = true,
        copyColor = true,
        copyDepth = true,
        renderScale = 1f,
        bicubicRescaling = CameraBufferSettings.BicubicRescalingMode.UpOnly,
        fxaa = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField]
    PostFXSettings postFXSettings = default;

    [SerializeField]
    Shader customBackDepthShader = default;

    [SerializeField]
    Shader cameraRendererShader = default;

    [SerializeField]
    ShadowSettings shadows = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            cameraBuffer,
            useGPUInstacing,
            useDynamicBatching,
            useSRPBatcher,
            shadows, useLightsPerObject, postFXSettings,
            (int)colorLUTResolution,
            customBackDepthShader, cameraRendererShader
        );
    }
}
