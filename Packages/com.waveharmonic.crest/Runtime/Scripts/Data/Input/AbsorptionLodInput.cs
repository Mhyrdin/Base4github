// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="AbsorptionLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the scattering color.
    /// </remarks>
    public sealed partial class AbsorptionLodInput : LodInput
    {
        internal override LodInputMode DefaultMode => LodInputMode.Area;

        internal override void InferBlend()
        {
            base.InferBlend();
            _Blend = LodInputBlend.Alpha;
        }

        // Looks fine moving around.
        private protected override bool FollowHorizontalMotion => true;
    }
}
