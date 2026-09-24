// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="FoamLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the foam simulation, such as
    /// depositing foam on the surface.
    /// </remarks>
    [@FilterEnum(nameof(_Blend), Filtered.Mode.Include, (int)LodInputBlend.Additive, (int)LodInputBlend.Maximum)]
    public sealed partial class FoamLodInput : LodInput
    {
        internal override LodInputMode DefaultMode => LodInputMode.Area;

        internal override void InferBlend()
        {
            base.InferBlend();

            if (_Mode is LodInputMode.Paint)
            {
                _Blend = LodInputBlend.Maximum;
            }
        }
    }
}
