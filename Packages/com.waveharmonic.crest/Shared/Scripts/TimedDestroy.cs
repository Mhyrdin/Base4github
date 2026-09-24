// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// Simple utility script to destroy the game object after a set time.
    /// </summary>
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed partial class TimedDestroy : CustomBehaviour
    {
        [@DecoratedField]
        [SerializeField]
        float _LifeTime = 2.0f;

        // this seems to make motion stutter?
        // [SerializeField]
        // float _ScaleToOneDuration = 0.1f;

        [@DecoratedField]
        [SerializeField]
        float _ScaleToZeroDuration = 0.0f;

        Vector3 _Scale;
        float _BirthTime;

        private protected override void OnStart()
        {
            base.OnStart();

            _BirthTime = Time.time;
            _Scale = Transform.localScale;
        }

        void Update()
        {
            var age = Time.time - _BirthTime;

            if (age >= _LifeTime)
            {
                Helpers.DestroyGameObject(this);
            }
            else if (age > _LifeTime - _ScaleToZeroDuration)
            {
                Transform.localScale = _Scale * (1.0f - (age - (_LifeTime - _ScaleToZeroDuration)) / _ScaleToZeroDuration);
            }
            /*else if (age < _ScaleToOneDuration && _ScaleToOneDuration > 0.0f)
            {
                Transform.localScale = _Scale * age / _ScaleToOneDuration;
            }*/
            else
            {
                Transform.localScale = _Scale;
            }
        }
    }
}
