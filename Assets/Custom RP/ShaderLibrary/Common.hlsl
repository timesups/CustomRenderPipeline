#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif
// 必须先于 SpaceTransforms：Instancing 会重写 UNITY_MATRIX_M，
// 否则 TransformObjectToWorld 会读到错误/过期的物体矩阵。
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"


float Square(float v)
{
	return v * v;
}

float DistanceSquared(float3 pA, float3 pB)
{
	return dot(pA - pB, pA - pB);
}

void ClipLOD(float2 positionCS,float fade)
{
#if defined(LOD_FADE_CROSSFADE)
	float dither = InterleavedGradientNoise(positionCS.xy,0);
	clip(fade + (fade <0.0?dither:-dither));
#endif
}

float3 DecodeNormal(float4 sample,float scale)
{
#if defined(UNITY_NO_DXT5nm)
	return normalize(UnpackNormalRGB(sample,scale));
#else
	return normalize(UnpackNormalmapRGorAG(sample,scale));
#endif
}

// 与 GLEngine Functions.glsl EncodeNormalOct / DecodeNormalOct 一致
float2 EncodeNormalOct(float3 n)
{
	n = normalize(n);
	float2 p = n.xy * (1.0 / (abs(n.x) + abs(n.y) + abs(n.z)));
	if (n.z < 0.0)
	{
		p = (1.0 - abs(p.yx)) * sign(p);
	}
	return p * 0.5 + 0.5;
}

float3 DecodeNormalOct(float2 enc)
{
	float2 p = enc * 2.0 - 1.0;
	float3 n = float3(p.x, p.y, 1.0 - abs(p.x) - abs(p.y));
	if (n.z < 0.0)
	{
		n.xy = (1.0 - abs(n.yx)) * sign(n.xy);
	}
	return normalize(n);
}

#endif
