// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Data storage for for the Geometry input mode.
    /// </summary>
    [@System.Serializable]
    public abstract partial class AreaLodInputData : LodInputData
    {
        [Tooltip("The <i>Crest Area</i> to use with this input.")]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        private protected Area _Area;

        internal override bool IsEnabled => _Area != null && AreaShader != null;
        internal override Mesh InternalMesh => _Area._Mesh;
        internal abstract Vector4 Value { get; }
        private protected abstract ComputeShader AreaShader { get; }
        private protected virtual Vector4 GetDrawValue(WaterRenderer water, Vector2 rotation) => Value;

        internal override void Draw(Lod lod, Component component, CommandBuffer buffer, RenderTargetIdentifier target, int slices, int offset)
        {
            var transform = _Input.Transform;
            var wrapper = new PropertyWrapperCompute(buffer, AreaShader, 0);
            var rotation = new Vector2(transform.localToWorldMatrix.m20, transform.localToWorldMatrix.m00).normalized;
            wrapper.SetVector(ShaderIDs.s_TextureSize, transform.lossyScale.XZ());
            wrapper.SetVector(ShaderIDs.s_TexturePosition, transform.position.XZ());
            wrapper.SetVector(ShaderIDs.s_TextureRotation, rotation);
            wrapper.SetInteger(ShaderIDs.s_Blend, (int)_Input.Blend);
            wrapper.SetTexture(ShaderIDs.s_Target, target);
            wrapper.SetFloat(ShaderIDs.s_FeatherWidth, _Input.FeatherWidth);
            wrapper.SetVector(ShaderIDs.s_Value, GetDrawValue(lod.Water, rotation));

            LodInput.Dispatch(wrapper, lod, slices, offset, transform, Rect);
        }

        internal override void OnEnable()
        {
            if (_Area != null)
            {
                _Area.UpdateSharedMesh();
            }
        }

        internal override void OnDisable()
        {
            if (_Area != null)
            {
                _Area.UpdateSharedMesh();
            }
        }

        internal override void OnDestroy()
        {
            // Empty.
        }

        internal override void RecalculateBounds()
        {
            _Bounds = _Area.Bounds;
        }

        internal override void RecalculateRect()
        {
            _Rect = _Area.Rect;
        }

        private protected virtual void SetDirty<I>(I previous, I current) where I : System.IEquatable<I>
        {
            if (EqualityComparer<I>.Default.Equals(previous, current)) return;
            _Area.UpdateSharedMesh();
        }
    }

    /// <inheritdoc/>
    public abstract partial class ColorAreaLodInputData : AreaLodInputData
    {
        [Tooltip("The water color.")]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField]
        [@SerializeField]
        internal Color _Color = Color.white;
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(AbsorptionLodInput), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class AbsorptionAreaLodInputData : ColorAreaLodInputData
    {
        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._AbsorptionArea;
        internal override Vector4 Value => _Absorption;

        Vector4 _Absorption = WaterRenderer.UpdateAbsorptionFromColor(AbsorptionLod.s_DefaultColor);

        /// <summary>
        /// Constructs a AbsorptionAreaLodInputData.
        /// </summary>
        public AbsorptionAreaLodInputData()
        {
            _Color = AbsorptionLod.s_DefaultColor;
        }

        internal override void OnEnable()
        {
            base.OnEnable();

            _Absorption = WaterRenderer.UpdateAbsorptionFromColor(_Color);
        }

        // NOTE: this will be an overhead if we ever add more dirty fields.
        private protected override void SetDirty<I>(I previous, I current)
        {
            if (EqualityComparer<I>.Default.Equals(previous, current)) return;
            _Absorption = WaterRenderer.UpdateAbsorptionFromColor(_Color);
            base.SetDirty(previous, current);
        }
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(ScatteringLodInput), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class ScatteringAreaLodInputData : ColorAreaLodInputData
    {
        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._ScatteringArea;
        internal override Vector4 Value => _Color.MaybeLinear();

        /// <summary>
        /// Constructs a ScatteringAreaLodInputData.
        /// </summary>
        public ScatteringAreaLodInputData()
        {
            _Color = ScatteringLod.s_DefaultColor;
        }
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(FlowLodInput), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class FlowAreaLodInputData : AreaLodInputData
    {
        [Tooltip("The speed of flow (m/s).")]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField]
        [@SerializeField]
        float _Speed = 1f;

        [Tooltip("The direction of flow in turns (0-1).")]
        [@GenerateAPI(Setter.Dirty)]
        [@Range(0, 1)]
        [@SerializeField]
        float _Direction;

        float DirectionRadians => _Direction * 6.283185f;

        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._FlowArea;
        internal override Vector4 Value => new(Mathf.Cos(DirectionRadians) * _Speed, Mathf.Sin(DirectionRadians) * _Speed);

        private protected override Vector4 GetDrawValue(WaterRenderer water, Vector2 rotation)
        {
            var value = Value;
            rotation = new(rotation.y, rotation.x);
            var flow = rotation * value.y - new Vector2(-rotation.y, rotation.x) * value.x;
            value.x = flow.x;
            value.y = flow.y;
            return value;
        }
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(FoamLodInput), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class FoamAreaLodInputData : AreaLodInputData
    {
        [@DecoratedField]
        [@SerializeField]
        float _Amount = 0.5f;

        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._FoamArea;
        internal override Vector4 Value => new(_Amount, 0, 0, 0);
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(LevelLodInput), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class LevelAreaLodInputData : AreaLodInputData
    {
        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._LevelArea;
        internal override Vector4 Value => new(_Input.Transform.position.y, 0, 0, 0);

        private protected override Vector4 GetDrawValue(WaterRenderer water, Vector2 rotation)
        {
            var value = Value;
            value.x -= water.SeaLevel;
            return value;
        }
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(ShadowLodInput), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class ShadowAreaLodInputData : AreaLodInputData
    {
        [@DecoratedField]
        [@SerializeField]
        Vector2 _Value;

        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._ShadowArea;
        internal override Vector4 Value => _Value;
    }

    /// <inheritdoc/>
    [@ForLodInput(typeof(ShapeWaves), LodInputMode.Area)]
    [@System.Serializable]
    public sealed partial class ShapeWavesAreaLodInputData : AreaLodInputData
    {
        private protected override ComputeShader AreaShader => WaterResources.Instance.Compute._ShapeWavesTransfer;
        internal override Vector4 Value => Vector4.zero;
    }
}
