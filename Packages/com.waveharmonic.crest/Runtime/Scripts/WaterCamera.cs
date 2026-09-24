// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Marks a camera to render water regardless of exclusions.
    /// </summary>
    /// <remarks>
    /// This is only necessary when using <see cref="WaterCameraExclusion"/>.
    /// </remarks>
    [@HelpURL(typeof(WaterCamera))]
    [AddComponentMenu(Constants.k_MenuPrefixScripts + "Water Camera")]
    public sealed partial class WaterCamera : ManagedBehaviour<WaterRenderer>
    {
        [Tooltip("The viewpoint which drives the water detail - the center of the LOD system.\n\nSetting this is optional. Defaults to the camera.")]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        Transform _Viewpoint;

        [Tooltip("Override the transform for data and repeated textures.\n\nThis can be used to give the appearance of moving the water without moving the entire system or camera.")]
        [@GenerateAPI]
        [@DecoratedField]
        [@SerializeField]
        Transform _ViewpointDataOverride;

        internal Vector3 _PreviousDataPosition;
    }
}
