using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;



[CreateAssetMenu(menuName ="Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset 
{

    [SerializeField]
    bool useGPUInstacing = true, useSRPBatcher = true, useLightsPerObject=false, useDynamicBatching = false;
    [SerializeField]
    PostFXSettings postFXSettings = default;
    [SerializeField]
    ShadowSettings shadows = default;


    
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            useGPUInstacing,
            useDynamicBatching, 
            useSRPBatcher,
            shadows, useLightsPerObject,postFXSettings);
    }
}