using System;
using UnityEngine;

[Serializable]
public struct CameraBufferSettings
{
	public bool allowHDR;
	public bool copyColor;
	public bool copyColorReflection;
	public bool copyDepth;
	public bool copyDepthReflection;

	public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

	public BicubicRescalingMode bicubicRescaling;

	[Range(CameraRender.renderScaleMin, CameraRender.renderScaleMax)]
	public float renderScale;

	[Serializable]
	public struct FXAA
	{
		public bool enabled;

		public enum Quality { Low, Medium, High }

		public Quality quality;

		[Range(0.0312f, 0.0833f)]
		public float fixedThreshold;

		[Range(0.063f, 0.250f)]
		public float relativeThreshold;

		[Range(0f, 1f)]
		public float subpixelBlending;
	}

	public FXAA fxaa;
}
