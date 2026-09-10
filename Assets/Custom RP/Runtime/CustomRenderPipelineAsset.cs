using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;



[CreateAssetMenu(menuName ="Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{

    [SerializeField]
    bool useGPUInstacing = true, useSRPBatcher = true, useLightsPerObject=false, useDynamicBatching = false;
    [SerializeField]
    bool allowHDR = true;
    [SerializeField]
    PostFXSettings postFXSettings = default;



    [SerializeField]
    ShadowSettings shadows = default;

    //lut
    public enum ColorLUTResolution{_16 = 16,_32 = 32,_64 = 64}

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;






    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            useGPUInstacing,
            useDynamicBatching,
            useSRPBatcher,
            shadows, useLightsPerObject,postFXSettings, allowHDR,(int)colorLUTResolution);
    }
}
