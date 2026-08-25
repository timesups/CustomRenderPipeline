using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;



public partial class CustomRenderPipeline : RenderPipeline 
{
    CameraRender renderer = new CameraRender();
    bool useGPUInstacing, useDynamciBatching, useLightsPerObject;

    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;

    bool allowHDR;

    public CustomRenderPipeline(
        bool useGPUInstacing, bool useDynamciBatching, 
        bool useSRPBatcher,ShadowSettings shadowSettings,
        bool useLightsPerObject,PostFXSettings postFXSettings,
        bool allowHDR) 
    {

        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.useGPUInstacing = useGPUInstacing;
        this.useDynamciBatching = useDynamciBatching;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.allowHDR = allowHDR;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }


    //旧版抽象方法,占位实现
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(context, cameras[i],
                useGPUInstacing, useDynamciBatching,
                shadowSettings, useLightsPerObject,
                postFXSettings,allowHDR);
        }
    }
}