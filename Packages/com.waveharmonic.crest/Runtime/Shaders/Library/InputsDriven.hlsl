// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Anything marked as "Source" is from the previous frame.

#ifndef CREST_INPUTS_DRIVEN_H
#define CREST_INPUTS_DRIVEN_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"

// NOTE: Unity does not recognize uint in FrameDebugger. It will be under Floats
// with incorrect values. Change to int for debugging.
uint _Crest_LodIndex;


Texture2DArray g_Crest_CascadeAbsorption;
m_DisplacementTexture(Texture2DArray, 4) g_Crest_CascadeAnimatedWaves;
m_DisplacementTexture(Texture2DArray, 4) g_Crest_CascadeAnimatedWavesSource;
Texture2DArray g_Crest_CascadeDepth;
m_DisplacementTexture(Texture2DArray, 4) g_Crest_CascadeLevel;
Texture2DArray g_Crest_CascadeClip;
Texture2DArray g_Crest_CascadeFoam;
Texture2DArray g_Crest_CascadeFlow;
Texture2DArray g_Crest_CascadeDynamicWaves;
Texture2DArray g_Crest_CascadeScattering;
Texture2DArray g_Crest_CascadeShadow;
Texture2DArray g_Crest_CascadeAlbedo;

#endif // CREST_INPUTS_DRIVEN_H
