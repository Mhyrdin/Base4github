// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Renders the underwater effect.
    /// </summary>
    [System.Serializable]
    public sealed partial class UnderwaterRenderer
    {
        internal const float k_CullLimitMinimum = 0.000001f;
        internal const float k_CullLimitMaximum = 0.01f;

        [@Space(10)]

        [Tooltip("Whether the underwater effect is enabled.\n\nAllocates/releases resources if state has changed.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _Enabled = true;

        // This is mainly for reflection probes (HDRP planar specifically). It gives
        // developers the option to make a TIR probe which should not render the surface.
        [Tooltip("Any camera or probe with this layer in its culling mask will render underwater.")]
        [@Layer]
        [@GenerateAPI]
        [SerializeField]
        int _Layer = 4; // Water

        [Tooltip("The underwater material. The water surface material is copied into this material.")]
        [@AttachMaterialEditor(order: 2)]
        [@MaterialField(k_ShaderNameEffect, name: "Underwater", title: "Create Underwater Material")]
        [@GenerateAPI]
        [SerializeField]
        internal Material _Material;


        [@Heading("Environmental Lighting")]

        [@Label("Enable")]
        [Tooltip("Provides out-scattering based on the camera's underwater depth.\n\nIt scales down environmental lighting (sun, reflections, ambient etc) with the underwater depth. This works with vanilla lighting, but uncommon or custom lighting will require a custom solution (use this for reference)")]
        [@GenerateAPI(Setter.Custom, name: "AffectsEnvironmentalLighting")]
        [@DecoratedField, SerializeField]
        internal bool _EnvironmentalLightingEnable;

        [@Label("Weight")]
        [Tooltip("How much this effect applies.\n\nValues less than 1 attenuate light less underwater. Value of 1 is physically based.")]
        [@Range(0, 3)]
        [@GenerateAPI]
        [SerializeField]
        internal float _EnvironmentalLightingWeight = 1f;

#if d_UnitySRP
        [@Label("Volume")]
        [Tooltip("This profile will be weighed in the deeper underwater the camera goes.")]
        [@Show(RenderPipeline.HighDefinition)]
        [@DecoratedField, SerializeField]
        VolumeProfile _EnvironmentalLightingVolumeProfile = null;

        Volume _EnvironmentalLightingVolume;
#endif


        [@Heading("Advanced")]

        [Tooltip("Rules to exclude cameras from rendering underwater.\n\nThese are exclusion rules, so for all cameras, select Nothing. These rules are applied on top of the Layer rules.")]
        [@DecoratedField]
        [@GenerateAPI]
        [SerializeField]
        internal WaterCameraExclusion _CameraExclusions = WaterCameraExclusion.Hidden | WaterCameraExclusion.Reflection;

        [Tooltip("Copying parameters each frame ensures underwater appearance stays consistent with the water surface.\n\nHas a small overhead so should be disabled if not needed.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _CopyWaterMaterialParametersEachFrame = true;

#if !d_Crest_LegacyUnderwater
        [@HideInInspector]
#endif
        [Tooltip("Adjusts the far plane for horizon line calculation. Helps with horizon line issue.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _FarPlaneMultiplier = 0.68f;

        [Tooltip("Whether to enable culling of water chunks when below water.")]
        [@GenerateAPI]
        [@InlineToggle]
        [@SerializeField]
        bool _EnableChunkCulling = true;

        [Tooltip("Proportion of visibility below which the water surface will be culled when underwater.\n\nThe larger the number, the closer to the camera the water tiles will be culled.")]
        [@Enable(nameof(_EnableChunkCulling))]
        [@Range(k_CullLimitMinimum, k_CullLimitMaximum)]
        [@GenerateAPI]
        [SerializeField]
        internal float _CullLimit = 0.001f;

        [@Space(10)]

        [@DecoratedField, SerializeField]
        DebugFields _Debug = new();

        [System.Serializable]
        sealed class DebugFields
        {
            [SerializeField]
            internal bool _VisualizeMask;

#if !d_Crest_LegacyUnderwater
            [@HideInInspector]
#endif
            [SerializeField]
            internal bool _DisableMask;

            [SerializeField]
            internal bool _VisualizeStencil;

            [SerializeField]
            internal bool _DisableHeightAboveWaterOptimization;

#if !d_Crest_LegacyUnderwater
            [@HideInInspector]
#endif
            [SerializeField]
            internal bool _DisableArtifactCorrection;

            [SerializeField]
            internal bool _OnlyReflectionCameras;
        }

        /// <summary>
        /// Raised after copying the water material properties to the underwater material.
        /// </summary>
        public static System.Action<WaterRenderer, Material> AfterCopyMaterial { get; set; }

        // Always render before surface, unless legacy mode which always renders after transparency.
#if d_Crest_LegacyUnderwater
        internal bool UseLegacyMask => true;
        internal bool RenderBeforeTransparency => false;
#else
        // Legacy mask works except for negative volumes. Not officially supported.
        internal bool UseLegacyMask => false;
        internal bool RenderBeforeTransparency => true;
#endif

        internal WaterRenderer _Water;

#if d_CrestPortals
        // BUG: NonSerialized as Unity shows a serialization depth warning even though field is internal.
        [System.NonSerialized]
        internal Portals.PortalRenderer _Portals;
#endif
        bool Portaled => _Water._ActiveModules.HasFlag(WaterRenderer.ActiveModules.Portal);


        int _MaterialLastUpdatedFrame = -1;


        //
        // Camera Loop Flags
        //

        internal bool UseStencilBuffer { get; set; }

        // Force the full-screen mask for Portals that need it.
        internal bool RequiresFullScreenMask { get; set; }

        // Requested the temporary color texture.
        internal bool NeedsColorTexture { get; set; }


        // These are the materials we actually use, overridable by Water Body.
        Material _SurfaceMaterial;
        Material _VolumeMaterial;

        WaterBody _CurrentWaterBody;
        bool _WaterBodyCulled;

        float _MinimumFogDensity;
        Vector3 _Extinction;

        readonly SampleCollisionHelper _SamplingHeightHelper = new();
        float _ViewerWaterHeight;

        internal static partial class ShaderIDs
        {
            // Empty.
        }

        internal void OnEnable()
        {
            _VolumeMaterial = _Material;

            if (_MaskMaterial == null)
            {
                _MaskMaterial = new(WaterResources.Instance.Shaders._UnderwaterMask);
            }

            if (_HorizonMaskMaterial == null)
            {
                _HorizonMaskMaterial = new(WaterResources.Instance.Shaders._HorizonMask);
            }

            if (_ArtifactsShader == null)
            {
                _ArtifactsShader = WaterResources.Instance.Compute._UnderwaterArtifacts;
            }

            OnEnableMask();

            if (RenderPipelineHelper.IsUniversal)
            {
#if d_UnityURP
                UnderwaterEffectPassURP.Enable(this);
#endif
            }
            else if (RenderPipelineHelper.IsHighDefinition)
            {
#if d_UnityHDRP
                UnderwaterEffectPassHDRP.Enable(this);
#endif
            }
            else
            {
                OnEnableLegacy();
            }

            EnableEnvironmentalLighting();
        }

        internal void OnDisable()
        {
            OnDisableMask();

#if d_UnityURP
            UnderwaterEffectPassURP.Disable();
#endif

#if d_UnityHDRP
            UnderwaterEffectPassHDRP.Disable();
#endif

            DisableEnvironmentalLighting();

            _ArtifactsShader = null;
        }

        internal void OnDestroy()
        {
            Helpers.Destroy(ref _EffectCommandBuffer);
            Helpers.Destroy(ref _MaskMaterial);
            Helpers.Destroy(ref _HorizonMaskMaterial);
        }

        internal bool ShouldRender(Camera camera)
        {
            // Clear state.
            // NOTE: Danger! Nested cameras can clear! Only used by portals!
            UseStencilBuffer = false;
            NeedsColorTexture = false;
            RequiresFullScreenMask = true;

            if (!_Enabled || _Material == null)
            {
                return false;
            }

            if (!WaterRenderer.ShouldRender(camera, _Layer))
            {
                return false;
            }

            if (_Debug._OnlyReflectionCameras && camera.cameraType != CameraType.Reflection)
            {
                return false;
            }

            // Option to exclude cameras that is not the view camera or our reflection camera.
            // Otherwise, filtering depends on the camera's culling mask which is not always
            // accessible like with the global "Reflection Probes Camera". But whether those
            // cameras triggering camera events is a bug is TBD as it is intermittent.
            if (camera != _Water.Reflections.ReflectionCamera && !WaterRenderer.ShouldRender(camera, _CameraExclusions))
            {
                return false;
            }

            var flags = _Water._ActiveModules;

            if (!_Debug._DisableHeightAboveWaterOptimization && !Portaled && flags.HasFlag(WaterRenderer.ActiveModules.Surface))
            {
                _Water.UpdatePerCameraHeight(camera);
                _ViewerWaterHeight = _Water._ViewerHeightAboveWaterPerCamera;

                if (_ViewerWaterHeight > 2f)
                {
                    return false;
                }
            }

            var viewpoint = camera.transform.position;
            var culling = !flags.HasFlag(WaterRenderer.ActiveModules.Portal);
            var culled = culling && _Water.DefaultExcludes.HasFlag(WaterBodyAffects.Volume);

            // Reflections will have the same Water Body, as they share an XZ position.
            if (camera == _Water.Reflections.ReflectionCamera)
            {
                return !culled || _WaterBodyCulled;
            }

            _CurrentWaterBody = null;

            WaterBody wbExclusion = null;
            WaterBody wbOverride = null;

            // We are grabbing the water body that contains the camera.
            // Grab material from a water body if camera is within its XZ bounds.
            foreach (var wb in WaterBody.WaterBodies)
            {
                if (!wb.Affects.HasFlag(WaterBodyAffects.Volume))
                {
                    continue;
                }

                var hasMaterial = wb.OverrideMaterials;
                var hasExclusion = culling && wb.Exclusion != WaterBodyExclusion.None;

                if (!hasMaterial && !hasExclusion)
                {
                    continue;
                }

                if (!wb.Contains(viewpoint))
                {
                    continue;
                }

                if (hasExclusion && WaterBody.IsBetterMatch(wb, wbExclusion))
                {
                    culled = wb.Exclusion == WaterBodyExclusion.Exclude;
                    wbExclusion = wb;
                }

                if (hasMaterial && WaterBody.IsBetterMatch(wb, wbOverride))
                {
                    _CurrentWaterBody = wb;
                    wbOverride = wb;
                }
            }

            _WaterBodyCulled = culled;

            return !culled;
        }

        // Called by WaterRenderer. Camera must have water layer.
        internal void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (_VolumeMaterial == null)
            {
                return;
            }

            // Populated by this point.
            if (_VolumeMaterial.shader != WaterResources.Instance.Shaders._UnderwaterEffect)
            {
                return;
            }
#endif

            if (_EnvironmentalInitialized)
            {
                _Water.UpdatePerCameraHeight(camera);
                _ViewerWaterHeight = _Water._ViewerHeightAboveWaterPerCamera;
                UpdateEnvironmentalLighting(camera, _Extinction, _ViewerWaterHeight);
            }

#pragma warning disable format
#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
                UnderwaterEffectPassURP.s_Instance?.EnqueuePass(context, camera);
            }
            else
#endif

#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                UnderwaterEffectPassHDRP.s_Instance?.OnBeginCameraRendering(context, camera);
            }
            else
#endif

            {
                OnBeforeLegacyRender(camera);
            }
#pragma warning restore format
        }

        // Used for culling and other things. TIRs can reuse calculations. Camera dependent.
        internal void UpdateRenderingParameters()
        {
            _SurfaceMaterial = _Water.Surface.AboveOrBelowSurfaceMaterial;
            _VolumeMaterial = _Material;

            if (_CurrentWaterBody != null)
            {
                var body = _CurrentWaterBody;
                if (body.AboveOrBelowSurfaceMaterial != null) _SurfaceMaterial = body.AboveOrBelowSurfaceMaterial;
                if (body.VolumeMaterial != null) _VolumeMaterial = body.VolumeMaterial;
            }

#if UNITY_EDITOR
            if (_VolumeMaterial.shader != WaterResources.Instance.Shaders._UnderwaterEffect)
            {
                return;
            }
#endif

            _Extinction = Vector3.zero;
            _MinimumFogDensity = 0f;

            // Calculate extinction.
            if (_SurfaceMaterial != null)
            {
                var densityFactor = _VolumeMaterial.GetFloat(ShaderIDs.s_ExtinctionMultiplier);

                // Get absorption from current material.
                if (_SurfaceMaterial.HasVector(WaterRenderer.ShaderIDs.s_Absorption))
                {
                    _Extinction = _SurfaceMaterial.GetVector(WaterRenderer.ShaderIDs.s_Absorption);
                    Shader.SetGlobalVector(WaterRenderer.ShaderIDs.s_Absorption, _Extinction);
                }

                // Do not use for culling because:
                // - Scattering is not uniform due to anisotropy
                // - Also need to take sun light into account
                if (_SurfaceMaterial.HasProperty(WaterRenderer.ShaderIDs.s_Scattering))
                {
                    var volumeExtinction = _Extinction + _SurfaceMaterial.GetVector(WaterRenderer.ShaderIDs.s_Scattering).XYZ();
                    volumeExtinction *= densityFactor;
                    _MinimumFogDensity = Mathf.Min(Mathf.Min(volumeExtinction.x, volumeExtinction.y), volumeExtinction.z);
                    Shader.SetGlobalFloat(WaterRenderer.ShaderIDs.s_VolumeExtinctionLength, -Mathf.Log(k_CullLimitMinimum) / _MinimumFogDensity);
                }

                _Extinction *= densityFactor;
                _MinimumFogDensity = Mathf.Min(Mathf.Min(_Extinction.x, _Extinction.y), _Extinction.z);
                // Prevent divide by zero.
                _MinimumFogDensity = Mathf.Max(_MinimumFogDensity, 0.0001f);
            }
        }

        internal void UpdateChunkCulling(Camera camera)
        {
            if (!_EnableChunkCulling)
            {
                return;
            }

            // Only relevant to cameras rendering the surface from here.
            if (!_Water._ActiveModules.HasFlag(WaterRenderer.ActiveModules.Surface))
            {
                return;
            }

            if (Portaled || _ViewerWaterHeight > -5f)
            {
                return;
            }

            var extinctionLength = -Mathf.Log(_CullLimit) / _MinimumFogDensity;
            var viewpoint = camera.transform.position;

            foreach (var tile in _Water.Surface.Chunks)
            {
                tile._CulledByVolume = false;

                if (tile.Rend == null || tile._Culled)
                {
                    continue;
                }

                // Cull tiles the viewer cannot see through the underwater fog.
                if ((viewpoint - tile.Rend.bounds.ClosestPoint(viewpoint)).magnitude >= extinctionLength)
                {
                    tile.Rend.forceRenderingOff = tile._CulledByVolume = true;
                }
            }
        }

        internal void OnEndCameraRendering(Camera camera)
        {
            RestoreEnvironmentalLighting();

            // Will only be set if surface is active, which prevents reflection camera from
            // interfering. This ensures the value is valid which is most robust.
            if (_Water._ActiveModules.HasFlag(WaterRenderer.ActiveModules.Surface))
            {
                foreach (var tile in _Water.Surface.Chunks)
                {
                    tile._CulledByVolume = false;
                }
            }

            if (RenderPipelineHelper.IsLegacy)
            {
                OnAfterLegacyRender(camera);
            }
        }

        internal void ExecuteHeightField(Camera camera)
        {
            if (UseLegacyMask)
            {
                return;
            }

            if (!RequiresFullScreenMask)
            {
                return;
            }

            if (!_Water._ActiveModules.HasFlag(WaterRenderer.ActiveModules.Surface))
            {
                return;
            }

            _Water.Surface.UpdateDisplacedSurfaceData(camera);
        }

        void SetEnabled(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled) return;
            if (_Enabled) OnEnable(); else OnDisable();
        }

        void SetAffectsEnvironmentalLighting(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled || !_Enabled) return;
            if (_EnvironmentalLightingEnable) EnableEnvironmentalLighting(); else DisableEnvironmentalLighting();
        }

#if UNITY_EDITOR
        [@OnChange]
        void OnChange(string propertyPath, object previousValue)
        {
            switch (propertyPath)
            {
                case nameof(_Enabled):
                    SetEnabled((bool)previousValue, _Enabled);
                    break;
                case nameof(_EnvironmentalLightingEnable):
                    SetAffectsEnvironmentalLighting((bool)previousValue, _EnvironmentalLightingEnable);
                    break;
            }
        }
#endif
    }

    // Obsolete / Migration
    partial class UnderwaterRenderer : Versioned
    {
        // No migration as default value for new control is far more effective than this toggle.
        [System.Obsolete("Please use Camera Exclusion instead.")]
        [Tooltip("Whether to execute for all cameras.\n\nIf disabled, then additionally ignore any camera that is not the view camera or our reflection camera. It will require managing culling masks of all cameras.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        [HideInInspector]
        bool _AllCameras;
    }
}
