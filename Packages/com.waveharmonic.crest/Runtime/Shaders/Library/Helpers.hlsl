// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// LOD data - data, samplers and functions associated with LODs

#ifndef CREST_WATER_HELPERS_H
#define CREST_WATER_HELPERS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"

m_CrestNameSpace

#define m_Blend(type) \
    type Blend(const int i_Blend, const float i_Alpha, const float i_DeltaTime, const type i_Source, const type i_Target) \
    { \
        switch (i_Blend) \
        { \
            case m_CrestBlendMinimum: \
                return min(i_Target, i_Source * i_Alpha); \
            case m_CrestBlendMaximum: \
                return max(i_Target, i_Source * i_Alpha); \
            case m_CrestBlendAdditive: \
                return i_Target + i_Source * i_Alpha * i_DeltaTime; \
            case m_CrestBlendAlpha: \
                return lerp(i_Target, i_Source, i_Alpha); \
            case m_CrestBlendNone: \
            default: \
                return i_Source * i_Alpha; \
        } \
    } \

m_Blend(float)
m_Blend(float2)
m_Blend(float3)
m_Blend(float4)

// Enforces casting hygiene.
uint ComputeSlice(uint slice, int offset, uint maximum)
{
    // We cast to int since offset can be negative.
    // We must cast all parameters otherwise problems occur.
    return clamp((int)slice + offset, 0, (int)maximum);
}

float PositionToSliceNumber
(
    const float2 i_PositionXZ,
    const float i_MinimumSlice,
    const float i_MaximumSlice,
    const float i_WaterScale0
)
{
    const float2 offset = abs(i_PositionXZ - g_Crest_WaterCenter.xz);
    const float taxicab = max(offset.x, offset.y);
    const float radius0 = i_WaterScale0;
    const float slice = log2(max(taxicab / radius0, 1.0));
    return clamp(slice, i_MinimumSlice, i_MaximumSlice);
}

uint PositionToSliceIndex
(
    const float2 i_PositionXZ,
    const float i_MinimumSlice,
    const float i_WaterScale0
)
{
    // Don't use last slice - this is a "transition" slice used to cross fade waves
    // between LOD resolutions to avoid pops.
    const float slice = PositionToSliceNumber
    (
        i_PositionXZ,
        i_MinimumSlice,
        g_Crest_LodCount - 2,
        i_WaterScale0
    );

    return floor(slice);
}

void PositionToSliceIndices
(
    const float2 i_PositionXZ,
    const uint i_MinimumSlice,
    const uint i_MaximumSlice,
    const float i_WaterScale0,
    out uint o_Slice0,
    out uint o_Slice1,
    out float o_LodAlpha
)
{
    const float slice = PositionToSliceNumber
    (
        i_PositionXZ,
        i_MinimumSlice,
        i_MaximumSlice,
        i_WaterScale0
    );

    o_LodAlpha = frac(slice);

    // Fixes artefact with DX12 & Vulkan. Likely a compiler bug.
    // Sampling result appears to be all over the place.
    o_Slice0 = floor(slice) + 0.01;
    o_Slice1 = o_Slice0 + 1;

    // lod alpha is remapped to ensure patches weld together properly. patches can vary significantly in shape (with
    // strips added and removed), and this variance depends on the base density of the mesh, as this defines the strip width.
    // using .15 as black and .85 as white should work for base mesh density as low as 16.
    const float BLACK_POINT = 0.15, WHITE_POINT = 0.85;
    o_LodAlpha = saturate((o_LodAlpha - BLACK_POINT) / (WHITE_POINT - BLACK_POINT));

    if (o_Slice0 == 0)
    {
        // blend out lod0 when viewpoint gains altitude. we're using the global g_Crest_MeshScaleLerp so check for LOD0 is necessary
        o_LodAlpha = min(o_LodAlpha + g_Crest_MeshScaleLerp, 1.0);
    }
}

// Use this when rendering a quad as the surface.
void MeshPositionToSliceIndices
(
    const float2 i_PositionXZ,
    const float i_MinimumSlice,
    const float i_MaximumSlice,
    const float i_WaterScale0,
    out uint o_Slice0,
    out uint o_Slice1,
    out float o_LodAlpha
)
{
    float slice = PositionToSliceNumber
    (
        i_PositionXZ,
        i_MinimumSlice,
        i_MaximumSlice + 1,
        i_WaterScale0
    );

    o_LodAlpha = frac(slice);

    uint extent = floor(slice);

    slice = min(slice, i_MaximumSlice);

    // Fixes artefact with DX12 & Vulkan. Likely a compiler bug.
    // Sampling result appears to be all over the place.
    o_Slice0 = floor(slice) + 0.01;
    o_Slice1 = o_Slice0 + 1;

    // lod alpha is remapped to ensure patches weld together properly. patches can vary significantly in shape (with
    // strips added and removed), and this variance depends on the base density of the mesh, as this defines the strip width.
    // using .15 as black and .85 as white should work for base mesh density as low as 16.
    const float BLACK_POINT = 0.15, WHITE_POINT = 0.85;
    o_LodAlpha = saturate((o_LodAlpha - BLACK_POINT) / (WHITE_POINT - BLACK_POINT));

    if (o_Slice0 == 0)
    {
        // blend out lod0 when viewpoint gains altitude. we're using the global g_Crest_MeshScaleLerp so check for LOD0 is necessary
        o_LodAlpha = min(o_LodAlpha + g_Crest_MeshScaleLerp, 1.0);
    }

    // Matches mesh solution.
    // Comparing to maxSlice + 1 can make any maxSlice work, but no point.
    if (extent == g_Crest_LodCount)
    {
        o_LodAlpha = 1.0;
    }
}

bool IsUnderWater(const bool i_FrontFace, const int i_ForceUnderwater)
{
    bool underwater = false;

    // We are well below water.
    if (i_ForceUnderwater == 1)
    {
        underwater = true;
    }
    // We are well above water.
    else if (i_ForceUnderwater == 2)
    {
        underwater = false;
    }
    // Use facing.
    else
    {
        underwater = !i_FrontFace;
    }

    return underwater;
}

float FeatherWeightFromUV(const float2 i_uv, const half i_featherWidth)
{
    const float2 offset = abs(i_uv - 0.5);
    const float largest = max(offset.x, offset.y);

    // Early exit (also handles zero feather).
    if (largest > 0.5)
    {
        return 0.0;
    }
    else
    {
        float r_l1 = max(offset.x, offset.y) - (0.5 - i_featherWidth);
        if (i_featherWidth > 0.0) r_l1 /= i_featherWidth;
        float weight = saturate(1.0 - r_l1);
        return weight;
    }
}

bool WithinUV(const float2 i_UV)
{
    const float2 d = abs(i_UV - 0.5);
    return max(d.x, d.y) <= 0.5;
}

m_CrestNameSpaceEnd

#endif // CREST_WATER_HELPERS_H
