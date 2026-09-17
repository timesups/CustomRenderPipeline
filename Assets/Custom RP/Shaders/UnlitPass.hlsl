#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color      : COLOR;
	#if defined(_FLIPBOOK_BLENDING)
		float4 baseUV : TEXCOORD0;
		float flipbookBlend : TEXCOORD1;
	#else
		float2 baseUV : TEXCOORD0;
	#endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 positionCS : SV_POSITION;

#if defined(_VERTEX_COLORS)
    float4 color      : COLOR;
#endif


    float2 baseUV     : VAR_BASE_UV;


#if defined(_FLIPBOOK_BLENDING)
		float3 flipbookUVB : VAR_FLIPBOOK;
#endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.baseUV =TransformBaseUV(input.baseUV.xy);
#if defined(_FLIPBOOK_BLENDING)
	output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
	output.flipbookUVB.z = input.flipbookBlend;
#endif

#if defined(_VERTEX_COLORS)
    output.color = input.color;
#endif
    return output;
}

float GetFresnel (float2 baseUV) {
	return 0.0;
}

float4 UnlitPassFragment(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.positionCS,input.baseUV);

#if defined(_VERTEX_COLORS)
    config.color = input.color;
#endif


#if defined(_FLIPBOOK_BLENDING)
    config.flipbookUVB = input.flipbookUVB;
    config.flipbookBlending = true;
#endif
#if defined(_NEAR_FADE)
    config.nearFade = true;
#endif

    float4 base = GetBase(config);

#if defined(_CLIPPING)
    clip(base.a - GetCutoff(input.baseUV));
#endif

    return float4(base.rgb,GetFinalAlpha(base.a));
}

#endif
