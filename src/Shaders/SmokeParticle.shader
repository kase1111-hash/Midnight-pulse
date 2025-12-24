// ============================================================================
// Nightflow - Smoke Particle Shader
// Soft, volumetric-looking smoke with subtle neon tint
// ============================================================================

Shader "Nightflow/SmokeParticle"
{
    Properties
    {
        _MainTex ("Noise Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.3, 0.35, 0.4, 0.5)
        _NeonTint ("Neon Tint Color", Color) = (0, 0.5, 0.5, 0.1)
        _SoftParticles ("Soft Particles Factor", Range(0, 5)) = 2.0
        _NoiseScale ("Noise Scale", Range(0.1, 5)) = 1.0
        _NoiseSpeed ("Noise Animation Speed", Range(0, 2)) = 0.5
        _EdgeSoftness ("Edge Softness", Range(0.1, 2)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha // Alpha blending for smoke
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SmokeParticle"
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
                float3 worldPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TintColor;
                float4 _NeonTint;
                float _SoftParticles;
                float _NoiseScale;
                float _NoiseSpeed;
                float _EdgeSoftness;
            CBUFFER_END

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(Props)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _Colors)
                UNITY_INSTANCING_BUFFER_END(Props)
            #endif

            // Simple noise function
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

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.projectedPosition = ComputeScreenPos(output.positionCS);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);

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

                // Circular base shape
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0;

                // Soft circular falloff
                float circle = 1.0 - saturate(dist);
                circle = pow(circle, _EdgeSoftness);

                // Animated noise for smoke turbulence
                float time = _Time.y * _NoiseSpeed;
                float2 noiseUV = input.uv * _NoiseScale + float2(time * 0.1, time * 0.05);
                float noiseValue = fbm(noiseUV);

                // Second noise layer for more detail
                float2 noiseUV2 = input.uv * _NoiseScale * 2.0 - float2(time * 0.15, time * 0.1);
                float noiseValue2 = fbm(noiseUV2);

                // Combine noise
                float combinedNoise = (noiseValue + noiseValue2 * 0.5) / 1.5;

                // Apply noise to alpha
                float alpha = circle * combinedNoise * input.color.a;

                // Soft particles
                #if defined(_SOFT_PARTICLES_ON)
                    float2 screenUV = input.projectedPosition.xy / input.projectedPosition.w;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float particleDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    float fade = saturate((sceneDepth - particleDepth) * _SoftParticles);
                    alpha *= fade;
                #endif

                // Base smoke color
                float3 smokeColor = input.color.rgb;

                // Add subtle neon tint based on noise
                float3 neonColor = _NeonTint.rgb * _NeonTint.a;
                smokeColor = lerp(smokeColor, smokeColor + neonColor, noiseValue * 0.5);

                // Darken edges for volumetric look
                float edgeDarken = 1.0 - pow(dist, 0.5) * 0.3;
                smokeColor *= edgeDarken;

                return half4(smokeColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
