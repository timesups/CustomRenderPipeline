using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;



public partial class CameraRender
{
    ScriptableRenderContext context;
    Camera camera;

    CullingResults cullingResults;
    static ShaderTagId unlitShaderTagId = new("SRPDefaultUnlit"),
        litShaderTagId = new("CustomLit");

    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName,
    };

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();

    static readonly int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    static readonly int cameraOpaqueTextureId = Shader.PropertyToID("_CameraOpaqueTexture");
    static readonly int cameraCustomBackDepthTextureId =
        Shader.PropertyToID("_CameraCustomBackDepthTexture");
    static readonly int matrixInvPId = Shader.PropertyToID("unity_MatrixInvP");
    static readonly int matrixInvVPId = Shader.PropertyToID("unity_MatrixInvVP");
    static readonly int customBackDepthUVFlipId =
        Shader.PropertyToID("_CustomBackDepthUVFlip");

    bool allowHDR;
    bool opaqueTexture;
    bool useIntermediateBuffer;
    bool hasCustomBackDepthTexture;
    Material customBackDepthMaterial;


    public void Render(
        RenderGraph renderGraph,
        ScriptableRenderContext context, Camera camera,
        bool useGPUInstacing, bool useDynamciBatching,
        ShadowSettings shadowSettings,
        bool useLightsPerObject,
        PostFXSettings postFXSettings,bool allowHDR,
        int colorLUTRes, bool opaqueTexture,
        Material customBackDepthMaterial)
    {
        this.context = context;
        this.camera = camera;
        this.allowHDR = allowHDR;
        this.opaqueTexture = opaqueTexture;
        this.customBackDepthMaterial = customBackDepthMaterial;


        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings,useLightsPerObject);
        postFXStack.Setup(context, camera, postFXSettings,allowHDR,colorLUTRes);
        useIntermediateBuffer = postFXStack.IsActive;

        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useGPUInstacing, useDynamciBatching, useLightsPerObject);
        DrawUnsupportedShaders();

        DrawGizmosBeforFX();

        //render graph
        var renderGraphParameters = new RenderGraphParameters()
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = "Render Camera",
            scriptableRenderContext = context
        };






        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();


        Cleanup();
        Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);





    }

    void DrawVisibleGeometry(
        bool useGPUInstacing, bool useDynamciBatching, bool useLightsPerObject)
    {
        //绘制自定义背面深度
        DrawCustomBackDepth();

        //绘制不透明物体
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


        DrawRendererList(drawingSettings,filterSettings);


        //绘制天空盒
        RendererList skyboxList = context.CreateSkyboxRendererList(camera);
        buffer.DrawRendererList(skyboxList);
        ExecuteBuffer();



        //将不透明物体绘制到一张单独的RT上
        CopyOpaqueColor();

        //绘制半透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        filterSettings.renderingLayerMask = uint.MaxValue;

        DrawRendererList(drawingSettings,filterSettings);



    }

    void DrawRendererList(DrawingSettings drawingSettings,FilteringSettings filteringSettings)
    {
        var param = new RendererListParams(cullingResults,drawingSettings,filteringSettings);
        RendererList opaqueList = context.CreateRendererList(ref param);
        buffer.DrawRendererList(opaqueList);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 绘制 Custom Back Depth。与前向使用同一套 VP（不翻转投影），
    /// 避免采样 UV 与相机俯仰错位滑动；Cull Front 语义正确，无需 InvertCulling。
    /// </summary>
    void DrawCustomBackDepth()
    {
        var targets = MeshRenderSetting.CustomBackDepthRenderers;
        if (targets.Count == 0 || customBackDepthMaterial == null)
        {
            return;
        }

        hasCustomBackDepthTexture = true;
        buffer.BeginSample("Custom Back Depth");
        buffer.GetTemporaryRT(
            cameraCustomBackDepthTextureId,
            camera.pixelWidth, camera.pixelHeight, 32,
            FilterMode.Point, RenderTextureFormat.ARGBFloat
        );
        buffer.SetRenderTarget(
            cameraCustomBackDepthTextureId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true, true, Color.clear);

        // 不改 VP：沿用 SetupCameraProperties，与水体前向像素一一对应
        Matrix4x4 view = camera.worldToCameraMatrix;
        bool renderIntoTexture = camera.targetTexture != null;
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(
            camera.projectionMatrix, renderIntoTexture
        );
        buffer.SetGlobalMatrix(matrixInvPId, proj.inverse);
        buffer.SetGlobalMatrix(matrixInvVPId, (proj * view).inverse);

        for (int i = 0; i < targets.Count; i++)
        {
            var setting = targets[i];
            if (setting == null)
            {
                continue;
            }
            var renderer = setting.CachedRenderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            int subMeshCount = renderer.sharedMaterials.Length;
            for (int submesh = 0; submesh < subMeshCount; submesh++)
            {
                buffer.DrawRenderer(renderer, customBackDepthMaterial, submesh, 0);
            }
        }

        buffer.SetGlobalTexture(
            cameraCustomBackDepthTextureId, cameraCustomBackDepthTextureId
        );
        buffer.SetGlobalFloat(customBackDepthUVFlipId, 0f);
        buffer.EndSample("Custom Back Depth");
        ExecuteBuffer();

        context.SetupCameraProperties(camera);
        if (useIntermediateBuffer)
        {
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        // 重建仍用与写入一致的 InvVP
        buffer.SetGlobalMatrix(matrixInvPId, proj.inverse);
        buffer.SetGlobalMatrix(matrixInvVPId, (proj * view).inverse);
        ExecuteBuffer();
    }

    void CopyOpaqueColor()
    {
        if (!opaqueTexture)
        {
            buffer.DisableShaderKeyword("_CAMERA_OPAQUE_TEXTURE");
            ExecuteBuffer();
            return;
        }

        buffer.BeginSample("Copy Opaque Color");
        buffer.GetTemporaryRT(
            cameraOpaqueTextureId,
            camera.pixelWidth, camera.pixelHeight, 0,
            FilterMode.Bilinear,
            allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
        );

        if (useIntermediateBuffer)
        {
            buffer.Blit(frameBufferId, cameraOpaqueTextureId);
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        else
        {
            buffer.Blit(BuiltinRenderTextureType.CameraTarget, cameraOpaqueTextureId);
            buffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }

        buffer.SetGlobalTexture(cameraOpaqueTextureId, cameraOpaqueTextureId);
        buffer.EnableShaderKeyword("_CAMERA_OPAQUE_TEXTURE");
        buffer.EndSample("Copy Opaque Color");
        ExecuteBuffer();
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);//设置相机属性

        CameraClearFlags flags = camera.clearFlags;

        if (useIntermediateBuffer)
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
            camera.backgroundColor.linear:Color.clear);//清除渲染目标
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
        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
        if (opaqueTexture)
        {
            buffer.ReleaseTemporaryRT(cameraOpaqueTextureId);
        }
        if (hasCustomBackDepthTexture)
        {
            buffer.ReleaseTemporaryRT(cameraCustomBackDepthTextureId);
            hasCustomBackDepthTexture = false;
        }
    }
}
