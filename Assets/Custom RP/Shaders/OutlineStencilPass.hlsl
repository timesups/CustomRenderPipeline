#ifndef CUSTOM_OUTLINE_STENCIL_PASS_INCLUDED
#define CUSTOM_OUTLINE_STENCIL_PASS_INCLUDED

struct Attributes
{
	float3 positionOS : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings OutlineStencilPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionCS = TransformObjectToHClip(input.positionOS);
	return output;
}

float4 OutlineStencilPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	return 0.0;
}

#endif
