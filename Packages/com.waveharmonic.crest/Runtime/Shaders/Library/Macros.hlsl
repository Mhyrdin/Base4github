// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_MACROS_H
#define CREST_MACROS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"

#define m_CrestNameSpace namespace WaveHarmonic { namespace Crest {
#define m_CrestNameSpaceEnd } }

#define m_Crest WaveHarmonic::Crest

#define m_FloatMaximum 3.402823466e+38

#ifndef k_Crest_LodOffset
#define k_Crest_LodOffset 0
#endif

//
// Fallback for WebGPU
//

#ifdef SHADER_API_WEBGPU
#define m_Float2 float4
#define m_Float2Constructor(x, y) float4(x, y, 0.0, 0.0)
#define m_Float2FromFloat2(p0) float4(p0.x, p0.y, 0, 0)
#else
#define m_Float2 float2
#define m_Float2Constructor(x, y) float2(x, y)
#define m_Float2FromFloat2(p0) p0
#endif


#if d_Crest_FullPrecisionDisplacement
#define m_DisplacementTexture(texture, components) texture<float##components>
#else
#define m_DisplacementTexture(texture, components) texture
#endif

#define m_CrestVertex \
m_Crest::Varyings Vertex(m_Crest::Attributes i_Input) \
{ \
    return m_Crest::Vertex(i_Input); \
}

#define m_CrestFragment(type) \
type Fragment(m_Crest::Varyings i_Input) : SV_Target \
{ \
    return m_Crest::Fragment(i_Input); \
}

#define m_CrestFragmentVariant(type, name) \
type Fragment(m_Crest::Varyings i_Input) : SV_Target \
{ \
    return m_Crest::name(i_Input); \
}

#define m_CrestFragmentWithFrontFace(type) \
type Fragment(m_Crest::Varyings i_Input, const bool i_IsFrontFace : SV_IsFrontFace) : SV_Target \
{ \
    return m_Crest::Fragment(i_Input, i_IsFrontFace); \
}

#define m_CrestKernel(name) \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(id); \
}

#define m_CrestKernelVariant(name, variant) \
void Crest##name##variant(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(id); \
}

#define m_CrestKernelDefault(name) \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(id); \
}

#define m_CrestInputKernel(name) \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(uint3(id.xy, g_Crest_LodCount - 1 - id.z)); \
}

#define m_CrestInputKernelDefault(name) \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(uint3(id.xy, g_Crest_LodCount - 1 - id.z - k_Crest_LodOffset)); \
}

// Cross render pipeline kernels.
#define m_CrestKernelXRP(name) \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] void Crest##name##BRP(uint3 id : SV_DispatchThreadID) { m_Crest::name(id); } \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] void Crest##name##HRP(uint3 id : SV_DispatchThreadID) { m_Crest::name(id); } \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] void Crest##name##URP(uint3 id : SV_DispatchThreadID) { m_Crest::name(id); } \

#endif // CREST_MACROS_H
