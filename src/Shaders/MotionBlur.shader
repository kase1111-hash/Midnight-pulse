// ============================================================================
// Nightflow - Speed-Based Motion Blur Shader
// Radial blur that intensifies with vehicle speed
// ============================================================================

Shader "Nightflow/PostProcess/MotionBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BlurStrength ("Blur Strength", Range(0, 1)) = 0.5
        _BlurSamples ("Blur Samples", Range(4, 16)) = 8
        _FocusPoint ("Focus Point", Vector) = (0.5, 0.5, 0, 0)
        _FocusRadius ("Focus Radius", Range(0, 1)) = 0.3
        _SpeedFactor ("Speed Factor", Range(0, 1)) = 0.0
        _DirectionalBias ("Directional Bias", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "MotionBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _BlurStrength;
            float _BlurSamples;
            float4 _FocusPoint;
            float _FocusRadius;
            float _SpeedFactor;
            float4 _DirectionalBias;

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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 center = _FocusPoint.xy;

                // Calculate radial direction from center
                float2 radialDir = uv - center;
                float radialDist = length(radialDir);

                // Focus area (no blur in center)
                float focusMask = smoothstep(_FocusRadius, _FocusRadius + 0.2, radialDist);

                // Add directional bias (for forward motion feel)
                float2 biasDir = normalize(_DirectionalBias.xy);
                float directionalMask = dot(normalize(radialDir + 0.001), biasDir) * 0.5 + 0.5;

                // Combined blur amount based on speed
                float blurAmount = _BlurStrength * _SpeedFactor * focusMask;
                blurAmount *= 0.5 + directionalMask * 0.5;

                // Sample direction (radial outward)
                float2 blurDir = normalize(radialDir + 0.001) * blurAmount;

                // Accumulate samples
                float3 color = float3(0, 0, 0);
                float totalWeight = 0;
                int samples = (int)_BlurSamples;

                [unroll(16)]
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / (float)(samples - 1) - 0.5;
                    float2 sampleUV = uv + blurDir * t;

                    // Weight falloff
                    float weight = 1.0 - abs(t) * 0.5;

                    color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).rgb * weight;
                    totalWeight += weight;
                }

                color /= totalWeight;

                // Slight vignette enhancement during blur
                float vignette = 1.0 - radialDist * radialDist * 0.3 * _SpeedFactor;
                color *= vignette;

                return half4(color, 1);
            }

            ENDHLSL
        }
    }
}
