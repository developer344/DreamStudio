// The game's illustration look, after the Dream Decoder cards: flat colour fills pulled toward a small,
// muted palette (cream, blush, peach, coral, mustard, dusty blue, teal, sage, taupe, navy), shaded in two
// soft tones (light, and a lavender shade instead of dark shadow), with a fine print grain.
//
// Light and shade are decided by the sun alone (which way a surface faces it, and whether something casts a
// shadow on it), so cast shadows read as crisp shapes however bright the sky light is. Lamps are gathered by
// URP's own Blinn-Phong on a white surface (minus the sun and sky) and brighten whatever they reach. The palette is a global set by
// DreamStudio.DreamStyle. Textures either supply the colour (cut-out plant cards, read from UVs) or, with
// _TRIPLANAR, only a faint, flattened detail projected in object space. Property names follow URP Lit so
// materials can switch between the two.
Shader "DreamStudio/Storybook"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _ShadeColor ("Shade Tint", Color) = (0.7, 0.68, 0.86, 1)
        _ShadeEdge ("Shade Edge", Range(0, 1)) = 0.35
        _ShadeSoftness ("Shade Softness", Range(0.01, 0.5)) = 0.1
        _Detail ("Texture Detail", Range(0, 1)) = 0.3
        _PaletteSnap ("Palette Snap", Range(0, 1)) = 0.75
        _Lift ("Lift Toward Light", Range(0, 1)) = 0.25
        _Grain ("Grain", Range(0, 0.3)) = 0.06
        [Toggle(_TRIPLANAR)] _Triplanar ("Triplanar Detail", Float) = 0
        _WorldScale ("Tiles Per Meter", Float) = 0.5
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 4
        // Unused here; kept so values survive switching to and from URP Lit.
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _ShadeColor;
            half _Cutoff;
            half _ShadeEdge;
            half _ShadeSoftness;
            half _Detail;
            half _PaletteSnap;
            half _Lift;
            half _Grain;
            float _WorldScale;
            half _BlendSharpness;
            half _Smoothness;
            half _Metallic;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // The palette, in gamma (sRGB) values, set globally by DreamStyle.
        float4 _DreamPalette[16];
        float _DreamPaletteCount;
        float _DreamContrast;   // the colour grade, set by DreamStyle (0 = not set: unchanged)
        float _DreamSaturation;

        float3 ObjectScale()
        {
            return float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _TRIPLANAR
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 triPos : TEXCOORD3;
                half3 triNormal : TEXCOORD4;
                half4 fogAndVertexLight : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                float3 scale = ObjectScale();
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.triPos = input.positionOS.xyz * scale;
                output.triNormal = normalize(input.normalOS / max(scale, 1e-4));
                output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
                output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, output.normalWS);
                return output;
            }

            // Nearest palette colour to a gamma-space colour (closer to how we see), returned in linear.
            half3 SnapToPalette(half3 g)
            {
                int count = (int)_DreamPaletteCount;
                if (count <= 0)
                    return g * g;
                half3 best = g;
                half bestDistance = 1e4;
                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    half3 p = (half3)_DreamPalette[i].rgb;
                    half3 d = (g - p) * half3(1.0, 1.2, 0.8);
                    half dist2 = dot(d, d);
                    if (i < count && dist2 < bestDistance)
                    {
                        bestDistance = dist2;
                        best = p;
                    }
                }
                return best * best;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x);
                float b = lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x);
                float c = lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x);
                float d = lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            #if defined(_ALPHATEST_ON)
                clip(tex.a * _BaseColor.a - _Cutoff);
            #endif

                half3 albedo;
            #if defined(_TRIPLANAR)
                // Only a faint trace of the texture: blend from its average colour toward its detail.
                half3 n = normalize(input.triNormal);
                half3 w = pow(abs(n), _BlendSharpness);
                w /= max(w.x + w.y + w.z, 1e-4);
                float3 p = input.triPos * _WorldScale;
                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy).rgb * w.x
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz).rgb * w.y
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy).rgb * w.z;
                half3 mean = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, float2(0.5, 0.5), 12).rgb;
                albedo = _BaseColor.rgb * lerp(mean, detail, _Detail);
            #else
                albedo = _BaseColor.rgb * tex.rgb;
            #endif
                // The cards are high-key: lift every colour toward the light, then pull it onto the palette.
                half3 gammaAlbedo = 1.0 - (1.0 - sqrt(max(albedo, 0))) * (1.0 - _Lift);
                albedo = lerp(gammaAlbedo * gammaAlbedo, SnapToPalette(gammaAlbedo), _PaletteSnap);

                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogAndVertexLight.x);
                inputData.vertexLighting = input.fogAndVertexLight.yzw;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // The sun alone decides light and shade: which way the surface faces it, and cast shadows.
                Light sun = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                half sunlit = saturate(dot(normalWS, sun.direction)) * sun.shadowAttenuation * sun.distanceAttenuation;
                half sunStrength = Luminance(sun.color);
                half band = smoothstep(_ShadeEdge - _ShadeSoftness, _ShadeEdge + _ShadeSoftness, sunlit);

                // Everything else (lamps, candles), from URP's own lighting on a white surface, minus the sun and sky.
                SurfaceData probe = (SurfaceData)0;
                probe.albedo = half3(1, 1, 1);
                probe.alpha = 1;
                probe.occlusion = 1;
                probe.normalTS = half3(0, 0, 1);
                half3 everything = UniversalFragmentBlinnPhong(inputData, probe).rgb;
                half3 lamps = max(0, everything - sun.color * sunlit - inputData.bakedGI);

                // Two tones: sunlit (tinted a little by the sun's colour) and a lavender shade, which the sky
                // light keeps from going too dark. Lamps brighten whatever they reach, shade included.
                half sky = saturate(Luminance(inputData.bakedGI));
                half3 sunHue = sun.color / max(sunStrength, 1e-3);
                half3 litTone = lerp(half3(1, 1, 1), sunHue, 0.25) * lerp(0.85, 1.05, saturate(sunStrength));
                half3 shadeTone = _ShadeColor.rgb * lerp(0.72, 1.0, sky);
                half3 tone = lerp(shadeTone, litTone, band * saturate(sunStrength * 1.5));
                tone += lamps * 0.65;

                half3 color = albedo * tone + _EmissionColor.rgb;

                // Print grain: fine speckle up close, and a faint, broad unevenness like paper.
                float distanceToEye = distance(_WorldSpaceCameraPos, input.positionWS);
                half fine = (ValueNoise(input.positionWS * 38.0) - 0.5) * 2.0;
                half broad = (ValueNoise(input.positionWS * 1.7) - 0.5) * 2.0;
                color *= 1.0 + fine * _Grain * saturate(1.5 - distanceToEye / 20.0) + broad * _Grain * 0.4;

                // The colour grade: more vivid and contrasty in the dream, flatter in the nightmare.
                half saturation = _DreamSaturation > 0 ? _DreamSaturation : 1.0;
                half contrast = _DreamContrast > 0 ? _DreamContrast : 1.0;
                color = max(0, lerp(Luminance(color).xxx, color, saturation));
                half3 graded = sqrt(color);
                graded = saturate((graded - 0.5) * contrast + 0.5);
                color = graded * graded + max(0, color - 1.0); // keep glows above white

                color = MixFog(color, inputData.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
                float2 uv : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

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

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
