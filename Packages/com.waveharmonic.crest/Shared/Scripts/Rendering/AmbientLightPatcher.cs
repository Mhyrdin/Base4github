// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [@ExecuteDuringEditMode]
    sealed partial class AmbientLightPatcher : RenderPipelinePatcher
    {
#if UNITY_EDITOR

        private protected override void OnEnable()
        {
            base.OnEnable();
            InitializeAmbientLighting();
        }

        void Update() => InitializeAmbientLighting();

        bool _Baked;
        bool _Force;

        void InitializeAmbientLighting()
        {
            if (_Baked)
            {
                return;
            }

            if (UnityEditor.Lightmapping.isRunning)
            {
                Log("Lightmapping.isRunning");
                return;
            }

            if (Application.isPlaying)
            {
                return;
            }

            // Throws a warning.
            if (UnityEditor.ShaderUtil.anythingCompiling)
            {
                Log("ShaderUtil.anythingCompiling");
                return;
            }

            // Only do skyboxes for now.
            if (RenderSettings.ambientMode != AmbientMode.Skybox)
            {
                Log("NotSkybox");
                return;
            }

            if (UnityEditor.Lightmapping.isRunning)
            {
                Log("Lightmapping.isRunning");
                return;
            }

#if UNITY_6000_3_OR_NEWER
            // NOTE: API introduced in 6.2 and backported to 6000.0.22f1.
            if (!_Force && UnityEditor.Lightmapping.bakeOnSceneLoad == UnityEditor.Lightmapping.BakeOnSceneLoadMode.IfMissingLightingData)
            {
                return;
            }
#endif

            var bake = true;
            var probe = RenderSettings.ambientProbe;

            // Check if the ambient probe is effectively empty.
            if (!_Force)
            {
                for (var i = 0; i < 9; i++)
                {
                    if (probe[0, i] != 0 || probe[1, i] != 0 || probe[2, i] != 0)
                    {
                        bake = false;
                        break;
                    }
                }
            }

            if (!bake)
            {
                return;
            }

#if !UNITY_6000_0_OR_NEWER
            var oldWorkflow = UnityEditor.Lightmapping.giWorkflowMode;
            UnityEditor.Lightmapping.giWorkflowMode = UnityEditor.Lightmapping.GIWorkflowMode.OnDemand;
#endif
            // Only attempt to bake once per scene load.
            _Baked = UnityEditor.Lightmapping.BakeAsync();
#if !UNITY_6000_0_OR_NEWER
            UnityEditor.Lightmapping.giWorkflowMode = oldWorkflow;
#endif

#if CREST_DEBUG
            Debug.Log($"Crest: Baked scene lighting!");
#endif

            if (!_Baked)
            {
                Debug.LogWarning($"Crest: Could not generate scene lighting. Lighting will look incorrect.");
            }

            _Force = false;
        }

        protected override void OnActiveRenderPipelineTypeChanged()
        {
            _Baked = false;
            _Force = true;
        }
#endif
    }
}
