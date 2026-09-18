// Ground splats (blood L2, later: light pools, telegraphs). Procedural blob, alpha-blended, instanced.
// Per instance: _Splat = (fade, seed, grow, unused).
Shader "LG/GroundDecal"
{
    Properties
    {
        _Color ("Color", Color) = (0.22, 0.015, 0.015, 0.85)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" }

        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Splat)
            UNITY_INSTANCING_BUFFER_END(Props)

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
                nointerpolation float4 splat : TEXCOORD1;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                output.splat = UNITY_ACCESS_INSTANCED_PROP(Props, _Splat);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float seed = input.splat.y * 37.0;
                float r = length(input.uv);
                float edge = 0.55 + 0.35 * Noise(input.uv * 3.0 + seed) * input.splat.z;
                float blob = saturate((edge - r) * 6.0);
                float drops = step(0.82, Noise(input.uv * 9.0 + seed * 1.7)) * saturate(1.0 - r);
                float alpha = saturate(blob + drops) * _Color.a * input.splat.x;
                return half4(_Color.rgb * (0.8 + 0.2 * Noise(input.uv * 6.0 + seed)), alpha);
            }
            ENDHLSL
        }
    }
}
