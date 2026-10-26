// Drawn on a small inside-out sphere around the camera, like a lens over the player's eyes.
// The first pass multiplies the view by a tint that darkens toward the edges (vignette);
// the second adds a soft glow at the edges. Neither touches the world itself.
Shader "DreamStudio/VisionFilter"
{
    Properties
    {
        _Tint ("Center Tint (multiply)", Color) = (1, 1, 1, 1)
        _EdgeTint ("Edge Tint (multiply)", Color) = (1, 1, 1, 1)
        _Glow ("Edge Glow (additive)", Color) = (0, 0, 0, 0)
        _VignetteStart ("Vignette Start", Range(0, 2)) = 0.05
        _VignetteEnd ("Vignette End", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+400" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Front

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half4 _EdgeTint;
            half4 _Glow;
            half _VignetteStart;
            half _VignetteEnd;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 directionOS : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.directionOS = input.positionOS.xyz;
            return output;
        }

        // 0 straight ahead, rising toward 1 at the edges of the view. The sphere is parented
        // to the head, so object-space +Z is where the player is looking.
        half Vignette(float3 directionOS)
        {
            half offAxis = 1.0 - normalize(directionOS).z;
            return smoothstep(_VignetteStart, _VignetteEnd, offAxis);
        }
        ENDHLSL

        Pass
        {
            Name "Tint"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            half4 frag(Varyings input) : SV_Target
            {
                return half4(lerp(_Tint.rgb, _EdgeTint.rgb, Vignette(input.directionOS)), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_Glow.rgb * _Glow.a * Vignette(input.directionOS), 0.0);
            }
            ENDHLSL
        }
    }
}
