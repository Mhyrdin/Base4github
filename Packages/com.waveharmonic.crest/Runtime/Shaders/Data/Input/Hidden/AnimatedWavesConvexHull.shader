// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Hidden/Crest/Inputs/Animated Waves/Convex Hull"
{
    SubShader
    {
        ZTest Always
        ZWrite Off

        Pass
        {
            BlendOp Min
            Cull Front
            ColorMask G

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_Weight;
            CBUFFER_END

            Texture2DArray _Crest_CascadeAnimatedWaves;

            m_CrestNameSpace

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings o;

                o.positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                // Displace sample to make it accurate.
                o.positionWS.xz -= Cascade::MakeAnimatedWaves(_Crest_LodIndex).Sample(_Crest_CascadeAnimatedWaves, o.positionWS.xz).xz;
                o.positionCS = mul(UNITY_MATRIX_VP, float4(o.positionWS, 1.0));

                return o;
            }

            half4 Fragment(Varyings input)
            {
                // Write displacement to get from sea level of water to the y value of this geometry.
                half seaLevelOffset = Cascade::MakeLevel(_Crest_LodIndex).SampleLevel(input.positionWS.xz);
                return half4(0.0, _Crest_Weight * (input.positionWS.y - g_Crest_WaterCenter.y - seaLevelOffset), 0.0, 0.0);
            }

            m_CrestNameSpaceEnd

            m_CrestVertex
            m_CrestFragment(half4)

            ENDCG
        }
    }
}
