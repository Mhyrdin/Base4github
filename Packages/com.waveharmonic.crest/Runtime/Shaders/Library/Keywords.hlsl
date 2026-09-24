// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef d_WaveHarmonic_Crest_Keywords
#define d_WaveHarmonic_Crest_Keywords

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"

#define d_Crest_AlbedoLod CREST_ALBEDO_SIMULATION
#define d_Crest_FlowLod defined(_CREST_FLOW_LOD) || defined(CREST_FLOW_ON_INTERNAL)
#define d_Crest_ShadowLod CREST_SHADOW_SIMULATION
#define d_Crest_AbsorptionLod CREST_ABSORPTION_SIMULATION
#define d_Crest_ScatteringLod CREST_SCATTERING_SIMULATION

#define d_Crest_OutScattering CREST_OUT_SCATTERING

#endif
