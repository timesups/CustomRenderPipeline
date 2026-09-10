Shader "Custom RP/WaterBody"
{
    Properties
    {


    }
    SubShader
    {
		Pass
		{
			HLSLPROGRAM
			#pragma target 3.5
			#pragma multi_compile_instancing
            #pragma vertex WaterPassVertex
			#pragma fragment WaterPassFragment
		    #include "../ShaderLibrary/Common.hlsl"


            TEXTURE2D(_CameraCustomBackDepthTexture);
            SAMPLER(sampler_CameraCustomBackDepthTexture);

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

            Varyings WaterPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);

                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.baseUV = input.baseUV;

                return output;
            }

            float4 WaterPassFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 screenUV = input.positionCS.xy/_ScreenParams.xy;

                float3 customBackDepth = SAMPLE_TEXTURE2D(
                    _CameraCustomBackDepthTexture, sampler_CameraCustomBackDepthTexture, screenUV
                ).xyz;

                float3 backNormalWS = DecodeNormalOct(customBackDepth.xy);
                float3 backPositionWS = ReconstructPositionWS(screenUV, customBackDepth.z);

                return float4(backPositionWS,1.0);
            }


			ENDHLSL
		}
    }
}
