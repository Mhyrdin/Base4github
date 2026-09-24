// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Debug draw crosses in an area around the GameObject on the water surface.
    /// </summary>
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixDebug + "Collision Area Visualizer")]
    sealed class CollisionAreaVisualizer : ManagedBehaviour<WaterRenderer>
    {
        [Tooltip(ICollisionProvider.k_LayerTooltip)]
        [@DecoratedField]
        [SerializeField]
        internal CollisionLayer _Layer;

        [@DecoratedField]
        [SerializeField]
        float _ObjectWidth = 0f;

        [@DecoratedField]
        [SerializeField]
        float _StepSize = 5f;

        [@DecoratedField]
        [SerializeField]
        int _Steps = 10;

        [@DecoratedField]
        [SerializeField]
        bool _UseDisplacements;

        [@DecoratedField]
        [SerializeField]
        bool _UseNormals;

        [@DecoratedField]
        [@SerializeField]
        bool _UseVelocity;

        [Tooltip("How many previous frames of velocity to store.\n\nUsed to mitigate velocity spikes.")]
        [@DecoratedField]
        [@SerializeField]
        int _VelocityPreviousFrameCount = 3;

        float[] _ResultHeights;
        Vector3[] _ResultDisplacements;
        Vector3[] _ResultNormals;
        BufferedData<Vector3[]> _ResultVelocities;
        Vector3[] _SamplePositions;

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (water.AnimatedWavesLod.Provider == null)
            {
                return;
            }

            if (_ResultHeights == null || _ResultHeights.Length != _Steps * _Steps)
            {
                _ResultHeights = new float[_Steps * _Steps];
            }
            if (_ResultDisplacements == null || _ResultDisplacements.Length != _Steps * _Steps)
            {
                _ResultDisplacements = new Vector3[_Steps * _Steps];
            }
            if (_ResultVelocities == null || _ResultVelocities.Current.Length != _Steps * _Steps || _ResultVelocities.Size != _VelocityPreviousFrameCount + 1)
            {
                _ResultVelocities = new(1 + _VelocityPreviousFrameCount, () => new Vector3[_Steps * _Steps]);
            }
            if (_ResultNormals == null || _ResultNormals.Length != _Steps * _Steps)
            {
                _ResultNormals = new Vector3[_Steps * _Steps];

                for (var i = 0; i < _ResultNormals.Length; i++)
                {
                    _ResultNormals[i] = Vector3.up;
                }
            }
            if (_SamplePositions == null || _SamplePositions.Length != _Steps * _Steps)
            {
                _SamplePositions = new Vector3[_Steps * _Steps];
            }

            var collProvider = water.AnimatedWavesLod.Provider;

            for (var i = 0; i < _Steps; i++)
            {
                for (var j = 0; j < _Steps; j++)
                {
                    _SamplePositions[j * _Steps + i] = new(((i + 0.5f) - _Steps / 2f) * _StepSize, 0f, ((j + 0.5f) - _Steps / 2f) * _StepSize);
                    _SamplePositions[j * _Steps + i].x += Transform.position.x;
                    _SamplePositions[j * _Steps + i].z += Transform.position.z;
                }
            }

            _ResultVelocities.Flip();

            var success = _UseDisplacements
                ? collProvider.RetrieveSucceeded(collProvider.Query(GetHashCode(), _ObjectWidth, _SamplePositions, _ResultDisplacements, _UseNormals ? _ResultNormals : null, _UseVelocity ? _ResultVelocities.Current : null, _Layer))
                : collProvider.RetrieveSucceeded(collProvider.Query(GetHashCode(), _ObjectWidth, _SamplePositions, _ResultHeights, _UseNormals ? _ResultNormals : null, _UseVelocity ? _ResultVelocities.Current : null, _Layer));

#if !UNITY_EDITOR
            // Gizmos handle this in editor.
            if (success)
            {
                Render(water, Debug.DrawLine);
            }
#endif
        }

        internal void Render(WaterRenderer water, DebugUtility.DrawLine draw)
        {
            if (_SamplePositions == null)
            {
                return;
            }

            for (var i = 0; i < _Steps; i++)
            {
                for (var j = 0; j < _Steps; j++)
                {
                    var result = _SamplePositions[j * _Steps + i];

                    if (_UseDisplacements)
                    {
                        result.y = water.SeaLevel;
                        result += _ResultDisplacements[j * _Steps + i];
                    }
                    else
                    {
                        result.y = _ResultHeights[j * _Steps + i];
                    }

                    var normal = _UseNormals ? _ResultNormals[j * _Steps + i] : Vector3.up;
                    DebugUtility.DrawCross(draw, result, normal, Mathf.Min(_StepSize / 4f, 1f), Color.green);

                    if (_UseVelocity)
                    {
                        var velocity = _ResultVelocities.Current[j * _Steps + i];

                        if (_VelocityPreviousFrameCount > 0)
                        {
                            for (var frame = 1; frame < _ResultVelocities.Size; frame++)
                            {
                                var lastVelocity = _ResultVelocities.Previous(frame)[j * _Steps + i];
                                velocity = velocity.sqrMagnitude <= lastVelocity.sqrMagnitude ? velocity : lastVelocity;
                            }
                        }

                        Debug.DrawLine(result, result + velocity, Color.cyan);
                    }
                }
            }
        }
    }
}
