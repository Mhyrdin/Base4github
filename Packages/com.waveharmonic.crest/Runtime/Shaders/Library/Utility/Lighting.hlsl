// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Based on tutorial: https://connect.unity.com/p/adding-your-own-hlsl-code-to-shader-graph-the-custom-function-node

#ifndef CREST_LIGHTING_H
#define CREST_LIGHTING_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"

#if CREST_URP
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Unity renamed keyword.
#ifdef USE_FORWARD_PLUS
#define USE_CLUSTER_LIGHT_LOOP USE_FORWARD_PLUS
#endif // USE_FORWARD_PLUS

#ifdef FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
#define CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
#endif // FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

#endif // CREST_URP

#if CREST_HDRP_FORWARD_PASS
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

#if UNITY_VERSION < 60000000
#define GetMeshRenderingLayerMask GetMeshRenderingLightLayer
#endif // UNITY_VERSION

#if d_Crest_AdditionalLights
#if d_Crest_WaterSurface
// Causes rendering issues at a distance with foreground objects.
#ifndef LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER 1
#define d_Crest_LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#endif // LIGHTLOOP_DISABLE_TILE_AND_CLUSTER

m_CrestNameSpace

// Adapted from: com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl
half3 GetPunctualLights(float3 i_PositionWS, float2 i_PositionSS, const float4 i_ScreenPosition, const half3 i_Normal)
{
    half3 color = 0.0;

    BuiltinData builtinData;
    ZERO_INITIALIZE(BuiltinData, builtinData);

    LightLoopContext context;
    context.sampleReflection  = 0;
    context.shadowContext     = InitShadowContext();
    context.contactShadow     = 0;
    context.contactShadowFade = 0.0;
    context.shadowValue       = 1;
#if UNITY_VERSION < 60000000
    context.splineVisibility  = -1;
#endif
#ifdef APPLY_FOG_ON_SKY_REFLECTIONS
    context.positionWS        = i_PositionWS;
#endif

    float3 positionWS = GetCameraRelativePositionWS(i_PositionWS);
    ApplyCameraRelativeXR(positionWS);

    PositionInputs posInput;
    ZERO_INITIALIZE(PositionInputs, posInput);

    posInput.tileCoord = uint2(i_PositionSS) / GetTileSize();
    posInput.positionWS = positionWS;
    posInput.positionSS = i_PositionSS;
    posInput.positionNDC = i_ScreenPosition.xy / i_ScreenPosition.w;

    const uint renderingLayers = GetMeshRenderingLayerMask();

    uint lightCount, lightStart;

#ifndef LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
    GetCountAndStart(posInput, LIGHTCATEGORY_PUNCTUAL, lightStart, lightCount);
#else   // LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
    lightCount = _PunctualLightCount;
    lightStart = 0;
#endif

    bool fastPath = false;
#if SCALARIZE_LIGHT_LOOP
    uint lightStartLane0;
    fastPath = IsFastPath(lightStart, lightStartLane0);

    if (fastPath)
    {
        lightStart = lightStartLane0;
    }
#endif

    // Scalarized loop. All lights that are in a tile/cluster touched by any pixel in the wave are loaded (scalar load), only the one relevant to current thread/pixel are processed.
    // For clarity, the following code will follow the convention: variables starting with s_ are meant to be wave uniform (meant for scalar register),
    // v_ are variables that might have different value for each thread in the wave (meant for vector registers).
    // This will perform more loads than it is supposed to, however, the benefits should offset the downside, especially given that light data accessed should be largely coherent.
    // Note that the above is valid only if wave intriniscs are supported.
    uint v_lightListOffset = 0;
    uint v_lightIdx = lightStart;

#if NEED_TO_CHECK_HELPER_LANE
    // On some platform helper lanes don't behave as we'd expect, therefore we prevent them from entering the loop altogether.
    // IMPORTANT! This has implications if ddx/ddy is used on results derived from lighting, however given Lightloop is called in compute we should be
    // sure it will not happen.
    bool isHelperLane = WaveIsHelperLane();
    while (!isHelperLane && v_lightListOffset < lightCount)
#else
    while (v_lightListOffset < lightCount)
#endif
    {
        // Causes flickering. Manually increment below.
        // v_lightIdx = FetchIndex(lightStart, v_lightListOffset);

#if SCALARIZE_LIGHT_LOOP
        uint s_lightIdx = ScalarizeElementIndex(v_lightIdx, fastPath);
#else
        uint s_lightIdx = v_lightIdx;
#endif
        if (s_lightIdx == -1)
        {
            break;
        }

        LightData s_light = FetchLight(s_lightIdx);

        // If current scalar and vector light index match, we process the light. The v_lightListOffset for current thread is increased.
        // Note that the following should really be ==, however, since helper lanes are not considered by WaveActiveMin, such helper lanes could
        // end up with a unique v_lightIdx value that is smaller than s_lightIdx hence being stuck in a loop. All the active lanes will not have this problem.
        if (s_lightIdx >= v_lightIdx)
        {
            v_lightListOffset++;
            // Manually increment.
            v_lightIdx += 1;

            if (IsMatchingLightLayer(s_light.lightLayers, renderingLayers))
            {
                float3 L; float4 distances; // {d, d^2, 1/d, d_proj}
                GetPunctualLightVectors(positionWS, s_light, L, distances);

                // Is it worth evaluating the light?
                if (s_light.lightDimmer > 0)
                {
                    float4 lightColor = EvaluateLight_Punctual(context, posInput, s_light, L, distances);
                    lightColor.rgb *= lightColor.a * max(k_Crest_AdditionalLightLerp, dot(i_Normal, L)); // Composite

                    SHADOW_TYPE shadow = EvaluateShadow_Punctual(context, posInput, s_light, builtinData, i_Normal, L, distances);

                    lightColor.rgb *= ComputeShadowColor(shadow, s_light.shadowTint, s_light.penumbraTint);

                    color += lightColor.rgb;
                }
            }
        }
    }

    return color;
}

