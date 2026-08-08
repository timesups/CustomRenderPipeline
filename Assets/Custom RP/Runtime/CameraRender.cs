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

    public void Render(
        ScriptableRenderContext context, Camera camera,
        bool useGPUInstacing, bool useDynamciBatching,ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;


        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance)) 
        {
            return;
        }
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context,cullingResults,shadowSettings);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useGPUInstacing, useDynamciBatching);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();
        Submit();
    }
    void DrawVisibleGeometry(bool useGPUInstacing,bool useDynamciBatching) 
    {
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
                            PerObjectData.OcclusionProbeProxyVolume,
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
        context.Submit(); //�ύ����
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
}