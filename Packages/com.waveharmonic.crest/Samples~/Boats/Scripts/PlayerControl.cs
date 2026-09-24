// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if ENABLE_INPUT_SYSTEM
#define d_InputSystem
#endif

using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable CS0414

namespace WaveHarmonic.Crest.Watercraft.Examples
{
    sealed class PlayerControl : Control
    {
#if d_InputSystem
        [@DecoratedField]

        [SerializeField]
        Key _DriveForward = Key.W;

        [@DecoratedField]
        [SerializeField]
        Key _DriveBackward = Key.S;

        [@DecoratedField]
        [SerializeField]
        Key _SteerLeftward = Key.A;

        [@DecoratedField]
        [SerializeField]
        Key _SteerRightward = Key.D;

        [@DecoratedField]
        [SerializeField]
        Key _FloatUpward = Key.E;

        [@DecoratedField]
        [SerializeField]
        Key _FloatDownward = Key.Q;
#else
        [@DecoratedField]
        [SerializeField]
        int _DriveForward;

        [@DecoratedField]
        [SerializeField]
        int _DriveBackward;

        [@DecoratedField]
        [SerializeField]
        int _SteerLeftward;

        [@DecoratedField]
        [SerializeField]
        int _SteerRightward;

        [@DecoratedField]
        [SerializeField]
        int _FloatUpward;

        [@DecoratedField]
        [SerializeField]
        int _FloatDownward;
#endif

#if d_InputSystem
        [HideInInspector]
#endif
        [Tooltip("The input axis name for throttle. See Project Settings > Input Manager.")]
        [@DecoratedField]
        [SerializeField]
        string _DriveInputAxis = "Vertical";

#if d_InputSystem
        [HideInInspector]
#endif
        [Tooltip("The input axis name for steering. See Project Settings > Input Manager.")]
        [@DecoratedField]
        [SerializeField]
        string _SteerInputAxis = "Horizontal";

#if d_InputSystem
        [HideInInspector]
#endif
        [@DecoratedField]
        [SerializeField]
        KeyCode _FloatUpwards = KeyCode.E;

#if d_InputSystem
        [HideInInspector]
#endif
        [@DecoratedField]
        [SerializeField]
        KeyCode _FloatDownwards = KeyCode.Q;

        [Tooltip("Whether to allow submerge control.")]
        [@DecoratedField]
        [SerializeField]
        bool _Submersible;

        public override Vector3 Input
        {
            get
            {
                if (!isActiveAndEnabled || !Application.isFocused) return Vector3.zero;

                var input = Vector3.zero;
#if d_InputSystem
                input.z += Keyboard.current[_DriveForward].isPressed ? 1f : 0f;
                input.z += Keyboard.current[_DriveBackward].isPressed ? -1f : 0f;
                input.x += Keyboard.current[_SteerLeftward].isPressed ? -1f : 0f;
                input.x += Keyboard.current[_SteerRightward].isPressed ? 1f : 0f;
                input.y += Keyboard.current[_FloatUpward].isPressed ? 1f : 0f;
                input.y += Keyboard.current[_FloatDownward].isPressed ? -1f : 0f;
#else
                input.z = UnityEngine.Input.GetAxis(_DriveInputAxis);
                input.x = UnityEngine.Input.GetAxis(_SteerInputAxis);
                input.y += UnityEngine.Input.GetKey(_FloatUpwards) ? 1f : 0f;
                input.y += UnityEngine.Input.GetKey(_FloatDownwards) ? -1f : 0f;
#endif

                // Steering towards same direction as forward when going backwards.
                if (input.z < 0f) input.x *= -1f;
                if (!_Submersible) input.y = 0f;
                return input;
            }
        }
    }
}
