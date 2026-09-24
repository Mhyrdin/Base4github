// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// The default state for clipping.
    /// </summary>
    [@GenerateDoc]
    public enum DefaultClippingState
    {
        /// <inheritdoc cref="Generated.DefaultClippingState.NothingClipped"/>
        [Tooltip("By default, nothing is clipped. Use clip inputs to remove water.")]
        NothingClipped,

        /// <inheritdoc cref="Generated.DefaultClippingState.EverythingClipped"/>
        [Tooltip("By default, everything is clipped. Use clip inputs to add water.")]
        EverythingClipped,
    }

    /// <summary>
    /// Drives water surface clipping (carving holes).
    /// </summary>
    /// <remarks>
    /// 0-1 values, surface clipped when > 0.5.
    /// </remarks>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public sealed partial class ClipLod : Lod
    {
        [@Space(10)]

        [Tooltip("The default clipping behavior.\n\nWhether to clip nothing by default (and clip inputs remove patches of surface), or to clip everything by default (and clip inputs add patches of surface).")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal DefaultClippingState _DefaultClippingState = DefaultClippingState.NothingClipped;

        [Tooltip("Whether to include terrain holes automatically.\n\nWater will be clipped where terrain holes are.")]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        internal bool _IncludeTerrainHoles;

        internal static readonly Color s_GizmoColor = new(0f, 1f, 1f, 0.5f);

        internal override string ID => "Clip";
        internal override string Name => "Clip Surface";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => _DefaultClippingState == DefaultClippingState.EverythingClipped ? Color.white : Color.black;
        private protected override bool NeedToReadWriteTextureData => true;
        private protected override bool RequiresClearBorder => true;
        internal override bool SkipEndOfFrame => true;
        private protected override bool CanBeSampledOnce => true;
        private protected override bool CanRenderToOneSlice => true;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            // The clip values only really need 8bits (unless using signed distance).
            LodTextureFormatMode.Performance => GraphicsFormat.R8_UNorm,
            LodTextureFormatMode.Precision => GraphicsFormat.R16_UNorm,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        internal ClipLod()
        {
            _TextureFormat = GraphicsFormat.R8_UNorm;
        }

        internal static readonly SortedList<int, ILodInput> s_Inputs = new(Helpers.DuplicateComparison);
        private protected override SortedList<int, ILodInput> Inputs => s_Inputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            s_Inputs.Clear();
        }

        void SetDefaultClippingState(DefaultClippingState previous, DefaultClippingState current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled || !Enabled) return;

            // Change default clipping state.
            _TargetsToClear = true;
        }

#if UNITY_EDITOR
        [@OnChange]
        private protected override void OnChange(string path, object previous)
        {
            base.OnChange(path, previous);

            switch (path)
            {
                case nameof(_DefaultClippingState):
                    SetDefaultClippingState((DefaultClippingState)previous, _DefaultClippingState);
                    break;
            }
        }
#endif

#if d_Unity_Terrain
        TerrainClipInput _TerrainClipInput;

        internal override void Enable()
        {
            base.Enable();

            if (Enabled)
            {
                _TerrainClipInput ??= new(this);
                Inputs.Add(_TerrainClipInput.Queue, _TerrainClipInput);
            }
        }

        internal override void Disable()
        {
            base.Disable();

            Inputs.Remove(_TerrainClipInput);
        }

        sealed class TerrainClipInput : ILodInput
        {
            public bool Enabled => _ClipLod._IncludeTerrainHoles;
            public bool IsCompute => true;
            public int Queue => int.MinValue;
            public int Pass => -1;

            // We could if we had an input per terrain.
            public Rect Rect => Rect.zero;

            public MonoBehaviour Component => null;
            public float Filter(WaterRenderer water, int slice) => 1f;

            readonly ClipLod _ClipLod;
            readonly System.Collections.Generic.List<Terrain> _Terrains = new();

            public TerrainClipInput(ClipLod lod)
            {
                _ClipLod = lod;
            }

            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slices = -1, int offset = 0)
            {
                var resources = WaterResources.Instance;
                var wrapper = new PropertyWrapperCompute(buffer, resources.Compute._ClipTexture, 0);

                var threads = lod.Resolution / k_ThreadGroupSize;

                wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);
                wrapper.SetVector(Crest.ShaderIDs.s_TextureRotation, new(0, 1));
                wrapper.SetVector(Crest.ShaderIDs.s_Multiplier, Vector4.one);
                wrapper.SetBoolean(Crest.ShaderIDs.s_InvertSource, true);
                wrapper.SetInteger(ShaderIDs.s_LodOffset, offset);

                // Cannot because rect is zero.
                wrapper.SetBoolean(LodInput.ShaderIDs.s_TargetRegion, false);

                Terrain.GetActiveTerrains(_Terrains);
                foreach (var terrain in _Terrains)
                {
                    var data = terrain.terrainData;
                    if (data == null) continue;
                    var size = data.size;
                    var position = terrain.GetPosition();

                    wrapper.SetVector(Crest.ShaderIDs.s_TexturePosition, position.XZ() + (size.XZ() * 0.5f));
                    wrapper.SetVector(Crest.ShaderIDs.s_TextureSize, size.XZ());
                    wrapper.SetTexture(Crest.ShaderIDs.s_Texture, data.holesTexture);
                    wrapper.Dispatch(threads, threads, slices);
                }
                _Terrains.Clear();
            }
        }
#endif // d_Unity_Terrain
    }
}
