using System.Diagnostics;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class UnsupportedShadersPass
{
	static readonly ProfilingSampler sampler = new("Unsupported Shaders");

#if UNITY_EDITOR
	CameraRender renderer;

	void Render(RenderGraphContext context) => renderer.DrawUnsupportedShaders();
#endif

	[Conditional("UNITY_EDITOR")]
	public static void Record(RenderGraph renderGraph, CameraRender renderer)
	{
#if UNITY_EDITOR
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out UnsupportedShadersPass pass, sampler
		);
		pass.renderer = renderer;
		builder.SetRenderFunc<UnsupportedShadersPass>(
			(pass, context) => pass.Render(context)
		);
#endif
	}
}
