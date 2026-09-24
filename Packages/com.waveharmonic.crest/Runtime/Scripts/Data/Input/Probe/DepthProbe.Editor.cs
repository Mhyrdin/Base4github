// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if UNITY_EDITOR

using UnityEngine;

namespace WaveHarmonic.Crest
{
    partial class DepthProbe
    {
        private protected override void OnValidate()
        {
            base.OnValidate();
            // OnChange has a bug (possibly with Unity) with LayerMask where it cannot detect changes.
            HashState(ref _CurrentStateHash);
        }

        void Update()
        {
            if (Transform.hasChanged)
            {
                HashState(ref _CurrentStateHash);
            }
        }

        [@OnChange]
        void OnChange(string propertyPath, object oldValue)
        {
#if CREST_DEBUG
            ILodInput.Detach(_Input, ClipLod.s_Inputs);
            if (_Debug._ShowSimulationDataInScene)
            {
                ILodInput.Attach(_Input, ClipLod.s_Inputs);
            }
#endif

            if (_Camera == null) return;
            _Camera.gameObject.hideFlags = _Debug._ShowHiddenObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave;
        }
    }
}

#endif
