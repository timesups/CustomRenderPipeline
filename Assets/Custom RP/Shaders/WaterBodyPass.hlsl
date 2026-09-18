#ifndef WATER_BODY_INCLUDED
#define WATER_BODY_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64
#define FOUR_PI (4.0 * PI)

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

TEXTURE2D(_CameraCustomBackDepthTexture);
SAMPLER(sampler_CameraCustomBackDepthTexture);

TEXTURE2D(_NoiseRG);
SAMPLER(sampler_NoiseRG);

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
float4 _NormalMap_ST;
float _NormalScale;

TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

float _CustomBackDepthUVFlip;

float _IOR;
float _Density;
float _Clarity;
float _Speed;
float _DetailHeight;
float4 _WaterColour;
float4 _DetailScale;

static const float minDot = 1e-3;
static const float DETAIL_EPSILON = 2e-3;
static const float3 BLENDING_SHARPNESS = float3(4.0, 4.0, 4.0);

struct Attributes
{
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float4 screenPos : VAR_SCREEN_POS;
	float2 baseUV : VAR_BASE_UV;
	float3 normalWS : VAR_NORMAL;
	float4 tangentWS : VAR_TANGENT;
	float3 positionWS : VAR_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings WaterPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS = float4(
		TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w
	);
	output.screenPos = ComputeScreenPos(output.positionCS);
	output.baseUV = input.baseUV * _NormalMap_ST.xy + _NormalMap_ST.zw;
	return output;
}

float2 GetBackDepthScreenUV(float4 positionCS)
{
	// 与 Custom Back Depth RT（bufferSize）一致，勿用 _ScreenParams
	return positionCS.xy * _CameraBufferSize.xy;
}

// 用写入时的线性 Eye Depth + 与 BackDepth 一致的 UV/InvVP 重建世界坐标
float3 ReconstructWSFromEyeDepth(float2 uvRT, float eyeDepth)
{
#if UNITY_REVERSED_Z
	float farDeviceDepth = 0.0;
#else
	float farDeviceDepth = 1.0;
#endif
	// 仅取该像素视线方向（与 BackDepth 的 InvVP / UV 约定一致）
	float3 farPosWS = ComputeWorldSpacePosition(uvRT, farDeviceDepth, UNITY_MATRIX_I_VP);
	float3 rayWS = normalize(farPosWS - _WorldSpaceCameraPos);
	float3 rayVS = mul((float3x3)UNITY_MATRIX_V, rayWS);
	// eyeDepth = -viewZ，沿视线缩放到该深度
	float3 posVS = rayVS / max(-rayVS.z, 1e-5) * eyeDepth;
	return mul(UNITY_MATRIX_I_V, float4(posVS, 1.0)).xyz;
}

bool TrySampleBackSurface(
	float2 uvRT,
	out float3 backNormalWS,
	out float3 backPositionWS
)
{
	float4 raw = SAMPLE_TEXTURE2D(
		_CameraCustomBackDepthTexture, sampler_CameraCustomBackDepthTexture, uvRT
	);
	// Clear 的 a=0；有效像素 a=1。eyeDepth 必须为正。
	if (raw.a < 0.5 || raw.z < 1e-4)
	{
		backNormalWS = 0.0;
		backPositionWS = 0.0;
		return false;
	}
	backNormalWS = DecodeNormalOct(raw.xy);
	backPositionWS = ReconstructWSFromEyeDepth(uvRT, raw.z);
	return true;
}

// 与 GLSL faceforward 一致（避免 HLSL 内置在 dot==0 时行为差异）
float3 FaceForwardWater(float3 n, float3 i, float3 nRef)
{
	return (dot(nRef, i) < 0.0) ? n : -n;
}

float dot_c(float3 a, float3 b)
{
	return max(dot(a, b), minDot);
}

