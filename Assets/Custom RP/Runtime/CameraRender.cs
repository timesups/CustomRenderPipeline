using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;



public partial class CameraRender
{
    ScriptableRenderContext context;
    Camera camera;

    CullingResults cullingResults;
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName,
    };

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;



        PrepareBuffer();
        //因为会向场景中添加Mesh,所以需要在剔除之前调用,保证新添加的对象可以被剔除
        PrepareForSceneWindow();
        if (!Cull()) 
        {
            return;
        }
        Setup();
        DrawVisibleGeometry();
        DrawUnsupportedShaders();
        DrawGizmos();
        Submit();
    }
    void DrawVisibleGeometry() 
    {
        var sortingSettings = new SortingSettings() { 
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings);
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(
            cullingResults,
            ref drawingSettings,
            ref filterSettings
            );
        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(
            cullingResults,
            ref drawingSettings,
            ref filterSettings
            );
    }
    void Setup()
    {
        context.SetupCameraProperties(camera);//传递相机属性

        CameraClearFlags flags = camera.clearFlags;

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
        context.Submit(); //提交命令
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);//执行buffer
        buffer.Clear();//清空buffer
    }
    bool Cull() 
    {
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p)) 
        {
            cullingResults = context.Cull(ref p);//execute culling 
            return true;
        }
        return false;
    }
}