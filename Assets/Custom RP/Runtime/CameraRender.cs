using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;



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

    static readonly int
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        depthTextureId = Shader.PropertyToID("_CameraDepthTextgure"),
        cameraOpaqueTextureId = Shader.PropertyToID("_CameraOpaqueTexture"),
        cameraCustomBackDepthTextureId = Shader.PropertyToID("_CameraCustomBackDepthTexture"),
        matrixInvPId = Shader.PropertyToID("unity_MatrixInvP"),
        matrixInvVPId = Shader.PropertyToID("unity_MatrixInvVP"),
        customBackDepthUVFlipId = Shader.PropertyToID("_CustomBackDepthUVFlip");

    bool allowHDR;
    bool opaqueTexture;
    bool useIntermediateBuffer;
    bool hasCustomBackDepthTexture;
    bool useDepthTexture;
    Material customBackDepthMaterial;

    static CameraSettings defaultCameraSettings = new CameraSettings();

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

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

        useDepthTexture = true;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();

        lighting.Setup(
            context,cullingResults,shadowSettings,
            useLightsPerObject,
            cameraSettings.maskLights?cameraSettings.renderingLayerMask : -1);


        if(cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }


        postFXStack.Setup(context, camera, postFXSettings,
        allowHDR,colorLUTRes,cameraSettings.finalBlendMode);
        useIntermediateBuffer = postFXStack.IsActive || useDepthTexture;

        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useGPUInstacing, useDynamciBatching, useLightsPerObject,cameraSettings.renderingLayerMask);
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
            postFXStack.Render(colorAttachmentId);
        }
        DrawGizmosAfterFX();


        Cleanup();
        Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);

    }

    void DrawVisibleGeometry(
        bool useGPUInstacing, bool useDynamciBatching, bool useLightsPerObject,int renderingLayerMask)
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
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque,renderingLayerMask:(uint) renderingLayerMask);


        DrawRendererList(drawingSettings,filterSettings);


        //绘制天空盒
        RendererList skyboxList = context.CreateSkyboxRendererList(camera);
        buffer.DrawRendererList(skyboxList);
        ExecuteBuffer();
        //将不透明物体绘制到一张单独的RT上
        CopyOpaqueColor();
        CopyAttachments();

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
                colorAttachmentId,
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
            buffer.Blit(colorAttachmentId, cameraOpaqueTextureId);
            buffer.SetRenderTarget(
                colorAttachmentId,
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
                colorAttachmentId, camera.pixelWidth, camera.pixelHeight,
                0, FilterMode.Bilinear,
                allowHDR? RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default
                );
            buffer.GetTemporaryRT(
                depthAttachmentId,camera.pixelWidth,camera.pixelHeight,
                32,FilterMode.Point,RenderTextureFormat.Depth
            );

            buffer.SetRenderTarget(
                colorAttachmentId,
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store,
                depthAttachmentId,
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


        if(useDepthTexture)
        {
            buffer.ReleaseTemporaryRT(depthAttachmentId);
        }

        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);
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


    void CopyAttachments()
    {
        if(useDepthTexture)
        {
            buffer.GetTemporaryRT(
                depthTextureId,camera.pixelWidth,camera.pixelHeight,
                32,FilterMode.Point,RenderTextureFormat.Depth
            );
            buffer.CopyTexture(depthAttachmentId,depthTextureId);
            ExecuteBuffer();
        }
    }
}
