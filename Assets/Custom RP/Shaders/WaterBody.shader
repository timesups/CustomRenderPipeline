Shader "Custom RP/WaterBody"
{
	Properties
	{
		_NoiseRG("Noise RG", 2D) = "gray" {}
		_IOR("IOR", Range(1, 3)) = 1.333
		_WaterColour("Water Colour", Color) = (0.085, 0.6375, 0.765, 1)
		_Density("Density", Float) = 3.5
		_Clarity("Clarity", Range(0, 1)) = 0.75
		_Speed("Speed", Float) = 0.01
		_DetailHeight("Detail Height", Float) = 0.1
		_DetailScale("Detail Scale", Vector) = (1, 1, 1, 0)
	}
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

		Pass
		{
			Tags
			{
				"LightMode" = "CustomLit"
			}

			ZWrite Off
			Blend One Zero

			HLSLPROGRAM
			#pragma target 3.5
			#pragma multi_compile_instancing
			#pragma vertex WaterPassVertex
			#pragma fragment WaterPassFragment
			#include "../ShaderLibrary/Common.hlsl"
			#include "WaterBodyPass.hlsl"
			ENDHLSL
		}
	}
}
