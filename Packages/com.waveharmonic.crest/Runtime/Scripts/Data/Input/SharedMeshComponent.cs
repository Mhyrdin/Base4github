// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    interface IRenderMaximumLOD
    {
        bool Enabled { get; }
        Vector4 FeatherWidths { get; }
        Vector4 Weights { get; }
        Rect Rect { get; }
        Bounds Bounds { get; }
        Transform Transform { get; }
        bool HasData { get; }
        bool HasLevel { get; }
        Mesh Mesh { get; }
    }

    /// <summary>
    /// Shared input that can fall back to a mesh.
    /// </summary>
    public abstract partial class SharedMeshComponent : ManagedBehaviour<WaterRenderer>
    {
        [Tooltip("Render this input directly where there is no data.\n\nWithout this enabled, inputs will not be visible outside the LOD system where there is no data. This will render the mesh directly with the water surface material.\n\nSupports water level, flow, absorption and scattering.")]
        [@Order(Order.Placement.Below)]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField]
        [@SerializeField]
        bool _RenderMaximumLOD = true;

        internal Mesh _Mesh;
        bool _IsDirty;

        internal LevelLodInput _LevelLodInput;
        internal FlowLodInput _FlowLodInput;
        internal AbsorptionLodInput _AbsorptionLodInput;
        internal ScatteringLodInput _ScatteringLodInput;

        internal bool _HasAbsorption;
        internal bool _HasScattering;
        internal bool _HasFlow;
        internal bool _HasLevel;

        private protected bool ShouldRenderMaximumLOD { get; private set; }
        private protected virtual bool AlwaysNeedsCulling => false;

        // Transform changed or water level added/removed
        internal abstract void RecalculateCulling();
        private protected abstract LodInputMode InputMode { get; }

        Vector4 _FeatherWidths;
        Vector4 _Weights;

        bool _RecalculateCulling;
        internal Rect Rect { get; private protected set; }
        internal Bounds Bounds { get; private protected set; }

        private protected override void Initialize()
        {
            base.Initialize();

            SetRenderMaximumLOD(!_RenderMaximumLOD, _RenderMaximumLOD);
        }

        private protected override void Disable()
        {
            base.Disable();

            SurfaceRenderer.s_SurfaceInputs.Remove(this);
        }

        private protected override void OnDestroy()
        {
            base.OnDestroy();
            Helpers.Destroy(ref _Mesh);
            _IsDirty = false;
        }

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        private protected virtual void OnUpdate(WaterRenderer water)
        {
            if (Transform.hasChanged)
            {
                _RecalculateCulling = true;
            }

            if (_IsDirty)
            {
                UpdateMesh(water);
            }

            if (ShouldRenderMaximumLOD)
            {
                _FeatherWidths = Vector4.zero;
                if (_HasFlow) _FeatherWidths.x = _FlowLodInput.FeatherWidth;
                if (_HasAbsorption) _FeatherWidths.z = _AbsorptionLodInput.FeatherWidth;
                if (_HasScattering) _FeatherWidths.w = _ScatteringLodInput.FeatherWidth;

                _Weights = Vector4.zero;
                _Weights.x = _HasFlow ? _FlowLodInput.Weight : 0f;
                _Weights.z = _HasAbsorption ? _AbsorptionLodInput.Weight : 0f;
                _Weights.w = _HasScattering ? _ScatteringLodInput.Weight : 0f;
            }

            // Some use culling for input dispatch.
            if (AlwaysNeedsCulling || ShouldRenderMaximumLOD)
            {
                if (_RecalculateCulling)
                {
                    RecalculateCulling();
                    _RecalculateCulling = false;
                }
            }
        }

        internal void UpdateSharedMesh()
        {
            _IsDirty = true;
        }

        private protected virtual void UpdateMesh(WaterRenderer water)
        {
            ShouldRenderMaximumLOD = false;

            // Shared mesh is used for Maximum LOD rendering only.
            if (!_RenderMaximumLOD || !water.RenderMaximumLOD)
            {
                return;
            }

            var mode = InputMode;

            _HasFlow = water.FlowLod.Enabled && TryGetComponent(out _FlowLodInput) && _FlowLodInput.Enabled && _FlowLodInput.Mode == mode;
            _HasLevel = water.LevelLod.Enabled && TryGetComponent(out _LevelLodInput) && _LevelLodInput.Enabled && _LevelLodInput.Mode == mode;
            _HasAbsorption = water.AbsorptionLod.Enabled && TryGetComponent(out _AbsorptionLodInput) && _AbsorptionLodInput.Enabled && _AbsorptionLodInput.Mode == mode;
            _HasScattering = water.ScatteringLod.Enabled && TryGetComponent(out _ScatteringLodInput) && _ScatteringLodInput.Enabled && _ScatteringLodInput.Mode == mode;

            ShouldRenderMaximumLOD = _HasFlow || _HasLevel || _HasAbsorption || _HasScattering;

            _IsDirty = false;
        }
    }

    partial class SharedMeshComponent : IRenderMaximumLOD
    {
        bool IRenderMaximumLOD.Enabled => ShouldRenderMaximumLOD;
        bool IRenderMaximumLOD.HasLevel => _HasLevel;
        bool IRenderMaximumLOD.HasData => _HasFlow || _HasAbsorption || _HasScattering;
        Transform IRenderMaximumLOD.Transform => Transform;
        Mesh IRenderMaximumLOD.Mesh => _Mesh;
        Rect IRenderMaximumLOD.Rect => Rect;
        Bounds IRenderMaximumLOD.Bounds => Bounds;
        Vector4 IRenderMaximumLOD.Weights => _Weights;
        Vector4 IRenderMaximumLOD.FeatherWidths => _FeatherWidths;
    }

    partial class SharedMeshComponent
    {
        void SetRenderMaximumLOD(bool previous, bool current)
        {
            if (previous == current) return;
            SurfaceRenderer.s_SurfaceInputs.Remove(this);
            if (current) SurfaceRenderer.s_SurfaceInputs.Add(this);
        }
    }

#if UNITY_EDITOR
    partial class SharedMeshComponent
    {
        [@OnChange(skipIfInactive: false)]
        void OnChange(string path, object previous)
        {
            switch (path)
            {
                case nameof(_RenderMaximumLOD):
                    SetRenderMaximumLOD((bool)previous, _RenderMaximumLOD);
                    break;
            }
        }
    }
#endif
}
