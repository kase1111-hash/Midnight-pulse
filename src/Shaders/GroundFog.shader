// ============================================================================
// Nightflow - Ground Fog Shader
// Layered fog planes with animated smoke noise and per-vertex opacity
// Used by GroundFogRenderer; vertex color carries tint + distance-faded alpha
// ============================================================================

Shader "Nightflow/GroundFog"
{
    Properties
    {
        _NoiseScale ("Noise Scale (world units)", Range(0.001, 0.2)) = 0.02
        _NoiseSpeed ("Noise Drift Speed", Range(0, 2)) = 0.25
        _DensityBoost ("Density Boost", Range(0.5, 3)) = 1.4
        _NoiseContrast ("Noise Contrast", Range(0.5, 3)) = 1.2
        _NeonTint ("Neon Tint Color", Color) = (0.15, 0.35, 0.5, 0.25)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GroundFog"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _NoiseScale;
                float _NoiseSpeed;
                float _DensityBoost;
                float _NoiseContrast;
                float4 _NeonTint;
            CBUFFER_END

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // World-space smoke noise so layers drift independently of the
                // camera-following quads (UVs would swim as planes recenter)
                float time = _Time.y * _NoiseSpeed;
                float2 noiseUV = input.positionWS.xz * _NoiseScale + float2(time * 0.35, time * 0.2);
                float smoke = fbm(noiseUV);

                // Counter-scrolling second octave breaks up visible repetition
                float2 noiseUV2 = input.positionWS.xz * _NoiseScale * 2.7 - float2(time * 0.5, time * 0.3);
                smoke = (smoke + fbm(noiseUV2) * 0.5) / 1.5;

                smoke = saturate(pow(smoke * 1.6, _NoiseContrast));

                // Vertex alpha carries layer density and distance fades
                float alpha = saturate(input.color.a * smoke * _DensityBoost);

                // Neon tint rises where the smoke is thickest, catching nearby light
                float3 fogRgb = input.color.rgb + _NeonTint.rgb * _NeonTint.a * smoke;

                // Distant fog planes converge on the global fog color
                fogRgb = MixFog(fogRgb, input.fogFactor);

                return half4(fogRgb, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
