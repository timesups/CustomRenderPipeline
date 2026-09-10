Shader "Hidden/Custom RP/Custom Back Depth"
{
	SubShader
	{
		Pass
		{
			Name "CustomBackDepth"
			Cull Front
			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma target 3.5
			#pragma multi_compile_instancing
			#pragma vertex CustomBackDepthVertex
			#pragma fragment CustomBackDepthFragment
			#include "CustomBackDepthPass.hlsl"
			ENDHLSL
		}
	}
}
