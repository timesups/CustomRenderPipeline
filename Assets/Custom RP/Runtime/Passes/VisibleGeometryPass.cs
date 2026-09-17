using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class VisibleGeometryPass
{
	static readonly ProfilingSampler sampler = new("Visible Geometry");

	CameraRender renderer;
	bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
	int renderingLayerMask;

	void Render(RenderGraphContext context) => renderer.DrawVisibleGeometry(
		useDynamicBatching, useGPUInstancing, useLightsPerObject, renderingLayerMask
	);

	public static void Record(
		RenderGraph renderGraph, CameraRender renderer,
		bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
		int renderingLayerMask
	)
	{
		using RenderGraphBuilder builder = renderGraph.AddRenderPass(
			sampler.name, out VisibleGeometryPass pass, sampler
		);
		pass.renderer = renderer;
		pass.useDynamicBatching = useDynamicBatching;
		pass.useGPUInstancing = useGPUInstancing;
		pass.useLightsPerObject = useLightsPerObject;
		pass.renderingLayerMask = renderingLayerMask;
		builder.SetRenderFunc<VisibleGeometryPass>(
			(pass, context) => pass.Render(context)
		);
	}
}
