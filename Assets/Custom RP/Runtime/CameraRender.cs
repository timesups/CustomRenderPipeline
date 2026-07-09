using UnityEngine;
using UnityEngine.Rendering;




public class CameraRender
{
    ScriptableRenderContext context;
    Camera camera;

    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName,
    };

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

        Setup();
        DrawVisibleGeometry();
        Submit();
    }
    void DrawVisibleGeometry() 
    {
        context.DrawSkybox(camera);
    }
    void Setup()
    {
        context.SetupCameraProperties(camera);//设置相机矩阵
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
    }
    void Submit() 
    {
        buffer.EndSample(bufferName);
        ExecuteBuffer();
        context.Submit(); //提交命令
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);//执行buffer
        buffer.Clear();//清空buffer
    }
}