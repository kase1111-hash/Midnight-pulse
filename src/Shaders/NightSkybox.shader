// ============================================================================
// Nightflow - Night Skybox Shader
// Deep black horizon with subtle gradient and distant city glow
// ============================================================================

Shader "Nightflow/NightSkybox"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.02, 0.02, 0.04, 1)
        _HorizonColor ("Horizon Color", Color) = (0.05, 0.03, 0.08, 1)
        _BottomColor ("Bottom Color", Color) = (0.04, 0.02, 0.06, 1)
        _HorizonHeight ("Horizon Height", Range(-1, 1)) = 0.0
        _HorizonSharpness ("Horizon Sharpness", Range(0.1, 10)) = 2.0

        [Header(City Glow)]
        _CityGlowColor ("City Glow Color", Color) = (0.4, 0.2, 0.5, 1)
        _CityGlowIntensity ("City Glow Intensity", Range(0, 1)) = 0.3
        _CityGlowHeight ("City Glow Height", Range(-0.5, 0.5)) = -0.1
        _CityGlowWidth ("City Glow Width", Range(0.01, 1)) = 0.2

        [Header(Stars)]
        _StarDensity ("Star Density", Range(0, 500)) = 200
        _StarBrightness ("Star Brightness", Range(0, 2)) = 0.5
        _StarTwinkleSpeed ("Twinkle Speed", Range(0, 5)) = 1.0

        [Header(Fog)]
        _FogColor ("Fog Color", Color) = (0.03, 0.02, 0.05, 1)
        _FogDensity ("Fog Density", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _HorizonColor;
                float4 _BottomColor;
                float _HorizonHeight;
                float _HorizonSharpness;
                float4 _CityGlowColor;
                float _CityGlowIntensity;
                float _CityGlowHeight;
                float _CityGlowWidth;
                float _StarDensity;
                float _StarBrightness;
                float _StarTwinkleSpeed;
                float4 _FogColor;
                float _FogDensity;
            CBUFFER_END

            // Hash function for procedural stars
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 2D noise for variation
            float Noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDir);
                float y = viewDir.y;

                // Sky gradient
                float horizonBlend = saturate((y - _HorizonHeight) * _HorizonSharpness + 0.5);
                float bottomBlend = saturate((-y - _HorizonHeight) * _HorizonSharpness + 0.5);

                float3 skyColor = lerp(_HorizonColor.rgb, _TopColor.rgb, horizonBlend);
                skyColor = lerp(skyColor, _BottomColor.rgb, bottomBlend);

                // City glow on horizon
                float cityGlowDist = abs(y - _CityGlowHeight);
                float cityGlow = exp(-cityGlowDist / _CityGlowWidth) * _CityGlowIntensity;

                // Add variation to city glow
                float glowNoise = Noise2D(float2(atan2(viewDir.z, viewDir.x) * 10, 0) + _Time.x * 0.1);
                cityGlow *= 0.7 + glowNoise * 0.3;

                skyColor += _CityGlowColor.rgb * cityGlow;

                // Stars (only above horizon)
                if (y > _HorizonHeight - 0.1)
                {
                    float2 starUV = float2(atan2(viewDir.z, viewDir.x), asin(y)) * _StarDensity;
                    float starHash = Hash(floor(starUV));

                    if (starHash > 0.97) // Only show some cells as stars
                    {
                        float2 cellCenter = floor(starUV) + 0.5;
                        float2 diff = starUV - cellCenter;
                        float starDist = length(diff);

                        // Star intensity with twinkle
                        float twinkle = sin(_Time.y * _StarTwinkleSpeed * (starHash * 5 + 1)) * 0.5 + 0.5;
                        float starIntensity = (1.0 - smoothstep(0.0, 0.15, starDist)) * _StarBrightness;
                        starIntensity *= 0.5 + twinkle * 0.5;

                        // Fade stars near horizon
                        starIntensity *= saturate((y - _HorizonHeight) * 5);

                        // Star color (slight variation)
                        float3 starColor = lerp(float3(0.8, 0.9, 1.0), float3(1.0, 0.9, 0.8), starHash);
                        skyColor += starColor * starIntensity;
                    }
                }

                // Atmospheric fog near horizon
                float fogBlend = exp(-abs(y - _HorizonHeight) / 0.3) * _FogDensity;
                skyColor = lerp(skyColor, _FogColor.rgb, fogBlend);

                return half4(skyColor, 1);
            }

            ENDHLSL
        }
    }
}
