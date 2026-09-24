// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    enum WaterMeshType
    {
        [Tooltip("Chunks implemented as a clip-map.")]
        Chunks,

        [Tooltip("A single quad.\n\nOptimal for demanding platforms like mobile. Displacement will only contribute to normals.")]
        Quad,

        [Tooltip("Custom geometry only.\n\nUse Mesh Renderers with Custom Mesh enabled on the surface material.\n\nDo note that the other mesh types also support custom geometry, but the difference is that this type only supports custom geometry.")]
        Custom,
    }

    /// <summary>
    /// Renders the water surface.
    /// </summary>
    [System.Serializable]
    public sealed partial class SurfaceRenderer : Versioned
    {
        [@Space(10)]

        [Tooltip("Whether the underwater effect is enabled.\n\nAllocates/releases resources if state has changed.")]
        [@GenerateAPI(Getter.Custom, Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _Enabled = true;

        [Tooltip("The water chunk renderers will have this layer.")]
        [@Layer]
        [@GenerateAPI]
        [SerializeField]
        internal int _Layer = 4; // Water

        [@Space(10)]

        [@Label("Mesh")]
        [Tooltip("The meshing solution for the water surface.")]
        [@DecoratedField]
        [@SerializeField]
        WaterMeshType _MeshType;

        [Tooltip("Template for water chunks as a prefab.\n\nThe only requirements are that the prefab must contain a MeshRenderer at the root and not a MeshFilter or WaterChunkRenderer. MR values will be overwritten where necessary and the prefabs are linked in edit mode.")]
        [@PrefabField(title: "Create Chunk Prefab", name: "Water Chunk")]
        [SerializeField]
        internal GameObject _ChunkTemplate;

        [Tooltip("Whether to support using the surface material with other renderers.\n\nAlso requires enabling Custom Mesh on the material.")]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        bool _SupportCustomRenderers = true;

        [@Space(10)]

        [Tooltip("Material to use for the water surface.")]
        [@AttachMaterialEditor(order: 0)]
        [@MaterialField("Crest/Water", name: "Water", title: "Create Water Material")]
        [@GenerateAPI]
        [SerializeField]
        internal Material _Material = null;

        [Tooltip("Underwater will copy from this material if set.\n\nUseful for overriding properties for the underwater effect. To see what properties can be overridden, see the disabled properties on the underwater material. This does not affect the surface.")]
        [@AttachMaterialEditor(order: 1)]
        [@MaterialField("Crest/Water", name: "Water (Below)", title: "Create Water Material", parent: "_Material")]
        [@GenerateAPI]
        [SerializeField]
        internal Material _VolumeMaterial = null;

        [@Space(10)]

        [Tooltip("Have the water surface cast shadows for albedo (both foam and custom).")]
        [@GenerateAPI(Getter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _CastShadows;

        [@Heading("Culling")]

        [Tooltip("How many frames to distribute the chunk bounds calculation.\n\nThe chunk bounds are calculated per frame to ensure culling is correct when using inputs that affect displacement. Some performance can be saved by distributing the load over several frames. The higher the frames, the longer it will take - lowest being instant.")]
        [@Range(1, 30, Range.Clamp.Minimum)]
        [@GenerateAPI]
        [SerializeField]
        internal int _TimeSliceBoundsUpdateFrameCount = 1;

        [@Heading("Advanced")]

        [Tooltip("Rules to exclude cameras from surface rendering.\n\nThese are exclusion rules, so for all cameras, select Nothing. These rules are applied on top of the Layer rules.")]
        [@DecoratedField]
        [@GenerateAPI]
        [SerializeField]
        internal WaterCameraExclusion _CameraExclusions = WaterCameraExclusion.Hidden | WaterCameraExclusion.Reflection;

        [Tooltip("How to handle self-intersections of the water surface.\n\nThey can be caused by choppy waves which can cause a flipped underwater effect. When not using the portals/volumes, this fix is only applied when within 2 meters of the water surface. Automatic will disable the fix if portals/volumes are used which is the recommend setting.")]
        [@DecoratedField, SerializeField]
        internal SurfaceSelfIntersectionFixMode _SurfaceSelfIntersectionFixMode = SurfaceSelfIntersectionFixMode.Automatic;

        [Tooltip("Whether to allow sorting using the render queue.\n\nIf you need to change the minor part of the render queue (eg +100), then enable this option. As a side effect, it will also disable the front-to-back rendering optimization for Crest. This option does not affect changing the major part of the render queue (eg AlphaTest, Transparent), as that is always allowed.\n\nRender queue sorting is required for some third-party integrations.")]
        [@Hide(RenderPipeline.HighDefinition)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _AllowRenderQueueSorting;

        [Tooltip("Adds an LPPV for indirect lighting.\n\nLighting from light probes can have minor tiling when not using an LPPV. This LPPV follows the camera.")]
        [@Show(RenderPipeline.Legacy)]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        bool _ManageLightProbeProxyVolume;

        [@Space(10)]

#if !CREST_DEBUG
        [HideInInspector]
#endif
        [@DecoratedField, SerializeField]
        internal DebugFields _Debug = new();

        [System.Serializable]
        internal sealed class DebugFields
        {
#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Whether to generate water geometry tiles uniformly (with overlaps).")]
            [@DecoratedField, SerializeField]
            public bool _UniformTiles;

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Disable generating a wide strip of triangles at the outer edge to extend water to edge of view frustum.")]
            [@DecoratedField, SerializeField]
            public bool _DisableSkirt;

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [Tooltip("Toggle the Draw Renderer Bounds on each chunk.")]
            [@DecoratedField, SerializeField]
            public bool _DrawRendererBounds;
        }

        const string k_DrawWaterSurface = "Surface";

        internal WaterRenderer _Water;
        Transform _Root;
        internal Transform Root => _Root;
        internal List<WaterChunkRenderer> Chunks { get; } = new();
        internal bool _Rebuild;
        Renderer _RendererTemplate;
        internal bool _MaybeTransparent;


        //
        // Level of Detail
        //

        readonly MaterialPropertyBlock[] _PerCascadeMPB = new MaterialPropertyBlock[Lod.k_MaximumSlices];
        internal MaterialPropertyBlock[] PerCascadeMPB { get; private set; }

        // We are computing these values to be optimal based on the base mesh vertex density.
        float _LodAlphaBlackPointFade;
        float _LodAlphaBlackPointWhitePointFade;


        //
        // Culling
        //

        bool _CanSkipCulling;


        //
        // Events
        //

        /// <summary>
        /// Invoked after water chunk modification.
        /// </summary>
        /// <remarks>
        /// Gives an opportunity to modify the renderer.
        /// </remarks>
        public static System.Action<Renderer> OnCreateChunkRenderer { get; set; }


        internal Material _MotionVectorMaterial;
        int _ForwardPass;

        internal Material AboveOrBelowSurfaceMaterial => _VolumeMaterial == null ? _Material : _VolumeMaterial;
        internal bool IsQuadMesh => _MeshType == WaterMeshType.Quad;
        internal bool IsChunkMesh => _MeshType == WaterMeshType.Chunks;


        //
        // Facing
        //

        internal enum SurfaceSelfIntersectionFixMode
        {
            [Tooltip("Uses VFACE/IsFrontFace.")]
            Off,

            [Tooltip("Force entire water surface to render as below water.")]
            ForceBelowWater,

            [Tooltip("Force entire water surface to render as above water.")]
            ForceAboveWater,

            [Tooltip("Force entire water surface to render as above or below water if beyond a distance from surface, otherwise use mask/facing.")]
            On,

            [Tooltip("Force entire water surface to render as above or below water if beyond a distance from surface (except in special circumstances like  Portals).")]
            Automatic,
        }

        enum ForceFacing
        {
            None,
            BelowWater,
            AboveWater,
            Facing,
        }


        static partial class ShaderIDs
        {
            public static readonly int s_ForceUnderwater = Shader.PropertyToID("g_Crest_ForceUnderwater");
            public static readonly int s_LodAlphaBlackPointFade = Shader.PropertyToID("g_Crest_LodAlphaBlackPointFade");
            public static readonly int s_LodAlphaBlackPointWhitePointFade = Shader.PropertyToID("g_Crest_LodAlphaBlackPointWhitePointFade");

            public static readonly int s_BuiltShadowCasterZTest = Shader.PropertyToID("_Crest_BUILTIN_ShadowCasterZTest");

            public static readonly int s_ChunkMeshScaleAlpha = Shader.PropertyToID("_Crest_ChunkMeshScaleAlpha");
            public static readonly int s_ChunkGeometryGridWidth = Shader.PropertyToID("_Crest_ChunkGeometryGridWidth");
            public static readonly int s_ChunkFarNormalsWeight = Shader.PropertyToID("_Crest_ChunkFarNormalsWeight");
            public static readonly int s_ChunkNormalScrollSpeed = Shader.PropertyToID("_Crest_ChunkNormalScrollSpeed");
            public static readonly int s_NormalMapParameters = Shader.PropertyToID("_Crest_NormalMapParameters");

            public static readonly int s_BoundsXZ = Shader.PropertyToID("_Crest_BoundsXZ");
            public static readonly int s_HasBoundsXZ = Shader.PropertyToID("_Crest_HasBoundsXZ");
            public static readonly int s_InvertBoundsXZ = Shader.PropertyToID("_Crest_InvertBoundsXZ");
            public static readonly int s_DataFromUVs = Shader.PropertyToID("_Crest_DataFromUVs");

            // Visualizer
            public static readonly int s_DataType = Shader.PropertyToID("_Crest_DataType");
            public static readonly int s_Exposure = Shader.PropertyToID("_Crest_Exposure");
            public static readonly int s_Range = Shader.PropertyToID("_Crest_Range");
            public static readonly int s_Saturate = Shader.PropertyToID("_Crest_Saturate");
        }

        bool _ForceRenderingOff;

        internal bool ForceRenderingOff
        {
            get => _ForceRenderingOff;
            set
            {
                _ForceRenderingOff = value;

                if (_Enabled && IsChunkMesh)
                {
                    foreach (var chunk in Chunks)
                    {
                        // Do not override WB culled.
                        if (chunk._Culled) continue;
                        if (chunk._Duplicate) continue;
                        chunk.Rend.forceRenderingOff = _ForceRenderingOff;
                    }
                }
            }
        }

        Material _VisualizeDataMaterial;
        internal Material VisualizeDataMaterial
        {
            get
            {
                if (_VisualizeDataMaterial == null)
                {
                    _VisualizeDataMaterial = new(Shader.Find("Hidden/Crest/Debug/Visualize Data"));
                }

                return _VisualizeDataMaterial;
            }
        }

        internal void Initialize()
        {
            if (IsChunkMesh)
            {
                var root = new GameObject("Root");
                Debug.Assert(root != null, "Crest: The water Root transform could not be immediately constructed. Please report this issue to us.");
                root.hideFlags = _Water._Debug._ShowHiddenObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave;
                root.transform.parent = _Water.Container.transform;
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                _Root = root.transform;

                Builder.GenerateMesh(_Water, this);

                Root.position = _Water.Position;
                Root.localScale = new(_Water.Scale, 1f, _Water.Scale);
            }

            if (_ChunkTemplate != null)
            {
                _RendererTemplate = _ChunkTemplate.GetComponent<Renderer>();
            }

            // Populate MPBs with defaults. Protects against null exceptions etc.
            PerCascadeMPB = _PerCascadeMPB;
            NormalMapParameters = _NormalMapParameters;
            _PreviousObjectToWorld = new Matrix4x4[Chunks.Count];
            PreviousObjectToWorld = _PreviousObjectToWorld;
            _CustomGeometryMPB = new();
            _CustomGeometryWithDataMPB = new();
            _CustomGeometryMaterial = new(_Material);
            CustomGeometryMaterial = _CustomGeometryMaterial;
            InitializeProperties();

            // Resolution is 4 tiles across.
            var baseMeshDensity = _Water.LodResolution * 0.25f / _Water._GeometryDownSampleFactor;
            // 0.4f is the "best" value when base mesh density is 8. Scaling down from there produces results similar to
            // hand crafted values which looked good when the water is flat.
            _LodAlphaBlackPointFade = 0.4f / (baseMeshDensity / 8f);
            _LodAlphaBlackPointWhitePointFade = 1f - _LodAlphaBlackPointFade - _LodAlphaBlackPointFade;

            Shader.SetGlobalFloat(ShaderIDs.s_LodAlphaBlackPointFade, _LodAlphaBlackPointFade);
            Shader.SetGlobalFloat(ShaderIDs.s_LodAlphaBlackPointWhitePointFade, _LodAlphaBlackPointWhitePointFade);

            UpdateMaterial(_Material, ref _MotionVectorMaterial);

            _CanSkipCulling = false;

            if (RenderPipelineHelper.IsLegacy)
            {
                LegacyOnEnable();
            }

#if UNITY_EDITOR
            EnableWaterLevelDepthTexture();
#endif
        }

        internal void OnDestroy()
        {
#if UNITY_EDITOR
            DisableWaterLevelDepthTexture();
#endif

            // Clean up everything created through the Water Builder.
            // Not every mesh is assigned to a chunk thus we should destroy all of them here.
            for (var i = 0; i < _Meshes?.Length; i++)
            {
                Helpers.Destroy(ref _Meshes[i]);
            }

            Chunks.Clear();
            Helpers.Destroy(ref _MotionVectorMaterial);
            Helpers.Destroy(ref _DisplacedMaterial);

            Helpers.Destroy(ref _BeforeRenderingCommands);

            Helpers.Destroy(ref _CustomGeometryMaterial);

            // Clear camera data.
            _PerCameraPerCascadeMPB.Clear();
            _PerCameraNormalMapParameters.Clear();
            _PerCameraPreviousObjectToWorld.Clear();

            foreach (var (_, item) in _PerCameraCustomGeometryMaterial)
            {
                var material = item;
                Helpers.Destroy(ref material);
            }
            _PerCameraCustomGeometryMaterial.Clear();

            Helpers.DestroyGameObject(ref _Root);

            if (RenderPipelineHelper.IsLegacy)
            {
                LegacyOnDisable();
            }
        }

        void ShowHiddenObjects(bool show)
        {
            foreach (var chunk in Chunks)
            {
                chunk.gameObject.hideFlags = show ? HideFlags.DontSave : HideFlags.HideAndDontSave;
            }
        }

        internal void UpdateChunkVisibility(Camera camera)
        {
            if (!IsChunkMesh)
            {
                return;
            }

            foreach (var chunk in Chunks)
            {
                // Legacy Underwater only checks visible, so we need to set it always.
                // Water Level for painting does too, but should be fine.
#if !d_Crest_LegacyUnderwater
                if (chunk._Culled) continue;
#endif
                var renderer = chunk.Rend;
                // Can happen in edit mode.
                if (renderer == null) continue;
                chunk._Visible = Helpers.TestPlanesAndPointsAABB(_Water._CameraFrustumPlanes, _Water._CameraFrustumPoints, renderer.bounds);
                chunk.Rend.forceRenderingOff = chunk._Culled || chunk._Duplicate || !chunk._Visible;
            }
        }

        internal void RestoreCulling()
        {
            foreach (var chunk in Chunks)
            {
                chunk.Rend.forceRenderingOff = chunk._Culled || chunk._Duplicate || !chunk._Visible || chunk._CulledByVolume;
            }
        }

        internal void UpdateMaterial(Material material, ref Material motion)
        {
            if (material == null)
            {
                return;
            }

            var enable = !_Water.RenderBeforeTransparency;
            material.SetShaderPassEnabled("Forward", enable);
            material.SetShaderPassEnabled("ForwardAdd", enable);
            material.SetShaderPassEnabled("ForwardBase", enable);
            material.SetShaderPassEnabled("UniversalForward", enable);

            // HDRP will automatically disable this pass for unknown reasons. It might be that
            // we are sampling from the depth texture which does not work with shadow casting.
            if (RenderPipelineHelper.IsHighDefinition)
            {
                material.SetShaderPassEnabled("ShadowCaster", _CastShadows);
            }

            UpdateMotionVectorsMaterial(material, ref motion);
        }

        internal static bool IsTransparent(Material material)
        {
            return RenderPipelineHelper.IsLegacy
                ? material.IsKeywordEnabled("_BUILTIN_SURFACE_TYPE_TRANSPARENT")
                : material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
        }

        void Rebuild()
        {
            if (!_Water._Initialized) return;
            OnDestroy();
            if (!Enabled) return;
            Initialize();
            _Rebuild = false;
        }

        internal bool ShouldRender(Camera camera)
        {
            if (!_Enabled)
            {
                return false;
            }

            if (!WaterRenderer.ShouldRender(camera, Layer, _CameraExclusions))
            {
                return false;
            }

            // Our planar reflection camera must never render the surface.
            if (camera == _Water.Reflections.ReflectionCamera)
            {
                return false;
            }

            if (Material == null)
            {
                return false;
            }

            return true;
        }

        internal bool ShouldCull()
        {
            foreach (var chunk in Chunks)
            {
                if (chunk.Rend == null) continue;
                if (!chunk.Rend.forceRenderingOff) return false;
            }

            return IsChunkMesh;
        }

        internal void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            WritePerCameraMaterialParameters(camera);

            // Motion Vectors.
            if (ShouldRenderMotionVectors(camera) && QueueMotionVectors)
            {
                foreach (var chunk in Chunks)
                {
                    chunk.RenderMotionVectors(this, camera);
                }
            }

#pragma warning disable format
#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
#if UNITY_EDITOR
                WaterLevelDepthTextureURP.s_Instance?.OnBeginCameraRendering(context, camera);
#endif
                WaterSurfaceRenderPass.Instance?.OnBeginCameraRendering(context, camera);
            }
            else
#endif

            if (RenderPipelineHelper.IsLegacy)
            {
                OnBeginCameraRenderingLegacy(camera);
            }
#pragma warning restore format
        }

        internal void OnEndCameraRendering(Camera camera)
        {
            if (RenderPipelineHelper.IsLegacy)
            {
                OnEndCameraRenderingLegacy(camera);
            }
        }

        void InitializeProperties()
        {
            System.Array.Fill(NormalMapParameters, new Vector4(0, 0, 1, 0));

            // Populate MPBs with defaults.
            for (var index = 0; index < PerCascadeMPB.Length; index++)
            {
                var block = new MaterialPropertyBlock();
                block.SetInteger(Lod.ShaderIDs.s_LodIndex, index);
                block.SetFloat(ShaderIDs.s_ChunkFarNormalsWeight, 1f);
                PerCascadeMPB[index] = block;
            }

            foreach (var chunk in Chunks)
            {
                PreviousObjectToWorld[chunk._SiblingIndex] = chunk.Transform.localToWorldMatrix;
            }

            _CustomGeometryWithDataMPB.SetBoolean(ShaderIDs.s_DataFromUVs, true);
        }

        void WritePerCameraMaterialParameters(Camera camera)
        {
            if (Material == null)
            {
                return;
            }

            // If no underwater, then no need for underwater surface.
            if (!_Water._ActiveModules.HasFlag(WaterRenderer.ActiveModules.Volume) && _SurfaceSelfIntersectionFixMode == SurfaceSelfIntersectionFixMode.Automatic)
            {
                Shader.SetGlobalInteger(ShaderIDs.s_ForceUnderwater, (int)ForceFacing.AboveWater);
                return;
            }

            _Water.UpdatePerCameraHeight(camera);

            // Override isFrontFace when camera is far enough from the water surface to fix self-intersecting waves.
            // Hack - due to SV_IsFrontFace occasionally coming through as true for back faces,
            // add a param here that forces water to be in underwater state. I think the root
            // cause here might be imprecision or numerical issues at water tile boundaries, although
            // i'm not sure why cracks are not visible in this case.
            var height = _Water._ViewerHeightAboveWaterPerCamera;

            var value = _SurfaceSelfIntersectionFixMode switch
            {
                SurfaceSelfIntersectionFixMode.On =>
                    !_Water._PerCameraHeightReady
                    ? ForceFacing.None
                    : height < -2f
                    ? ForceFacing.BelowWater
                    : height > 2f
                    ? ForceFacing.AboveWater
                    : ForceFacing.None,
                // Skip for portals as it is possible to see both sides of the surface at any position.
                SurfaceSelfIntersectionFixMode.Automatic =>
                    _Water._ActiveModules.HasFlag(WaterRenderer.ActiveModules.Portal) || !_Water._PerCameraHeightReady
                    ? ForceFacing.None
                    : height < -2f
                    ? ForceFacing.BelowWater
                    : height > 2f
                    ? ForceFacing.AboveWater
                    : ForceFacing.None,
                // Always use facing (VFACE).
                SurfaceSelfIntersectionFixMode.Off => ForceFacing.Facing,
                _ => (ForceFacing)_SurfaceSelfIntersectionFixMode,
            };

            Shader.SetGlobalInteger(ShaderIDs.s_ForceUnderwater, (int)value);
        }

        internal void LateUpdate()
        {
            if (_Rebuild)
            {
                Rebuild();
            }

            if (_ForceRenderingOff)
            {
                return;
            }

            LoadCameraData(_Water.CurrentCamera);

            UpdateLightProbeProxyVolume(_Water.CurrentCamera);

            if (IsChunkMesh)
            {
                Root.position = _Water.Position;
                Root.localScale = new(_Water.Scale, 1f, _Water.Scale);
            }

#if CREST_DEBUG
            if (_Water._Debug._VisualizeData)
            {
                var material = _Water.Surface.VisualizeDataMaterial;
                material.SetInteger(ShaderIDs.s_DataType, (int)_Water._Debug._VisualizeDataType);
                material.SetBoolean(ShaderIDs.s_Saturate, _Water._Debug._VisualizeDataSaturate);
                material.SetFloat(ShaderIDs.s_Exposure, _Water._Debug._VisualizeDataExposure);
                material.SetFloat(ShaderIDs.s_Range, _Water._Debug._VisualizeDataRange);
            }
#endif

            if (Material != null)
            {
                // FindKeyword allocates, and:
                // Cannot cache or receive the following on shader recompilation:
                // Local keyword … comes from a different shader.
                var keyword = new LocalKeyword(Material.shader, "_CREST_CUSTOM_MESH");

                if (keyword.isValid)
                {
                    Material.SetKeyword(keyword, IsQuadMesh);
                }
            }

            WritePerCascadeInstanceData();

            if (!IsChunkMesh || _SupportCustomRenderers)
            {
                // For simple and custom meshes.
                Shader.SetGlobalVectorArray(ShaderIDs.s_NormalMapParameters, NormalMapParameters);
            }

            RenderMaximumLOD();

            if (IsQuadMesh)
            {
                LateUpdateQuadMesh();
                return;
            }

            var allowMixedMaterialTypes = !RenderPipelineHelper.IsHighDefinition;
            _MaybeTransparent = _Water.RenderBeforeTransparency;

            foreach (var chunk in Chunks)
            {
                if (allowMixedMaterialTypes && !_MaybeTransparent && chunk.MaterialOverridden)
                {
                    _MaybeTransparent = IsTransparent(chunk.Rend.sharedMaterial);
                }

                chunk.UpdateMeshBounds(_Water, this);
            }

            ApplyWaterBodyCulling();

            LateUpdateMotionVectors();

            UpdateMaterial(_Material, ref _MotionVectorMaterial);

#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition && _Material != null)
            {
                _ForwardPass = _Material.FindPass("Forward");
            }
            else
            {
                _ForwardPass = 0;
            }
#endif

            foreach (var body in WaterBody.WaterBodies)
            {
                if (body.AboveSurfaceMaterial != null)
                {
                    UpdateMaterial(body.AboveSurfaceMaterial, ref body._MotionVectorMaterial);
                }
            }

            foreach (var chunk in Chunks)
            {
                chunk.OnLateUpdate();
            }
        }

        void WritePerCascadeInstanceData()
        {
            var levels = _Water.LodLevels;
            var texel = _Water.LodResolution * 0.25f / _Water._GeometryDownSampleFactor;

            // LOD 0
            {
                // Blend LOD 0 shape in/out to avoid pop, if scale could increase.
                PerCascadeMPB[0].SetFloat(ShaderIDs.s_ChunkMeshScaleAlpha, _Water.ScaleCouldIncrease ? _Water.ViewerAltitudeLevelAlpha : 0f);
            }

            // LOD N
            {
                // Blend furthest normals scale in/out to avoid pop, if scale could reduce.
                var weight = _Water.ScaleCouldDecrease ? _Water.ViewerAltitudeLevelAlpha : 1f;
                PerCascadeMPB[levels - 1].SetFloat(ShaderIDs.s_ChunkFarNormalsWeight, weight);
                NormalMapParameters[levels - 1] = new(0, 0, weight, 0);
            }

            for (var index = 0; index < levels; index++)
            {
                var mpb = PerCascadeMPB[index];

                // geometry data
                // compute grid size of geometry. take the long way to get there - make sure we land exactly on a power of two
                // and not inherit any of the lossy-ness from lossyScale.
                var scale = _Water.CascadeData.Current[index].x;
                var width = scale / texel;

                mpb.SetFloat(ShaderIDs.s_ChunkGeometryGridWidth, width);

                var mul = 1.875f; // fudge 1
                var pow = 1.4f; // fudge 2
                var texelWidth = width / _Water._GeometryDownSampleFactor;
                var speed = new Vector2
                (
                    Mathf.Pow(Mathf.Log(1f + 2f * texelWidth) * mul, pow),
                    Mathf.Pow(Mathf.Log(1f + 4f * texelWidth) * mul, pow)
                );

                mpb.SetVector(ShaderIDs.s_ChunkNormalScrollSpeed, speed);

                var normals = NormalMapParameters[index];
                normals.x = speed.x;
                normals.y = speed.y;
                NormalMapParameters[index] = normals;
            }
        }

        void ApplyWaterBodyCulling()
        {
            var hasWaterBodies = WaterBody.WaterBodies.Count > 0;
            var canSkipCulling = !hasWaterBodies && _CanSkipCulling;
            var defaultExclusion = _Water.DefaultExcludes.HasFlag(WaterBodyAffects.Surface)
                ? WaterBodyExclusion.Exclude : WaterBodyExclusion.None;

            // Chunk bounds needs to be up-to-date at this point.
            foreach (var tile in Chunks)
            {
                if (tile.Rend == null)
                {
                    continue;
                }

                // No need to skip based on visibility, as this happens before.

                tile._Culled = false;
                tile._Duplicate = false;
                tile.MaterialOverridden = false;

                var oldExclusion = defaultExclusion;

                if (canSkipCulling)
                {
                    tile.Rend.forceRenderingOff = tile._Culled = oldExclusion == WaterBodyExclusion.Exclude;
                    continue;
                }

                // If there are local bodies of water, this will do overlap tests between the water tiles
                // and the water bodies and turn off any that don't overlap.

                var chunkBounds = tile.Rend.bounds;
                var chunkUndisplacedBoundsXZ = tile.UnexpandedBoundsXZ;

                var largestOverlap = 0f;

                foreach (var body in WaterBody.WaterBodies)
                {
                    // Nothing to do!
                    if (!body.Affects.HasFlag(WaterBodyAffects.Surface))
                    {
                        continue;
                    }

                    // No point overriding the material if excluding. Precise handled elsewhere.
                    var hasMaterial = body.OverrideMaterials && !body.Precise && body.Exclusion != WaterBodyExclusion.Exclude;
                    var hasExclusion = body.Exclusion != WaterBodyExclusion.None;

                    // Prioritize conservative.
                    // This is used for performance culling. Do not proceed further.
                    if (body.Conservative && body.Exclusion == WaterBodyExclusion.Exclude)
                    {
                        if (body.AABB.ContainsXZ(chunkBounds))
                        {
                            oldExclusion = WaterBodyExclusion.Exclude;
                            break;
                        }

                        continue;
                    }

                    if (body.OverrideMaterials && body.Precise && body.Exclusion != WaterBodyExclusion.Exclude)
                    {
                        // Aggressively cull duplicate chunks. Already comes with a small buffer.
                        if (body.AABB.RectXZ().Encapsulates(chunkUndisplacedBoundsXZ))
                        {
                            tile._Duplicate = true;
                        }
                    }

                    // Nothing to do!
                    if (!hasExclusion && !hasMaterial)
                    {
                        continue;
                    }

                    // If chunk has already been excluded from culling, then skip this iteration. But
                    // finish this iteration if the water body has a material override to work out most
                    // influential water body. Even if the chunk is marked for exclusion, as it may
                    // still be included further into the loop.
                    if (oldExclusion == WaterBodyExclusion.Include && !hasMaterial)
                    {
                        continue;
                    }

                    var bounds = body.AABB;

                    if (body.Precise && body.Exclusion == WaterBodyExclusion.Exclude ? bounds.ContainsXZ(chunkBounds) : bounds.IntersectsXZ(chunkBounds))
                    {
                        // Inclusion always wins. Precise will refine it further.
                        if (hasExclusion && oldExclusion != WaterBodyExclusion.Include)
                        {
                            oldExclusion = body.Exclusion;
                        }

                        // No point overriding a material if excluding. Do not check old, as it could be
                        // included later.
                        if (hasMaterial)
                        {
                            var overlap = 0f;
                            {
                                // Use the unexpanded bounds to prevent leaking as generally this feature will be
                                // for an inland body of water where hopefully there is attenuation between it and
                                // the water to handle the water's displacement. The inland water body will unlikely
                                // have large displacement but can be mitigated with a decent buffer zone.
                                var xMin = Mathf.Max(bounds.min.x, chunkUndisplacedBoundsXZ.min.x);
                                var xMax = Mathf.Min(bounds.max.x, chunkUndisplacedBoundsXZ.max.x);
                                var zMin = Mathf.Max(bounds.min.z, chunkUndisplacedBoundsXZ.min.y);
                                var zMax = Mathf.Min(bounds.max.z, chunkUndisplacedBoundsXZ.max.y);
                                if (xMin < xMax && zMin < zMax)
                                {
                                    overlap = (xMax - xMin) * (zMax - zMin);
                                }
                            }

                            // If this water body has the most overlap, then the chunk will get its material.
                            if (overlap > largestOverlap)
                            {
                                tile.MaterialOverridden = body.AboveSurfaceMaterial != null;
                                if (tile.MaterialOverridden)
                                {
                                    tile.Rend.sharedMaterial = body.AboveSurfaceMaterial;
                                    tile._MotionVectorMaterial = body._MotionVectorMaterial;
                                }
                                largestOverlap = overlap;
                            }
                        }
                        else
                        {
                            tile.MaterialOverridden = false;
                        }
                    }
                }

                tile.Rend.forceRenderingOff = tile._Culled = oldExclusion == WaterBodyExclusion.Exclude;

                if (tile._Duplicate)
                {
                    tile.Rend.forceRenderingOff = true;
                }
            }

            // Can skip culling next time around if water body count stays at 0
            _CanSkipCulling = !hasWaterBodies;
        }

        internal void Render(Camera camera, CommandBuffer buffer, Material material = null, int pass = 0, bool culled = false, MaterialPropertyBlock mpb = null)
        {
            var noMaterial = material == null;

            if (noMaterial && Material == null)
            {
                return;
            }

            if (IsQuadMesh)
            {
                buffer.DrawMesh(Helpers.QuadMesh, Matrix4x4.TRS(_Water.Position, Quaternion.Euler(90f, 0, 0), new(10000, 10000, 1)), noMaterial ? Material : material, 0, shaderPass: pass, mpb);
                return;
            }

            // Spends approximately 0.2-0.3ms here on 2018 Dell XPS 15.
            foreach (var chunk in Chunks)
            {
                var renderer = chunk.Rend;

                // Can happen in edit mode.
                if (renderer == null)
                {
                    continue;
                }

                if (!chunk._Visible)
                {
                    continue;
                }

                if (culled && chunk._Culled)
                {
                    continue;
                }

                // Make sure properties are bound for this frame.
                if (!chunk._WaterDataHasBeenBound)
                {
                    chunk.Bind();
                }

                if (noMaterial)
                {
                    material = renderer.sharedMaterial;
                }

                buffer.DrawRenderer(renderer, material, submeshIndex: 0, pass);
            }
        }

        internal void RenderAdditionalChunks(CommandBuffer commands)
        {
            if (WaterBody.WaterBodies.Count <= 0)
            {
                return;
            }

            var rendered = false;
            var wrapper = new PropertyWrapperBuffer(commands);

            if (RenderPipelineHelper.IsLegacy)
            {
                SetUpDraw(commands);
            }

            foreach (var body in WaterBody.WaterBodies)
            {
                if (!body.OverrideMaterials || body.AboveSurfaceMaterial == null || !body.Precise || body.Exclusion == WaterBodyExclusion.Exclude)
                {
                    continue;
                }

                var bounds = new Vector4
                (
                    body.AABB.min.x,
                    body.AABB.min.z,
                    body.AABB.max.x,
                    body.AABB.max.z
                );

                var bound = false;

                foreach (var chunk in Chunks)
                {
                    if (!chunk._Visible || chunk._Culled)
                    {
                        continue;
                    }

                    // We should be able to get away with unexpanded bounds.
                    if (chunk._UnexpandedBoundsXZ.Overlaps(body.AABB.RectXZ()))
                    {
                        if (!bound)
                        {
                            wrapper.SetBoolean(ShaderIDs.s_HasBoundsXZ, true);
                            wrapper.SetVector(ShaderIDs.s_BoundsXZ, bounds);
                        }

                        commands.DrawRenderer(chunk.Rend, body.AboveSurfaceMaterial, 0, _ForwardPass);
                        rendered = bound = true;
                    }
                }

                if (bound)
                {
                    body.RenderMaterialClip(_Water, commands);
                }
            }

            if (rendered)
            {
                wrapper.SetBoolean(ShaderIDs.s_HasBoundsXZ, false);
            }
        }

        void RenderMesh(Mesh mesh, Material material, Matrix4x4 matrix, Bounds bounds, MaterialPropertyBlock mpb = null, int priority = 0)
        {
            var hasTemplate = _RendererTemplate != null;

            Graphics.RenderMesh
            (
                new(material)
                {
                    motionVectorMode = MotionVectorGenerationMode.Camera,
                    material = material,
                    worldBounds = bounds,
                    layer = Layer,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    lightProbeUsage = hasTemplate ? _RendererTemplate.lightProbeUsage : LightProbeUsage.Off,
                    reflectionProbeUsage = hasTemplate ? _RendererTemplate.reflectionProbeUsage : ReflectionProbeUsage.BlendProbesAndSkybox,
                    renderingLayerMask = hasTemplate ? _RendererTemplate.renderingLayerMask : 1,
                    // Not required for BIRP but required for SRPs.
                    camera = _Water.IsMultipleViewpointMode ? _Water.CurrentCamera : null,
                    rendererPriority = priority,
                    matProps = mpb,
                },
                mesh,
                submeshIndex: 0,
                matrix
            );
        }
    }

    // API
    partial class SurfaceRenderer
    {
        bool GetEnabled()
        {
            return _Enabled && !_Water.IsRunningWithoutGraphics;
        }

        void SetEnabled(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled) return;
            if (_Enabled) Initialize(); else OnDestroy();
        }

        void SetLayer(int previous, int current)
        {
            if (previous == current) return;

            foreach (var chunk in Chunks)
            {
                chunk.gameObject.layer = current;
            }
        }

        bool GetCastShadows()
        {
            return _CastShadows;
        }

        void SetCastShadows(bool previous, bool current)
        {
            if (previous == current) return;

            foreach (var chunk in Chunks)
            {
                chunk.Rend.shadowCastingMode = current ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        void SetAllowRenderQueueSorting(bool previous, bool current)
        {
            if (previous == current) return;

            foreach (var chunk in Chunks)
            {
                chunk.Rend.sortingOrder = current ? chunk._SortingOrder : 0;
            }
        }
    }

    // Motion Vectors
    partial class SurfaceRenderer
    {
        // Mostly to update the motion vector material only once.
        bool _QueueMotionVectors;
        bool QueueMotionVectors => _QueueMotionVectors && IsChunkMesh;
        Matrix4x4[] _PreviousObjectToWorld;
        internal Matrix4x4[] PreviousObjectToWorld { get; private set; }

        bool ShouldRenderMotionVectors(Camera camera)
        {
            // Unity enables this when motion vectors are used - even for SRPs.
            if (!camera.depthTextureMode.HasFlag(DepthTextureMode.MotionVectors))
            {
                return false;
            }

            return true;
        }

        void LateUpdateMotionVectors()
        {
            _QueueMotionVectors = false;

            // Handled by Unity.
            if (RenderPipelineHelper.IsHighDefinition)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (!_Water.WriteMotionVectors)
            {
                return;
            }

            // This will not support WBs with material overrides, but mixing opaque and
            // transparent would be odd.
            if (!IsTransparent(Material))
            {
                return;
            }

            var pool = ArrayPool<Camera>.Shared;
            var cameras = pool.Rent(Camera.allCamerasCount);
            Camera.GetAllCameras(cameras);

            for (var i = 0; i < Camera.allCamerasCount; i++)
            {
                var camera = cameras[i];

                // Clear the array before we return it.
                cameras[i] = null;

                if (!ShouldRender(camera))
                {
                    continue;
                }

                if (!ShouldRenderMotionVectors(camera))
                {
                    continue;
                }

                _QueueMotionVectors = true;
            }

            pool.Return(cameras);
        }

        void UpdateMotionVectorsMaterial(Material surface, ref Material motion)
        {
            if (!QueueMotionVectors)
            {
                return;
            }

            if (motion == null || motion.shader != surface.shader)
            {
                Helpers.Destroy(ref motion);
                motion = CoreUtils.CreateEngineMaterial(surface.shader);

                // BIRP
                motion.SetShaderPassEnabled("ForwardBase", false);
                motion.SetShaderPassEnabled("ForwardAdd", false);
                motion.SetShaderPassEnabled("Deferred", false);

                // URP
                motion.SetShaderPassEnabled("UniversalForward", false);
                motion.SetShaderPassEnabled("UniversalGBuffer", false);
                motion.SetShaderPassEnabled("Universal2D", false);

                motion.SetShaderPassEnabled("ShadowCaster", false);
                motion.SetShaderPassEnabled("DepthOnly", false);
                motion.SetShaderPassEnabled("DepthNormals", false);
                motion.SetShaderPassEnabled("Meta", false);
                motion.SetShaderPassEnabled("SceneSelectionPass", false);
                motion.SetShaderPassEnabled("Picking", false);
                motion.SetShaderPassEnabled("MotionVectors", true);
            }

            motion.CopyMatchingPropertiesFromMaterial(surface);
            motion.renderQueue = (int)RenderQueue.Geometry;
            motion.SetOverrideTag("RenderType", "Opaque");
            motion.SetFloat(Crest.ShaderIDs.Unity.s_Surface, 0); // SurfaceType.Opaque
            motion.SetFloat(Crest.ShaderIDs.Unity.s_SrcBlend, 1);
            motion.SetFloat(Crest.ShaderIDs.Unity.s_DstBlend, 0);
            motion.SetFloat(ShaderIDs.s_BuiltShadowCasterZTest, 1); // ZTest Never
        }
    }

    partial class SurfaceRenderer
    {
        internal Dictionary<Camera, MaterialPropertyBlock[]> _PerCameraPerCascadeMPB = new();
        internal Dictionary<Camera, Vector4[]> _PerCameraNormalMapParameters = new();
        internal Dictionary<Camera, Matrix4x4[]> _PerCameraPreviousObjectToWorld = new();
        internal Dictionary<Camera, Material> _PerCameraCustomGeometryMaterial = new();

        void LoadCameraData(Camera camera)
        {
            if (_Water.IsSingleViewpointMode)
            {
                return;
            }

            if (!_PerCameraPerCascadeMPB.ContainsKey(camera))
            {
                PerCascadeMPB = new MaterialPropertyBlock[Lod.k_MaximumSlices];
                _PerCameraPerCascadeMPB.Add(camera, PerCascadeMPB);
                NormalMapParameters = new Vector4[Lod.k_MaximumSlices];
                _PerCameraNormalMapParameters.Add(camera, NormalMapParameters);
                PreviousObjectToWorld = new Matrix4x4[Chunks.Count];
                _PerCameraPreviousObjectToWorld.Add(camera, PreviousObjectToWorld);
                CustomGeometryMaterial = new(Material);
                _PerCameraCustomGeometryMaterial.Add(camera, CustomGeometryMaterial);
                InitializeProperties();
            }
            else
            {
                PerCascadeMPB = _PerCameraPerCascadeMPB[camera];
                NormalMapParameters = _PerCameraNormalMapParameters[camera];
                PreviousObjectToWorld = _PerCameraPreviousObjectToWorld[camera];
                CustomGeometryMaterial = _PerCameraCustomGeometryMaterial[camera];
            }
        }

        internal void RemoveCameraData(Camera camera)
        {
            if (_PerCameraPerCascadeMPB.ContainsKey(camera))
            {
                _PerCameraPerCascadeMPB.Remove(camera);
                _PerCameraNormalMapParameters.Remove(camera);
                _PerCameraPreviousObjectToWorld.Remove(camera);

                var material = _PerCameraCustomGeometryMaterial[camera];
                Helpers.Destroy(ref material);
                _PerCameraCustomGeometryMaterial.Remove(camera);
            }
        }
    }

    // Quad
    partial class SurfaceRenderer
    {
        readonly Vector4[] _NormalMapParameters = new Vector4[Lod.k_MaximumSlices];
        Vector4[] NormalMapParameters { get; set; }

        void LateUpdateQuadMesh()
        {
            var scale = new Vector3(10000 * _Water.Scale, 10000 * _Water.Scale, 1);
            var bounds = Helpers.QuadMesh.bounds;
            bounds.Expand(scale);

            RenderMesh
            (
                Helpers.QuadMesh,
                Material,
                Matrix4x4.TRS(_Water.Position, Quaternion.Euler(90f, 0, 0), scale),
                Matrix4x4.TRS(_Water.Position, Quaternion.identity, scale).TransformBounds(bounds)
            );

            UpdateMaterial(_Material, ref _MotionVectorMaterial);
        }
    }

    // Off LOD Rendering
    partial class SurfaceRenderer
    {
        Material _CustomGeometryMaterial;
        Material CustomGeometryMaterial { get; set; }
        MaterialPropertyBlock _CustomGeometryMPB;
        MaterialPropertyBlock _CustomGeometryWithDataMPB;
        internal static readonly List<IRenderMaximumLOD> s_SurfaceInputs = new();

        internal void RenderMaximumLOD()
        {
            if (!_Water.RenderMaximumLOD)
            {
                return;
            }

            if (s_SurfaceInputs.Count == 0)
            {
                return;
            }

            var material = _Material;
            var rect = Rect.zero;

            // NOTE: happened in test harness.
            if (material == null)
            {
                return;
            }

            if (!IsQuadMesh)
            {
                CustomGeometryMaterial.CopyPropertiesFromMaterial(_Material);

                var scale = _Water.CascadeData.Current[_Water.LodLevels - 1].x;
                var texel = scale / _Water.LodResolution * 4f * _Water.GeometryDownSampleFactor;
                var width = 4f * scale;
                var snapped = _Water.Position.XZ() - new Vector2(Mathf.Repeat(_Water.Position.x, texel), Mathf.Repeat(_Water.Position.z, texel));
                rect = new Rect(snapped.x - width * 0.5f, snapped.y - width * 0.5f, width, width);

                var tileResolution = Mathf.Round(0.25f * _Water.LodResolution / _Water.GeometryDownSampleFactor);
                var offset = 1f / tileResolution * scale * 6f;
                rect.xMin += offset;
                rect.yMin += offset;
                rect.xMax -= offset;
                rect.yMax -= offset;

                CustomGeometryMaterial.SetBoolean(ShaderIDs.s_HasBoundsXZ, true);
                CustomGeometryMaterial.SetBoolean(ShaderIDs.s_InvertBoundsXZ, true);
                CustomGeometryMaterial.SetVector(ShaderIDs.s_BoundsXZ, new(rect.xMin, rect.yMin, rect.xMax, rect.yMax));

                var keywordCM = new LocalKeyword(CustomGeometryMaterial.shader, "_CREST_CUSTOM_MESH");

                if (keywordCM.isValid)
                {
                    CustomGeometryMaterial.SetKeyword(keywordCM, true);
                }

                var keywordOM = new LocalKeyword(CustomGeometryMaterial.shader, "_CREST_OFF_LOD_MESH");

                if (keywordOM.isValid)
                {
                    CustomGeometryMaterial.SetKeyword(keywordOM, true);
                }

                material = CustomGeometryMaterial;
            }

            foreach (var renderer in s_SurfaceInputs)
            {
                // Force for water level inputs with quad mesh.
                if (!(IsQuadMesh && renderer.HasLevel) && !renderer.Enabled)
                {
                    continue;
                }

                if (!IsQuadMesh && rect.Encapsulates(renderer.Rect))
                {
                    continue;
                }

                var hasData = renderer.HasData;

                var mpb = hasData ? _CustomGeometryWithDataMPB : _CustomGeometryMPB;

                var wrapper = new PropertyWrapperMPB(mpb);
                _Water.FoamLod.SetMaterialRenderingParameters(wrapper, material);
                _Water.AbsorptionLod.SetMaterialRenderingParameters(wrapper, material);
                _Water.ScatteringLod.SetMaterialRenderingParameters(wrapper, material);

                if (hasData)
                {
                    wrapper.SetVector(Crest.ShaderIDs.s_FeatherWidth, renderer.FeatherWidths);
                    wrapper.SetVector(LodInput.ShaderIDs.s_Weight, renderer.Weights);
                }

                RenderMesh
                (
                    renderer.Mesh,
                    material,
                    renderer.Transform.localToWorldMatrix,
                    renderer.Bounds,
                    mpb,
                    // Render before the extents, as it renders over it.
                    priority: -1
                );
            }
        }
    }

    // Obsolete
    partial class SurfaceRenderer
    {
        [@HideInInspector]
        [System.Obsolete("This setting no longer has any effect. Please use WaterRenderer.ExcludeEverythingByDefault instead.")]
        [@Label("Water Body Cull By Default")]
        [Tooltip("Whether 'Water Body' components will cull the water tiles.\n\nDisable if you want to use the 'Material Override' feature and still have an ocean.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _WaterBodyCulling = true;
    }
}
