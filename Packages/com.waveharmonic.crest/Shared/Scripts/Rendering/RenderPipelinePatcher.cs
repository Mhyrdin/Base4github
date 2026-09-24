// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Editor
{
    [@ExecuteDuringEditMode]
    abstract partial class RenderPipelinePatcher : CustomBehaviour
    {
#if UNITY_EDITOR
        // Causes exceptions if not. RevertPrefabOnRenderPipelineChange seems to be the problem.
        private protected override bool SkipLifeCycle => false;

        private protected override void OnEnable()
        {
            base.OnEnable();

            RenderPipelineHelper.s_ActiveRenderPipelineTypeChanged -= OnActiveRenderPipelineTypeChanged;
            RenderPipelineHelper.s_ActiveRenderPipelineTypeChanged += OnActiveRenderPipelineTypeChanged;
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            RenderPipelineHelper.s_ActiveRenderPipelineTypeChanged -= OnActiveRenderPipelineTypeChanged;
        }

        protected abstract void OnActiveRenderPipelineTypeChanged();
#endif
    }
}
