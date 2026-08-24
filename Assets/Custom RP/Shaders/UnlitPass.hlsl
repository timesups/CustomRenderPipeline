#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 positionCS : SV_POSITION;
    float2 baseUV     : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);

    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.baseUV =TransformBaseUV(input.baseUV);

    return output;
}

float GetFresnel (float2 baseUV) {
	return 0.0;
}

float4 UnlitPassFragment(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 base = GetBase(input.baseUV);
#if defined(_CLIPPING)
    clip(base.a - GetCutoff(input.baseUV));
#endif
    float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
    float3 sceneColor = SAMPLE_TEXTURE2D(
        _SceneColor, sampler_SceneColor, screenUV
    ).rgb;

    float4 customDepth = SAMPLE_TEXTURE2D(
        _CustomDepth, sampler_CustomDepth, screenUV
    );


    float3 normal = DecodeNormalOct(customDepth.xy);
    // 保持 HDR 线性，与材质色混合；SDR 由 PostFX ToneMapping 统一处理
    base.rgb =  normal;
    return base;
}

#endif
