// ============================================================================
// Nightflow - Film Grain Overlay Shader
// Adds cinematic film grain, scanlines, and subtle vignette
// ============================================================================

Shader "Nightflow/PostProcess/FilmGrain"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}

        [Header(Grain)]
        _GrainIntensity ("Grain Intensity", Range(0, 0.5)) = 0.1
        _GrainSize ("Grain Size", Range(0.5, 5)) = 1.5
        _GrainSpeed ("Grain Animation Speed", Range(0, 10)) = 5

        [Header(Scanlines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.1
        _ScanlineCount ("Scanline Count", Range(100, 1000)) = 400
        _ScanlineSpeed ("Scanline Scroll Speed", Range(0, 5)) = 0.5

        [Header(Vignette)]
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.4
        _VignetteRadius ("Vignette Radius", Range(0.1, 2)) = 0.8
        _VignetteSoftness ("Vignette Softness", Range(0.1, 1)) = 0.5

        [Header(Color)]
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.05
        _Brightness ("Brightness", Range(-0.5, 0.5)) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FilmGrain"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float _GrainIntensity;
            float _GrainSize;
            float _GrainSpeed;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _ScanlineSpeed;
            float _VignetteIntensity;
            float _VignetteRadius;
            float _VignetteSoftness;
            float _Saturation;
            float _Contrast;
            float _Brightness;

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

            // High-quality noise function
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float GrainNoise(float2 uv, float time)
            {
                float2 p = uv * _MainTex_TexelSize.zw / _GrainSize;
                p += time * 100;

                float n = Hash(p);
                n = n * 2.0 - 1.0;

                // Multi-octave for better quality
                n += (Hash(p * 2.0 + 1.5) * 2.0 - 1.0) * 0.5;
                n += (Hash(p * 4.0 + 3.7) * 2.0 - 1.0) * 0.25;

                return n * 0.5;
            }

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
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;

                // ============================================
                // Color adjustments
                // ============================================

                // Brightness
                color += _Brightness;

                // Contrast
                color = (color - 0.5) * _Contrast + 0.5;

                // Saturation
                float luminance = dot(color, float3(0.299, 0.587, 0.114));
                color = lerp(float3(luminance, luminance, luminance), color, _Saturation);

                // ============================================
                // Film grain
                // ============================================

                float time = _Time.y * _GrainSpeed;
                float grain = GrainNoise(uv, time);

                // Apply grain (more visible in darker areas)
                float grainMask = 1.0 - luminance * 0.5;
                color += grain * _GrainIntensity * grainMask;

                // ============================================
                // Scanlines
                // ============================================

                float scanline = sin((uv.y + _Time.y * _ScanlineSpeed * 0.01) * _ScanlineCount * 3.14159);
                scanline = scanline * 0.5 + 0.5; // 0-1 range
                scanline = lerp(1.0, scanline, _ScanlineIntensity);
                color *= scanline;

                // Subtle horizontal line flicker
                float flicker = sin(_Time.y * 60) * 0.01 + 1.0;
                color *= flicker;

                // ============================================
                // Vignette
                // ============================================

                float2 vignetteUV = uv - 0.5;
                float vignetteDist = length(vignetteUV);
                float vignette = smoothstep(_VignetteRadius, _VignetteRadius - _VignetteSoftness, vignetteDist);
                vignette = lerp(1.0, vignette, _VignetteIntensity);
                color *= vignette;

                // ============================================
                // Final output
                // ============================================

                // Clamp to valid range
                color = saturate(color);

                return half4(color, 1);
            }

            ENDHLSL
        }
    }
}
