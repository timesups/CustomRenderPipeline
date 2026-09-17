using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class CopyAttachmentsPass
{
	static readonly ProfilingSampler sampler = new("Copy Attachments");

	CameraRender renderer;

	void Render(RenderGraphContext context) => renderer.CopyAttachments();

	public static void Record(RenderGraph renderGraph, CameraRender renderer)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out CopyAttachmentsPass pass, sampler
		);
		pass.renderer = renderer;
		builder.SetRenderFunc<CopyAttachmentsPass>(
			(pass, context) => pass.Render(context)
		);
	}
}
