// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class RenderPipelineTerrainPatcher : RenderPipelinePatcher
    {
        [@DecoratedField]
        [SerializeField]
        Material _Material;

        [@DecoratedField]
        [SerializeField]
        Material _MaterialHDRP;

        [@DecoratedField]
        [SerializeField]
        Material _MaterialURP;

        static readonly List<Terrain> s_Terrains = new();

#if UNITY_EDITOR
        private protected override void OnEnable()
        {
            base.OnEnable();
            OnActiveRenderPipelineTypeChanged();
        }

        protected override void OnActiveRenderPipelineTypeChanged()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

#if d_Unity_Terrain
            GetComponentsInChildren(s_Terrains);
            foreach (var terrain in s_Terrains)
            {
                terrain.materialTemplate = RenderPipelineHelper.RenderPipeline switch
                {
                    RenderPipeline.Legacy => _Material,
                    RenderPipeline.Universal => _MaterialURP,
                    RenderPipeline.HighDefinition => _MaterialHDRP,
                    _ => throw new System.NotImplementedException(),
                };
            }
            s_Terrains.Clear();
#endif // d_Unity_Terrain
        }
#endif
    }
}
