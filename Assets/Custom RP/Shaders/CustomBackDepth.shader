Shader "Hidden/Custom RP/Custom Back Depth"
{
	SubShader
	{
		Pass
		{
			Name "CustomBackDepth"
			// 配合 CommandBuffer.SetInvertCulling：RT 投影 Y 翻转后仍表示剔正面、只画背面
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
