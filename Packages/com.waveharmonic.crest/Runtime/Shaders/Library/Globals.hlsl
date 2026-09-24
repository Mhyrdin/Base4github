// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// GLOBALs - we're allowed to use these anywhere.

#ifndef CREST_WATER_GLOBALS_H
#define CREST_WATER_GLOBALS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"

// Try and use already defined samplers.
// Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl
// Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl
#if defined(SHADERCONFIG_CS_HLSL) && defined(UNITY_SHADER_VARIABLES_INCLUDED)
#define LODData_linear_clamp_sampler s_linear_clamp_sampler
#else
SamplerState LODData_linear_clamp_sampler;
#endif

CBUFFER_START(CrestPerFrame)
float3 g_Crest_WaterCenter;
float  g_Crest_WaterScale;
float  g_Crest_Time;
uint   g_Crest_LodCount;
uint   g_Crest_LodIndex;
int    g_Crest_LodChange;
float  g_Crest_MeshScaleLerp;
float  g_Crest_LodAlphaBlackPointFade;
float  g_Crest_LodAlphaBlackPointWhitePointFade;

float3 g_Crest_DataOffset;
float3 g_Crest_DataChange;

float4 g_Crest_Flow;

// Refraction
float  g_Crest_WaterDepthAtViewer;
float  g_Crest_MaximumVerticalDisplacement;
float2 g_Crest_HorizonNormal;

// Hack - due to SV_IsFrontFace occasionally coming through as true for
// backfaces, add a param here that forces water to be in undrwater state. I
// think the root cause here might be imprecision or numerical issues at water
// tile boundaries, although I'm not sure why cracks are not visible in this case.
int    g_Crest_ForceUnderwater;

float3 g_Crest_PrimaryLightDirection;
float3 g_Crest_PrimaryLightIntensity;
bool   g_Crest_PrimaryLightHasCookie;
bool   g_Crest_PrimaryLightFallback;

float g_Crest_DynamicSoftShadowsFactor;

bool g_Crest_SampleAbsorptionSimulation;
bool g_Crest_SampleScatteringSimulation;

// Motion Vector Parameters
float  g_Crest_WaterScaleChange;
float2 g_Crest_WaterCenterDelta;

// Shifting Origin
#if d_Crest_PackageShiftingOrigin
float3 g_Crest_ShiftingOriginOffset;
bool   g_Crest_ShiftingOriginWaveCompatibilityMode;
#endif

// Portals
#if d_Crest_PackagePortals
int    _Crest_Portal;
#endif


//
// LOD Data
//

// Cascade Data:                  Scale, Weight, MaximumWavelength, 0
float4 g_Crest_CascadeData[MAX_LOD_COUNT];
float4 g_Crest_CascadeDataSource[MAX_LOD_COUNT];

// Sampling Parameters:           LodCount, Resolution, OneOverResolution, 0
// Sampling Parameters (Cascade): SnappedPositionX, SnappedPositionZ, TexelWidth, 0
float4 g_Crest_SamplingParametersAbsorption;
float4 g_Crest_SamplingParametersCascadeAbsorption[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersAlbedo;
float4 g_Crest_SamplingParametersCascadeAlbedo[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersAnimatedWaves;
float4 g_Crest_SamplingParametersCascadeAnimatedWaves[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeAnimatedWavesSource[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersClip;
float4 g_Crest_SamplingParametersCascadeClip[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersDepth;
float4 g_Crest_SamplingParametersCascadeDepth[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersDynamicWaves;
float4 g_Crest_SamplingParametersCascadeDynamicWaves[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeDynamicWavesSource[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersFlow;
float4 g_Crest_SamplingParametersCascadeFlow[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersFoam;
float4 g_Crest_SamplingParametersCascadeFoam[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeFoamSource[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersLevel;
float4 g_Crest_SamplingParametersCascadeLevel[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersScattering;
float4 g_Crest_SamplingParametersCascadeScattering[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersShadow;
float4 g_Crest_SamplingParametersCascadeShadow[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeShadowSource[MAX_LOD_COUNT];

CBUFFER_END

#endif
