using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


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
    /// Override Material 绘制 Custom Back Depth（Cull Front）。
    /// 写入临时 RT 时可用独立投影；结束后必须 SetupCameraProperties 恢复，且不得再改前向 VP。
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

        Matrix4x4 view = camera.worldToCameraMatrix;
        Matrix4x4 projIntoRT = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        buffer.SetViewProjectionMatrices(view, projIntoRT);
        buffer.SetGlobalMatrix(matrixInvPId, projIntoRT.inverse);
        buffer.SetGlobalMatrix(matrixInvVPId, (projIntoRT * view).inverse);
        // Y 翻转会反转缠绕，InvertCulling 使 Cull Front 仍表示剔世界空间正面
        buffer.SetInvertCulling(true);

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

        buffer.SetInvertCulling(false);
        buffer.SetGlobalTexture(
            cameraCustomBackDepthTextureId, cameraCustomBackDepthTextureId
        );
        // BackDepth 为 RT 投影；前向用 SetupCameraProperties，采样时翻 Y 对齐
        buffer.SetGlobalFloat(customBackDepthUVFlipId, 1f);
        buffer.EndSample("Custom Back Depth");
        ExecuteBuffer();

        // 只恢复相机状态与渲染目标，不要 SetViewProjectionMatrices（会弄坏前向）
        context.SetupCameraProperties(camera);
        if (useIntermediateBuffer)
        {
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        buffer.SetGlobalMatrix(matrixInvPId, projIntoRT.inverse);
        buffer.SetGlobalMatrix(matrixInvVPId, (projIntoRT * view).inverse);
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
