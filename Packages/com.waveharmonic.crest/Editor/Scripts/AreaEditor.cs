// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
    [@CustomEditor(typeof(Area))]
    sealed partial class AreaEditor : Inspector
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var target = this.target as Area;

            EditorGUILayout.Space();

            // Helpers to quickly attach water inputs
            EditorGUILayout.Space();
            GUILayout.Label("Add Feature", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            FeatureButton<LevelLodInput>("Level", target.gameObject);
            FeatureButton<FlowLodInput>("Flow", target.gameObject);
            FeatureButton<FoamLodInput>("Foam", target.gameObject);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            FeatureButton<AbsorptionLodInput>("Absorption", target.gameObject);
            FeatureButton<ScatteringLodInput>("Scattering", target.gameObject);
            FeatureButton<ShadowLodInput>("Shadow", target.gameObject);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            FeatureButton<ShapeFFT>("Waves (FFT)", target.gameObject);
            FeatureButton<ShapeGerstner>("Waves (Gerstner)", target.gameObject);
            GUILayout.EndHorizontal();
        }

        internal static void FeatureButton<T>(string label, GameObject go) where T : Component
        {
            using (new EditorGUI.DisabledGroupScope(go.TryGetComponent<T>(out _)))
            {
                if (GUILayout.Button(label))
                {
                    Undo.AddComponent<T>(go);
                }
            }
        }
    }
}
