// ============================================================================
// Nightflow - Neon Vertex Glow Shader
// Additive vertex-color glow for backdrop elements: city windows, stars,
// moon halo. No UVs required - meshes bake color and alpha per vertex.
// ============================================================================

Shader "Nightflow/NeonVertexGlow"
{
    Properties
    {
        _Intensity ("Glow Intensity", Range(0.1, 8)) = 1.5
        _FogInfluence ("Fog Influence", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One // Additive
        ZWrite Off
        Cull Off

        Pass
        {
            Name "NeonVertexGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float fogFactor : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _FogInfluence;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 color = input.color.rgb * _Intensity;

                // Partial fog: backdrop glows dim into the haze but stay
                // visible as soft halos - city lights seen through fog.
                // _FogInfluence 0 = ignore fog (moon), 1 = full fog (near FX)
                float3 fogged = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                color = lerp(color, fogged, _FogInfluence);

                return half4(color, input.color.a);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
