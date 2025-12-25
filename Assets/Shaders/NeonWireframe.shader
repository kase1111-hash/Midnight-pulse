// ============================================================================
// Nightflow - Neon Wireframe Shader
// Renders geometry as glowing wireframe edges with bloom support
// ============================================================================

Shader "Nightflow/NeonWireframe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WireColor ("Wire Color", Color) = (0, 1, 1, 1)
        _WireThickness ("Wire Thickness", Range(0.001, 0.1)) = 0.02
        _GlowIntensity ("Glow Intensity", Range(0.5, 5.0)) = 1.5
        _GlowFalloff ("Glow Falloff", Range(0.1, 10.0)) = 2.0
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.05
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0.5
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "NeonWireframe"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GeomOut
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float3 barycentricCoords : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _WireColor;
                float _WireThickness;
                float _GlowIntensity;
                float _GlowFalloff;
                float _FillAlpha;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;

                return output;
            }

            [maxvertexcount(3)]
            void geom(triangle Varyings input[3], inout TriangleStream<GeomOut> outputStream)
            {
                GeomOut output[3];

                // Pass through with barycentric coordinates for edge detection
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    UNITY_SETUP_INSTANCE_ID(input[i]);
                    UNITY_TRANSFER_INSTANCE_ID(input[i], output[i]);

                    output[i].positionCS = input[i].positionCS;
                    output[i].positionWS = input[i].positionWS;
                    output[i].normalWS = input[i].normalWS;
                    output[i].uv = input[i].uv;
                    output[i].color = input[i].color;

                    // Assign barycentric coordinates
                    output[i].barycentricCoords = float3(i == 0, i == 1, i == 2);

                    outputStream.Append(output[i]);
                }

                outputStream.RestartStrip();
            }

            half4 frag(GeomOut input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Calculate distance to nearest edge using barycentric coordinates
                float3 bary = input.barycentricCoords;
                float3 deltas = fwidth(bary);
                float3 smoothing = deltas * 1.5;
                float3 thickness = deltas * _WireThickness * 50.0;

                // Distance to each edge
                float3 edgeDist = bary / thickness;
                float minEdgeDist = min(min(edgeDist.x, edgeDist.y), edgeDist.z);

                // Wireframe intensity with smooth falloff
                float wireIntensity = 1.0 - smoothstep(0.0, 1.0, minEdgeDist);

                // Glow effect - extends beyond the wire
                float glowDist = min(min(bary.x, bary.y), bary.z);
                float glow = exp(-glowDist * _GlowFalloff * 10.0) * _GlowIntensity;

                // Pulse animation
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.28318) * _PulseAmount;

                // Combine vertex color with wire color
                float4 baseColor = input.color * _WireColor;

                // Calculate final color
                float4 wireColor = baseColor * (wireIntensity + glow) * pulse;
                wireColor.rgb *= _GlowIntensity;

                // Add subtle fill
                float4 fillColor = baseColor * _FillAlpha;

                // Final output - wire on top of fill
                float4 finalColor = lerp(fillColor, wireColor, saturate(wireIntensity + glow * 0.5));
                finalColor.a = saturate(wireIntensity + glow * 0.3 + _FillAlpha);

                // HDR output for bloom
                finalColor.rgb *= 1.0 + glow;

                return finalColor;
            }

            ENDHLSL
        }

        // Depth-only pass for proper sorting
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
