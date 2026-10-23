// URP Lit-style shading with textures projected along three axes instead of read from UVs, so
// detail keeps its real-world size on stretched shapes (tall towers, flattened rocks, the ground).
// Projection happens in "scaled object space": textures keep their size whatever the object's
// scale, and travel with objects that move instead of swimming over them.
// Property names match URP Lit so materials can switch between the two without losing settings.
Shader "DreamStudio/TriplanarLit"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Detail (Albedo)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _WorldScale ("Tiles Per Meter", Float) = 0.5
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 4
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
            half _BumpScale;
            half _Smoothness;
            half _Metallic;
            float _WorldScale;
            half _BlendSharpness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);

        float3 ObjectScale()
        {
            return float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 triPos : TEXCOORD1;
                half3 triNormal : TEXCOORD2;
                float3 objScale : TEXCOORD3;
                half4 fogAndVertexLight : TEXCOORD4;
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
                output.triPos = input.positionOS.xyz * scale;
                output.triNormal = normalize(input.normalOS / max(scale, 1e-4));
                output.objScale = scale;

                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogAndVertexLight.x = ComputeFogFactor(position.positionCS.z);
                output.fogAndVertexLight.yzw = VertexLighting(position.positionWS, normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Blend weights from the surface normal; flip U on back-facing sides so the
                // texture (and its normal map) isn't mirrored there.
                half3 n = normalize(input.triNormal);
                half3 w = pow(abs(n), _BlendSharpness);
                w /= max(w.x + w.y + w.z, 1e-4);
                half3 axisSign = step(0, n) * 2 - 1;
                float3 p = input.triPos * _WorldScale;
                float2 uvX = float2(p.z * axisSign.x, p.y);
                float2 uvY = float2(p.x * axisSign.y, p.z);
                float2 uvZ = float2(-p.x * axisSign.z, p.y);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX) * w.x
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY) * w.y
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ) * w.z;

                half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 tY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                tX.x *= axisSign.x;
                tY.x *= axisSign.y;
                tZ.x *= -axisSign.z;

                // UDN blend: add each projection's tangent-space bend onto the surface normal.
                tX = half3(tX.xy + n.zy, n.x);
                tY = half3(tY.xy + n.xz, n.y);
                tZ = half3(tZ.xy + n.xy, n.z);
                half3 normalScaled = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);

                // Back from the scaled projection space to object space, then to world space.
                float3 normalWS = NormalizeNormalPerPixel(TransformObjectToWorldNormal(normalScaled * input.objScale));

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb * _BaseColor.rgb;
                surface.alpha = 1;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = _EmissionColor.rgb;
                surface.occlusion = 1;

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

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
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

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
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

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
