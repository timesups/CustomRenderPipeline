using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


public partial class CameraRender
{
    ScriptableRenderContext context;
    Camera camera;

    CullingResults cullingResults;
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName,
    };

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();

    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

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
        Setup();
        DrawVisibleGeometry(useGPUInstacing, useDynamciBatching, useLightsPerObject);
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
    void DrawVisibleGeometry(
        bool useGPUInstacing, bool useDynamciBatching, bool useLightsPerObject)
    {
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
    void Setup()
    {
        context.SetupCameraProperties(camera);//�����������

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
                allowHDR? RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default
                );
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store
                );
        }


        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags<=CameraClearFlags.Color,
            flags == CameraClearFlags.Color?
            camera.backgroundColor.linear:Color.clear);//�����ȾĿ��
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
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
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }
}