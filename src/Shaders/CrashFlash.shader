// ============================================================================
// Nightflow - Crash Flash Post-Process Shader
// Full-screen flash overlay with chromatic aberration for impact feedback
// ============================================================================

Shader "Nightflow/CrashFlash"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0
        _VignetteStrength ("Vignette Strength", Range(0, 2)) = 0.5
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.1)) = 0.02
        _Distortion ("Screen Distortion", Range(0, 0.1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "CrashFlashPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FlashColor;
                float _FlashIntensity;
                float _VignetteStrength;
                float _ChromaticAberration;
                float _Distortion;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 center = float2(0.5, 0.5);
                float2 toCenter = uv - center;
                float dist = length(toCenter);

                // Screen distortion (radial warp during flash)
                float2 distortedUV = uv;
                if (_Distortion > 0)
                {
                    float distortAmount = _Distortion * _FlashIntensity * dist;
                    distortedUV = uv + toCenter * distortAmount;
                }

                // Chromatic aberration
                float chromaOffset = _ChromaticAberration * _FlashIntensity;
                float2 direction = normalize(toCenter);

                float3 color;
                if (chromaOffset > 0.001)
                {
                    // Sample RGB channels with offset
                    float2 uvR = distortedUV + direction * chromaOffset;
                    float2 uvG = distortedUV;
                    float2 uvB = distortedUV - direction * chromaOffset;

                    color.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r;
                    color.g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG).g;
                    color.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b;
                }
                else
                {
                    color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV).rgb;
                }

                // Vignette effect (darkens edges, enhanced during flash)
                float vignette = 1.0 - smoothstep(0.3, 1.0, dist * _VignetteStrength * (1.0 + _FlashIntensity));
                color *= vignette;

                // Apply flash overlay
                float flashAmount = _FlashIntensity;

                // Flash color blend (additive for bright flash, then lerp to tint)
                float3 flashColor = _FlashColor.rgb;

                // Strong additive at peak, transitioning to color tint
                float additiveFactor = pow(_FlashIntensity, 0.5);
                color = color + flashColor * additiveFactor * 2.0;

                // Clamp to prevent over-saturation
                color = saturate(color);

                // Additional color tint (for red flash during fade)
                float tintFactor = _FlashIntensity * (1.0 - _FlashColor.a);
                color = lerp(color, flashColor, tintFactor * 0.3);

                // Slight desaturation during intense flash
                float luminance = dot(color, float3(0.299, 0.587, 0.114));
                float desatAmount = pow(_FlashIntensity, 2.0) * 0.3;
                color = lerp(color, float3(luminance, luminance, luminance), desatAmount);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
