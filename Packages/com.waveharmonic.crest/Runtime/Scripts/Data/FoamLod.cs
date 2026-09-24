// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A persistent foam simulation that moves around with a displacement LOD. The input is fully combined water surface shape.
    /// </summary>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public sealed partial class FoamLod : PersistentLod
    {
        [Tooltip("Prewarms the simulation on load and teleports.\n\nResults are only an approximation.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _Prewarm = true;

        [Tooltip("Settings for fine tuning this simulation.")]
        [@Embedded]
        [@GenerateAPI(Getter.Custom)]
        [SerializeField]
        FoamLodSettings _Settings;

        static new class ShaderIDs
        {
            public static readonly int s_MinimumWavesSlice = Shader.PropertyToID("_Crest_MinimumWavesSlice");
            public static readonly int s_FoamMaximum = Shader.PropertyToID("_Crest_FoamMaximum");
            public static readonly int s_FoamFadeRate = Shader.PropertyToID("_Crest_FoamFadeRate");
            public static readonly int s_WaveFoamStrength = Shader.PropertyToID("_Crest_WaveFoamStrength");
            public static readonly int s_WaveFoamCoverage = Shader.PropertyToID("_Crest_WaveFoamCoverage");
            public static readonly int s_ShorelineFoam = Shader.PropertyToID("_Crest_ShorelineFoam");
            public static readonly int s_ShorelineFoamBlend = Shader.PropertyToID("_Crest_ShorelineFoamBlend");
            public static readonly int s_ShorelineFoamMaxDepth = Shader.PropertyToID("_Crest_ShorelineFoamMaxDepth");
            public static readonly int s_ShorelineFoamStrength = Shader.PropertyToID("_Crest_ShorelineFoamStrength");
            public static readonly int s_FoamNegativeDepthPriming = Shader.PropertyToID("_Crest_FoamNegativeDepthPriming");
        }

        internal static readonly Color s_GizmoColor = new(1f, 1f, 1f, 0.5f);

        internal override string ID => "Foam";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.black;
        private protected override ComputeShader SimulationShader => WaterResources.Instance.Compute._UpdateFoam;

        // Prewarm simulation for first frame or teleporting. It will not be the same
        // results as running the simulation for multiple frames - but good enough.
        private protected override bool RequiresPrewarming => Prewarm;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Performance => GraphicsFormat.R16_SFloat,
            LodTextureFormatMode.Precision => GraphicsFormat.R32_SFloat,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        private protected override void SetAdditionalSimulationParameters(PropertyWrapperCompute properties)
        {
            base.SetAdditionalSimulationParameters(properties);

            properties.SetFloat(ShaderIDs.s_FoamFadeRate, Settings._FoamFadeRate);
            properties.SetFloat(ShaderIDs.s_WaveFoamStrength, Settings._WaveFoamStrength);
            properties.SetFloat(ShaderIDs.s_WaveFoamCoverage, Settings._WaveFoamCoverage);
            properties.SetBoolean(ShaderIDs.s_ShorelineFoam, Settings._ShorelineFoamEnabled);
            properties.SetFloat(ShaderIDs.s_ShorelineFoamMaxDepth, Settings._ShorelineFoamMaximumDepth);
            properties.SetFloat(ShaderIDs.s_ShorelineFoamStrength, Settings._ShorelineFoamStrength);
            properties.SetFloat(ShaderIDs.s_FoamMaximum, Settings.Maximum);
            properties.SetFloat(ShaderIDs.s_FoamNegativeDepthPriming, -Settings._ShorelineFoamPriming);
            properties.SetInteger(ShaderIDs.s_MinimumWavesSlice, Mathf.Min(Settings.FilterWaves, Slices - 2));
        }

        internal void SetMaterialRenderingParameters(PropertyWrapperMPB wrapper, Material material)
        {
            // NOTE: happened in test harness.
            if (material == null)
            {
                return;
            }

            if (!material.HasBoolean(ShaderIDs.s_ShorelineFoam))
            {
                return;
            }

            if (Enabled && Settings._ShorelineFoamEnabled && material.GetFloat(ShaderIDs.s_ShorelineFoam) == 0f)
            {
                wrapper.SetBoolean(ShaderIDs.s_ShorelineFoamBlend, true);
                wrapper.SetBoolean(ShaderIDs.s_ShorelineFoam, true);
                wrapper.SetFloat(ShaderIDs.s_ShorelineFoamMaxDepth, Settings.ShorelineFoamMaximumDepth);
                wrapper.SetFloat(ShaderIDs.s_ShorelineFoamStrength, Settings.ShorelineFoamStrength * Mathf.Max(1f / _SimulationFrequency, 1f / Settings.FoamFadeRate));
            }
            else
            {
                // TODO: We could use Clear instead.
                wrapper.SetBoolean(ShaderIDs.s_ShorelineFoamBlend, false);
                wrapper.SetFloat(ShaderIDs.s_ShorelineFoam, material.GetFloat(ShaderIDs.s_ShorelineFoam));
                wrapper.SetFloat(ShaderIDs.s_ShorelineFoamMaxDepth, material.GetFloat(ShaderIDs.s_ShorelineFoamMaxDepth));
                wrapper.SetFloat(ShaderIDs.s_ShorelineFoamStrength, material.GetFloat(ShaderIDs.s_ShorelineFoamStrength));
            }
        }

        internal FoamLod()
        {
            _Enabled = true;
            _TextureFormat = GraphicsFormat.R16_SFloat;
            _SimulationFrequency = 30;
        }

        internal static readonly SortedList<int, ILodInput> s_Inputs = new(Helpers.DuplicateComparison);
        private protected override SortedList<int, ILodInput> Inputs => s_Inputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            s_Inputs.Clear();
        }
    }
}
