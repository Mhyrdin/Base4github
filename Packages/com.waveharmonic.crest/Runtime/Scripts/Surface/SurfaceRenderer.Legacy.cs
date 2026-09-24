// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    partial class SurfaceRenderer
    {
        partial class ShaderIDs
        {
            public static readonly int s_DummyTarget = Shader.PropertyToID("_Crest_DummyTarget");
            public static readonly int s_WorldToShadow = Shader.PropertyToID("_Crest_WorldToShadow");

            public static class Unity
            {
                public static readonly int s_BuiltInSurface = Shader.PropertyToID("_BUILTIN_Surface");
                public static readonly int s_BuiltInTransparentReceiveShadows = Shader.PropertyToID("_BUILTIN_TransparentReceiveShadows");
            }
        }

        CommandBuffer _DrawWaterSurfaceBuffer;
        MaterialPropertyBlock _QuadMeshMPB;

        void OnBeginCameraRenderingLegacy(Camera camera)
        {
            _Water.UpdateMatrices(camera);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                OnPreRenderWaterLevelDepthTexture(camera);
            }
#endif

            // Everything from here depends on the material being transparent.
            if (!_MaybeTransparent && !IsTransparent(Material))
            {
                return;
            }

            _DrawWaterSurfaceBuffer ??= new() { name = WaterRenderer.k_DrawWater };
            _DrawWaterSurfaceBuffer.Clear();

            // Create or update RT.
            _Water.OnBeginCameraOpaqueTexture(camera);

            SetUpShadows(camera);

            RenderAdditionalChunks(_DrawWaterSurfaceBuffer);

            if (_Water.RenderBeforeTransparency)
            {
                Draw(_DrawWaterSurfaceBuffer, camera);
            }

            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _DrawWaterSurfaceBuffer);
        }

        void OnEndCameraRenderingLegacy(Camera camera)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                OnPostRenderWaterLevelDepthTexture(camera);
            }
