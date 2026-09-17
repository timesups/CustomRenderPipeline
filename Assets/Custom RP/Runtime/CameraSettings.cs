using System;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderingLayerMaskFieldAttribute : PropertyAttribute
{
}

[Serializable]
public class CameraSettings
{
	public bool maskLights = false;

	[RenderingLayerMaskFieldAttribute]
	public int renderingLayerMask = -1;

	[Serializable]
	public struct FinalBlendMode
	{
		public BlendMode source, destination;
	}

	public enum RenderScaleMode { Inherit, Multiply, Override }

	public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

	[Range(CameraRender.renderScaleMin, CameraRender.renderScaleMax)]
	public float renderScale = 1f;

	public bool overridePostFX = false;

	public PostFXSettings postFXSettings = default;

	public bool copyColor = true;
	public bool copyDepth = true;

	public bool allowFXAA = false;
	public bool keepAlpha = false;

	public FinalBlendMode finalBlendMode = new()
	{
		source = BlendMode.One,
		destination = BlendMode.Zero
	};

	public float GetRenderScale(float scale)
	{
		return
			renderScaleMode == RenderScaleMode.Inherit ? scale :
			renderScaleMode == RenderScaleMode.Override ? renderScale :
			scale * renderScale;
	}
}
