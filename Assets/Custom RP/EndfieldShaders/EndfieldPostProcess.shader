Shader "Endfield/PostProcess"
{
    Properties
    {
        _MainTex ("Scene Color (set 3, binding 3)", 2D) = "white" {}
        _AuxTex  ("Auxiliary Highlight/Bloom (set 3, binding 4)", 2D) = "black" {}
        _Lut2D   ("32x32x32 LUT Strip, 1024x32 (set 3, binding 2)", 2D) = "white" {}

        // uniform6.csv: element 82 (_6._child22) and element 109.x (_6._child42.x)
        _SourceSize ("Source Size: width, height, 1/width, 1/height", Vector) = (2560, 1440, 0.000390625, 0.000694444444)
        _Exposure ("Exposure", Float) = 1.0

        // uniform14.csv: element 24.x (_14._child24.x)
        _SharpenStrength ("Adaptive Sharpen Strength", Float) = 0.3

        // uniform14.csv: elements 9, 10 and 11
        _AuxBlendParams ("Aux Blend: amount, unused, attenuation, unused", Vector) = (0.3660402, 0, 0, 0)
        _HighlightCurve ("Highlight Curve", Vector) = (0.5225216, 0.2612508, 0.5225416, 0.9568616)
        _AuxTint ("Auxiliary Tint (raw GPU value)", Vector) = (0.9999999, 0.9999999, 0.9999999, 0.9999999)

        // uniform14.csv: elements 1, 2 and 4
        _VignetteCenterStereo ("Vignette Center XY / Stereo W", Vector) = (0.5, 0.5, 0, 0)
        _VignetteSettings ("Vignette: intensity, smoothness, roundness, rounded", Vector) = (0.9, 2.05, 1.3, 0)
        _VignetteColor ("Vignette Color (raw GPU value)", Vector) = (0.06666667, 0.06717458, 0.07450981, 1)

        // uniform14.csv: element 7. xyz = (1/1024, 1/32, 31), w = pre-LUT multiplier
        _LutParams ("LUT: 1/width, 1/height, size-1, multiplier", Vector) = (0.0009765625, 0.03125, 31, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }

        Pass
        {
            Name "RestoredPostProcess"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AuxTex;
            sampler2D _Lut2D;

            float4 _SourceSize;
            float _Exposure;
            float _SharpenStrength;
            float4 _AuxBlendParams;
            float4 _HighlightCurve;
            float4 _AuxTint;
            float4 _VignetteCenterStereo;
            float4 _VignetteSettings;
            float4 _VignetteColor;
            float4 _LutParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float4 SampleLod0(sampler2D textureSampler, float2 uv)
            {
                return tex2Dlod(textureSampler, float4(uv, 0.0, 0.0));
            }

            float ApproxLuma(float3 color)
            {
                // Exact weighting reconstructed from the SPIR-V.
                return color.g + 0.5 * (color.r + color.b);
            }

            float3 AdaptiveSharpen(
                float3 center,
                float3 up,
                float3 left,
                float3 right,
                float3 down)
            {
                float centerLuma = ApproxLuma(center);
                float4 neighborLuma = float4(
                    ApproxLuma(up),
                    ApproxLuma(left),
                    ApproxLuma(right),
                    ApproxLuma(down));

                float neighborAverage = dot(neighborLuma, float4(0.25, 0.25, 0.25, 0.25));
                float localMaximum = max(centerLuma, max(max(neighborLuma.x, neighborLuma.y), max(neighborLuma.z, neighborLuma.w)));
                float localMinimum = min(centerLuma, min(min(neighborLuma.x, neighborLuma.y), min(neighborLuma.z, neighborLuma.w)));

                // Kept structurally equivalent to the original shader. On common GPU backends,
                // saturate handles the flat-region 0/0 case the same way as the captured shader.
                float contrast = saturate(abs(neighborAverage - centerLuma) / (localMaximum - localMinimum));
                float contrastAttenuation = 1.0 - 0.5 * contrast;

                float3 neighborMinimum = min(min(up, left), min(right, down));
                float3 neighborMaximum = max(max(up, left), max(right, down));

                float3 limitA = -0.25 * neighborMinimum / neighborMaximum;
                float3 limitB = (1.0 - neighborMaximum) / (4.0 * neighborMinimum - 4.0);
                float3 channelLimits = max(limitA, limitB);
                float peak = max(channelLimits.x, max(channelLimits.y, channelLimits.z));
                peak = max(-0.1875, min(peak, 0.0));

                float weight = peak * _SharpenStrength * contrastAttenuation;
                return (center + weight * (up + left + right + down)) / (1.0 + 4.0 * weight);
            }

            float TransformAuxChannel(float value, float attenuation)
            {
                return value * (1.0 - attenuation) > 0.3
                    ? pow(value, 0.33) * 1.4938 - 0.7
                    : value;
            }

            float3 CompositeAuxiliary(float3 color, float3 auxiliary)
            {
                float attenuation = _AuxBlendParams.z;
                float3 transformedAuxiliary = float3(
                    TransformAuxChannel(auxiliary.r, attenuation),
                    TransformAuxChannel(auxiliary.g, attenuation),
                    TransformAuxChannel(auxiliary.b, attenuation));

                float maximumChannel = max(color.r, max(color.g, color.b));
                float curveDistance = clamp(
                    maximumChannel - _HighlightCurve.y,
                    0.0,
                    _HighlightCurve.z);
                float polynomial = _HighlightCurve.w * curveDistance * curveDistance;
                float highlightScale = max(polynomial, maximumChannel - _HighlightCurve.x)
                    / max(maximumChannel, 0.0001);

                float3 processed = color - color * highlightScale * attenuation;
                processed += transformedAuxiliary * _AuxTint.rgb;
                return lerp(color, processed, _AuxBlendParams.x);
            }

            float3 ApplyVignette(float3 color, float2 uv)
            {
                float intensity = _VignetteSettings.x;
                float stereoAdjustment = _VignetteCenterStereo.w;

                float adjustedIntensity = lerp(intensity, 1.0, stereoAdjustment);
                float2 distanceFromCenter = abs(uv - _VignetteCenterStereo.xy) * adjustedIntensity;

                float verticalScale = lerp(1.0, intensity * 2.0, stereoAdjustment);
                float verticalBias = saturate(intensity - 2.8) * 5.0;
                distanceFromCenter.y = saturate(distanceFromCenter.y * verticalScale + verticalBias);

                float horizontalScale = lerp(
                    1.0,
                    1.5 * saturate(intensity * 1.05),
                    stereoAdjustment);
                distanceFromCenter.x *= horizontalScale;

                float aspect = _SourceSize.x / _SourceSize.y;
                float aspectScale = lerp(1.0, aspect, _VignetteSettings.w);
                aspectScale = lerp(aspectScale, aspect * 0.5625, stereoAdjustment);
                distanceFromCenter.x *= aspectScale;

                distanceFromCenter = pow(saturate(distanceFromCenter), _VignetteSettings.zz);
                float vignetteFactor = pow(
                    saturate(1.0 - dot(distanceFromCenter, distanceFromCenter)),
                    _VignetteSettings.y);
                return color * lerp(_VignetteColor.rgb, float3(1.0, 1.0, 1.0), vignetteFactor);
            }

            float3 LinearToLogC(float3 color)
            {
                // Alexa LogC El 1000 fast path, matching the constants in the SPIR-V.
                float3 encoded = log2(max(color * 5.5556 + 0.048, float3(0.0, 0.0, 0.0)));
                encoded = encoded * 0.3010 * 0.2442 + 0.386;
                return saturate(encoded);
            }

            float3 ApplyLut2D(float3 logColor)
            {
                float blue = logColor.b * _LutParams.z;
                float slice = floor(blue);

                float2 lutUV = logColor.rg * _LutParams.z * _LutParams.xy;
                lutUV += 0.5 * _LutParams.xy;
                lutUV.x += slice * _LutParams.y;

                float3 slice0 = SampleLod0(_Lut2D, lutUV).rgb;
                float3 slice1 = SampleLod0(_Lut2D, lutUV + float2(_LutParams.y, 0.0)).rgb;
                return lerp(slice0, slice1, blue - slice);
            }

            float LinearToSrgbChannel(float value)
            {
                return value <= 0.0031
                    ? value * 12.92
                    : pow(abs(value), 0.4167) * 1.055 - 0.055;
            }

            float3 LinearToSrgb(float3 color)
            {
                return float3(
                    LinearToSrgbChannel(color.r),
                    LinearToSrgbChannel(color.g),
                    LinearToSrgbChannel(color.b));
            }

            float3 Dither(float2 uv)
            {
                float2 pixelPosition = uv * _SourceSize.xy;
                float seed = dot(float2(171.0, 231.0), pixelPosition);
                float3 noise = frac(seed * float3(0.0097, 0.0141, 0.0103)) - 0.5;
                return noise * 0.0039 * 0.35;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 texel = _SourceSize.zw;

                float4 centerSample = SampleLod0(_MainTex, uv);
                float3 up = SampleLod0(_MainTex, uv + float2(0.0, texel.y)).rgb;
                float3 left = SampleLod0(_MainTex, uv - float2(texel.x, 0.0)).rgb;
                float3 right = SampleLod0(_MainTex, uv + float2(texel.x, 0.0)).rgb;
                float3 down = SampleLod0(_MainTex, uv - float2(0.0, texel.y)).rgb;

                float3 color = AdaptiveSharpen(centerSample.rgb, up, left, right, down);
                color *= _Exposure;

                float3 auxiliary = SampleLod0(_AuxTex, uv).rgb;
                color = CompositeAuxiliary(color, auxiliary);
                color = ApplyVignette(color, uv);

                float3 logColor = LinearToLogC(color * _LutParams.w);
                color = ApplyLut2D(logColor);
                color = LinearToSrgb(color);
                color += Dither(uv);

                return float4(color, min(centerSample.a, 1.0));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
