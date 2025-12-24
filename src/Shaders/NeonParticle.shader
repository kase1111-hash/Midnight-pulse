// ============================================================================
// Nightflow - Neon Particle Shader
// Additive billboard particles with glow for sparks and effects
// ============================================================================

Shader "Nightflow/NeonParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 0.6, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2.0
        _SoftParticles ("Soft Particles Factor", Range(0, 3)) = 1.0
        [Toggle] _UseTexture ("Use Texture", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend One One // Additive blending
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ParticleUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _SOFT_PARTICLES_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float4 projectedPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TintColor;
                float _EmissionIntensity;
                float _SoftParticles;
                float _UseTexture;
            CBUFFER_END

            // Per-instance color data
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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.projectedPosition = ComputeScreenPos(output.positionCS);

                // Get per-instance color or use tint
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

                // Calculate radial gradient for circular particle
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0;

                // Soft circular falloff
                float circle = 1.0 - saturate(dist);
                circle = pow(circle, 1.5); // Sharper center

                // Optional texture
                float4 texColor = float4(1, 1, 1, 1);
                if (_UseTexture > 0.5)
                {
                    texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                }

                // Combine with instance color
                float4 color = input.color;
                color.rgb *= _EmissionIntensity;

                // Apply circular gradient
                float alpha = circle * color.a * texColor.a;

                // Soft particles (fade near geometry)
                #if defined(_SOFT_PARTICLES_ON)
                    float2 screenUV = input.projectedPosition.xy / input.projectedPosition.w;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float particleDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    float fade = saturate((sceneDepth - particleDepth) * _SoftParticles);
                    alpha *= fade;
                #endif

                // Final color with glow
                float3 finalColor = color.rgb * circle * texColor.rgb;

                // Add extra glow at center
                float coreGlow = pow(1.0 - saturate(dist * 1.5), 3.0);
                finalColor += color.rgb * coreGlow * 0.5;

                return half4(finalColor * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
