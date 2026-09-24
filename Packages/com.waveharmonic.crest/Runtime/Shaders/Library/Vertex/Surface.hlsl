// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// d_WaterLevelDisplacementOnly
// d_RequirePositionWS
// d_RequireUndisplacedXZ

#ifndef d_WaveHarmonic_Crest_LibraryVertexSurface
#define d_WaveHarmonic_Crest_LibraryVertexSurface

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Geometry.hlsl"

m_CrestNameSpace

struct Attributes
{
    float3 _PositionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 _PositionCS : SV_POSITION;
#if d_RequirePositionWS
    float3 _PositionWS : TEXCOORD;
#endif
#if d_RequireUndisplacedXZ
    float2 _UndispacedPositionXZ : TEXCOORD1;
#endif
#if d_RequireLodAlpha
    float _LodAlpha : TEXCOORD2;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vertex(const Attributes i_Input)
{
    // This will work for all pipelines.
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(i_Input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    const uint slice0 = _Crest_LodIndex;
    const uint slice1 = slice0 + 1;

    const Cascade cascade0 = Cascade::Make(slice0);
    const Cascade cascade1 = Cascade::Make(slice1);

    float3 positionWS = mul(UNITY_MATRIX_M, float4(i_Input._PositionOS, 1.0)).xyz;

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    positionWS.xz += _WorldSpaceCameraPos.xz;
#endif

    float alpha;
    SnapAndTransitionVertLayout(_Crest_ChunkMeshScaleAlpha, cascade0, _Crest_ChunkGeometryGridWidth, positionWS, alpha);

    {
        float2 center = UNITY_MATRIX_M._m03_m23;
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
        center += _WorldSpaceCameraPos.xz;
#endif
        PatchVerticeLayout(center, _WorldSpaceCameraPos.xz, positionWS.xz);
    }

    const float weight0 = (1.0 - alpha) * cascade0._Weight;
    const float weight1 = (1.0 - weight0) * cascade1._Weight;

    const float2 undisplaced = positionWS.xz;

    float4 displacement = 0.0;

    // Data that needs to be sampled at the undisplaced position.
    if (weight0 > m_CrestSampleLodThreshold)
    {
        Cascade::MakeAnimatedWaves(slice0).SampleMeshDisplacement(undisplaced, weight0, 1.0 - weight1, displacement);
    }
    if (weight1 > m_CrestSampleLodThreshold)
    {
        Cascade::MakeAnimatedWaves(slice1).SampleMeshDisplacement(undisplaced, weight1, weight1, displacement);
    }

#if !d_WaterLevelDisplacementOnly
    positionWS += displacement.xyz;
#endif

    positionWS.y += displacement.w;

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    positionWS.xz -= _WorldSpaceCameraPos.xz;
#endif

    output._PositionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));

#if d_RequirePositionWS
    output._PositionWS = positionWS;
#endif

#if d_RequireUndisplacedXZ
    output._UndispacedPositionXZ = undisplaced;
#endif

#if d_RequireLodAlpha
    output._LodAlpha = alpha;
#endif

    return output;
}

m_CrestNameSpaceEnd

#endif // d_WaveHarmonic_Crest_LibraryVertexSurface
