#ifndef CUSTOM_CUSTOM_DEPTH_PASS_INCLUDED
#define CUSTOM_CUSTOM_DEPTH_PASS_INCLUDED

// 对齐 GLEngine CustomDepth.glsl：
// FragColor = vec4(EncodeNormalOct(NormalWS), gl_FragCoord.z, 0)
// 用于水体折射等：采样侧 DecodeNormalOct(xy) + z 为硬件深度

struct Attributes
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings CustomDepthPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	return output;
}

float4 CustomDepthPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);

	float3 normalWS = normalize(input.normalWS);
	// SV_POSITION.z 对应 gl_FragCoord.z（窗口深度）
	return float4(EncodeNormalOct(normalWS), input.positionCS.z, 0.0);
}

#endif
