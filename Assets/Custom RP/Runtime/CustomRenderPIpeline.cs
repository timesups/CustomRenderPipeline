using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRender renderer;
    bool useGPUInstacing, useDynamciBatching, useLightsPerObject;

    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    CameraBufferSettings cameraBufferSettings;
    bool opaqueTexture;
    int colorLUTRes;
    Material customBackDepthMaterial;

    readonly RenderGraph renderGraph = new("Custom SRP Render Graph");

    public CustomRenderPipeline(
        CameraBufferSettings cameraBufferSettings,
        bool useGPUInstacing, bool useDynamciBatching,
        bool useSRPBatcher, ShadowSettings shadowSettings,
        bool useLightsPerObject, PostFXSettings postFXSettings,
        int colorLUTRes,
        bool opaqueTexture,
        Shader customBackDepthShader,
        Shader cameraRendererShader)
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.cameraBufferSettings = cameraBufferSettings;
        this.useGPUInstacing = useGPUInstacing;
        this.useDynamciBatching = useDynamciBatching;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.colorLUTRes = colorLUTRes;
        this.opaqueTexture = opaqueTexture;
        GraphicsSettings.lightsUseLinearIntensity = true;

        renderer = new CameraRender(cameraRendererShader);

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
                renderGraph, context, cameras[i],
                cameraBufferSettings,
                useGPUInstacing, useDynamciBatching,
                shadowSettings, useLightsPerObject,
                postFXSettings, colorLUTRes, opaqueTexture,
                customBackDepthMaterial
            );
        }
        renderGraph.EndFrame();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        renderer.Dispose();
        CoreUtils.Destroy(customBackDepthMaterial);
        renderGraph.Cleanup();
    }
}
