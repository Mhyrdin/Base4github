// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Whether to exclude or include this area as water.
    /// </summary>
    [@GenerateDoc]
    public enum WaterBodyExclusion
    {
        /// <inheritdoc cref="Generated.WaterBodyExclusion.None" />
        [Tooltip("No exclusion applied.")]
        None,

        /// <inheritdoc cref="Generated.WaterBodyExclusion.Include" />
        [Tooltip("Include this area.")]
        Include,

        /// <inheritdoc cref="Generated.WaterBodyExclusion.Exclude" />
        [Tooltip("Exclude this area.")]
        Exclude,
    }

    /// <summary>
    /// What aspects of water does this area affect.
    /// </summary>
    [@GenerateDoc]
    [System.Flags]
    public enum WaterBodyAffects
    {
        /// <inheritdoc cref="Generated.WaterBodyAffects.Nothing" />
        [Tooltip("Affects nothing.")]
        Nothing = 0,

        /// <inheritdoc cref="Generated.WaterBodyAffects.Surface" />
        [Tooltip("Affects the water surface.")]
        Surface = 1 << 1,

        /// <inheritdoc cref="Generated.WaterBodyAffects.Volume" />
        [Tooltip("Affects the water volume.")]
        Volume = 1 << 2,

        /// <inheritdoc cref="Generated.WaterBodyAffects.Physics" />
        [Tooltip("Affects water physics (anything that calls HasWater).")]
        Physics = 1 << 3,

        /// <inheritdoc cref="Generated.WaterBodyAffects.Everything" />
        [Tooltip("Affects everything.")]
        Everything = ~0,
    }

    /// <summary>
    /// Demarcates an AABB area where water is present in the world.
    /// </summary>
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixScripts + "Local Override")]
    [@HelpURL(typeof(WaterBody))]
    public sealed partial class WaterBody : ManagedBehaviour<WaterRenderer>
    {
        [Tooltip("What aspects of water does this area affect.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField]
        [@SerializeField]
        WaterBodyAffects _Affects = WaterBodyAffects.Everything;

        [Tooltip("Whether to take the vertical size and position into account.\n\nThis will only affect the volume and physics. The surface will only consider the XZ values.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField]
        [@SerializeField]
        bool _Vertical;

        [Tooltip("Affects the surface more precisely.\n\nUses the clip surface to more precisely include/exclude the surface. All other aspects are already precise.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField]
        [@SerializeField]
        [FormerlySerializedAs("_Clip")]
        bool _Precise = true;

        [@Space(10)]

        [Tooltip("Whether to exclude or include this area as water.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField]
        [@SerializeField]
        WaterBodyExclusion _Exclusion;

        [Tooltip("Whether exclusion tests are conservative.\n\nThis is only useful for removing chunks for performance. It is conservative, as a water body must contain a chunk completely rather than overlap.")]
        [@Enable(nameof(_Exclusion), nameof(WaterBodyExclusion.Exclude))]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        bool _Conservative;


        [@Heading("Material Overrides")]

        [@Label("Enable")]
        [Tooltip("Whether this area overrides materials.\n\nIf enabled and no materials are set, then the global materials are used.")]
        [@Disable(nameof(_Exclusion), nameof(WaterBodyExclusion.Exclude))]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField]
        [@SerializeField]
        bool _OverrideMaterials;

        [Tooltip("Water chunks that overlap this waterbody area will be assigned this material.\n\nThis is useful for varying water appearance across different water bodies. If no override material is specified, the default material assigned to the WaterRenderer component will be used.")]
        [@Disable(nameof(_Exclusion), nameof(WaterBodyExclusion.Exclude))]
        [@AttachMaterialEditor]
        [@GenerateAPI]
        [FormerlySerializedAs("_Material")]
        [@MaterialField("Crest/Water", name: "Water", title: "Create Water Material"), SerializeField]
        Material _AboveSurfaceMaterial = null;

        [Tooltip("Overrides the property on the Water Renderer with the same name when the camera is inside the bounds.")]
        [@Disable(nameof(_Exclusion), nameof(WaterBodyExclusion.Exclude))]
        [@AttachMaterialEditor]
        [@GenerateAPI]
        [@MaterialField("Crest/Water", name: "Water (Below)", title: "Create Water Material", parent: nameof(_AboveSurfaceMaterial)), SerializeField]
        Material _BelowSurfaceMaterial;

        [Tooltip("Overrides the Water Renderer's volume material when the camera is inside the bounds.")]
        [@Disable(nameof(_Exclusion), nameof(WaterBodyExclusion.Exclude))]
        [@MaterialField(UnderwaterRenderer.k_ShaderNameEffect, name: "Underwater", title: "Create Underwater Material")]
        [@AttachMaterialEditor]
        [@GenerateAPI]
        [SerializeField]
        Material _VolumeMaterial;


        bool _RecalculateRect = true;
        bool _RecalculateBounds = true;

        internal float _BoundsArea;
        internal float _BoundsVolume;

        internal Material _MotionVectorMaterial;

        internal void RenderMaterialClip(WaterRenderer water, CommandBuffer commands)
        {
            if (!water.ClipLod.Enabled)
            {
                return;
            }

            var slices = water.ClipLod.OverlappingSlicesCount(Rect, out var offset);

            if (slices < 1)
            {
                return;
            }

            _ClipInput.ForceExclude = _ForceClipInput || OverrideMaterials;
            _ClipInput.Draw(water.ClipLod, commands, water.ClipLod.DataTexture, -1, 1f, slices, offset);
            _ClipInput.ForceExclude = false;
        }

        sealed class ClipInput : ILodInput
        {
            readonly WaterBody _Owner;
            readonly Transform _Transform;

            public bool Enabled => _Owner.Affects.HasFlag(WaterBodyAffects.Surface) && ((_Owner.Precise && _Owner.Exclusion != WaterBodyExclusion.None) || (_Owner._ForceClipInput && _Owner.Exclusion == WaterBodyExclusion.Include));
            public bool IsCompute => true;
            public int Pass => -1;

            public bool ForceExclude { get; set; }

            internal int _Queue;

            public int Queue => _Queue;
            public MonoBehaviour Component => _Owner;

            public Rect Rect => _Owner.Rect;

            public ClipInput(WaterBody owner)
            {
                _Owner = owner;
                _Transform = owner.Transform;
            }

            public void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slices = -1, int offset = 0)
            {
                var wrapper = new PropertyWrapperCompute(buffer, WaterResources.Instance.Compute._ClipPrimitive, 0);

                wrapper.SetMatrix(ShaderIDs.s_Matrix, _Transform.worldToLocalMatrix);

                // For culling.
                wrapper.SetVector(ShaderIDs.s_Position, _Transform.position);
                wrapper.SetFloat(ShaderIDs.s_Diameter, _Transform.lossyScale.Maximum());

                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveInverted, !ForceExclude && _Owner._Exclusion == WaterBodyExclusion.Include);
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveSphere, false);
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveCube, false);
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveRectangle, true);

                wrapper.SetTexture(ShaderIDs.s_Target, target);

                LodInput.Dispatch(wrapper, simulation, slices, offset, _Transform, Rect);
            }

            public float Filter(WaterRenderer water, int slice)
            {
                return 1f;
            }
        }

        /// <summary>
        /// List of all active WaterBody components in the scene.
        /// </summary>
        /// <remarks>
        /// This list never contains null entries - WaterBodies are removed from this list when disabled or destroyed.
        /// </remarks>
        internal static List<WaterBody> WaterBodies { get; } = new();
        internal static Utility.SortedList<int, WaterBody> SortedWaterBodies { get; } = new(Helpers.DuplicateComparison);
        int _SortedIndex;

        Bounds _Bounds;
        internal Bounds AABB
        {
            get
            {
                if (_RecalculateBounds)
                {
                    CalculateBounds();
                    _RecalculateBounds = false;
                }

                return _Bounds;
            }
        }

        Rect _Rect;
        Rect Rect
        {
            get
            {
                if (_RecalculateRect)
                {
                    _Rect = AABB.RectXZ();
                    _RecalculateRect = false;
                }

                return _Rect;
            }
        }

        internal Material AboveOrBelowSurfaceMaterial => _BelowSurfaceMaterial == null ? _AboveSurfaceMaterial : _BelowSurfaceMaterial;
        bool RequiresClipInput => Affects.HasFlag(WaterBodyAffects.Surface) && (_ForceClipInput || (Precise && (Exclusion != WaterBodyExclusion.None || OverrideMaterials)));

        ClipInput _ClipInput;

        bool _ForceClipInput;

        private protected override void Initialize()
        {
            base.Initialize();

            CalculateBounds();

            WaterBodies.Add(this);

            HandleClipInputRegistration();
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            WaterBodies.Remove(this);

            HandleClipInputRegistration(disable: true);
        }

        void CalculateBounds()
        {
            var bounds = new Bounds();
            bounds.center = Transform.position;
            bounds.Encapsulate(Transform.TransformPoint(Vector3.right / 2f + Vector3.forward / 2f));
            bounds.Encapsulate(Transform.TransformPoint(Vector3.right / 2f - Vector3.forward / 2f));
            bounds.Encapsulate(Transform.TransformPoint(Vector3.left / 2f + Vector3.forward / 2f));
            bounds.Encapsulate(Transform.TransformPoint(Vector3.left / 2f - Vector3.forward / 2f));

            _BoundsArea = bounds.size.x * bounds.size.z;

            if (_Vertical)
            {
                bounds.Encapsulate(Transform.TransformPoint(Vector3.up / 2f + Vector3.forward / 2f));
                bounds.Encapsulate(Transform.TransformPoint(Vector3.up / 2f - Vector3.forward / 2f));
                bounds.Encapsulate(Transform.TransformPoint(Vector3.down / 2f + Vector3.forward / 2f));
                bounds.Encapsulate(Transform.TransformPoint(Vector3.down / 2f - Vector3.forward / 2f));

                _BoundsVolume = _BoundsArea * bounds.size.y;
            }

            if (RequiresClipInput)
            {
                // Sort by size. We do not need full precision.
                var index = -Mathf.RoundToInt(_BoundsArea);

                if (SortedWaterBodies.Contains(this) && _SortedIndex != index)
                {
                    _SortedIndex = index;
                    SortedWaterBodies.Remove(this);
                    SortedWaterBodies.Add(_SortedIndex, this);
                    s_ClipSurfaceRegistrationNeedsUpdating = true;
                }
            }

            _Bounds = bounds;
        }

        void HandleClipInputRegistration(bool disable = false)
        {
            var registered = _ClipInput != null;
            var shouldBeRegistered = !disable && RequiresClipInput;

            if (registered != shouldBeRegistered)
            {
                SortedWaterBodies.Remove(this);

                if (shouldBeRegistered)
                {
                    _ClipInput = new(this);

                    _SortedIndex = -Mathf.RoundToInt(_BoundsArea);
                    SortedWaterBodies.Add(_SortedIndex, this);

                    ILodInput.Attach(_ClipInput, ClipLod.s_Inputs);
                }
                else
                {

                    ILodInput.Detach(_ClipInput, ClipLod.s_Inputs);

                    _ClipInput = null;
                }

                s_ClipSurfaceRegistrationNeedsUpdating = true;
            }
        }

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (Transform.hasChanged)
            {
                _RecalculateRect = _RecalculateBounds = true;
            }
        }

        private protected override System.Action<WaterRenderer> OnLateUpdateMethod => OnLateUpdate;
        void OnLateUpdate(WaterRenderer water)
        {
            Transform.hasChanged = false;

            // We need to use clip if we want to include the surface in the following case.
            var force = water.ClipLod.Enabled &&
                water.ClipLod.DefaultClippingState == DefaultClippingState.EverythingClipped &&
                _Exclusion == WaterBodyExclusion.Include;

            if (force != _ForceClipInput)
            {
                _ForceClipInput = force;
                HandleClipInputRegistration();
            }
        }
    }

    partial class WaterBody
    {
        internal static bool IsBetterMatch(WaterBody newBody, WaterBody oldBody)
        {
            if (oldBody == null)
            {
                return true;
            }

            var oldVertical = oldBody._Vertical;
            var newVertical = newBody._Vertical;

            // Prioritize vertical hits.
            if (oldVertical != newVertical)
            {
                return newVertical;
            }

            // Prioritize smallest hits.
            return newVertical ? newBody._BoundsVolume < oldBody._BoundsVolume : newBody._BoundsArea < oldBody._BoundsArea;
        }

        internal bool Contains(Vector3 position)
        {
            return _Vertical ? AABB.Contains(position) : AABB.ContainsXZ(position);
        }


        static bool s_ClipSurfaceRegistrationNeedsUpdating;
        internal static void UpdateClipSurfaceRegistration()
        {
            if (!s_ClipSurfaceRegistrationNeedsUpdating)
            {
                return;
            }

            var index = 0;

            foreach (var (size, wb) in SortedWaterBodies)
            {
                wb._ClipInput._Queue = -1000 + index++;

                // Calls remove.
                ILodInput.Attach(wb._ClipInput, ClipLod.s_Inputs);
            }

            s_ClipSurfaceRegistrationNeedsUpdating = false;
        }
    }

    // API getters/setters
    partial class WaterBody
    {
        void SetVertical(bool previous, bool current)
        {
            if (previous == current) return;
            _RecalculateBounds = true;
        }

        void SetPrecise(bool previous, bool current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            HandleClipInputRegistration();
        }

        void SetAffects(WaterBodyAffects previous, WaterBodyAffects current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            HandleClipInputRegistration();
        }

        void SetExclusion(WaterBodyExclusion previous, WaterBodyExclusion current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            HandleClipInputRegistration();
        }

        void SetOverrideMaterials(bool previous, bool current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            HandleClipInputRegistration();
        }
    }

#if UNITY_EDITOR
    partial class WaterBody
    {
        [UnityEditor.InitializeOnLoadMethod]
        static void OnLoad()
        {
            WaterBodies?.Clear();
            SortedWaterBodies?.Clear();
        }

        [@OnChange]
        void OnChange(string path, object previous)
        {
            switch (path)
            {
                case nameof(_Affects):
                    SetAffects((WaterBodyAffects)previous, _Affects);
                    break;
                case nameof(_Exclusion):
                    SetExclusion((WaterBodyExclusion)previous, _Exclusion);
                    break;
                case nameof(_Vertical):
                    SetVertical((bool)previous, _Vertical);
                    break;
                case nameof(_OverrideMaterials):
                    SetOverrideMaterials((bool)previous, _OverrideMaterials);
                    break;
                case nameof(_Precise):
                    SetPrecise((bool)previous, _Precise);
                    break;
            }
        }
    }
#endif

    // Obsolete
    partial class WaterBody
    {
        /// <summary>
        /// Makes sure this water body is not clipped.
        /// </summary>
        /// <remarks>
        /// If clipping is enabled and set to clip everywhere by default, this option will register this water body to ensure its area does not get clipped.
        /// </remarks>
        [System.Obsolete("Use Precise instead.")]
        public bool Clipped { get => _Precise; set => _Precise = value; }
    }
}
