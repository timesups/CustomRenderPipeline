using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class CustomBackDepthPass
{
	static readonly ProfilingSampler sampler = new("Custom Back Depth");

	CameraRender renderer;

	void Render(RenderGraphContext context) => renderer.DrawCustomBackDepth();

	public static void Record(RenderGraph renderGraph, CameraRender renderer)
	{
		if (MeshRenderSetting.CustomBackDepthRenderers.Count == 0 ||
			!renderer.HasCustomBackDepthMaterial)
		{
			return;
		}

		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out CustomBackDepthPass pass, sampler
		);
		pass.renderer = renderer;
		builder.SetRenderFunc<CustomBackDepthPass>(
			(pass, context) => pass.Render(context)
		);
	}
}
