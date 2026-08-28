Shader "Endfield/Lit"
{
    Properties
    {
        _BaseColorMap ("Base Color Map", 2D) = "white" {}
        [NoScaleOffset]_NormalMap("Normal Map",2D) = "bump"{}
        [NoScaleOffset]_MaskMap("Mask Map",2D) = "white"{}
        [NoScaleOffset]_RampMap("Ramp Map",2D) = "white"{}


        _ForwardDirStrength("Forward Dir Strength",Range(0,1)) = 1.0


        [Enum(UnityEngine.Rendering.CullMode)]_CullMode("Cull Mode",FLoat) = 0
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        ENDHLSL
        Pass
        {
            Tags
		    {
			    "LightMode" = "CustomLit"
		    }
            Cull [_CullMode]
            HLSLPROGRAM
            #pragma vertex EndfieldLitVertex
            #pragma fragment EndfieldLitFragment
            #include "LitPass.hlsl"
            ENDHLSL
        }
    }
}
