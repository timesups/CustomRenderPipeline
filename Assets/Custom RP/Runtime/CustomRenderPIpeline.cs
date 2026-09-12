using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;


public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRender renderer = new CameraRender();
    bool useGPUInstacing, useDynamciBatching, useLightsPerObject;

    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;

    bool allowHDR;
    bool opaqueTexture;

    int colorLUTRes;

    Material customBackDepthMaterial;

    //渲染图表
    readonly RenderGraph renderGraph = new("Custom SRP Render Graph");



    public CustomRenderPipeline(
        bool useGPUInstacing, bool useDynamciBatching,
        bool useSRPBatcher,ShadowSettings shadowSettings,
        bool useLightsPerObject,PostFXSettings postFXSettings,
        bool allowHDR,
        int colorLUTRes,
        bool opaqueTexture,
        Shader customBackDepthShader)
    {

        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.useGPUInstacing = useGPUInstacing;
        this.useDynamciBatching = useDynamciBatching;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.allowHDR = allowHDR;
        this.colorLUTRes = colorLUTRes;
        this.opaqueTexture = opaqueTexture;
        GraphicsSettings.lightsUseLinearIntensity = true;

        if (customBackDepthShader != null)
        {
            customBackDepthMaterial = CoreUtils.CreateEngineMaterial(customBackDepthShader);
        }

        InitializeForEditor();
    }


    protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(
                renderGraph,context, cameras[i],
                useGPUInstacing, useDynamciBatching,
                shadowSettings, useLightsPerObject,
                postFXSettings,allowHDR,colorLUTRes, opaqueTexture,
                customBackDepthMaterial);
        }
        renderGraph.EndFrame();
    }

    protected override void Dispose(bool disposing)
    {
        renderGraph.Cleanup();
        base.Dispose(disposing);
        CoreUtils.Destroy(customBackDepthMaterial);
        DisposeForEditor();
    }
}
