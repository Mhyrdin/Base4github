// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef d_WaveHarmonic_Crest_SurfaceFog
#define d_WaveHarmonic_Crest_SurfaceFog

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"

#define d_Crest_WaterSurface 1

#if !d_Crest_LegacyUnderwater
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Graph/IntegrateWaterVolume.hlsl"
#endif

m_CrestNameSpace

#if d_Crest_LegacyUnderwater
static bool s_IsUnderWater;
#endif

void SetUpFog(bool i_Underwater, float3 i_PositionWS, float i_Multiplier, float i_FogDistance, half3 i_ViewWS, float2 i_PositionSS)
{
    s_IsUnderWater = i_Underwater;

    // XR does not like early returns in URP.
#if !defined(STEREO_INSTANCING_ON) && !defined(STEREO_MULTIVIEW_ON)
    if (!s_IsUnderWater)
    {
        return;
    }
#endif

#if !d_Crest_LegacyUnderwater
    ApplyUnderwaterEffect
    (
        0,                  // Not used (color)
        0,                  // TIR only
        0,                  // Caustics only
        i_FogDistance,
        i_ViewWS,
        i_PositionSS,
        i_PositionWS,
        false,              // No caustics
        true,               // TODO: implement
        false,              // Do not apply lighting
        1.0,                // TODO: implement
        s_VolumeOpacity,
        s_VolumeLighting
    );
#endif
}

m_CrestNameSpaceEnd

#if d_Crest_LegacyUnderwater
#if d_Crest_DiscardAtmosphericScattering

#if CREST_BIRP
#ifdef UNITY_PASS_FORWARDADD
#define m_Unity_FogColor fixed4(0, 0, 0, 0)
#else
#define m_Unity_FogColor unity_FogColor
#endif // UNITY_PASS_FORWARDADD

#undef UNITY_APPLY_FOG
#define UNITY_APPLY_FOG(coord, color) \
if (!m_Crest::s_IsUnderWater) \
{ \
    UNITY_APPLY_FOG_COLOR(coord, color, m_Unity_FogColor); \
}
#endif // CREST_BIRP

#if CREST_HDRP
#define EvaluateAtmosphericScattering(i, V, color) m_Crest::s_IsUnderWater ? color : EvaluateAtmosphericScattering(i, V, color)
#endif

#if CREST_URP
#if UNITY_VERSION < 60070000
#define MixFog(color, coord) m_Crest::s_IsUnderWater ? color : MixFog(color, coord)
#else
#define BlendDistanceFog(color, coord) m_Crest::s_IsUnderWater ? color : BlendDistanceFog(color, coord)
#endif
#endif

#endif // CREST_DISCARD_ATMOSPHERIC_SCATTERING
#endif // CREST_LEGACY_UNDERWATER

#endif // d_WaveHarmonic_Crest_SurfaceFog
