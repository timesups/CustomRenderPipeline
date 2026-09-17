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
}
