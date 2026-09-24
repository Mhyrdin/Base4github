// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef d_WaveHarmonic_Crest_Settings
#define d_WaveHarmonic_Crest_Settings

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.hlsl"

// Packages
#define d_Crest_PackageHDRP (CREST_PACKAGE_HDRP != 0)
#define d_Crest_PackageURP (CREST_PACKAGE_URP != 0)
#define d_Crest_PackagePortals (CREST_PORTALS != 0)
#define d_Crest_PackageShiftingOrigin (CREST_SHIFTING_ORIGIN != 0)

// Platforms
#define d_Crest_PlatformStandalone (CREST_PLATFORM_STANDALONE != 0)
#define d_Crest_PlatformServer (CREST_PLATFORM_SERVER != 0)
#define d_Crest_PlatformAndroid (CREST_PLATFORM_ANDROID != 0)
#define d_Crest_PlatformIOS (CREST_PLATFORM_IOS != 0)
#define d_Crest_PlatformWeb (CREST_PLATFORM_WEB != 0)
#define d_Crest_PlatformTVOS (CREST_PLATFORM_TVOS != 0)
#define d_Crest_PlatformVisionOS (CREST_PLATFORM_VISIONOS != 0)

// Settings
#define d_Crest_FullPrecisionDisplacement (CREST_FULL_PRECISION_DISPLACEMENT != 0)
#define d_Crest_DiscardAtmosphericScattering (CREST_DISCARD_ATMOSPHERIC_SCATTERING != 0)
#define d_Crest_LegacyUnderwater (CREST_LEGACY_UNDERWATER != 0)

#if   d_Crest_PlatformStandalone
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.Standalone.hlsl"
#elif d_Crest_PlatformServer
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.Server.hlsl"
#elif d_Crest_PlatformAndroid
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.Android.hlsl"
#elif d_Crest_PlatformIOS
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.iOS.hlsl"
#elif d_Crest_PlatformWeb
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.Web.hlsl"
#elif d_Crest_PlatformTVOS
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.tvOS.hlsl"
#elif d_Crest_PlatformVisionOS
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.visionOS.hlsl"
#else
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings/Settings.Crest.Default.hlsl"
#endif

#endif // d_WaveHarmonic_Crest_Settings
