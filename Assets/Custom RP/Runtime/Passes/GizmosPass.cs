using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class GizmosPass
{
	static readonly ProfilingSampler sampler = new("Gizmos");

#if UNITY_EDITOR
	CameraRender renderer;

	void Render(RenderGraphContext context)
	{
		if (renderer.useIntermediateBuffer)
		{
			renderer.Draw(
				CameraRender.depthAttachmentId,
				BuiltinRenderTextureType.CameraTarget,
				true
			);
			renderer.ExecuteBuffer();
		}
		context.renderContext.DrawGizmos(renderer.camera, GizmoSubset.PreImageEffects);
		context.renderContext.DrawGizmos(renderer.camera, GizmoSubset.PostImageEffects);
	}
#endif

	[Conditional("UNITY_EDITOR")]
	public static void Record(RenderGraph renderGraph, CameraRender renderer)
	{
#if UNITY_EDITOR
		if (Handles.ShouldRenderGizmos())
		{
			using RenderGraphBuilder builder =
				renderGraph.AddRenderPass(sampler.name, out GizmosPass pass, sampler);
			pass.renderer = renderer;
			builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
		}
#endif
	}
}
