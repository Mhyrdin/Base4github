// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

#if UNITY_EDITOR
using MonoBehaviour = WaveHarmonic.Crest.Internal.EditorBehaviour;
#else
using MonoBehaviour = UnityEngine.MonoBehaviour;
#endif

#pragma warning disable IDE0036 // Order modifiers

namespace WaveHarmonic.Crest.Internal
{
    /// <summary>
    /// Implements logic to smooth out Unity's wrinkles.
    /// </summary>
    public abstract partial class CustomBehaviour : MonoBehaviour
    {
        /// <inheritdoc cref="Component.transform" />
        public Transform Transform { get; private set; }

        // Not available in 2022.3:
        // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour-didStart.html
        bool _AfterStart;

#if UNITY_EDITOR
        override
#else
        virtual
#endif
        private protected void Awake()
        {
            Transform = transform;

#if UNITY_EDITOR
            base.Awake();
#endif
        }

        /// <summary>
        /// Unity's Start method. Make sure to call base if overridden.
        /// </summary>
#if UNITY_EDITOR
        override
#else
        virtual
#endif
        private protected void Start()
        {
            _AfterStart = true;

#if UNITY_EDITOR
            // Appears to only happen in tests.
            if (this == null) return;
            base.Start();
            if (!enabled) return;
#endif

            OnStart();
        }

        /// <summary>
        /// Called in OnEnable only after Start has ran.
        /// </summary>
        private protected virtual void Initialize()
        {

        }

        /// <summary>
        /// Replaces Start. Only called in the editor if passes validation.
        /// </summary>
        private protected virtual void OnStart()
        {
            Initialize();
        }

        /// <summary>
        /// Replaces OnDisable.
        /// </summary>
        private protected virtual void Disable()
        {

        }

        /// <summary>
        /// Unity's OnEnable method. Make sure to call base if overridden.
        /// </summary>
#if UNITY_EDITOR
        override
#else
        virtual
#endif
        private protected void OnEnable()
        {
            Transform = transform;

#if UNITY_EDITOR
            base.OnEnable();
#endif

            if (!_AfterStart) return;
            Initialize();
        }

        /// <summary>
        /// Unity's OnDisable method. Make sure to call base if overridden.
        /// </summary>
#if UNITY_EDITOR
        override
#else
        virtual
#endif
        private protected void OnDisable()
        {
            Disable();

#if UNITY_EDITOR
            base.OnDisable();
#endif
        }

#if UNITY_EDITOR
        override
#else
        virtual
#endif
        private protected void OnDestroy()
        {
#if UNITY_EDITOR
            base.OnDestroy();
#endif

            _AfterStart = false;
        }

        internal void Rebuild()
        {
            if (isActiveAndEnabled)
            {
                OnDisable();
            }

            OnDestroy();
            Awake();

            if (isActiveAndEnabled)
            {
                OnEnable();
                Start();
            }
        }
    }

    partial class CustomBehaviour : ISerializationCallbackReceiver
    {
#pragma warning disable 414
        [@SerializeField, @HideInInspector]
        private protected int _Version;
#pragma warning restore 414

        private protected virtual int Version => 0;

        private protected CustomBehaviour()
        {
            // Sets the default version. Overridden by serialized field above.
            _Version = Version;
        }

        private protected virtual void OnMigrate()
        {

        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (_Version < Version)
            {
                OnMigrate();
                _Version = Version;
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {

        }
    }

#if UNITY_EDITOR
    partial class CustomBehaviour
    {
        private protected override void OnValidate()
        {
            base.OnValidate();

            Transform = transform;
        }
    }
#endif
}
