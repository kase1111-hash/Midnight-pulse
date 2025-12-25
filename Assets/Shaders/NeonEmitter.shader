// ============================================================================
// Nightflow - Neon Light Emitter Shader
// For headlights, taillights, streetlights, and other glowing elements
// ============================================================================

Shader "Nightflow/NeonEmitter"
{
    Properties
    {
        _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionIntensity ("Emission Intensity", Range(0.5, 10)) = 2.0
        _CoreIntensity ("Core Brightness", Range(1, 5)) = 2.0
        _GlowRadius ("Glow Radius", Range(0, 2)) = 0.5
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0

        [Header(Strobe Effect)]
        [Toggle] _EnableStrobe ("Enable Strobe", Float) = 0
        _StrobeSpeed ("Strobe Speed", Range(1, 20)) = 5
        _StrobeColor2 ("Strobe Color 2", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "NeonEmitter"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One // Additive blending
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature _ENABLESTROBE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float3 viewDirWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float _EmissionIntensity;
                float _CoreIntensity;
                float _GlowRadius;
                float _PulseSpeed;
                float _PulseAmount;
                float _EnableStrobe;
                float _StrobeSpeed;
                float4 _StrobeColor2;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);

                // Distance from center (using UV)
                float2 centeredUV = input.uv - 0.5;
                float distFromCenter = length(centeredUV) * 2.0;

                // Core brightness (brightest at center)
                float core = 1.0 - smoothstep(0.0, 0.3, distFromCenter);
                core = pow(core, 0.5) * _CoreIntensity;

                // Soft glow falloff
                float glow = exp(-distFromCenter * (2.0 / max(_GlowRadius, 0.01)));

                // View-dependent glow (brighter when looking at it)
                float fresnel = pow(saturate(dot(normal, viewDir)), 0.5);
                glow *= 0.5 + fresnel * 0.5;

                // Combine core and glow
                float intensity = core + glow;

                // Pulse animation
                if (_PulseSpeed > 0)
                {
                    float pulse = sin(_Time.y * _PulseSpeed * 6.28318) * _PulseAmount + 1.0;
                    intensity *= pulse;
                }

                // Base emission color (combine with vertex color)
                float4 emissionColor = _EmissionColor * input.color;

                // Strobe effect for emergency lights
                #ifdef _ENABLESTROBE_ON
                {
                    float strobe = sin(_Time.y * _StrobeSpeed * 6.28318);
                    strobe = step(0, strobe); // Square wave
                    emissionColor = lerp(_StrobeColor2, _EmissionColor, strobe);
                }
                #endif

                // Final color with HDR intensity for bloom
                float3 finalColor = emissionColor.rgb * intensity * _EmissionIntensity;

                // Alpha based on intensity for proper blending
                float alpha = saturate(intensity);

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}
