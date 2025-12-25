// ============================================================================
// Nightflow - Neon Bloom Post-Process Shader
// Multi-pass bloom effect optimized for neon wireframe visuals
// ============================================================================

Shader "Nightflow/PostProcess/NeonBloom"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _Threshold ("Bloom Threshold", Range(0, 2)) = 0.8
        _Intensity ("Bloom Intensity", Range(0, 5)) = 1.5
        _Scatter ("Scatter", Range(0, 1)) = 0.7
        _TintColor ("Bloom Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        // Pass 0: Prefilter - Extract bright pixels
        Pass
        {
            Name "BloomPrefilter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Threshold;

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

            // Soft knee curve for smooth threshold
            float3 Prefilter(float3 color)
            {
                float brightness = max(max(color.r, color.g), color.b);
                float knee = _Threshold * 0.5;
                float soft = brightness - _Threshold + knee;
                soft = clamp(soft, 0, 2 * knee);
                soft = soft * soft / (4 * knee + 0.00001);
                float contribution = max(soft, brightness - _Threshold) / max(brightness, 0.00001);
                return color * contribution;
            }

            half4 FragPrefilter(Varyings input) : SV_Target
            {
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                color = Prefilter(color);
                return half4(color, 1);
            }

            ENDHLSL
        }

        // Pass 1: Horizontal Gaussian blur
        Pass
        {
            Name "BloomBlurH"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Scatter;

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

            // 9-tap Gaussian weights
            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            half4 FragBlurH(Varyings input) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _Scatter;
                float3 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * weights[0];

                [unroll]
                for (int i = 1; i < 5; i++)
                {
                    float2 offset = float2(texelSize.x * i, 0);
                    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset).rgb * weights[i];
                    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - offset).rgb * weights[i];
                }

                return half4(result, 1);
            }

            ENDHLSL
        }

        // Pass 2: Vertical Gaussian blur
        Pass
        {
            Name "BloomBlurV"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Scatter;

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

            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            half4 FragBlurV(Varyings input) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _Scatter;
                float3 result = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * weights[0];

                [unroll]
                for (int i = 1; i < 5; i++)
                {
                    float2 offset = float2(0, texelSize.y * i);
                    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset).rgb * weights[i];
                    result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - offset).rgb * weights[i];
                }

                return half4(result, 1);
            }

            ENDHLSL
        }

        // Pass 3: Upsample and combine
        Pass
        {
            Name "BloomUpsample"
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Scatter;

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

            half4 FragUpsample(Varyings input) : SV_Target
            {
                // Tent filter for smooth upsampling
                float2 texelSize = _MainTex_TexelSize.xy * _Scatter * 0.5;

                float3 result = 0;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texelSize.x, -texelSize.y)).rgb;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( texelSize.x, -texelSize.y)).rgb;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-texelSize.x,  texelSize.y)).rgb;
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( texelSize.x,  texelSize.y)).rgb;
                result *= 0.25;

                return half4(result, 1);
            }

            ENDHLSL
        }

        // Pass 4: Final composite
        Pass
        {
            Name "BloomComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BloomTex);
            SAMPLER(sampler_BloomTex);
            float _Intensity;
            float4 _TintColor;

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

            half4 FragComposite(Varyings input) : SV_Target
            {
                float3 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                float3 bloom = SAMPLE_TEXTURE2D(_BloomTex, sampler_BloomTex, input.uv).rgb;

                // Apply tint and intensity
                bloom *= _TintColor.rgb * _Intensity;

                // Additive blend with slight tone mapping
                float3 result = source + bloom;

                // Simple tone mapping to prevent over-saturation
                result = result / (1.0 + result * 0.1);

                return half4(result, 1);
            }

            ENDHLSL
        }
    }
}
