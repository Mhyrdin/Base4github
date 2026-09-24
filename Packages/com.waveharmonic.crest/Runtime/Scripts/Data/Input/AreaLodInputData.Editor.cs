// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if UNITY_EDITOR

using System.Reflection;
using UnityEngine;

namespace WaveHarmonic.Crest
{
    partial class AreaLodInputData
    {
        [@OnChange(skipIfInactive: false)]
        internal override void OnChange(string path, object previous)
        {
            var field = GetType().GetField(path, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (field?.GetCustomAttribute<GenerateAPI>()?._Setter == Setter.Dirty)
            {
                SetDirty(previous, field.GetValue(this), path);
            }
        }

        internal override bool InferMode(Component component, ref LodInputMode mode)
        {
            if (component.TryGetComponent(out _Area))
            {
                mode = LodInputMode.Area;
                return true;
            }

            return false;
        }

        private protected virtual void SetDirty(object previous, object current, string name)
        {
            if (previous == current) return;
            _Area.UpdateSharedMesh();
        }
    }

    partial class AbsorptionAreaLodInputData
    {
        private protected override void SetDirty(object previous, object current, string name)
        {
            if (previous == current) return;
            if (name == nameof(_Color)) _Absorption = WaterRenderer.UpdateAbsorptionFromColor(_Color);
            base.SetDirty(previous, current, name);
        }
    }
}

#endif
