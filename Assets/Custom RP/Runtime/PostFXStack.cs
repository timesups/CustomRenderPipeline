using System;
using UnityEngine;
using UnityEngine.Rendering;
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

    ScriptableRenderContext context;

    Camera camera;
    PostFXSettings settings;
    const string bufferName = "Post FX";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CameraSettings.FinalBlendMode finalBlendModel;

    public bool IsActive => settings != null;
    bool allowHDR;
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

    void DrawFinal(RenderTargetIdentifier from)
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
               Matrix4x4.identity, settings.Material, (int)Pass.Final,
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
        Final,
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

        bicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");


    int bloomPyramidId;

    int colorLUTRes;


    bool DoBloom(int sourceId)
    {
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth/2, height = camera.pixelHeight/2;
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
        Pass combinePass, finalPass;


        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1.0f);
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
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
        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
        buffer.SetGlobalTexture(fxSource2Id, sourceId);

        buffer.GetTemporaryRT(
            bloomResultId, camera.pixelWidth, camera.pixelHeight, 0,
            FilterMode.Bilinear, format
        );

        Draw(fromId, bloomResultId, finalPass);
        buffer.ReleaseTemporaryRT(fromId);
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


    void DotColorGradingAdnToneMapping(int sourceId)
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

        // GetLutStripValue 参数：烘焙 LUT 用
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

        // 烘焙 Color Grading + Tone Mapping 到 LUT
        Draw(sourceId, colorGradingLUTId, pass);

        // ApplyLut2D 参数：应用 LUT 用
        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
            1f / lutWidth, 1f / lutHeight, lutHeight - 1f
        ));

        DrawFinal(sourceId);
        buffer.ReleaseTemporaryRT(colorGradingLUTId);
    }


    public void Render(int sourceId)
    {
        if (DoBloom(sourceId))
        {
            DotColorGradingAdnToneMapping(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DotColorGradingAdnToneMapping(sourceId);
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Setup(
    ScriptableRenderContext context,
    Camera camera, PostFXSettings settings,
    bool allowHDR,int colorLUTRes,CameraSettings.FinalBlendMode finalBlendMode
    )
    {
        this.context = context;
        this.camera = camera;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;
        this.allowHDR = allowHDR;
        this.colorLUTRes = colorLUTRes;
        this.finalBlendModel = finalBlendMode;
        ApplySceneViewState();
    }
}
