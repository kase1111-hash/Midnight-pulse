// ============================================================================
// Nightflow - Road Surface Shader
// Dark road with glowing lane markings and subtle reflections
// ============================================================================

Shader "Nightflow/RoadSurface"
{
    Properties
    {
        _BaseColor ("Road Color", Color) = (0.1, 0.1, 0.15, 1)
        _LaneLineColor ("Lane Line Color", Color) = (0.27, 0.53, 1, 1)
        _EdgeLineColor ("Edge Line Color", Color) = (1, 0.53, 0, 1)
        _LaneLineWidth ("Lane Line Width", Range(0.01, 0.5)) = 0.15
        _EdgeLineWidth ("Edge Line Width", Range(0.01, 0.5)) = 0.2
        _LaneWidth ("Lane Width", Float) = 3.6
        _NumLanes ("Number of Lanes", Int) = 4
        _DashLength ("Dash Length", Float) = 3.0
        _DashGap ("Dash Gap", Float) = 6.0
        _GlowIntensity ("Line Glow Intensity", Range(0.5, 3)) = 1.5
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.3
        _WetnessFactor ("Wetness", Range(0, 1)) = 0.2
        _GridSize ("Grid Size", Float) = 10.0
        _GridIntensity ("Grid Intensity", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RoadSurface"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LaneLineColor;
                float4 _EdgeLineColor;
                float _LaneLineWidth;
                float _EdgeLineWidth;
                float _LaneWidth;
                int _NumLanes;
                float _DashLength;
                float _DashGap;
                float _GlowIntensity;
                float _ReflectionStrength;
                float _WetnessFactor;
                float _GridSize;
                float _GridIntensity;
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
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);

                return output;
            }

            // Smooth line function with glow
            float LineMask(float dist, float width, float glowWidth)
            {
                float core = 1.0 - smoothstep(0, width * 0.5, abs(dist));
                float glow = exp(-abs(dist) / glowWidth) * 0.5;
                return core + glow;
            }

            // Dashed line pattern
            float DashPattern(float pos, float dashLen, float gapLen)
            {
                float cycle = dashLen + gapLen;
                float t = fmod(pos, cycle);
                return smoothstep(0, 0.1, t) * smoothstep(dashLen, dashLen - 0.1, t);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);

                // Road base color
                float3 roadColor = _BaseColor.rgb;

                // Calculate road-space coordinates
                float roadX = input.uv.x; // 0-1 across road width
                float roadZ = input.positionWS.z; // Along road length

                // Convert to world units (assuming UV is normalized)
                float totalWidth = _LaneWidth * _NumLanes;
                float xWorld = (roadX - 0.5) * totalWidth;

                // Lane line positions (between lanes)
                float3 lineColor = float3(0, 0, 0);
                float lineIntensity = 0;

                // Interior lane lines (dashed)
                for (int i = 1; i < _NumLanes; i++)
                {
                    float linePos = (i - _NumLanes * 0.5) * _LaneWidth;
                    float dist = abs(xWorld - linePos);
                    float mask = LineMask(dist, _LaneLineWidth, _LaneLineWidth * 2);
                    float dash = DashPattern(roadZ, _DashLength, _DashGap);
                    float intensity = mask * dash;
                    lineColor += _LaneLineColor.rgb * intensity;
                    lineIntensity = max(lineIntensity, intensity);
                }

                // Edge lines (solid, brighter)
                float leftEdge = -totalWidth * 0.5;
                float rightEdge = totalWidth * 0.5;

                float leftDist = abs(xWorld - leftEdge);
                float rightDist = abs(xWorld - rightEdge);

                float leftMask = LineMask(leftDist, _EdgeLineWidth, _EdgeLineWidth * 2);
                float rightMask = LineMask(rightDist, _EdgeLineWidth, _EdgeLineWidth * 2);

                lineColor += _EdgeLineColor.rgb * (leftMask + rightMask);
                lineIntensity = max(lineIntensity, leftMask + rightMask);

                // Subtle grid pattern for depth
                float2 gridUV = input.positionWS.xz / _GridSize;
                float gridX = abs(frac(gridUV.x) - 0.5) * 2;
                float gridZ = abs(frac(gridUV.y) - 0.5) * 2;
                float grid = max(
                    1.0 - smoothstep(0.95, 1.0, gridX),
                    1.0 - smoothstep(0.95, 1.0, gridZ)
                ) * _GridIntensity;

                // Fresnel-based reflection
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 3);
                float reflection = fresnel * _ReflectionStrength * (1.0 + _WetnessFactor);

                // Combine
                float3 finalColor = roadColor;
                finalColor += grid * float3(0.1, 0.1, 0.15); // Grid adds subtle blue
                finalColor += lineColor * _GlowIntensity;
                finalColor += reflection * float3(0.05, 0.1, 0.15); // Sky reflection tint

                return half4(finalColor, 1);
            }

            ENDHLSL
        }
    }
}
