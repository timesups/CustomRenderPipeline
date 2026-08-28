#ifndef Endfield_LIT_PASS_INCLUDED
#define Endfield_LIT_PASS_INCLUDED


#define MAX_DIRECTIONAL_LIGHT_COUNT 4
CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END



TEXTURE2D(_BaseColorMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_RampMap);
SAMPLER(sampler_BaseColorMap);

float4 _BaseColorMap_ST;
float _ForwardDirStrength;

float4 GetBaseColor(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseColorMap,sampler_BaseColorMap,uv);
}

float3 GetNormalTS(float2 uv)
{
	float4 map = SAMPLE_TEXTURE2D(_NormalMap,sampler_BaseColorMap,uv);
	float3 normal = DecodeNormal(map,1.0f);
    return normal;
}

float4 GetMask(float2 uv)
{
    return SAMPLE_TEXTURE2D(_MaskMap,sampler_BaseColorMap,uv);
}
float GetMetallic(float2 uv)
{
    return GetMask(uv).r;
}

float GetRflectivity(float2 uv)
{
    return GetMask(uv).g;
}

float GetAO(float2 uv)
{
    return GetMask(uv).b;
}

float GetSmoothness(float2 uv)
{
    return GetMask(uv).w;
}

float4 GetRamp(float2 uv)
{
    return SAMPLE_TEXTURE2D(_RampMap,sampler_BaseColorMap,uv);
}

float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld =
		CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

struct appdata
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
};

struct v2f
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS   : VAR_NORMAL;
    float4 tangentWS  : VAR_TANGENT;
    float2 uv         : VAR_UV;
   };

v2f EndfieldLitVertex (appdata v)
{
    v2f o;
    o.positionWS = TransformObjectToWorld(v.positionOS);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.tangentWS = 
	float4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w);
    o.uv = v.uv;
    return o;
}

float4 EndfieldLitFragment (v2f i, uint isFrontFace : SV_IsFrontFace) : SV_Target
{
    float3 basecolor = saturate(GetBaseColor(i.uv).xyz * 100.0);
    float3 normalWS = normalize(i.normalWS);
    normalWS = normalize(NormalTangentToWorld(
		GetNormalTS(i.uv), i.normalWS, i.tangentWS
	));
    normalWS = normalWS * (isFrontFace?1.0:-1.0);

    float3 mainLightDir = _DirectionalLightDirections[0];
    float3 mainLightDir_xz = normalize(float3(mainLightDir.x,0.0,mainLightDir.z));
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);

    float3 cameraForward = normalize(UNITY_MATRIX_V[2].xyz);
    viewDir = normalize(lerp(viewDir,cameraForward,_ForwardDirStrength));


    //主光
    float3 mainLightColor = _DirectionalLightColors[0];

    float NoL = dot(normalWS,mainLightDir);

    //辅光
    float3 otherLightDir = float3(0,1,0);
    float otherLightNoL = dot(otherLightDir,normalWS) * 0.5 + 0.5;

    //阴影





    float3 color = otherLightNoL * basecolor;
    return float4(color,1.0);
}



#endif
