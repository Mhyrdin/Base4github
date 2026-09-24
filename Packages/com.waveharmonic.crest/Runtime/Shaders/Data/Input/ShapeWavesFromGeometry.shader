// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Generates waves from geometry that is rendered into the water simulation from a top down camera. Expects
// following data on verts:
//   - POSITION: Vert positions as normal.
//   - TEXCOORD0: Axis - direction for waves to travel. "Forward vector" for waves.
//   - TEXCOORD1: X - 0 at start of waves, 1 at end of waves
//
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ uv1.x = 0             |
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  |                    |  uv0 - wave direction vector
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  |                   \|/
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ uv1.x = 1
//  ------------------- shoreline --------------------
//

Shader "Crest/Inputs/Shape Waves/Add From Geometry"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)]
        _Crest_BlendModeSource("Source Blend Mode", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _Crest_BlendModeTarget("Target Blend Mode", Int) = 1

        [Toggle(d_Feather)]
        _Crest_Feather("Feather At UV Extents", Float) = 0
        _Crest_FeatherWidth("Feather Width", Range(0.001, 1)) = 0.1

        [Toggle(d_FeatherDirectional)]
        _Crest_FeatherDirectional("Feather Directional", Float) = 0
        // Controls ramp distance over which waves grow/fade as they move forwards
        _Crest_FeatherWaveStart( "Feather wave start (0-1)", Range( 0.0, 10 ) ) = 0.1

        [Toggle(d_Bicubic)]
        _Crest_Bicubic("Bicubic Sampling", Float) = 0

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    CGINCLUDE
    #pragma vertex Vertex
    #pragma fragment Fragment
    // #pragma enable_d3d11_debug_symbols

    #pragma shader_feature_local_fragment d_Feather
    #pragma shader_feature_local_fragment d_FeatherDirectional
    #pragma shader_feature_local_fragment d_Bicubic

    #define d_Quantize 1

    #include "UnityCG.cginc"

    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Waves.hlsl"

    CBUFFER_START(CrestPerWaterInput)
    float _Crest_RespectShallowWaterAttenuation;
    int _Crest_WaveBufferSliceIndex;
    float _Crest_AverageWavelength;
    float _Crest_AttenuationInShallows;
    float _Crest_Weight;
    float2 _Crest_AxisX;
    half _Crest_MaximumAttenuationDepth;
    half _Crest_FeatherWidth;
    half _Crest_FeatherWaveStart;
    uint _Crest_Resolution;
    CBUFFER_END

    m_CrestNameSpace

    struct Attributes
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 uv_slice : TEXCOORD1;
        float2 axis : TEXCOORD2;
        float3 worldPosScaled : TEXCOORD3;
        float2 worldPosXZ : TEXCOORD5;
    };

    Varyings Vertex(Attributes v)
    {
        Varyings o;

        const float3 positionOS = v.vertex.xyz;
        o.vertex = UnityObjectToClipPos(positionOS);
        const float3 worldPos = mul( unity_ObjectToWorld, float4(positionOS, 1.0) ).xyz;

        // UV coordinate into the cascade we are rendering into
        o.uv_slice = Cascade::MakeAnimatedWaves(_Crest_LodIndex).WorldToUV(worldPos.xz);

        o.worldPosXZ = worldPos.xz;

        o.uv = v.uv;

        // World pos prescaled by wave buffer size, suitable for using as UVs in fragment shader
        const float waveBufferSize = 0.5f * (1 << _Crest_WaveBufferSliceIndex);
        o.worldPosScaled = worldPos / waveBufferSize;

        // Rotate forward axis around y-axis into world space
        o.axis = unity_ObjectToWorld._m00_m20.xy;
        o.axis = _Crest_AxisX.x * o.axis + _Crest_AxisX.y * float2(-o.axis.y, o.axis.x);

        return o;
    }

    float4 Fragment(Varyings input)
    {
        float wt = _Crest_Weight;

#if d_FeatherDirectional
        // Feature at away from shore.
        wt *= saturate(input.uv.x / _Crest_FeatherWaveStart);
#endif

#if d_Feather
        wt *= FeatherWeightFromUV(input.uv, _Crest_FeatherWidth);
#endif

        float alpha = wt;

        // Attenuate if depth is less than half of the average wavelength
        const half depth = Cascade::MakeDepth(_Crest_LodIndex).SampleSignedDepthFromSeaLevel(input.worldPosXZ) +
            Cascade::MakeLevel(_Crest_LodIndex).SampleLevel(input.worldPosXZ);
        half depth_wt = saturate(2.0 * depth / _Crest_AverageWavelength);
        if (_Crest_MaximumAttenuationDepth < k_Crest_MaximumWaveAttenuationDepth)
        {
            depth_wt = lerp(depth_wt, 1.0, saturate(depth / _Crest_MaximumAttenuationDepth));
        }
        const float attenuationAmount = _Crest_AttenuationInShallows * _Crest_RespectShallowWaterAttenuation;
        wt *= attenuationAmount * depth_wt + (1.0 - attenuationAmount);

        const float3 displacement = SampleWaves
        (
            input.worldPosScaled.xz,
            input.axis,
            _Crest_WaveBufferSliceIndex,
            _Crest_Resolution
        );

        return float4(displacement * wt, alpha);
    }

    m_CrestNameSpaceEnd

    m_CrestVertex
    m_CrestFragment(float4)
    ENDCG

    SubShader
    {
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            // Either additive or alpha blend for geometry waves.
            Blend [_Crest_BlendModeSource] [_Crest_BlendModeTarget]
            CGPROGRAM
            ENDCG
        }

        Pass
        {
            // Subsequent draws need to be additive. We cannot change render state with command
            // buffer and changing on material is not aligned with command buffer usage.
            Blend One One
            CGPROGRAM
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
