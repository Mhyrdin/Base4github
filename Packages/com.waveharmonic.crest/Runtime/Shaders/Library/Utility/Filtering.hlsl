// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef d_WaveHarmonic_Utility_Filtering
#define d_WaveHarmonic_Utility_Filtering

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Macros.hlsl"

m_UtilityNameSpace

// NOTE: We have to roll our own bilinear filter in Compute shaders when
// reading from a RWTexture. The documentation below explains how SRV
// and UAV mappings of the same texture cannot exist at the same time.
// https://docs.microsoft.com/en-us/windows/desktop/direct3dhlsl/sm5-object-rwtexture2d
float4 SampleBilinear(const RWTexture2DArray<float4> i_Texture, const float2 i_UV, const uint i_Slice, const float i_Resolution)
{
    // Make relative to pixel centers.
    const float2 center = i_UV * i_Resolution - 0.5;

    // Compute integral (bottom left) and fractional parts
    // Prevent overflow from possible negative cast to unsigned.
    const uint maximum = (uint)i_Resolution - 1;
    const uint2 integral = max((float2)0.0, floor(center));
    const uint2 opposite = uint2(min(integral.x + 1, maximum), min(integral.y + 1, maximum));
    const float2 fractional = frac(center);

    // Clamp from below and above (desired?).
    const float4 bl = i_Texture[uint3(integral, i_Slice)];
    const float4 br = i_Texture[uint3(uint2(opposite.x, integral.y), i_Slice)];
    const float4 tl = i_Texture[uint3(uint2(integral.x, opposite.y), i_Slice)];
    const float4 tr = i_Texture[uint3(opposite, i_Slice)];

    return lerp
    (
        lerp(bl, br, fractional.x),
        lerp(tl, tr, fractional.x),
        fractional.y
    );
}

// Taken from:
// https://gist.github.com/TheRealMJP/c83b8c0f46b63f3a88a5986f4fa982b1
//
// The following code is licensed under the MIT license:
// https://gist.github.com/TheRealMJP/bc503b0b87b643d3505d41eab8b332ae
//
// Samples a texture with Catmull-Rom filtering, using 9 texture fetches instead of 16.
// See http://vec3.ca/bicubic-filtering-in-fewer-taps/ for more details
float4 SampleTextureCatmullRom(in Texture2D<float4> tex, in SamplerState linearSampler, in float2 uv, in float2 texSize)
{
    // We're going to sample a a 4x4 grid of texels surrounding the target UV coordinate. We'll do this by rounding
    // down the sample location to get the exact center of our "starting" texel. The starting texel will be at
    // location [1, 1] in the grid, where [0, 0] is the top left corner.
    float2 samplePos = uv * texSize;
    float2 texPos1 = floor(samplePos - 0.5f) + 0.5f;

    // Compute the fractional offset from our starting texel to our original sample location, which we'll
    // feed into the Catmull-Rom spline function to get our filter weights.
    float2 f = samplePos - texPos1;

    // Compute the Catmull-Rom weights using the fractional offset that we calculated earlier.
    // These equations are pre-expanded based on our knowledge of where the texels will be located,
    // which lets us avoid having to evaluate a piece-wise function.
    float2 w0 = f * (-0.5f + f * (1.0f - 0.5f * f));
    float2 w1 = 1.0f + f * f * (-2.5f + 1.5f * f);
    float2 w2 = f * (0.5f + f * (2.0f - 1.5f * f));
    float2 w3 = f * f * (-0.5f + 0.5f * f);

    // Work out weighting factors and sampling offsets that will let us use bilinear filtering to
    // simultaneously evaluate the middle 2 samples from the 4x4 grid.
    float2 w12 = w1 + w2;
    float2 offset12 = w2 / (w1 + w2);

    // Compute the final UV coordinates we'll use for sampling the texture
    float2 texPos0 = texPos1 - 1;
    float2 texPos3 = texPos1 + 2;
    float2 texPos12 = texPos1 + offset12;

    texPos0 /= texSize;
    texPos3 /= texSize;
    texPos12 /= texSize;

    float4 result = 0.0f;
    result += tex.SampleLevel(linearSampler, float2(texPos0.x, texPos0.y), 0.0f) * w0.x * w0.y;
    result += tex.SampleLevel(linearSampler, float2(texPos12.x, texPos0.y), 0.0f) * w12.x * w0.y;
    result += tex.SampleLevel(linearSampler, float2(texPos3.x, texPos0.y), 0.0f) * w3.x * w0.y;

    result += tex.SampleLevel(linearSampler, float2(texPos0.x, texPos12.y), 0.0f) * w0.x * w12.y;
    result += tex.SampleLevel(linearSampler, float2(texPos12.x, texPos12.y), 0.0f) * w12.x * w12.y;
    result += tex.SampleLevel(linearSampler, float2(texPos3.x, texPos12.y), 0.0f) * w3.x * w12.y;

    result += tex.SampleLevel(linearSampler, float2(texPos0.x, texPos3.y), 0.0f) * w0.x * w3.y;
    result += tex.SampleLevel(linearSampler, float2(texPos12.x, texPos3.y), 0.0f) * w12.x * w3.y;
    result += tex.SampleLevel(linearSampler, float2(texPos3.x, texPos3.y), 0.0f) * w3.x * w3.y;

    return result;
}

float4 CubicWeights(const float f)
{
    const float f2 = f * f;
    const float f3 = f2 * f;

    // Catmull–Rom (a = -0.5)
    return float4
    (
        -0.5 * f3 +       f2 - 0.5 * f,
         1.5 * f3 - 2.5 * f2 + 1.0,
        -1.5 * f3 + 2.0 * f2 + 0.5 * f,
         0.5 * f3 - 0.5 * f2
    );
}

float4 SampleBicubicRepeat
(
    const Texture2DArray<float4> i_Texture,
    const float2 i_UV,
    const uint2 i_Size,
    const uint i_Slice
)
{
    // Convert to texel space (centered at pixel centers).
    const float2 samplePosition = i_UV * (float2)i_Size - 0.5;
    const int2 texelPosition = (int2)floor(samplePosition);
    const float2 f = samplePosition - (float2)texelPosition;

    // Precompute weights.
    const float4 wx = CubicWeights(f.x);
    const float4 wy = CubicWeights(f.y);

    const uint2 size = i_Size - 1;

    const uint4 x = uint4
    (
        (texelPosition.x - 1) & size.x,
        (texelPosition.x + 0) & size.x,
        (texelPosition.x + 1) & size.x,
        (texelPosition.x + 2) & size.x
    );

    // Horizontal pass.
    float4 row[4];
    [unroll]
    for (int j = -1; j <= 2; ++j)
    {
        const int y = (texelPosition.y + j) & size.y;

        const float4 t0 = i_Texture[uint3(x.x, y, i_Slice)];
        const float4 t1 = i_Texture[uint3(x.y, y, i_Slice)];
        const float4 t2 = i_Texture[uint3(x.z, y, i_Slice)];
        const float4 t3 = i_Texture[uint3(x.w, y, i_Slice)];

        row[j + 1] = t0 * wx.x + t1 * wx.y + t2 * wx.z + t3 * wx.w;
    }

    // Vertical pass.
    return row[0] * wy.x + row[1] * wy.y + row[2] * wy.z + row[3] * wy.w;
}

m_UtilityNameSpaceEnd

#endif // d_WaveHarmonic_Utility_Filtering
