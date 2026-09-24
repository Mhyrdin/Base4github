// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// Moves this transform.
    /// </summary>
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed partial class SimpleMotion : CustomBehaviour
    {
        [@DecoratedField]
        [SerializeField]
        bool _ResetOnDisable;

        [@DecoratedField]
        [SerializeField]
        bool _IsLocal;


        [@Heading("Translation")]

        [@DecoratedField]
        [SerializeField]
        Vector3 _Velocity;


        [@Heading("Rotation")]

        [@DecoratedField]
        [SerializeField]
        Vector3 _AngularVelocity;

        Vector3 _OldPosition;
        Quaternion _OldRotation;

        private protected override void OnEnable()
        {
            base.OnEnable();

            _OldPosition = Transform.position;
            _OldRotation = Transform.rotation;
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            if (_ResetOnDisable)
            {
                Transform.SetPositionAndRotation(_OldPosition, _OldRotation);
            }
        }

        void Update()
        {
            // Translation
            {
                Transform.position += (_IsLocal ? Transform.TransformDirection(_Velocity) : _Velocity) * Time.deltaTime;
            }

            // Rotation
            {
                Transform.rotation *= Quaternion.Euler(_AngularVelocity * Time.deltaTime);
            }
        }
    }
}
