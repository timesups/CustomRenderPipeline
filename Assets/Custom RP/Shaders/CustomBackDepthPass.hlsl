#ifndef CUSTOM_CUSTOM_BACK_DEPTH_PASS
#define CUSTOM_CUSTOM_BACK_DEPTH_PASS

#include "../ShaderLibrary/Common.hlsl"

struct Attributes
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 normalWS : VAR_NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings CustomBackDepthVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	return output;
}

float4 CustomBackDepthFragment(Varyings input) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input);
	return float4(EncodeNormalOct(input.normalWS), input.positionCS.z, 1.0);
}

#endif