m_CrestNameSpaceEnd

#ifdef d_Crest_LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#undef LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#endif
#endif // d_Crest_WaterSurface
#endif // d_Crest_AdditionalLights

#if UNITY_VERSION < 60000000
#if PROBE_VOLUMES_L1
// URP sets this to zero.
#define AMBIENT_PROBE_BUFFER 1
#endif // PROBE_VOLUMES_L1
#endif // UNITY_VERSION
#endif // CREST_HDRP_FORWARD_PASS

m_CrestNameSpace

void PrimaryLight
(
    const float3 i_PositionWS,
    out half3 o_Color,
    out half3 o_Direction
)
{
    o_Direction = half3(0.0, 1.0, 0.0);
    o_Color = 0.0;

#ifndef d_IsAdditionalLight
    if (g_Crest_PrimaryLightFallback)
    {
        o_Direction = g_Crest_PrimaryLightDirection;
        o_Color = g_Crest_PrimaryLightIntensity;
        return;
    }
#endif

#if CREST_URP
    // Actual light data from the pipeline.
    Light light = GetMainLight();
    o_Direction = light.direction;
    o_Color = light.color;
#elif CREST_BIRP
#ifndef USING_DIRECTIONAL_LIGHT
    // Yes. This function wants the world position of the surface.
    o_Direction = UnityWorldSpaceLightDir(i_PositionWS);
    // Prevents divide by zero.
    if (all(o_Direction == 0)) o_Direction = half3(0.0, 1.0, 0.0);
    o_Direction = normalize(o_Direction);
#else
    o_Direction = _WorldSpaceLightPos0.xyz;
    // Prevents divide by zero.
    if (all(o_Direction == 0)) o_Direction = half3(0.0, 1.0, 0.0);
#endif
    o_Color = _LightColor0.rgb;
#if SHADERPASS == SHADERPASS_FORWARD_ADD
#if !SHADOWS_SCREEN
    // FIXME: undeclared identifier 'IN' in Pass: BuiltIn ForwardAdd, Vertex program with DIRECTIONAL SHADOWS_SCREEN
    UNITY_LIGHT_ATTENUATION(attenuation, IN, i_PositionWS)
    o_Color *= attenuation;
#endif
#endif
#endif
}

half3 AmbientLight(const half3 i_AmbientLight)
{
    half3 ambient = i_AmbientLight;

#ifndef SHADERGRAPH_PREVIEW
#if CREST_HDRP_FORWARD_PASS
    // Allows control of baked lighting through volume framework.
    // We could create a BuiltinData struct which would have rendering layers on it, but it seems more complicated.
    ambient *= GetIndirectDiffuseMultiplier(GetMeshRenderingLayerMask());
#endif // CREST_HDRP
#endif // SHADERGRAPH_PREVIEW

    return ambient;
}

half3 AmbientLight()
{
    // Use the constant term (0th order) of SH stuff - this is the average.
    const half3 ambient =
#if AMBIENT_PROBE_BUFFER
        half3(_AmbientProbeData[0].w, _AmbientProbeData[1].w, _AmbientProbeData[2].w);
#else
        half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
#endif

    return AmbientLight(ambient);
}

#if d_Crest_AdditionalLights
#if d_Crest_WaterSurface
half3 AdditionalLighting(const float3 i_PositionWS, const float4 i_ScreenPosition, const float2 i_PositionSS, const half3 i_Normal)
{
    half3 color = 0.0;

#if CREST_URP
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = i_ScreenPosition.xy / i_ScreenPosition.w;
    inputData.positionWS = i_PositionWS;

    const half4 shadowMask = CalculateShadowMask(inputData);

    // No AO, but we need the struct.
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 0.0);

    uint pixelLightCount = GetAdditionalLightsCount();

#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
#endif

LIGHT_LOOP_BEGIN(pixelLightCount)
    // Includes shadows and cookies.
    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
    {
        color += light.color * max(k_Crest_AdditionalLightLerp, dot(i_Normal, light.direction)) * (light.distanceAttenuation * light.shadowAttenuation);
    }
LIGHT_LOOP_END

#if USE_CLUSTER_LIGHT_LOOP
    // Additional directional lights.
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            color += light.color * (light.distanceAttenuation * light.shadowAttenuation);
        }
    }
#endif // USE_CLUSTER_LIGHT_LOOP
#endif // _ADDITIONAL_LIGHTS
#endif // CREST_URP

#if CREST_HDRP_FORWARD_PASS
    color = GetPunctualLights(i_PositionWS, i_PositionSS, i_ScreenPosition, i_Normal);
#endif

    // BIRP has additional lights as additional passes. Handled elsewhere.

    return color;
}
#endif // d_Crest_WaterSurface
#endif // d_Crest_AdditionalLights

m_CrestNameSpaceEnd

#endif