float3 fresnelSchlick(float cosTheta, float3 F0)
{
	return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float3 BoxProjectedCubemapDirection(
	float3 reflectionWS, float3 positionWS,
	float4 probePosition, float4 boxMin, float4 boxMax
)
{
	// ProbePosition.w > 0 时探针启用盒投影
	if (probePosition.w > 0.0)
	{
		float3 direction = normalize(reflectionWS);
		float3 factors = (direction > 0.0)
			? (boxMax.xyz - positionWS) / direction
			: (boxMin.xyz - positionWS) / direction;
		float t = min(min(factors.x, factors.y), factors.z);
		reflectionWS = positionWS + direction * t - probePosition.xyz;
	}
	return reflectionWS;
}

float3 sampleEnv(float3 positionWS, float3 dir, float perceptualRoughness)
{
	dir = normalize(dir);
#if defined(UNITY_SPECCUBE_BOX_PROJECTION)
	dir = BoxProjectedCubemapDirection(
		dir, positionWS,
		unity_SpecCube0_ProbePosition,
		unity_SpecCube0_BoxMin,
		unity_SpecCube0_BoxMax
	);
#endif
	float mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
	float4 environment = SAMPLE_TEXTURECUBE_LOD(
		unity_SpecCube0, samplerunity_SpecCube0, dir, mip
	);
	return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

float2 getGradient(float2 uv)
{
	float delta = 1e-1;
	uv *= 0.3;
	float data = SAMPLE_TEXTURE2D_LOD(_NoiseRG, sampler_NoiseRG, uv, 0).r;
	float gradX = data - SAMPLE_TEXTURE2D_LOD(
		_NoiseRG, sampler_NoiseRG, uv - float2(delta, 0.0), 0
	).r;
	float gradY = data - SAMPLE_TEXTURE2D_LOD(
		_NoiseRG, sampler_NoiseRG, uv - float2(0.0, delta), 0
	).r;
	return float2(gradX, gradY);
}

float getDistortedTexture(float2 uv)
{
	float strength = 0.5;
	float time = _Speed * _Time.y +
		SAMPLE_TEXTURE2D_LOD(_NoiseRG, sampler_NoiseRG, 0.25 * uv, 0).g;
	float f = frac(time);
	float2 grad = getGradient(uv);
	float2 distortion = strength * grad + float2(0.0, -0.3);
	float distort1 = SAMPLE_TEXTURE2D_LOD(
		_NoiseRG, sampler_NoiseRG, uv + f * distortion, 0
	).r;
	float distort2 = SAMPLE_TEXTURE2D_LOD(
		_NoiseRG, sampler_NoiseRG, uv + frac(time + 0.5) * distortion, 0
	).r;
	return (1.0 - length(grad)) *
		lerp(distort1, distort2, abs(1.0 - 2.0 * f));
}

float getTriplanarHeight(float3 position, float3 normal)
{
	float3 detailScale = _DetailScale.xyz;
	float xaxis = getDistortedTexture(detailScale.x * position.zy);
	float yaxis = getDistortedTexture(detailScale.y * position.zx);
	float zaxis = getDistortedTexture(detailScale.z * position.xy);
	float3 blending = abs(normal);
	blending = normalize(max(blending, float3(1e-5, 1e-5, 1e-5)));
	blending = pow(blending, BLENDING_SHARPNESS);
	blending /= dot(blending, float3(1.0, 1.0, 1.0));
	return dot(float3(xaxis, yaxis, zaxis), blending);
}

void pixarONB(float3 n, out float3 b1, out float3 b2)
{
	float sign_ = n.z >= 0.0 ? 1.0 : -1.0;
	float a = -1.0 / (sign_ + n.z);
	float b = n.x * n.y * a;
	b1 = float3(
		1.0 + sign_ * n.x * n.x * a,
		sign_ * b,
		-sign_ * n.x
	);
	b2 = float3(
		b,
		sign_ + n.y * n.y * a,
		-n.y
	);
}

float3 getDetailExtrusion(float3 p, float3 normal)
{
	float detail = _DetailHeight * 1.7320508 * getTriplanarHeight(p, normal);
	float lowerMask = 1.0 - smoothstep(-0.5, 0.0, p.y);
	float d = 1.0 + lowerMask;
	return p + d * detail * normal;
}

float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
	float3x3 tangentToWorld = CreateTangentToWorld(
		normalWS, tangentWS.xyz, tangentWS.w
	);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

float3 GetMeshNormalWS(float2 uv, float3 normalWS, float4 tangentWS)
{
	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
	float3 normalTS = DecodeNormal(map, _NormalScale);
	return normalize(NormalTangentToWorld(normalTS, normalWS, tangentWS));
}

float3 getDetailNormal(float3 p, float3 normal)
{
	float3 tangent;
	float3 bitangent;
	pixarONB(normal, tangent, bitangent);
	tangent = normalize(tangent);
	bitangent = normalize(bitangent);
	float3 delTangent = 0.0;
	float3 delBitangent = 0.0;
	UNITY_UNROLL
	for (int i = 0; i < 2; i++)
	{
		float s = (i == 0) ? 1.0 : -1.0;
		delTangent += s * getDetailExtrusion(
			p + s * tangent * DETAIL_EPSILON, normal
		);
		delBitangent += s * getDetailExtrusion(
			p + s * bitangent * DETAIL_EPSILON, normal
		);
	}
	float3 n = normalize(cross(delTangent, delBitangent));
	if (dot(n, normal) < 0.0)
	{
		n = -n;
	}
	return n;
}

float distributionGGX(float3 n, float3 h, float roughness)
{
	float a2 = roughness * roughness;
	float NdotH = dot_c(n, h);
	return a2 / (PI * pow(NdotH * NdotH * (a2 - 1.0) + 1.0, 2.0));
}

float geometrySchlick(float cosTheta, float k)
{
	return cosTheta / (cosTheta * (1.0 - k) + k);
}

float smiths(float NdotV, float NdotL, float roughness)
{
	float k = pow(roughness + 1.0, 2.0) / 8.0;
	return geometrySchlick(NdotV, k) * geometrySchlick(NdotL, k);
}

float3 WaterBRDF(
	float3 n, float3 viewDir, float3 lightDir, float3 F0, float roughness
)
{
	float3 h = normalize(viewDir + lightDir);
	float NdotL = dot_c(n, lightDir);
	float NdotV = dot_c(n, viewDir);
	float HdotV = dot_c(h, viewDir);
	float D = distributionGGX(n, h, roughness);
	float G = smiths(NdotV, NdotL, roughness);
	float3 F = fresnelSchlick(HdotV, F0);
	return D * F * G / max(0.0001, 4.0 * NdotV * NdotL);
}

float HenyeyGreenstein(float g, float costh)
{
	return (1.0 / FOUR_PI) *
		((1.0 - g * g) / pow(1.0 + g * g - 2.0 * g * costh, 1.5));
}

float3 getEnvironmentThroughWater(
	float3 p,
	float3 rayDir,
	float3 entryNormal,
	float3 geoNormal,
	float2 screenUV,
	float3 backOutNormal,
	float3 backPos,
	bool hasBackSurface,
	out float3 transmittance,
	out float3 insideDir,
	out float pathLength
)
{
	float eta = 1.0 / max(_IOR, 1.0001);
	float etaReverse = max(_IOR, 1.0001);

	insideDir = refract(rayDir, entryNormal, eta);
	if (dot(insideDir, insideDir) < 1e-6)
	{
		insideDir = rayDir;
	}
	insideDir = normalize(insideDir);

	pathLength = 0.0;
	if (hasBackSurface)
	{
		float3 toBack = backPos - p;
		// 背面应在视线前方；否则视为无效
		float alongRay = dot(toBack, rayDir);
		if (alongRay > 1e-4)
		{
			float viewThickness = alongRay; // 沿视线投影更稳，避免 UV 微偏导致的斜向距离
			float viewCos = max(0.15, abs(dot(rayDir, geoNormal)));
			float refrCos = max(0.15, abs(dot(insideDir, geoNormal)));
			pathLength = viewThickness * viewCos / refrCos;
			pathLength = clamp(pathLength, 0.0, 10.0);
		}
	}

	float3 waterColour = _WaterColour.rgb;
	float d = _Density * pathLength;
	transmittance = exp(-d * (1.0 - waterColour));

	float3 exitDir;
	if (hasBackSurface)
	{
		float3 exitNormal = -backOutNormal;
		exitDir = refract(insideDir, exitNormal, etaReverse);
		if (dot(exitDir, exitDir) < 1e-6 ||
			dot(-insideDir, exitNormal) <= 0.66125)
		{
			exitDir = reflect(insideDir, exitNormal);
		}
	}
	else
	{
		exitDir = insideDir;
	}

	return sampleEnv(p, exitDir, 0.01);
}

// 厚度超过该距离后，屏幕色不再可信，改用环境
static const float SCENE_FADE_DISTANCE = 4.0;

float SampleSceneEyeDepth(float2 uv)
{
	float raw = SAMPLE_DEPTH_TEXTURE_LOD(
		_CameraDepthTexture, sampler_point_clamp, uv, 0
	);
	return IsOrthographicCamera()
		? OrthographicDepthBufferToLinear(raw)
		: LinearEyeDepth(raw, _ZBufferParams);
}

float SurfaceEyeDepth(float4 positionCS)
{
	return IsOrthographicCamera()
		? OrthographicDepthBufferToLinear(positionCS.z)
		: positionCS.w;
}

float2 GetRefractedScreenUV(
	float3 position, float3 insideDir, float2 screenUV, float distance
)
{
	float4 currentCS = TransformWorldToHClip(position);
	float4 refractedCS = TransformWorldToHClip(position + insideDir * distance);
	float2 currentNDC = currentCS.xy / max(currentCS.w, 1e-5);
	float2 refractedNDC = refractedCS.xy / max(refractedCS.w, 1e-5);
	return screenUV + (refractedNDC - currentNDC) * 0.5;
}

float SceneColorWeight(float2 uv, float surfaceEyeDepth, float pathLength)
{
	float2 edge = min(uv, 1.0 - uv);
	float inScreen = smoothstep(0.0, 0.02, min(edge.x, edge.y));

	float sceneEye = SampleSceneEyeDepth(uv);
	float behind = smoothstep(0.0, 0.25, sceneEye - surfaceEyeDepth);
	float farPlane = max(_ProjectionParams.z, 1.0);
	float notSky = 1.0 - smoothstep(farPlane * 0.9, farPlane * 0.99, sceneEye);
	float thin = 1.0 - saturate(pathLength / SCENE_FADE_DISTANCE);
	return inScreen * behind * notSky * thin;
}

float4 GetSceneColor(float2 uv)
{
	return SAMPLE_TEXTURE2D_LOD(
		_CameraColorTexture, sampler_linear_clamp, uv, 0
	);
}

float4 WaterPassFragment(Varyings input) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input);
	float2 screenUV = GetBackDepthScreenUV(input.positionCS);

	float3 backNormalWS;
	float3 backPositionWS;
	bool hasBackSurface = TrySampleBackSurface(
		screenUV, backNormalWS, backPositionWS
	);

	float3 p = input.positionWS;
	float3 rayDir = normalize(p - _WorldSpaceCameraPos);
	float3 geoNormal = GetMeshNormalWS(
		input.baseUV, normalize(input.normalWS), input.tangentWS
	);
	geoNormal = FaceForwardWater(geoNormal, rayDir, geoNormal);

	float3 n = getDetailNormal(p, geoNormal);
	n = FaceForwardWater(n, rayDir, n);

	float3 V = -rayDir;
	float3 L = normalize(_DirectionalLightDirectionsAndMasks[0].xyz);
	float3 lightColor = _DirectionalLightColors[0].rgb;
	float3 F0 = float3(0.02, 0.02, 0.02);
	float roughness = 0.1;

	float3 directSpec =
		WaterBRDF(n, V, L, F0, roughness) * lightColor * dot_c(n, L);

	float3 transmittance;
	float3 insideDir;
	float pathLength;
	float3 envColor = getEnvironmentThroughWater(
		p, rayDir, n, geoNormal, screenUV,
		backNormalWS, backPositionWS, hasBackSurface,
		transmittance, insideDir, pathLength
	);
	float2 refractUV = GetRefractedScreenUV(p, insideDir, screenUV, 1.0);
	float3 sceneColor = GetSceneColor(refractUV).rgb;
	float sceneWeight = SceneColorWeight(
		refractUV, SurfaceEyeDepth(input.positionCS), pathLength
	);
	float3 throughWater =
		lerp(envColor, sceneColor, sceneWeight) * transmittance;

	float lowMask = 1.0 - smoothstep(-0.5, 0.0, p.y);
	float3 result = 0.0;

	result += (1.0 - lowMask) * _Clarity * throughWater;

	float mu = dot(insideDir, L);
	float phase = lerp(
		HenyeyGreenstein(-0.3, mu),
		HenyeyGreenstein(0.85, mu),
		0.5
	);
	result += _Clarity * lightColor * transmittance * phase;

	float3 reflectedDir = reflect(rayDir, n);
	float3 reflectedCol = sampleEnv(p, reflectedDir, roughness);
	float3 F = fresnelSchlick(dot_c(n, V), F0);
	result = lerp(result, reflectedCol, F);

	float waveHeight = 1.7320508 * getTriplanarHeight(p, n);
	float highMask = smoothstep(-1.3, 0.2, p.y);
	float e = lerp(2.0, 16.0, highMask);
	result += lowMask * pow(max(waveHeight, 0.0), e);

	float3 col = result + directSpec;
	return float4(col, 1.0);
}

#endif
