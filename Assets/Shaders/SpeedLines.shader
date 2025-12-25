// ============================================================================
// Nightflow - Speed Lines Shader
// Radial speed streaks for high-velocity effect
// ============================================================================

Shader "Nightflow/SpeedLines"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.8, 1, 1, 0.8)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 2.0
        _LineWidth ("Line Width", Range(0.01, 0.5)) = 0.1
        _LineFalloff ("Line Falloff", Range(0.1, 5)) = 2.0
        _CoreBrightness ("Core Brightness", Range(1, 10)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One // Additive
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SpeedLines"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _EmissionIntensity;
                float _LineWidth;
                float _LineFalloff;
                float _CoreBrightness;
            CBUFFER_END

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(Props)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _Colors)
                UNITY_INSTANCING_BUFFER_END(Props)
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                #ifdef UNITY_INSTANCING_ENABLED
                    output.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Colors);
                #else
                    output.color = _TintColor;
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // UV.x is width (cross-section), UV.y is length
                float2 uv = input.uv;

                // Calculate line shape
                // Cross-section falloff (thin line)
                float crossSection = abs(uv.x - 0.5) * 2.0;
                float lineShape = 1.0 - saturate(crossSection / _LineWidth);
                lineShape = pow(lineShape, _LineFalloff);

                // Length falloff (fade at ends)
                float lengthFade = 1.0 - abs(uv.y - 0.5) * 2.0;
                lengthFade = pow(lengthFade, 0.5);

                // Combine
                float alpha = lineShape * lengthFade * input.color.a;

                // Core glow (brightest at center)
                float coreGlow = pow(lineShape, 2.0) * _CoreBrightness;

                // Color with emission
                float3 color = input.color.rgb * _EmissionIntensity;
                color += color * coreGlow;

                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
