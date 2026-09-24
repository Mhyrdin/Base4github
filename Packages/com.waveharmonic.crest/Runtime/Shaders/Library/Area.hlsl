// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#include "HLSLSupport.cginc"

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"

#ifndef m_AreaTargetType
#define m_AreaTargetType m_AreaType
#endif

#ifndef m_Result
#define m_Result
#endif

RWTexture2DArray<m_AreaTargetType> _Crest_Target;

CBUFFER_START(CrestPerMaterial)
int _Crest_Blend;
float _Crest_Weight;
float _Crest_FeatherWidth;
float2 _Crest_TextureSize;
float2 _Crest_TexturePosition;
float2 _Crest_TextureRotation;
float2 _Crest_TextureResolution;
bool  _Crest_TargetRegion;
uint2 _Crest_TargetRegionSize;
m_AreaType _Crest_Value;
#ifdef d_Persistent
float _Crest_SimDeltaTime;
#endif
CBUFFER_END

#ifdef d_Persistent
#define m_DeltaTime _Crest_SimDeltaTime
#else
#define m_DeltaTime 1.0
#endif

m_CrestNameSpace

void Execute(const uint3 i_ID, const Cascade i_Cascade)
{
    uint3 id = i_ID;

    if (_Crest_TargetRegion)
    {
        id.xy = i_Cascade.InputIDToLodID(id.xy, _Crest_TargetRegionSize, _Crest_TexturePosition);
    }

    const float2 uv = DataIDToInputUV(id.xy, i_Cascade, _Crest_TexturePosition, _Crest_TextureRotation, _Crest_TextureSize);

    const float weight = FeatherWeightFromUV(uv, _Crest_FeatherWidth) * _Crest_Weight;

    if (weight <= 0.0)
    {
        return;
    }

    const m_AreaType result = Blend(_Crest_Blend, weight, m_DeltaTime, _Crest_Value, _Crest_Target[id]);
    _Crest_Target[id] = m_Result(result);
}

m_CrestNameSpaceEnd
