// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Events;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [@ExecuteDuringEditMode]
    sealed partial class SendUnityEvent : CustomBehaviour
    {
        [@DecoratedField]
        [SerializeField]
        float _ExecuteUpdateEvery;

        [@DecoratedField]
        [SerializeField]
        float _StopExecutingUpdateAfter = Mathf.Infinity;

        [@DecoratedField]
        [SerializeField]
        UnityEvent _OnEnable = new();

        [@DecoratedField]
        [SerializeField]
        UnityEvent _OnDisable = new();

        [@DecoratedField]
        [SerializeField]
        UnityEvent<float> _OnUpdate = new();

        [@DecoratedField]
        [SerializeField]
        UnityEvent _OnLegacyRenderPipeline = new();

        [@DecoratedField]
        [SerializeField]
        UnityEvent _OnHighDefinitionPipeline = new();

        [@DecoratedField]
        [SerializeField]
        UnityEvent _OnUniversalRenderPipeline = new();

        float _TimeSinceEnabled;
        float _LastUpdateTime;

        private protected override void OnEnable()
        {
            base.OnEnable();

            _TimeSinceEnabled = 0f;
            _OnEnable.Invoke();

            if (RenderPipelineHelper.IsHighDefinition)
            {
                _OnHighDefinitionPipeline?.Invoke();
            }
            else if (RenderPipelineHelper.IsUniversal)
            {
                _OnUniversalRenderPipeline?.Invoke();
            }
            else
            {
                _OnLegacyRenderPipeline?.Invoke();
            }
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            _OnDisable.Invoke();
        }

        void Update()
        {
            _TimeSinceEnabled += Time.deltaTime;
            _LastUpdateTime += Time.deltaTime;

            if (_LastUpdateTime < _ExecuteUpdateEvery)
            {
                return;
            }

            _LastUpdateTime = 0;

            if (_TimeSinceEnabled > _StopExecutingUpdateAfter)
            {
                return;
            }

            _OnUpdate.Invoke(_TimeSinceEnabled);
        }
    }
}
