using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;



partial class CameraRender
{
    partial void DrawGizmos();
    partial void DrawUnsupportedShaders();
    partial void PrepareForSceneWindow();
    partial void PrepareBuffer();
#if UNITY_EDITOR
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PerpassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    static Material errorMaterial;

    string SampleName { get; set; }
    partial void PrepareBuffer() 
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }
    partial void PrepareForSceneWindow() 
    {
        if (camera.cameraType == CameraType.SceneView) 
        {
            //向场景中添加几何体
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
    }
    partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawUnsupportedShaders() 
    {
        if (errorMaterial == null) 
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }


        var drawsettings = new DrawingSettings(
                legacyShaderTagIds[0], new SortingSettings(camera)
            )
        {
            overrideMaterial = errorMaterial
        };

        for (int i = 1; i < legacyShaderTagIds.Length; i++) 
        {
            drawsettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }

        var filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(
                cullingResults,ref drawsettings,ref filteringSettings
            );
    }
#else
    string SampleName => bufferName;
#endif
}