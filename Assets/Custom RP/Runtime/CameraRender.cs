using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


public partial class CameraRender
{
    ScriptableRenderContext context;
    Camera camera;

    CullingResults cullingResults;
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit"),
        CustomDepthTagId = new ShaderTagId("CustomDepth"),
        OutlineStencilTagId = new ShaderTagId("OutlineStencil"),
        OutlineTagId = new ShaderTagId("Outline");

    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName,
    };

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();

    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer"),
        SceneColorID = Shader.PropertyToID("_SceneColor"),
        CustomDepthBufferId = Shader.PropertyToID("_CustomDepth");

    bool allowHDR;

    public void Render(
        ScriptableRenderContext context, Camera camera,
        bool useGPUInstacing, bool useDynamciBatching,
        ShadowSettings shadowSettings,
        bool useLightsPerObject,PostFXSettings postFXSettings,bool allowHDR)
    {
        this.context = context;
        this.camera = camera;
        this.allowHDR = allowHDR;


        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings,useLightsPerObject);
        postFXStack.Setup(context, camera, postFXSettings,allowHDR);
        buffer.EndSample(SampleName);

        context.SetupCameraProperties(camera);

        DrawCustomDepth(useGPUInstacing, useDynamciBatching);
        DrawVisibleGeometry(useGPUInstacing, useDynamciBatching, useLightsPerObject);
        DrawOutline(useGPUInstacing, useDynamciBatching);


        DrawUnsupportedShaders();


        DrawGizmosBeforFX();
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();


        Cleanup();
        Submit();
    }
    //绘制描边：仅 OutlineLayer 物体先写 Stencil，再扩边
    void DrawOutline(
        bool useGPUInstacing, bool useDynamciBatching)
    {
        if (postFXStack.IsActive)
        {
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
                );
        }

        buffer.BeginSample(SampleName);
        ExecuteBuffer();

        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque,
        };

        var filterSettings = new FilteringSettings(
            RenderQueueRange.all, -1, MeshRenderSettings.OutlineLayer);

        var stencilDrawingSettings = new DrawingSettings(OutlineStencilTagId, sortingSettings)
        {
            enableInstancing = useGPUInstacing,
            enableDynamicBatching = useDynamciBatching
        };
        context.DrawRenderers(
            cullingResults,
            ref stencilDrawingSettings,
            ref filterSettings
            );

        var outlineDrawingSettings = new DrawingSettings(OutlineTagId, sortingSettings)
        {
            enableInstancing = useGPUInstacing,
            enableDynamicBatching = useDynamciBatching
        };
        context.DrawRenderers(
            cullingResults,
            ref outlineDrawingSettings,
            ref filterSettings
            );

        buffer.EndSample(SampleName);
        ExecuteBuffer();
    }
    //绘制自定义深度
    void DrawCustomDepth(
        bool useGPUInstacing, bool useDynamciBatching)
    {
        buffer.GetTemporaryRT(
            CustomDepthBufferId, camera.pixelWidth, camera.pixelHeight,
            24, FilterMode.Point,
            RenderTextureFormat.ARGBFloat
            );
        buffer.SetRenderTarget(
            CustomDepthBufferId,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store
            );
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();


        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque,
        };

        var drawingSettings = new DrawingSettings(CustomDepthTagId, sortingSettings)
        {
            enableInstancing = useGPUInstacing,
            enableDynamicBatching = useDynamciBatching
        };
        var filterSettings = new FilteringSettings(
            RenderQueueRange.all, -1, MeshRenderSettings.CustomDepthRenderingLayer);

        context.DrawRenderers(
            cullingResults,
            ref drawingSettings,
            ref filterSettings
            );



        buffer.SetGlobalTexture(CustomDepthBufferId, CustomDepthBufferId);
        buffer.EndSample(SampleName);
        ExecuteBuffer();
    }
    void DrawVisibleGeometry(
        bool useGPUInstacing, bool useDynamciBatching, bool useLightsPerObject)
    {
        CameraClearFlags flags = camera.clearFlags;
        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }

            buffer.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear,
                allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store
                );
        }

        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
            camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();

        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;

        var sortingSettings = new SortingSettings() {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableInstancing = useGPUInstacing,
            enableDynamicBatching = useDynamciBatching,
            perObjectData = PerObjectData.Lightmaps |
                            PerObjectData.LightProbe|
                            PerObjectData.LightProbeProxyVolume |
                            PerObjectData.ShadowMask|
                            PerObjectData.OcclusionProbe|
                            PerObjectData.OcclusionProbeProxyVolume|
                            PerObjectData.ReflectionProbes|
                            lightsPerObjectFlags,
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);

        //绘制所有不透明物体
        context.DrawRenderers(
            cullingResults,
            ref drawingSettings,
            ref filterSettings
            );
        //绘制天空盒
        context.DrawSkybox(camera);

        // 拷贝不透明结果供半透明采样（保持 HDR 线性，不在此做 tonemap）
        if (postFXStack.IsActive)
        {
            buffer.GetTemporaryRT(
                SceneColorID, camera.pixelWidth, camera.pixelHeight,
                0, FilterMode.Bilinear,
                allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
            buffer.Blit(frameBufferId, SceneColorID);
            buffer.SetGlobalTexture(SceneColorID, SceneColorID);
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
                );
            ExecuteBuffer();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;

        //绘制所有半透明物体
        context.DrawRenderers(
            cullingResults,
            ref drawingSettings,
            ref filterSettings
            );
    }
    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);//ִ��buffer
        buffer.Clear();
    }
    bool Cull(float maxShadowDistance)
    {
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);//execute culling
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(SceneColorID);
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
        buffer.ReleaseTemporaryRT(CustomDepthBufferId);
    }
}
