// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="DynamicWavesLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the simulation, such as
    /// ripples etc.
    /// </remarks>
    public sealed partial class DynamicWavesLodInput : LodInput
    {
        internal override LodInputMode DefaultMode => LodInputMode.Renderer;
    }
}
