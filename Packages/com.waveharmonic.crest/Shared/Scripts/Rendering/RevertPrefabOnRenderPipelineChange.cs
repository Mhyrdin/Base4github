// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed partial class RevertPrefabOnRenderPipelineChange : RenderPipelinePatcher
    {
#if UNITY_EDITOR
        internal override bool DisableEventAssertions => true;
        private protected override bool SkipLifeCycle => true;

        bool _Reverted;

        private protected override void Start()
        {
            base.Start();
            if (!Application.isPlaying) RevertRenderPipelineChanges();
        }

        protected override void OnActiveRenderPipelineTypeChanged()
        {
            _Reverted = false;
            RevertRenderPipelineChanges();
        }

        void RevertRenderPipelineChanges()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_Reverted)
            {
                return;
            }

            foreach (var item in gameObject.GetComponents<Component>())
            {
                if (item is Transform) continue;
                if (item == null) continue; // Can happen if missing packages/scripts.
                if (!PrefabUtility.IsPartOfPrefabInstance(item)) continue;
                // NOTE: this will trigger scene refresh for our samples.
                PrefabUtility.RevertObjectOverride(item, InteractionMode.AutomatedAction);
            }

            _Reverted = true;
        }
#endif
    }
}
