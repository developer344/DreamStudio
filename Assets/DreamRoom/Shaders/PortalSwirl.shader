// Swirling, glowing portal surface. Draw on a quad; the UVs are masked to an ellipse.
Shader "DreamStudio/PortalSwirl"
{
    Properties
    {
        _ColorA ("Inner Color", Color) = (1, 0.6, 0.9, 1)
        _ColorB ("Outer Color", Color) = (0.4, 0.3, 1, 1)
        _Intensity ("Intensity", Range(0, 5)) = 1.6
        _SwirlSpeed ("Swirl Speed", Float) = 0.6
        _SwirlTightness ("Swirl Tightness", Float) = 6
        _Alpha ("Alpha", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half _Intensity;
                float _SwirlSpeed;
                float _SwirlTightness;
                half _Alpha;
            CBUFFER_END

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                if (r > 1.0) discard;

                float t = _Time.y * _SwirlSpeed;
                float angle = atan2(p.y, p.x);
                // Twist the angle by radius so bands spiral toward the center.
                float twisted = angle + (1.0 - r) * _SwirlTightness + t;
                float2 swirlUV = float2(cos(twisted), sin(twisted)) * r * 3.0;

                float n = noise(swirlUV + t) * 0.6 + noise(swirlUV * 2.3 - t * 1.3) * 0.4;
                float bands = sin(twisted * 3.0 + n * 4.0) * 0.5 + 0.5;

                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(r * 0.9 + bands * 0.35));
                // Bright core and sparkling flecks.
                col += (1.0 - smoothstep(0.0, 0.35, r)) * 0.8;
                col += step(0.985, noise(swirlUV * 9.0 + t * 2.0)) * 1.5;
                col *= _Intensity * (0.75 + bands * 0.5);

                half edge = 1.0 - smoothstep(0.85, 1.0, r);
                return half4(col, _Alpha * edge);
            }
            ENDHLSL
        }
    }
}
