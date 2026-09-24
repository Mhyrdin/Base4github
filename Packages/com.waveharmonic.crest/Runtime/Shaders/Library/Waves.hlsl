// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Common functions for sampling from the wave buffers.
// Do not use this to sample waves if you are a user - sample from Animated Waves.

#ifndef d_WaveHarmonic_Crest_Waves
#define d_WaveHarmonic_Crest_Waves

#if d_Bicubic
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Filtering.hlsl"
#define m_SampleWaveBuffers(u, r, i) Utility::SampleBicubicRepeat(_Crest_WaveBuffer, u, r, i)
#else
#define m_SampleWaveBuffers(u, r, i) _Crest_WaveBuffer.SampleLevel(sampler_Crest_Waves_linear_repeat, float3(u, i), 0.0)
#endif

Texture2DArray _Crest_WaveBuffer;
SamplerState sampler_Crest_Waves_linear_repeat;

m_CrestNameSpace

void QuantizeWaveDirection
(
    const float2 i_Axis,
    out float4 o_AxisXZ0,
    out float4 o_AxisXZ1,
    out float o_T
)
{
    // Quantize wave direction and interpolate waves.
    const float heading = atan2(i_Axis.y, i_Axis.x) + 2.0 * 3.141592654;
    const float theta = 0.5 * 0.314159265;
    const float rem = fmod(heading, theta);
    const float angle0 = heading - rem;
    const float angle1 = angle0 + theta;

    sincos(angle0, o_AxisXZ0.y, o_AxisXZ0.x);
    sincos(angle1, o_AxisXZ1.y, o_AxisXZ1.x);
    o_AxisXZ0.z = -o_AxisXZ0.y; o_AxisXZ0.w = o_AxisXZ0.x;
    o_AxisXZ1.z = -o_AxisXZ1.y; o_AxisXZ1.w = o_AxisXZ1.x;
    o_T = rem / theta;
}

float3 SampleWaveBuffers
(
    const float2 i_Position,
    const float4 i_Axis,
    const uint i_Slice,
    const uint2 i_Resolution
)
{
    // Sample displacement, rotate into frame.
    // Hardware bilinear produces small noise artifacts. Custom bilinear solves that,
    // but we still get texel artifacts that show up in normals, so use bicubic.
    const float2 uv = float2(dot(i_Position, i_Axis.xy), dot(i_Position, i_Axis.zw));
    float3 displacement = m_SampleWaveBuffers(uv, i_Resolution, i_Slice).xyz;
#if !d_QuantizeTexture
    displacement.xz = displacement.x * i_Axis + displacement.z * i_Axis.zw;
#endif
    return displacement;
}

float3 SampleWaves
(
    const float2 i_Position,
    const float2 i_Axis,
#if d_Quantize
    const float4 i_AxisXZ0,
    const float4 i_AxisXZ1,
    const float i_T,
#endif
    const uint i_Slice,
    const uint i_Resolution
)
{
#if !d_Quantize
    const float4 i_AxisXZ0 = float4(i_Axis, -i_Axis.y, i_Axis.x);
#endif

    const uint2 resolution = uint2(i_Resolution, i_Resolution);

    float3 displacement = SampleWaveBuffers(i_Position, i_AxisXZ0, i_Slice, resolution);

#if d_Quantize
    displacement = lerp(displacement, SampleWaveBuffers(i_Position, i_AxisXZ1, i_Slice, resolution), i_T);
#endif

#if d_QuantizeTexture
    // Texture applies the original axis instead of the quantized one, as it applies
    // axis length later.
    displacement.xz = displacement.x * i_Axis + displacement.z * float2(-i_Axis.y, i_Axis.x);
#endif

    return displacement;
}

#if d_Quantize
float3 SampleWaves
(
    const float2 i_Position,
    const float2 i_Axis,
    const uint i_Slice,
    const uint i_Resolution
)
{
    float4 axisXZ0; float4 axisXZ1; float t;
    QuantizeWaveDirection(i_Axis, axisXZ0, axisXZ1, t);

    return SampleWaves
    (
        i_Position,
        i_Axis,
        axisXZ0,
        axisXZ1,
        t,
        i_Slice,
        i_Resolution
    );
}
#endif

m_CrestNameSpaceEnd

#endif // d_WaveHarmonic_Crest_Waves
