#ifndef CUSTOM_OUTLINE_PASS_INCLUDED
#define CUSTOM_OUTLINE_PASS_INCLUDED

struct Attributes
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings OutlinePassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	float outlineWidth = INPUT_PROP(_OutlineWidth);
	float3 positionOS = input.positionOS + input.normalOS * outlineWidth;
	output.positionCS = TransformObjectToHClip(positionOS);
	return output;
}

float4 OutlinePassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	return 0.0;
}

#endif
