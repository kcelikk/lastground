// Crowd bodies: GPU skinning from a bone-matrix texture + GPU instancing (TDD_02 §21.3, D-018).
// Per instance: _Anim = (frameA, frameB, blend, tint index). Bone texture: x = bone*3 + row, y = frame.
// Lighting: main light (Lambert) + ambient SH + optional rim, no shadows (blob shadows come later).
Shader "LG/CrowdInstanced"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _BoneTex ("Bone Matrices", 2D) = "black" {}
        _RimColor ("Rim Color", Color) = (0.35, 0.3, 0.3, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        [Toggle(LG_RIM)] _Rim ("Rim Light", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nolightprobe nolightmap
            #pragma shader_feature_local LG_RIM
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BoneTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END

            // Tint palette (8 entries), set globally by the render system.
            float4 _LGCrowdTints[8];

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Anim)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 boneIndex : TEXCOORD1;
                float4 boneWeight : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                nointerpolation float4 tint : TEXCOORD3;
            };

            float3x4 LoadBone(uint bone, uint frame)
            {
                uint x = bone * 3;
                float4 r0 = LOAD_TEXTURE2D(_BoneTex, int2(x, frame));
                float4 r1 = LOAD_TEXTURE2D(_BoneTex, int2(x + 1, frame));
                float4 r2 = LOAD_TEXTURE2D(_BoneTex, int2(x + 2, frame));
                return float3x4(r0, r1, r2);
            }

            float3x4 SkinMatrix(float4 index, float4 weight, uint frame)
            {
                return LoadBone((uint)index.x, frame) * weight.x
                     + LoadBone((uint)index.y, frame) * weight.y
                     + LoadBone((uint)index.z, frame) * weight.z
                     + LoadBone((uint)index.w, frame) * weight.w;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 anim = UNITY_ACCESS_INSTANCED_PROP(Props, _Anim);

                float3x4 a = SkinMatrix(input.boneIndex, input.boneWeight, (uint)anim.x);
                float3x4 b = SkinMatrix(input.boneIndex, input.boneWeight, (uint)anim.y);
                float3x4 skin = lerp(a, b, anim.z);

                float3 positionOS = mul(skin, float4(input.positionOS.xyz, 1.0));
                float3 normalOS = mul((float3x3)skin, input.normalOS);

                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.tint = _LGCrowdTints[(uint)anim.w & 7];
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * input.tint.rgb;
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half3 color = albedo * (light.color * saturate(dot(normal, light.direction)) + SampleSH(normal));
            #if defined(LG_RIM)
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                color += _RimColor.rgb * pow(1.0 - saturate(dot(normal, view)), _RimPower);
            #endif
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
