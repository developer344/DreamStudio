// Skybox: pastel dusk gradient with drifting nebula and twinkling stars.
Shader "DreamStudio/DreamSky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.12, 0.08, 0.35, 1)
        _HorizonColor ("Horizon Color", Color) = (0.95, 0.6, 0.85, 1)
        _BottomColor ("Bottom Color", Color) = (0.55, 0.45, 0.9, 1)
        _NebulaColor ("Nebula Color", Color) = (0.5, 0.8, 1, 1)
        _StarDensity ("Star Density", Float) = 120
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _NebulaColor;
                float _StarDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), u.x),
                                 lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), u.x), u.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), u.x),
                                 lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), u.x), u.y), u.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dir = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                float h = d.y;

                half3 col = h > 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(h), 0.6))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-h), 0.5));

                float t = _Time.y * 0.02;
                float neb = noise3(d * 3.0 + t) * 0.6 + noise3(d * 7.0 - t) * 0.4;
                col += _NebulaColor.rgb * smoothstep(0.55, 0.9, neb) * 0.35 * saturate(h + 0.3);

                float3 cell = floor(d * _StarDensity);
                float star = hash3(cell);
                float twinkle = 0.5 + 0.5 * sin(_Time.y * (2.0 + star * 4.0) + star * 50.0);
                col += step(0.993, star) * twinkle * saturate(h * 2.0 + 0.2) * 1.4;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
