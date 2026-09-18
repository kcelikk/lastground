// Shared alpha-blended FX (TDD_02 §21.8): particle vertex colour × soft round sprite. No texture needed.
Shader "LG/FX_AlphaBlend"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "FX"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float alpha = saturate(1.0 - dot(input.uv, input.uv));
                return half4(input.color.rgb, input.color.a * alpha * alpha);
            }
            ENDHLSL
        }
    }
}
