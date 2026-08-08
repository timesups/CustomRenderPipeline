using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;



public class CustomRenderPipeline : RenderPipeline 
{
    CameraRender renderer = new CameraRender();
    bool useGPUInstacing, useDynamciBatching;

    ShadowSettings shadowSettings;

    public CustomRenderPipeline(
        bool useGPUInstacing, bool useDynamciBatching, 
        bool useSRPBatcher,ShadowSettings shadowSettings) 
    {

        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.useGPUInstacing = useGPUInstacing;
        this.useDynamciBatching = useDynamciBatching;
        this.shadowSettings = shadowSettings;
        GraphicsSettings.lightsUseLinearIntensity = true;
    }


    //旧版抽象方法,占位实现
    protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(context, cameras[i], useGPUInstacing, useDynamciBatching,shadowSettings);
        }
    }
}