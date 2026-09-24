// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    enum RenderPipeline
    {
        Legacy,
        HighDefinition,
        Universal,
    }

    sealed partial class RenderPipelineHelper
    {
        public static RenderPipeline RenderPipeline { get; private set; } = RenderPipelineAsset;

        public static RenderPipeline RenderPipelineAsset => GraphicsSettings.currentRenderPipeline switch
        {
#if d_UnityHDRP
            HDRenderPipelineAsset => RenderPipeline.HighDefinition,
#endif
#if d_UnityURP
            UniversalRenderPipelineAsset => RenderPipeline.Universal,
#endif
            _ => RenderPipeline.Legacy,
        };

        public static bool IsLegacy => RenderPipeline == RenderPipeline.Legacy;
        public static bool IsUniversal => RenderPipeline == RenderPipeline.Universal;
        public static bool IsHighDefinition => RenderPipeline == RenderPipeline.HighDefinition;

        public static bool s_SkipRenderPipelineChange;
    }

#if UNITY_EDITOR
    sealed partial class RenderPipelineHelper
    {
        static bool s_ChangingRenderPipelines;
        static bool s_Initialized;
        public static System.Action s_ActiveRenderPipelineTypeChanged;
        public static System.Action s_WillActiveRenderPipelineTypeChange;

        [UnityEditor.InitializeOnLoadMethod]
        static void Load()
        {
            s_ChangingRenderPipelines = false;
            s_Initialized = false;

            RenderPipelineManager.activeRenderPipelineTypeChanged -= OnActiveRenderPipelineTypeChanged;
            RenderPipelineManager.activeRenderPipelineTypeChanged += OnActiveRenderPipelineTypeChanged;

            // Problem is that since there is a gap between these two calls, a LateUpdate will
            // be called in between in bad state. Since this no longer runs in 6.4 anyway,
            // let's leave it.
            // UnityEditor.EditorApplication.update -= EditorUpdate;
            // UnityEditor.EditorApplication.update += EditorUpdate;
        }

        static void OnActiveRenderPipelineTypeChanged()
        {
            // In Unity 6 executed by Camera.Render which makes a mess of the test case.
            if (s_SkipRenderPipelineChange)
            {
                return;
            }

            // Fallback, as update does not work if changed via script.
            // Tried all the other events in RenderPipelineManager to no avail.
            if (RenderPipelineAsset != RenderPipeline)
            {
                s_WillActiveRenderPipelineTypeChange?.Invoke();
            }

            RenderPipeline = RenderPipelineAsset;
            s_ChangingRenderPipelines = false;
            s_ActiveRenderPipelineTypeChanged?.Invoke();
        }

        internal static void EditorUpdate()
        {
            if (!s_Initialized)
            {
                // This can be false on load.
                if (RenderPipelineManager.pipelineSwitchCompleted)
                {
                    s_Initialized = true;
                }
                else
                {
                    return;
                }
            }

            if (!s_ChangingRenderPipelines && !RenderPipelineManager.pipelineSwitchCompleted)
            {
                s_ChangingRenderPipelines = true;
                s_WillActiveRenderPipelineTypeChange?.Invoke();
                RenderPipeline = RenderPipelineAsset;
            }
        }
    }
#endif
}
