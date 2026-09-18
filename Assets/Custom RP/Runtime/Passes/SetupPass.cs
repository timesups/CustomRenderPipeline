using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class SetupPass
{
	static readonly ProfilingSampler sampler = new("Setup");

	CameraRender renderer;

	bool useIntermediateAttachments;
	TextureHandle colorAttachment,depthAttachment;
	Vector2Int attachmentSize;

	Camera camera;

	CameraClearFlags clearFlags;


	void Render(RenderGraphContext context) => renderer.Setup();

	public static void Record(
		RenderGraph renderGraph,
		bool useIntermediateAttachments,
		bool useHDR,
		Vector2Int attachmentsSize,
		Camera camera,
		 CameraRender renderer)
	{
		using RenderGraphBuilder builder =
			renderGraph.AddRenderPass(sampler.name, out SetupPass pass, sampler);
		pass.useIntermediateAttachments = useIntermediateAttachments;
		pass.attachmentSize = attachmentsSize;
		pass.camera = camera;
		pass.clearFlags = camera.clearFlags;
		if(useIntermediateAttachments)
		{
			if(pass.clearFlags > CameraClearFlags.Color)
			{
				pass.clearFlags = CameraClearFlags.Color;
			}
		}
		else
		{
			pass.colorAttachment = pass.depthAttachment = builder.WriteTexture(
				renderGraph.ImportBackbuffer(BuiltinRenderTextureType.CameraTarget)
			);
		}


		var desc = new TextureDesc(attachmentsSize.x,attachmentsSize.y)
		{
			colorFormat = SystemInfo.GetGraphicsFormat(
				useHDR?DefaultFormat.HDR:DefaultFormat.LDR
			),
			name = "Color Attachment"
		};
		pass.colorAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));


		desc.depthBufferBits = DepthBits.Depth32;
		desc.name = "Depth Attachment";
		pass.depthAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));

		pass.renderer = renderer;
		builder.AllowPassCulling(false);
		builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));
	}
}
