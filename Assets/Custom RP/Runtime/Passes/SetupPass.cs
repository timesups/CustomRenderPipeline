using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class SetupPass
{
	static readonly ProfilingSampler sampler = new("Setup");

	CameraRender renderer;

	void Render(RenderGraphContext context) => renderer.Setup();

	public static void Record(RenderGraph renderGraph, CameraRender renderer)
	{
		using RenderGraphBuilder builder =
			renderGraph.AddRenderPass(sampler.name, out SetupPass pass, sampler);
		pass.renderer = renderer;
		builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));
	}
}
