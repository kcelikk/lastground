// Draws the lighting grid onto the ground as additive light pools (fake sodium lamps, TDD_01 §0.4 / TDD_02 §21.2).
Shader "LG/LightPoolGround"
{
    Properties { _Intensity ("Intensity", Range(0, 2)) = 0.35 }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-200" }
        Pass
        {
            Name "Pools"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LGLightGrid);
            SAMPLER(sampler_LGLightGrid);
            float4 _LGLightGridRect;
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = (input.positionWS.xz - _LGLightGridRect.xy) * _LGLightGridRect.zw;
                return half4(SAMPLE_TEXTURE2D(_LGLightGrid, sampler_LGLightGrid, uv).rgb * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
