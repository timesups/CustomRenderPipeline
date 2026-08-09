#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"



struct Attributes
{
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	float3 normalOS : NORMAL;
	float4 tangentOS:TANGENT;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float3 normalWS : VAR_NORMAL;
	float4 tangentWS : VAR_TANGENT;
	float2 baseUV : VAR_BASE_UV;
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);

	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.baseUV = TransformBaseUV(input.baseUV);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	output.tangentWS = 
	float4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w);
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);

	ClipLOD(input.positionCS.xy,unity_LODFade.x);

	float4 base = GetBase(input.baseUV);

	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(input.baseUV));
	#endif

	Surface surface;
	surface.position = input.positionWS;

#if defined(_ENABLE_NORMAL_MAP)
	surface.normal = 
		NormalTangentToWorld(GetNormalTS(input.baseUV),input.normalWS,input.tangentWS);
	surface.interpolatedNormal = input.normalWS;
#else
	surface.normal = input.normalWS;
	surface.interpolatedNormal = surface.normal;
#endif


	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.fresnelStrength = GetFresnel(input.baseUV);
	surface.occlusion = GetOcclusion(input.baseUV);
	
	
	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif

	GI gi = GetGI(GI_FRAGMENT_DATA(input),surface,brdf);

	float3 color = GetLighting(surface, brdf,gi);

	color += GetEmission(input.baseUV);
	return float4(color, surface.alpha);
}

#endif
