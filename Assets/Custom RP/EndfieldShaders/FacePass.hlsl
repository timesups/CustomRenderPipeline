#ifndef CUSTOM_FACE_SDF_PASS_INCLUDED
#define CUSTOM_FACE_SDF_PASS_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END



TEXTURE2D(_FaceSDF);
SAMPLER(sampler_FaceSDF);

float _TestValue;


struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 positionCS    : SV_POSITION;
    float2 baseUV        : VAR_BASE_UV;
    float3 faceDirection : VAR_FACE_DIRECTION;
    float3 faceUp        : VAR_FACE_UP;
    float3 faceRight     : VAR_FACE_RIGHT;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings FaceSDFPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);

    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.baseUV =TransformBaseUV(input.baseUV);
    output.faceDirection = TransformObjectToWorldDir(float3(0.0,0.0,-1.0));
    output.faceUp = TransformObjectToWorldDir(float3(0.0,1.0,0.0));
    output.faceRight = TransformObjectToWorldDir(float3(1.0,0.0,0.0));

    return output;
}

float4 FaceSDFPassFragment(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 base = GetBase(input.baseUV);


#if defined(_CLIPPING)
    clip(base.a - GetCutoff(input.baseUV));
#endif
    float3 lightDirection = -_DirectionalLightDirections[0].xyz;


    float3 faceDirection = normalize(input.faceDirection);
    float3 faceUp = normalize(input.faceUp);
    float3 faceRight = normalize(input.faceRight);


    float3 mainLightDir_xz_faceDir = normalize(float3(
        dot(lightDirection,faceRight),
        6.10351562e-05,
        dot(lightDirection,faceDirection)
        ));
    float sdf_uvFlag = step(0,mainLightDir_xz_faceDir.x);


    float2 sdf_uv = float2(
        sdf_uvFlag * (2 * input.baseUV.x - 1) + 1 - input.baseUV.x,
        input.baseUV.y);

    float4 sdfTex = SAMPLE_TEXTURE2D(_FaceSDF,sampler_FaceSDF,sdf_uv);
    float sdf_var =  (sdfTex.x+sdfTex.y) * 0.5;


    float value = saturate(1-mainLightDir_xz_faceDir.z);

    float NdotL = smoothstep(value,value*_TestValue,sdf_var);

    float3 color = lerp(base.rgb*0.5,base.rgb,NdotL);

    return float4(float3(NdotL,NdotL,NdotL),1.0);
}





















#endif
