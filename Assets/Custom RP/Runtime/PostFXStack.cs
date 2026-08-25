using UnityEngine;
using UnityEngine.Rendering;



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
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
        Copy
    }


    int fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomPerfilterId = Shader.PropertyToID("_BloomPerfilter"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        bloomResultId = Shader.PropertyToID("_BloomResult"),
        bicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");

    int bloomPyramidId;
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



    //############################色调映射###################################//


    void DoToneMapping(int sourceId) 
    {
        PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = mode < 0 ? Pass.Copy : Pass.ToneMappingACES+(int)mode;
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
    }


    public void Render(int sourceId)
    {
        if (DoBloom(sourceId))
        {
            DoToneMapping(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else 
        {
            DoToneMapping(sourceId);
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Setup(
    ScriptableRenderContext context,
    Camera camera, PostFXSettings settings,
    bool allowHDR
    )
    {
        this.context = context;
        this.camera = camera;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;
        this.allowHDR = allowHDR;
        ApplySceneViewState();
    }
}