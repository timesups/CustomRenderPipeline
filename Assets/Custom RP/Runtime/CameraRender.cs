using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public partial class CameraRender
{
    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

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
        bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
        depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
        sourceTextureId = Shader.PropertyToID("_SourceTexture"),
        srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
        dstBlendId = Shader.PropertyToID("_CameraDstBlend"),
        cameraOpaqueTextureId = Shader.PropertyToID("_CameraOpaqueTexture"),
        cameraCustomBackDepthTextureId =
            Shader.PropertyToID("_CameraCustomBackDepthTexture"),
        matrixInvPId = Shader.PropertyToID("unity_MatrixInvP"),
        matrixInvVPId = Shader.PropertyToID("unity_MatrixInvVP"),
        customBackDepthUVFlipId = Shader.PropertyToID("_CustomBackDepthUVFlip");

    static bool copyTextureSupported =
        SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

    bool allowHDR;
    bool opaqueTexture;
    bool useIntermediateBuffer;
    bool useScaledRendering;
    bool hasCustomBackDepthTexture;
    bool useColorTexture;
    bool useDepthTexture;
    Vector2Int bufferSize;
    Material customBackDepthMaterial;
    Material material;
    Texture2D missingTexture;

    static CameraSettings defaultCameraSettings = new CameraSettings();

    public CameraRender(Shader cameraRendererShader)
    {
        if (cameraRendererShader == null)
        {
            cameraRendererShader = Shader.Find("Hidden/Custom RP/Camera Renderer");
        }
        if (cameraRendererShader != null)
        {
            material = CoreUtils.CreateEngineMaterial(cameraRendererShader);
        }
        else
        {
            Debug.LogError(
                "Custom RP: Camera Renderer shader missing. Assign it on the Pipeline Asset."
            );
        }

        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    public void Render(
        RenderGraph renderGraph,
        ScriptableRenderContext context, Camera camera,
        CameraBufferSettings bufferSettings,
        bool useGPUInstacing, bool useDynamciBatching,
        ShadowSettings shadowSettings,
        bool useLightsPerObject,
        PostFXSettings postFXSettings,
        int colorLUTRes, bool opaqueTexture,
        Material customBackDepthMaterial)
    {
        this.context = context;
        this.camera = camera;
        this.opaqueTexture = opaqueTexture;
        this.customBackDepthMaterial = customBackDepthMaterial;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings =
            crpCamera ? crpCamera.Settings : defaultCameraSettings;

        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = bufferSettings.copyColorReflection;
            useDepthTexture = bufferSettings.copyDepthReflection;
        }
        else
        {
            useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
        useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        allowHDR = bufferSettings.allowHDR && camera.allowHDR;
        if (useScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }

        buffer.BeginSample(SampleName);
        buffer.SetGlobalVector(bufferSizeId, new Vector4(
            1f / bufferSize.x, 1f / bufferSize.y,
            bufferSize.x, bufferSize.y
        ));
        ExecuteBuffer();

        lighting.Setup(
            context, cullingResults, shadowSettings,
            useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1
        );

        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
        postFXStack.Setup(
            context, camera, bufferSize, postFXSettings,
            cameraSettings.keepAlpha, allowHDR, colorLUTRes,
            cameraSettings.finalBlendMode,
            bufferSettings.bicubicRescaling, bufferSettings.fxaa
        );
        useIntermediateBuffer =
            useScaledRendering || useColorTexture || useDepthTexture ||
            postFXStack.IsActive;

        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(
            useGPUInstacing, useDynamciBatching, useLightsPerObject,
            cameraSettings.renderingLayerMask
        );
        DrawUnsupportedShaders();
        DrawGizmosBeforFX();

        if (postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentId);
        }
        else if (useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
        }

        DrawGizmosAfterFX();
        Cleanup();
        Submit();
    }

    void Draw(
        RenderTargetIdentifier from, RenderTargetIdentifier to,
        bool isDepth = false
    )
    {
        if (material == null)
        {
            return;
        }
        int pass = isDepth ? 1 : 0;
        if (pass >= material.passCount)
        {
            Debug.LogError(
                $"Custom RP Camera Renderer material '{material.shader.name}' " +
                $"has no pass {pass}. Assign Hidden/Custom RP/Camera Renderer."
            );
            return;
        }

        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.DrawProcedural(
            Matrix4x4.identity, material, pass,
            MeshTopology.Triangles, 3
        );
    }

    void DrawVisibleGeometry(
        bool useGPUInstacing, bool useDynamciBatching,
        bool useLightsPerObject, int renderingLayerMask
    )
    {
        DrawCustomBackDepth();

        PerObjectData lightsPerObjectFlags = useLightsPerObject
            ? PerObjectData.LightData | PerObjectData.LightIndices
            : PerObjectData.None;

        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableInstancing = useGPUInstacing,
            enableDynamicBatching = useDynamciBatching,
            perObjectData = PerObjectData.Lightmaps |
                            PerObjectData.LightProbe |
                            PerObjectData.LightProbeProxyVolume |
                            PerObjectData.ShadowMask |
                            PerObjectData.OcclusionProbe |
                            PerObjectData.OcclusionProbeProxyVolume |
                            PerObjectData.ReflectionProbes |
                            lightsPerObjectFlags,
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        var filterSettings = new FilteringSettings(
            RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask
        );

        DrawRendererList(drawingSettings, filterSettings);

        RendererList skyboxList = context.CreateSkyboxRendererList(camera);
        buffer.DrawRendererList(skyboxList);
        ExecuteBuffer();

        CopyOpaqueColor();
        if (useColorTexture || useDepthTexture)
        {
            CopyAttachments();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        filterSettings.renderingLayerMask = uint.MaxValue;
        DrawRendererList(drawingSettings, filterSettings);
    }

    void DrawRendererList(
        DrawingSettings drawingSettings, FilteringSettings filteringSettings
    )
    {
        var param = new RendererListParams(
            cullingResults, drawingSettings, filteringSettings
        );
        RendererList list = context.CreateRendererList(ref param);
        buffer.DrawRendererList(list);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

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
            bufferSize.x, bufferSize.y, 32,
            FilterMode.Point, RenderTextureFormat.ARGBFloat
        );
        buffer.SetRenderTarget(
            cameraCustomBackDepthTextureId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true, true, Color.clear);

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
            if (renderer == null || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
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
        RestoreCameraAttachments(true);
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
            bufferSize.x, bufferSize.y, 0,
            FilterMode.Bilinear,
            allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
        );

        if (useIntermediateBuffer)
        {
            buffer.Blit(colorAttachmentId, cameraOpaqueTextureId);
        }
        else
        {
            buffer.Blit(BuiltinRenderTextureType.CameraTarget, cameraOpaqueTextureId);
        }
        RestoreCameraAttachments(true);

        buffer.SetGlobalTexture(cameraOpaqueTextureId, cameraOpaqueTextureId);
        buffer.EnableShaderKeyword("_CAMERA_OPAQUE_TEXTURE");
        buffer.EndSample("Copy Opaque Color");
        ExecuteBuffer();
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);

        CameraClearFlags flags = camera.clearFlags;

        if (useIntermediateBuffer)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }

            buffer.GetTemporaryRT(
                colorAttachmentId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear,
                allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            buffer.GetTemporaryRT(
                depthAttachmentId, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth
            );
            buffer.SetRenderTarget(
                colorAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color
                ? camera.backgroundColor.linear
                : Color.clear
        );
        buffer.BeginSample(SampleName);
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
        buffer.SetGlobalTexture(colorTextureId, missingTexture);
        buffer.SetGlobalTexture(depthTextureId, missingTexture);
        ExecuteBuffer();
    }

    void RestoreCameraAttachments(bool load)
    {
        if (!useIntermediateBuffer)
        {
            buffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
            buffer.SetViewport(camera.pixelRect);
            return;
        }

        var loadAction = load
            ? RenderBufferLoadAction.Load
            : RenderBufferLoadAction.DontCare;
        buffer.SetRenderTarget(
            colorAttachmentId, loadAction, RenderBufferStoreAction.Store,
            depthAttachmentId, loadAction, RenderBufferStoreAction.Store
        );
    }

    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        if (material == null)
        {
            return;
        }

        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero &&
            camera.rect == fullViewRect
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
        );
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(
            Matrix4x4.identity, material, 0,
            MeshTopology.Triangles, 3
        );
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }

    void CopyAttachments()
    {
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(
                colorTextureId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear,
                allowHDR
                    ? RenderTextureFormat.DefaultHDR
                    : RenderTextureFormat.Default
            );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(colorAttachmentId, colorTextureId);
            }
            else
            {
                Draw(colorAttachmentId, colorTextureId);
            }
        }

        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(
                depthTextureId, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth
            );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(depthAttachmentId, depthTextureId);
            }
            else
            {
                Draw(depthAttachmentId, depthTextureId, true);
            }
        }

        if (!copyTextureSupported)
        {
            RestoreCameraAttachments(true);
        }

        if (useColorTexture)
        {
            buffer.SetGlobalTexture(colorTextureId, colorTextureId);
        }
        if (useDepthTexture)
        {
            buffer.SetGlobalTexture(depthTextureId, depthTextureId);
        }
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
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);
        }
        if (useColorTexture)
        {
            buffer.ReleaseTemporaryRT(colorTextureId);
        }
        if (useDepthTexture)
        {
            buffer.ReleaseTemporaryRT(depthTextureId);
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
