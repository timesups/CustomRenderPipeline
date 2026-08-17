using UnityEngine;
using UnityEngine.Rendering;



 public partial class PostFXStack 
{
    enum Pass 
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomPrefilter,
        BloomPrefilterFireflies,
        Copy
    }

    const string bufferName = "Post FX";


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;
    PostFXSettings settings;

    public bool IsActive => settings != null;
    bool allowHDR;

    int fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomPerfilterId = Shader.PropertyToID("_BloomPerfilter"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        bicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");



    //bloom
    const int maxBloomPyramidLevels = 16;

    int bloomPyramidId;
    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++) 
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    void DoBloom(int sourceId) 
    {
        PostFXSettings.BloomSettings bloom = settings.Bloom;
        buffer.BeginSample("Bloom");
        int width = camera.pixelWidth/2, height = camera.pixelHeight/2;
        if (
            bloom.maxInterations == 0 ||
            height < bloom.downscaleLimit * 2 ||
            width < bloom.downscaleLimit * 2 ||
            bloom.intensity <= 0f
            ) 
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }

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
        buffer.ReleaseTemporaryRT(bloomPerfilterId);

        buffer.SetGlobalFloat(bicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        buffer.SetGlobalFloat(bloomIntensityId, 1.0f);
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);

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
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
    }




    public void Setup(
        ScriptableRenderContext context,
        Camera camera,PostFXSettings settings,
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

    public void Render(int sourceId) 
    {
        DoBloom(sourceId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

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

}