// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    partial class WaterRenderer
    {
        /// <summary>
        /// Checks whether the position has water.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>Returns true if there is water at the given position.</returns>
        /// <remarks>
        /// <para>
        /// The purpose of this call is to check the position against local overrides
        /// (<see cref="WaterBody"/>) - whether or not they have added or removed water. If
        /// there are no local overrides, this will return true, as the global ocean is
        /// always present (unless it has been removed globally).
        /// </para>
        /// <para>
        /// Typically, it is only concerned with the XZ axis, but it will check the Y axis
        /// for overrides which also affect the Y axis.
        /// </para>
        /// </remarks>
        public bool HasWater(Vector3 position)
        {
            WaterBody current = null;
            var hasWater = !DefaultExcludes.HasFlag(WaterBodyAffects.Physics);

            foreach (var body in WaterBody.WaterBodies)
            {
                if (!body.Affects.HasFlag(WaterBodyAffects.Physics))
                {
                    continue;
                }

                if (body.Exclusion == WaterBodyExclusion.None)
                {
                    continue;
                }

                if (!body.Contains(position))
                {
                    continue;
                }

                if (!WaterBody.IsBetterMatch(body, current))
                {
                    continue;
                }

                current = body;
                hasWater = body.Exclusion == WaterBodyExclusion.Include;
            }

            return hasWater;
        }
    }

    // API backing methods.
    partial class WaterRenderer
    {
        void SetExtentsSizeMultiplier(float previous, float current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            Surface._Rebuild = true;
        }

        void SetGeometryDownSampleFactor(int previous, int current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            Surface._Rebuild = true;
        }
    }
}
