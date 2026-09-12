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




        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
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


			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
            Cull back

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
