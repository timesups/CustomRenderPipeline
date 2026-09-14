#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

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

struct Light
{
	float3 color;
	float3 direction;
	float attenuation;
	uint renderingLayerMask;
};

DirectionalShadowData GetDirectionalShadowData(
	int lightIndex, ShadowData shadowData
)
{
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x * shadowData.strength;
	data.tileIndex =
		_DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
	data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
	return data;
}

OtherShadowData GetOtherShadowData (int lightIndex) {
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;
	data.tileIndex = _OtherLightShadowData[lightIndex].y;
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.lightPositionWS = 0.0;
	data.spotDirectionWS = 0.0;
	data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
	data.lightDirectionWS = 0.0;

	return data;
}

Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData)
{
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
	DirectionalShadowData dirShadowData =
		GetDirectionalShadowData(index, shadowData);
	light.attenuation =
		GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);
	return light;
}


Light GetDirectionalLight(int index)
{
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
	light.attenuation = 1.0;
	light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);
	return light;
}



Light GetOtherLight(int index,Surface surfaceWS,ShadowData shadowData)
{
	Light light;
	light.color = _OtherLightColors[index].rgb;
	float3 position = _OtherLightPositions[index].xyz;


	float3 ray =  position - surfaceWS.position;
	light.direction = normalize(ray);

	float distance = max(dot(ray,ray),0.00001);
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distance * _OtherLightPositions[index].w))
	);
	float4 spotAngles = _OtherLightSpotAngles[index];

	float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;

	float spotAttenuation = saturate(dot(spotDirection,light.direction)*
		spotAngles.x + spotAngles.y);
	OtherShadowData otherShadowData = GetOtherShadowData(index);
	otherShadowData.lightPositionWS = position;
	otherShadowData.spotDirectionWS = spotDirection;
	otherShadowData.lightDirectionWS = light.direction;


	light.attenuation =
		GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) *
		spotAttenuation * rangeAttenuation/distance;

	light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);


	return light;
}

int GetOtherLightCount()
{
	return _OtherLightCount;
}

int GetDirectionalLightCount()
{
	return _DirectionalLightCount;
}

#endif