#endif

            _Water.OnEndCameraOpaqueTexture(camera);

            if (_DrawWaterSurfaceBuffer != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _DrawWaterSurfaceBuffer);
            }

            if (QualitySettings.shadows != ShadowQuality.Disable && _Water.PrimaryLight != null)
            {
                if (_ScreenSpaceShadowMapBuffer != null)
                {
                    _Water.PrimaryLight.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, _ScreenSpaceShadowMapBuffer);
                }

                if (_DeferredShadowMapBuffer != null)
                {
                    _Water.PrimaryLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, _DeferredShadowMapBuffer);
                }
            }

            Shader.SetGlobalTexture(Crest.ShaderIDs.Unity.s_ShadowMapTexture, Texture2D.whiteTexture);
        }

        internal void SetUpDraw(CommandBuffer commands)
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                // Unity does not set up lighting for us so we will get the last value which could incorrect.
                // SetGlobalColor is just an alias for SetGlobalVector (no color space conversion like Material.SetColor):
                // https://docs.unity3d.com/2017.4/Documentation/ScriptReference/Shader.SetGlobalColor.html
                commands.SetGlobalVector(Crest.ShaderIDs.Unity.s_LightColor0, sun.FinalColor());
                commands.SetGlobalVector(Crest.ShaderIDs.Unity.s_WorldSpaceLightPos0, -sun.transform.forward);
            }

            // Always enabled.
            commands.SetShaderKeyword("LIGHTPROBE_SH", true);
        }

        // Draws the water surface including lighting.
        internal void Draw(CommandBuffer commands, Camera camera)
        {
            commands.BeginSample(k_DrawWaterSurface);

            CoreUtils.SetRenderTarget(commands, BuiltinRenderTextureType.CameraTarget);

            SetUpDraw(commands);

            if (IsQuadMesh)
            {
                _QuadMeshMPB ??= new();
                new PropertyWrapperMPB(_QuadMeshMPB).SetSHCoefficients(_Water.Position);
                Render(camera, commands, Material, pass: 0, mpb: _QuadMeshMPB);
                commands.EndSample(k_DrawWaterSurface);
                return;
            }

            foreach (var chunk in Chunks)
            {
                var renderer = chunk.Rend;

                if (chunk.Rend == null)
                {
                    continue;
                }

                if (!chunk._Visible)
                {
                    continue;
                }

                if (chunk._Culled)
                {
                    continue;
                }

                if (!chunk._WaterDataHasBeenBound)
                {
                    chunk.Bind();
                }

                commands.DrawRenderer(chunk.Rend, renderer.sharedMaterial, 0, 0);
            }

            commands.EndSample(k_DrawWaterSurface);
        }
    }

    partial class SurfaceRenderer
    {
        Material _ForceShadowsMaterial;
        ComputeBuffer _ShadowMatrixBuffer;
        readonly Matrix4x4[] _ShadowMatrixDefaults = { Matrix4x4.zero, Matrix4x4.zero, Matrix4x4.zero, Matrix4x4.zero };
        Material _CaptureShadowMatrices;

        CommandBuffer _DeferredShadowMapBuffer;
        CommandBuffer _ScreenSpaceShadowMapBuffer;

        void LegacyOnEnable()
        {
            _ShadowMatrixBuffer ??= new(4, sizeof(float) * 16, ComputeBufferType.Structured);
            _ShadowMatrixBuffer.SetData(_ShadowMatrixDefaults);
            SetManageLightProbeProxyVolume(!_ManageLightProbeProxyVolume, _ManageLightProbeProxyVolume);
        }

        void LegacyOnDisable()
        {
            Helpers.Destroy(ref _ShadowMatrixBuffer);
            Helpers.Destroy(ref _DrawWaterSurfaceBuffer);
            Helpers.Destroy(ref _DeferredShadowMapBuffer);
            Helpers.Destroy(ref _ScreenSpaceShadowMapBuffer);
            Helpers.Destroy(ref _CaptureShadowMatrices);
            Helpers.Destroy(ref _ForceShadowsMaterial);
            Helpers.DestroyGameObject(ref _LightProbeProxyVolume);
        }

        void SetUpShadows(Camera camera)
        {
            if (QualitySettings.shadows == ShadowQuality.Disable || _Water.PrimaryLight == null)
            {
                return;
            }

            var transform = camera.transform;

            if (_ForceShadowsMaterial == null)
            {
                _ForceShadowsMaterial = new Material(WaterResources.Instance.Shaders._ForceShadows);
            }

            // Force shadows, as Unity ignores transparent shadow receivers, otherwise shadow
            // passes will skip if caster or receiver out of view. ShadowLod also depends on this.
            Graphics.RenderMesh
            (
                new(_ForceShadowsMaterial)
                {
                    receiveShadows = true,
                    shadowCastingMode = ShadowCastingMode.Off,
                },
                mesh: Helpers.QuadMesh,
                submeshIndex: 0,
                objectToWorld: QualitySettings.shadowProjection == ShadowProjection.StableFit
                    ? Matrix4x4.TRS(transform.position + transform.forward, Quaternion.LookRotation(transform.forward), Vector3.one * 0.01f)
                    // TODO: render water level inputs to support shadows for varying water level.
                    // Sort of works for close fit. But will decrease shadow quality.
                    : Matrix4x4.TRS(Vector3.up * _Water.SeaLevel, Quaternion.LookRotation(-Vector3.up), Vector3.one * 100f)
            );

            if (!Material.IsKeywordEnabled("_BUILTIN_TRANSPARENT_RECEIVES_SHADOWS"))
            {
                return;
            }

            if (_CaptureShadowMatrices == null)
            {
                _CaptureShadowMatrices = new Material(WaterResources.Instance.Shaders._CaptureShadowMatrices);
            }

            // Used ComputeBuffer must always be bound!
            Shader.SetGlobalBuffer(ShaderIDs.s_WorldToShadow, _ShadowMatrixBuffer);
            // Capture shadow matrices, as Unity clears all but the first cascade.
            _ScreenSpaceShadowMapBuffer ??= new() { name = WaterRenderer.k_DrawWater };
            _ScreenSpaceShadowMapBuffer.Clear();
            // Cannot set target to None, as it will make some UI black (Unity bug?).
            _ScreenSpaceShadowMapBuffer.GetTemporaryRT(ShaderIDs.s_DummyTarget, new RenderTextureDescriptor(4, 4));
            CoreUtils.SetRenderTarget(_ScreenSpaceShadowMapBuffer, ShaderIDs.s_DummyTarget);
            // Setting the buffer (SetGlobalBuffer) and writing to it only worked with Metal.
            // For other graphics APIs, had to use SetRandomWriteTarget.
            _ScreenSpaceShadowMapBuffer.ClearRandomWriteTargets();
            _ScreenSpaceShadowMapBuffer.SetRandomWriteTarget(1, _ShadowMatrixBuffer);
            _ScreenSpaceShadowMapBuffer.DrawProcedural(Matrix4x4.identity, _CaptureShadowMatrices, 0, MeshTopology.Triangles, 3);
            _ScreenSpaceShadowMapBuffer.ClearRandomWriteTargets();
            _ScreenSpaceShadowMapBuffer.ReleaseTemporaryRT(ShaderIDs.s_DummyTarget);
            _Water.PrimaryLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, _ScreenSpaceShadowMapBuffer);

            // Make shadow map available to transparent objects.
            // Call this regardless of rendering path as it has no negative consequences for forward.
            _DeferredShadowMapBuffer ??= new() { name = WaterRenderer.k_DrawWater };
            _DeferredShadowMapBuffer.Clear();
            _DeferredShadowMapBuffer.SetGlobalTexture(Crest.ShaderIDs.Unity.s_ShadowMapTexture, BuiltinRenderTextureType.CurrentActive);
            _Water.PrimaryLight.AddCommandBuffer(LightEvent.AfterShadowMap, _DeferredShadowMapBuffer);

            // Set up shadow keywords.
            _DrawWaterSurfaceBuffer.SetKeyword(new("SHADOWS_SINGLE_CASCADE"), QualitySettings.shadowCascades == 1);
            _DrawWaterSurfaceBuffer.SetKeyword(new("SHADOWS_SPLIT_SPHERES"), QualitySettings.shadowProjection == ShadowProjection.StableFit);
            _DrawWaterSurfaceBuffer.SetKeyword(new("SHADOWS_SOFT"), QualitySettings.shadows == ShadowQuality.All);
        }
    }

    partial class SurfaceRenderer
    {
#if UNITY_6000_5_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
#endif
        LightProbeProxyVolume _LightProbeProxyVolume;
        Vector2 _LightProbeProxyVolumeTexels;

        void AddLightProbeProxyVolume(GameObject go)
        {
            _LightProbeProxyVolume = go.AddComponent<LightProbeProxyVolume>();
            _LightProbeProxyVolume.boundingBoxMode = LightProbeProxyVolume.BoundingBoxMode.Custom;
            _LightProbeProxyVolume.resolutionMode = LightProbeProxyVolume.ResolutionMode.Custom;
        }
#if UNITY_6000_5_OR_NEWER
#pragma warning restore CS0618 // Type or member is obsolete
#endif

        void SetManageLightProbeProxyVolume(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled) return;
            if (!RenderPipelineHelper.IsLegacy) return;

            if (current)
            {
                var go = new GameObject("LPPV");
                go.transform.parent = _Water.Container.transform;
                go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                AddLightProbeProxyVolume(go);
                _LightProbeProxyVolume.sizeCustom = new(50f, 1f, 50f);
                _LightProbeProxyVolume.gridResolutionX = 32;
                _LightProbeProxyVolume.gridResolutionZ = 32;
                _LightProbeProxyVolume.gridResolutionY = 1;

                _LightProbeProxyVolumeTexels = new
                (
                    _LightProbeProxyVolume.sizeCustom.x / _LightProbeProxyVolume.gridResolutionX,
                    _LightProbeProxyVolume.sizeCustom.z / _LightProbeProxyVolume.gridResolutionZ
                );

                // TODO: support other mesh options.
                foreach (var chunk in Chunks)
                {
                    chunk.Rend.lightProbeProxyVolumeOverride = _LightProbeProxyVolume.gameObject;
                    chunk.Rend.lightProbeUsage = LightProbeUsage.UseProxyVolume;
                }
            }
            else
            {
                Helpers.DestroyGameObject(_LightProbeProxyVolume);
                foreach (var chunk in Chunks)
                {
                    var renderer = chunk.Rend;

                    if (_RendererTemplate != null)
                    {
                        renderer.lightProbeProxyVolumeOverride = _RendererTemplate.lightProbeProxyVolumeOverride;
                        renderer.lightProbeUsage = _RendererTemplate.lightProbeUsage;
                    }
                    else
                    {
                        renderer.lightProbeProxyVolumeOverride = null;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                    }
                }
            }
        }

        void UpdateLightProbeProxyVolume(Camera camera)
        {
            if (!ManageLightProbeProxyVolume)
            {
                return;
            }

            var position = camera.transform.position;
            position.x = Mathf.Round(position.x / _LightProbeProxyVolumeTexels.x) * _LightProbeProxyVolumeTexels.x;
            position.z = Mathf.Round(position.z / _LightProbeProxyVolumeTexels.y) * _LightProbeProxyVolumeTexels.y;
            _LightProbeProxyVolume.originCustom = position.XNZ();
        }
    }
}
