// ============================================================================
// Nightflow - Vehicle Wireframe Shader
// Specialized wireframe for vehicles with per-vertex color support
// ============================================================================

Shader "Nightflow/VehicleWireframe"
{
    Properties
    {
        _WireThickness ("Wire Thickness", Range(0.001, 0.1)) = 0.015
        _GlowIntensity ("Glow Intensity", Range(0.5, 5.0)) = 1.5
        _GlowFalloff ("Glow Falloff", Range(0.1, 10.0)) = 3.0
        _FillAlpha ("Fill Alpha", Range(0, 0.3)) = 0.02
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.3

        [Header(Animation)]
        _SpeedPulse ("Speed-Based Pulse", Range(0, 1)) = 0.0
        _DamageFlash ("Damage Flash", Range(0, 1)) = 0.0
        _DamageColor ("Damage Color", Color) = (1, 0.3, 0, 1)
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
            Name "VehicleWireframe"
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
                float3 viewDirWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GeomOut
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
                float3 viewDirWS : TEXCOORD3;
                float3 barycentricCoords : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float _WireThickness;
                float _GlowIntensity;
                float _GlowFalloff;
                float _FillAlpha;
                float _FresnelPower;
                float _FresnelIntensity;
                float _SpeedPulse;
                float _DamageFlash;
                float4 _DamageColor;
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

            [maxvertexcount(3)]
            void geom(triangle Varyings input[3], inout TriangleStream<GeomOut> outputStream)
            {
                GeomOut output[3];

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
                    output[i].viewDirWS = input[i].viewDirWS;
                    output[i].barycentricCoords = float3(i == 0, i == 1, i == 2);

                    outputStream.Append(output[i]);
                }

                outputStream.RestartStrip();
            }

            half4 frag(GeomOut input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);

                // Wireframe edge detection
                float3 bary = input.barycentricCoords;
                float3 deltas = fwidth(bary);
                float3 thickness = deltas * _WireThickness * 50.0;
                float3 edgeDist = bary / thickness;
                float minEdgeDist = min(min(edgeDist.x, edgeDist.y), edgeDist.z);
                float wireIntensity = 1.0 - smoothstep(0.0, 1.0, minEdgeDist);

                // Glow effect
                float glowDist = min(min(bary.x, bary.y), bary.z);
                float glow = exp(-glowDist * _GlowFalloff * 10.0) * _GlowIntensity * 0.5;

                // Fresnel rim lighting
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float rim = fresnel * _FresnelIntensity;

                // Speed pulse (for player vehicle during boost)
                float speedPulse = 1.0;
                if (_SpeedPulse > 0)
                {
                    speedPulse = 1.0 + sin(_Time.y * 15.0) * _SpeedPulse * 0.3;
                }

                // Base color from vertex
                float4 baseColor = input.color;

                // Damage flash effect
                if (_DamageFlash > 0)
                {
                    float flash = sin(_Time.y * 30.0) * 0.5 + 0.5;
                    baseColor = lerp(baseColor, _DamageColor, _DamageFlash * flash);
                }

                // Calculate final color
                float3 wireColor = baseColor.rgb * (wireIntensity + glow + rim) * _GlowIntensity * speedPulse;

                // Subtle fill
                float3 fillColor = baseColor.rgb * _FillAlpha;

                // Combine
                float3 finalColor = lerp(fillColor, wireColor, saturate(wireIntensity + glow * 0.5 + rim * 0.3));

                // HDR boost for bloom
                finalColor *= 1.0 + (wireIntensity + glow) * 0.5;

                // Alpha
                float alpha = saturate(wireIntensity + glow * 0.3 + rim * 0.2 + _FillAlpha);

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
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
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}
