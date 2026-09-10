using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class MeshRenderSetting : MonoBehaviour
{
	/// <summary>
	/// Rendering Layer：Default = bit 0，Custom Back Depth = bit 1。
	/// </summary>
	public const uint DefaultRenderingLayerMask = 1u;
	public const int CustomBackDepthRenderingLayerIndex = 1;
	public const uint CustomBackDepthRenderingLayerMask =
		1u << CustomBackDepthRenderingLayerIndex;

	static readonly List<MeshRenderSetting> customBackDepthList = new();

	/// <summary>
	/// 当前启用了 RenderCustomBackDepth 的实例（供管线 opt-in 绘制）。
	/// </summary>
	public static IReadOnlyList<MeshRenderSetting> CustomBackDepthRenderers =>
		customBackDepthList;

	[SerializeField]
	bool renderCustomBackDepth;

	public bool RenderCustomBackDepth => renderCustomBackDepth;

	public Renderer CachedRenderer { get; private set; }

	void Awake() => CachedRenderer = GetComponent<Renderer>();

	void OnEnable()
	{
		if (CachedRenderer == null)
		{
			CachedRenderer = GetComponent<Renderer>();
		}
		Apply();
	}

	void OnValidate()
	{
		if (CachedRenderer == null)
		{
			CachedRenderer = GetComponent<Renderer>();
		}
		Apply();
	}

	void OnDisable()
	{
		UnregisterCustomBackDepth();
		if (CachedRenderer == null)
		{
			return;
		}
		CachedRenderer.renderingLayerMask &= ~CustomBackDepthRenderingLayerMask;
	}

	void Apply()
	{
		if (CachedRenderer == null)
		{
			return;
		}

		uint mask = CachedRenderer.renderingLayerMask;
		if (mask == uint.MaxValue)
		{
			mask = DefaultRenderingLayerMask;
		}

		if (renderCustomBackDepth)
		{
			mask |= CustomBackDepthRenderingLayerMask;
			RegisterCustomBackDepth();
		}
		else
		{
			mask &= ~CustomBackDepthRenderingLayerMask;
			UnregisterCustomBackDepth();
		}

		CachedRenderer.renderingLayerMask = mask;
	}

	void RegisterCustomBackDepth()
	{
		if (!customBackDepthList.Contains(this))
		{
			customBackDepthList.Add(this);
		}
	}

	void UnregisterCustomBackDepth()
	{
		customBackDepthList.Remove(this);
	}
}
