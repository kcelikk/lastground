// Additive instanced FX quads (TDD_01 §6.5): tracers and muzzle flashes. Per instance: _FxColor (rgb × a).
// uv.x runs along the quad (tail → head), uv.y across it; the shape is a soft capsule brighter at the head.
Shader "LG/FX_Additive"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "FX"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FxColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; nointerpolation float4 color : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = UNITY_ACCESS_INSTANCED_PROP(Props, _FxColor);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = 1.0 - abs(input.uv.y * 2.0 - 1.0);
                float along = saturate(input.uv.x);
                float ends = saturate(min(along, 1.0 - along) * 8.0);
                float intensity = across * across * ends * (0.35 + 0.65 * along);
                return half4(input.color.rgb * input.color.a * intensity, 0);
            }
            ENDHLSL
        }
    }
}
