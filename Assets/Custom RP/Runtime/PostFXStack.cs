using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using static PostFXSettings;



 public partial class PostFXStack
{
    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    Camera camera;
    PostFXSettings settings;
    CommandBuffer buffer;

    CameraSettings.FinalBlendMode finalBlendModel;
    Vector2Int bufferSize;
    CameraBufferSettings.BicubicRescalingMode bicubicRescaling;
    CameraBufferSettings.FXAA fxaa;
    bool keepAlpha;
    bool allowHDR;

    public bool IsActive => settings != null;

    const string
        fxaaQualityLowKeyword = "FXAA_QUALITY_LOW",
        fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM",
        bloomAdditiveKeyword = "BLOOM_ADDITIVE",
        bloomScatteringKeyword = "BLOOM_SCATTERING";

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        buffer.DrawProcedural(
               Matrix4x4.identity, settings.Material, (int)pass,
               MeshTopology.Triangles, 3);
    }
	static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
    int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
        finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");

    void DrawFinal(RenderTargetIdentifier from, Pass pass)
    {
        buffer.SetGlobalFloat(finalSrcBlendId,(float)finalBlendModel.source);
        buffer.SetGlobalFloat(finalDstBlendId,(float)finalBlendModel.destination);
        buffer.SetGlobalTexture(fxSourceId, from);

        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendModel.destination == BlendMode.Zero && camera.rect == fullViewRect ?
                RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
            );
        buffer.SetViewport(camera.pixelRect);


        buffer.DrawProcedural(
               Matrix4x4.identity, settings.Material, (int)pass,
               MeshTopology.Triangles, 3);
    }

    //############################Bloom###################################//
    const int maxBloomPyramidLevels = 16;
    enum Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        BloomPrefilter,
        BloomPrefilterFireflies,
        ColorGradingNone,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
        ApplyColorGrading,
        FinalRescale,
        FXAA,
        ApplyColorGradingWithLuma,
        FXAAWithLuma,
        Copy
    }


    int fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomPerfilterId = Shader.PropertyToID("_BloomPerfilter"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        bloomResultId = Shader.PropertyToID("_BloomResult"),
        colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
        ColorFilterId = Shader.PropertyToID("_ColorFilter"),

        whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),

        splitToningShadowId = Shader.PropertyToID("_SplitToningShadow"),
        splitToningHightLihgtsId = Shader.PropertyToID("_SplitToningHightLight"),

        channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
        channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
        channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
        smhShadowsId = Shader.PropertyToID("_SMHShadows"),
        smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
        smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
        smhRangeId = Shader.PropertyToID("_SMHRange"),

        colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),

        colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),

        colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),

        bicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
        colorGradingResultId = Shader.PropertyToID("_ColorGradingResult"),
        finalResultId = Shader.PropertyToID("_FinalResult"),
        fxaaConfigId = Shader.PropertyToID("_FXAAConfig");


    int bloomPyramidId;

    int colorLUTRes;


    bool DoBloom(int sourceId)
    {
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width, height;
        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        }
        else
        {
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }
        if (
            bloom.maxInterations == 0 ||
            height < bloom.downscaleLimit * 2 ||
            width < bloom.downscaleLimit * 2 ||
            bloom.intensity <= 0f
            )
        {
            return false;
        }

        buffer.BeginSample("Bloom");

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);


        RenderTextureFormat format =
            allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default;




        buffer.GetTemporaryRT(
                bloomPerfilterId,width,height,0,FilterMode.Bilinear,format
            );

        Draw(sourceId, bloomPerfilterId,
            bloom.fadeFireFiles?Pass.BloomPrefilterFireflies:Pass.BloomPrefilter);
        width /= 2;
        height /= 2;

        int fromId = bloomPerfilterId, toId = bloomPyramidId+1;
        int i;
        for (i = 0; i < bloom.maxInterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }
            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width,
                height, 0, FilterMode.Bilinear, format);

            buffer.GetTemporaryRT(toId, width,
                height, 0, FilterMode.Bilinear, format);

            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        Pass combinePass;

        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1.0f);
        }
        else
        {
            combinePass = Pass.BloomScatter;
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
        }

        buffer.ReleaseTemporaryRT(bloomPerfilterId);
        buffer.SetGlobalFloat(bicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);

                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }

        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }

        // 只输出 bloom，与原图的合成挪到 Apply Color Grading
        buffer.SetGlobalFloat(bloomIntensityId, 1f);
        buffer.SetGlobalTexture(fxSource2Id, Texture2D.blackTexture);
        buffer.GetTemporaryRT(
            bloomResultId, bufferSize.x, bufferSize.y, 0,
            FilterMode.Bilinear, format
        );
        Draw(fromId, bloomResultId, Pass.BloomAdd);
        buffer.ReleaseTemporaryRT(fromId);

        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
        buffer.SetGlobalTexture(bloomResultId, bloomResultId);
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            buffer.EnableShaderKeyword(bloomAdditiveKeyword);
            buffer.DisableShaderKeyword(bloomScatteringKeyword);
        }
        else
        {
            buffer.EnableShaderKeyword(bloomScatteringKeyword);
            buffer.DisableShaderKeyword(bloomAdditiveKeyword);
        }

        buffer.EndSample("Bloom");
        return true;
    }


    void ConfigureColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;

        buffer.SetGlobalVector(colorAdjustmentsId,
            new Vector4(
                (float)Math.Pow(2f,colorAdjustments.postExposure),
                colorAdjustments.contrast * 0.01f + 1f,
                colorAdjustments.hueShift * (1f/360f),
                colorAdjustments.saturation * 0.01f + 1f
            )
        );

        buffer.SetGlobalColor(ColorFilterId, colorAdjustments.colorFilter.linear);

    }

    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalanceSettings = settings.WhiteBalance;
        buffer.SetGlobalVector(
            whiteBalanceId,
            ColorUtils.ColorBalanceToLMSCoeffs(
                whiteBalanceSettings.temperature,whiteBalanceSettings.tint
            )
        );

    }


    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowId,splitColor);
        buffer.SetGlobalColor(splitToningHightLihgtsId,splitToning.highlights);
    }

    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId,channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId,channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId,channelMixer.blue);
    }

	void ConfigureShadowsMidtonesHighlights () {
		ShadowMidtonesHighlightsSettings smh = settings.ShadowMidtonesHightlights;
		buffer.SetGlobalColor(smhShadowsId, smh.shadow.linear);
		buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
		buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
		buffer.SetGlobalVector(smhRangeId, new Vector4(
			smh.shadowStart, smh.shadowEnd, smh.highlightsStart, smh.highLightsEnd
		));
	}


    void ConfigureFXAA()
    {
        if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
        {
            buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
        }
        else
        {
            buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
            buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
        }
        buffer.SetGlobalVector(fxaaConfigId, new Vector4(
            fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending
        ));
    }

    void DoFinal(int sourceId)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();

        int lutHeight = colorLUTRes;
        int lutWidth = lutHeight * lutHeight;
        buffer.GetTemporaryRT(
            colorGradingLUTId, lutWidth, lutHeight, 0,
            FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
        );

        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
            lutHeight,
            0.5f / lutWidth,
            0.5f / lutHeight,
            lutHeight / (lutHeight - 1f)
        ));

        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = Pass.ColorGradingNone + (int)mode;

        buffer.SetGlobalFloat(
            colorGradingLUTInLogId,
            allowHDR && pass != Pass.ColorGradingNone ? 1f : 0f
        );

        Draw(sourceId, colorGradingLUTId, pass);

        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
            1f / lutWidth, 1f / lutHeight, lutHeight - 1f
        ));

        buffer.SetGlobalFloat(finalSrcBlendId, 1f);
        buffer.SetGlobalFloat(finalDstBlendId, 0f);
        if (fxaa.enabled)
        {
            ConfigureFXAA();
            buffer.GetTemporaryRT(
                colorGradingResultId, bufferSize.x, bufferSize.y, 0,
                FilterMode.Bilinear, RenderTextureFormat.Default
            );
            Draw(
                sourceId, colorGradingResultId,
                keepAlpha ? Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma
            );
        }

        if (bufferSize.x == camera.pixelWidth)
        {
            if (fxaa.enabled)
            {
                DrawFinal(
                    colorGradingResultId,
                    keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma
                );
                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            else
            {
                DrawFinal(sourceId, Pass.ApplyColorGrading);
            }
        }
        else
        {
            buffer.GetTemporaryRT(
                finalResultId, bufferSize.x, bufferSize.y, 0,
                FilterMode.Bilinear, RenderTextureFormat.Default
            );
            if (fxaa.enabled)
            {
                Draw(
                    colorGradingResultId, finalResultId,
                    keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma
                );
                buffer.ReleaseTemporaryRT(colorGradingResultId);
            }
            else
            {
                Draw(sourceId, finalResultId, Pass.ApplyColorGrading);
            }
            bool bicubicSampling =
                bicubicRescaling ==
                    CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                bicubicRescaling ==
                    CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                bufferSize.x < camera.pixelWidth;
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);
            DrawFinal(finalResultId, Pass.FinalRescale);
            buffer.ReleaseTemporaryRT(finalResultId);
        }
        buffer.ReleaseTemporaryRT(colorGradingLUTId);
    }


    public void Render(RenderGraphContext context, int sourceId)
    {
        buffer = context.cmd;
        bool bloomed = DoBloom(sourceId);
        if (!bloomed)
        {
            buffer.DisableShaderKeyword(bloomAdditiveKeyword);
            buffer.DisableShaderKeyword(bloomScatteringKeyword);
        }
        DoFinal(sourceId);
        if (bloomed)
        {
            buffer.ReleaseTemporaryRT(bloomResultId);
            buffer.DisableShaderKeyword(bloomAdditiveKeyword);
            buffer.DisableShaderKeyword(bloomScatteringKeyword);
        }
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Setup(
        Camera camera, Vector2Int bufferSize, PostFXSettings settings,
        bool keepAlpha, bool allowHDR, int colorLUTRes,
        CameraSettings.FinalBlendMode finalBlendMode,
        CameraBufferSettings.BicubicRescalingMode bicubicRescaling,
        CameraBufferSettings.FXAA fxaa
    )
    {
        this.bufferSize = bufferSize;
        this.camera = camera;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;
        this.keepAlpha = keepAlpha;
        this.allowHDR = allowHDR;
        this.colorLUTRes = colorLUTRes;
        this.finalBlendModel = finalBlendMode;
        this.bicubicRescaling = bicubicRescaling;
        this.fxaa = fxaa;
        ApplySceneViewState();
    }
}
