// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest.Watercraft
{
    /// <summary>
    /// Constantly moves/turns.
    /// </summary>
    [@HelpURL(typeof(FixedControl))]
    [AddComponentMenu(Constants.k_MenuPrefixPhysics + "Watercraft Control (Constant)")]
    public sealed partial class FixedControl : Control
    {
        [@GenerateAPI]
        [@DecoratedField]
        [Tooltip("Constantly move."), SerializeField]
        float _Move = 0;

        [@GenerateAPI]
        [@DecoratedField]
        [Tooltip("Constantly turn."), SerializeField]
        float _Turn = 0;

        /// <inheritdoc/>
        public override Vector3 Input => isActiveAndEnabled ? new(_Turn, 0f, _Move) : Vector3.zero;
    }
}
