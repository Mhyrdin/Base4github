// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// #define PROFILE_CONSTRUCTION

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    partial class SurfaceRenderer
    {
        // Keep references to meshes so they can be cleaned up later.
        readonly Mesh[] _Meshes = new Mesh[(int)Builder.PatchType.Count];

        /// <summary>
        /// Instantiates all the water geometry, as a set of tiles.
        /// </summary>
        static class Builder
        {
            // The comments below illustrate case when BASE_VERT_DENSITY = 2. The water mesh is built up from these patches. Rotational symmetry
            // is used where possible to eliminate combinations. The slim variants are used to eliminate overlap between patches.
            internal enum PatchType
            {
                /// <summary>
                /// Adds no skirt. Used in interior of highest detail LOD (0)
                ///
                ///    1 -------
                ///      |  |  |
                ///  z   -------
                ///      |  |  |
                ///    0 -------
                ///      0     1
                ///         x
                ///
                /// </summary>
                Interior,

                /// <summary>
                /// Adds a full skirt all of the way around a patch
                ///
                ///      -------------
                ///      |  |  |  |  |
                ///    1 -------------
                ///      |  |  |  |  |
                ///  z   -------------
                ///      |  |  |  |  |
                ///    0 -------------
                ///      |  |  |  |  |
                ///      -------------
                ///         0     1
                ///            x
                ///
                /// </summary>
                Fat,

                /// <summary>
                /// Adds a skirt on the right hand side of the patch
                ///
                ///    1 ----------
                ///      |  |  |  |
                ///  z   ----------
                ///      |  |  |  |
                ///    0 ----------
                ///      0     1
                ///         x
                ///
                /// </summary>
                FatX,

                /// <summary>
                /// Adds a skirt on the right hand side of the patch, removes skirt from top
                /// </summary>
                FatXSlimZ,

                /// <summary>
                /// Outer most side - this adds an extra skirt on the left hand side of the patch,
                /// which will point outwards and be extended to z far
                ///
                ///    1 --------------------------------------------------------------------------------------
                ///      |  |  |                                                                              |
                ///  z   --------------------------------------------------------------------------------------
                ///      |  |  |                                                                              |
                ///    0 --------------------------------------------------------------------------------------
                ///      0     1
                ///         x
                ///
                /// </summary>
                FatXOuter,

                /// <summary>
                /// Adds skirts at the top and right sides of the patch
                /// </summary>
                FatXZ,

                /// <summary>
                /// Adds skirts at the top and right sides of the patch and pushes them to horizon
                /// </summary>
                FatXZOuter,

                /// <summary>
                /// One less set of vertices in x direction
                /// </summary>
                SlimX,

                /// <summary>
                /// One less set of vertices in both x and z directions
                /// </summary>
                SlimXZ,

                /// <summary>
                /// One less set of vertices in x direction, extra vertices at start of z direction
                ///
                ///      ----
                ///      |  |
                ///    1 ----
                ///      |  |
                ///  z   ----
                ///      |  |
                ///    0 ----
                ///      0     1
                ///         x
                ///
                /// </summary>
                SlimXFatZ,

                /// <summary>
                /// Number of patch types
                /// </summary>
                Count,
            }

            // Instance Indices:
            // 00 01 02 03
            // 04       05
            // 06       07
            // 08 09 10 11
            //
            // First LOD has inside bit as well:
            // XX XX XX XX
            // XX 00 00 XX
            // XX 00 00 XX
            // XX XX XX XX
            static readonly Vector2[] s_Offsets =
            {
                new(-1.5f, +1.5f), new(-0.5f, +1.5f), new(+0.5f, +1.5f), new(+1.5f, +1.5f),
                new(-1.5f, +0.5f),                                       new(+1.5f, +0.5f),
                new(-1.5f, -0.5f),                                       new(+1.5f, -0.5f),
                new(-1.5f, -1.5f), new(-0.5f, -1.5f), new(+0.5f, -1.5f), new(+1.5f, -1.5f),
            };

            // Usually rings have an extra side of vertices that point inwards. The outermost
            // ring has both the inward vertices and also an additional outwards set of
            // vertices that go to the horizon.
            static readonly PatchType[] s_PatchTypes =
            {
                PatchType.SlimXFatZ, PatchType.SlimX, PatchType.SlimX, PatchType.SlimXZ,
                PatchType.FatX,                                        PatchType.SlimX,
                PatchType.FatX,                                        PatchType.SlimX,
                PatchType.FatXZ,     PatchType.FatX,  PatchType.FatX,  PatchType.FatXSlimZ,
            };

            // All interior - the "side" types have an extra skirt that points inwards - this
            // means that this inner most section does not need any skirting. This is good, as
            // this is the highest density part of the mesh.
            static readonly PatchType[] s_PatchTypesLastLod =
            {
                PatchType.FatXZOuter, PatchType.FatXOuter, PatchType.FatXOuter, PatchType.FatXZOuter,
                PatchType.FatXOuter,                                            PatchType.FatXOuter,
                PatchType.FatXOuter,                                            PatchType.FatXOuter,
                PatchType.FatXZOuter, PatchType.FatXOuter, PatchType.FatXOuter, PatchType.FatXZOuter,
            };

            static int s_SiblingIndex;

            static readonly List<Vector3> s_Vertices = new();
            static readonly List<Vector4> s_Tangents = new();
            static readonly List<Vector3> s_Normals = new();
            static readonly List<Vector4> s_UVs = new();
            static readonly List<int> s_Indices = new();

            public static void GenerateMesh(WaterRenderer water, SurfaceRenderer surface)
            {
                var lodCount = water.LodLevels;

                if (lodCount < 1)
                {
                    Debug.LogError("Crest: Invalid LOD count: " + lodCount.ToString(), water);
                    return;
                }

#if PROFILE_CONSTRUCTION
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
#endif

                s_SiblingIndex = 0;

                // Create mesh data.
                // 4 tiles across a LOD, and support lowering density by a factor.
                var tileResolution = (int)MathF.Round(0.25f * water.LodResolution / water.GeometryDownSampleFactor);
                for (var i = 0; i < (int)PatchType.Count; i++)
                {
                    surface._Meshes[i] = BuildPatch(water, (PatchType)i, tileResolution);
                }

                var parent = surface.Root.transform;

                for (var i = 0; i < lodCount; i++)
                {
                    CreateLOD(water, surface, i, parent);
                }

#if PROFILE_CONSTRUCTION
                sw.Stop();
                Debug.Log($"Crest: Finished generating {lodCount} LODs, time: {sw.Elapsed.TotalSeconds * 1000:.000}ms");
#endif
            }

            static Mesh BuildPatch(WaterRenderer water, PatchType type, int density)
            {
                s_Vertices.Clear();
                s_Indices.Clear();

                var vertices = s_Vertices;
                var indices = s_Indices;

                // Stick a bunch of vertices into a 1m x 1m patch (scaling happens later).
                var dx = (type == PatchType.Interior ? 4f : 1f) / density;

                // Vertices

                // See comments within PatchType for diagrams of each patch mesh.

                // Skirt widths on left, right, bottom and top (in order).
                var skirtMinusX = 0; var skirtPlusX = 0;
                var skirtMinusZ = 0; var skirtPlusZ = 0;

                // Set the patch size.
                switch (type)
                {
                    case PatchType.Fat: skirtMinusX = skirtPlusX = skirtMinusZ = skirtPlusZ = 1; break;
                    case PatchType.FatX:
                    case PatchType.FatXOuter: skirtPlusX = 1; break;
                    case PatchType.FatXZ:
                    case PatchType.FatXZOuter: skirtPlusX = skirtPlusZ = 1; break;
                    case PatchType.FatXSlimZ: skirtPlusX = 1; skirtPlusZ = -1; break;
                    case PatchType.SlimX: skirtPlusX = -1; break;
                    case PatchType.SlimXZ: skirtPlusX = skirtPlusZ = -1; break;
                    case PatchType.SlimXFatZ: skirtPlusX = -1; skirtPlusZ = 1; break;
                }

                var sideLengthVerticesX = 1 + density + skirtMinusX + skirtPlusX;
                var sideLengthVerticesZ = 1 + density + skirtMinusZ + skirtPlusZ;

                var startX = -0.5f - skirtMinusX * dx;
                var startZ = -0.5f - skirtMinusZ * dx;
                var endX = 0.5f + skirtPlusX * dx;
                var endZ = 0.5f + skirtPlusZ * dx;

                if (type is PatchType.Interior)
                {
                    sideLengthVerticesX *= 2;
                    sideLengthVerticesZ *= 2;
                    sideLengthVerticesX -= 1;
                    sideLengthVerticesZ -= 1;
                    startX *= 2f;
                    startZ *= 2f;
                    endX *= 2f;
                    endZ *= 2f;
                }

                // With a default value of 100, this will reach the horizon at all levels at
                // a far plane of 200k.
                var extentsMultiplier = water._ExtentsSizeMultiplier * (Lod.k_MaximumSlices + 1 - water.LodLevels);

                for (var j = 0; j < sideLengthVerticesZ; j++)
                {
                    // Interpolate Z across patch.
                    var z = Mathf.Lerp(startZ, endZ, j / (float)(sideLengthVerticesZ - 1));

                    // Push outermost edge out to horizon.
                    if (type == PatchType.FatXZOuter && j == sideLengthVerticesZ - 1)
                    {
                        z *= extentsMultiplier;
                    }

                    for (var i = 0; i < sideLengthVerticesX; i++)
                    {
                        // Interpolate X across patch.
                        var x = Mathf.Lerp(startX, endX, i / (float)(sideLengthVerticesX - 1));

                        // Push outermost edge out to horizon.
                        if (i == sideLengthVerticesX - 1 && (type is PatchType.FatXOuter or PatchType.FatXZOuter))
                        {
                            x *= extentsMultiplier;
                        }

                        // Could store something in y, although keep in mind this is a shared mesh that is
                        // shared across multiple LODs.
                        vertices.Add(new(x, 0f, z));
                    }
                }

                // Indices

                var sideLengthSquaresX = sideLengthVerticesX - 1;
                var sideLengthSquaresZ = sideLengthVerticesZ - 1;

                for (var j = 0; j < sideLengthSquaresZ; j++)
                {
                    for (var i = 0; i < sideLengthSquaresX; i++)
                    {
                        var flipEdge = false;

                        if (i % 2 == 1) flipEdge = !flipEdge;
                        if (j % 2 == 1) flipEdge = !flipEdge;

                        var i0 = i + j * (sideLengthSquaresX + 1);
                        var i1 = i0 + 1;
                        var i2 = i0 + (sideLengthSquaresX + 1);
                        var i3 = i2 + 1;

                        // Triangle 1
                        indices.Add(i3);
                        indices.Add(i1);
                        indices.Add(flipEdge ? i2 : i0);

                        // Triangle 2
                        indices.Add(i0);
                        indices.Add(i2);
                        indices.Add(flipEdge ? i1 : i3);
                    }
                }

                // Mesh

                var mesh = new Mesh();

                mesh.SetVertices(vertices);

                // HDRP needs full data. Do this on a define to keep door open to runtime changing of RP.
#if d_UnityHDRP
                s_Normals.Clear();
                for (var i = 0; i < vertices.Count; i++) s_Normals.Add(Vector3.up);
                mesh.SetNormals(s_Normals);

                s_Tangents.Clear();
                for (var i = 0; i < vertices.Count; i++) s_Tangents.Add(new Vector4(1, 0, 0, 1));
                mesh.SetTangents(s_Tangents);
#endif

                mesh.indexFormat = indices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;

                mesh.SetIndices(indices, MeshTopology.Triangles, 0);

                mesh.RecalculateBounds();

                // Increase snapping allowance (see #1148). Value was chosen by observation with a
                // custom debug mode to show pixels that were out of bounds.
                dx *= 3f;

                // Add a little allowance for snapping. In the chunk renderer script, the bounds
                // will be expanded further to allow for horizontal displacement.
                var bounds = mesh.bounds;
                bounds.extents = new(bounds.extents.x + dx, bounds.extents.y, bounds.extents.z + dx);
                mesh.bounds = bounds;
                mesh.name = type.ToString();

                return mesh;
            }

            static void CreateLOD(WaterRenderer water, SurfaceRenderer surface, int lodIndex, Transform parent)
            {
                var isBiggestLOD = lodIndex == water.LodLevels - 1;
                var generateSkirt = isBiggestLOD;

#if CREST_DEBUG
                generateSkirt = generateSkirt && !surface._Debug._DisableSkirt;
#endif

                Vector2[] offsets;
                PatchType[] patchTypes;

                var scale = MathF.Pow(2f, lodIndex);

                if (lodIndex == 0)
                {
                    CreateChunk(water, surface, parent, lodIndex, Vector2.zero, PatchType.Interior, scale);
                }

                offsets = s_Offsets;
                patchTypes = generateSkirt ? s_PatchTypesLastLod : s_PatchTypes;

#if CREST_DEBUG
                // Debug toggle to force all patches to be the same. They'll be made with a
                // surrounding skirt to make sure patches overlap.
                if (surface._Debug._UniformTiles)
                {
                    patchTypes = new PatchType[patchTypes.Length];
                    Array.Fill(patchTypes, PatchType.Fat);
                }
#endif

                // Create the water patches.
                for (var i = 0; i < offsets.Length; i++)
                {
                    CreateChunk(water, surface, parent, lodIndex, offsets[i], patchTypes[i], scale);
                }
            }

            static void CreateChunk(WaterRenderer water, SurfaceRenderer surface, Transform parent, int lodIndex, Vector2 offset, PatchType patchType, float scale)
            {
                var patch = surface._ChunkTemplate
                    ? Helpers.InstantiatePrefab(surface._ChunkTemplate)
                    : new();

                // Also applying the hide flags to the chunk will prevent it from being pickable in the editor.
                patch.hideFlags = water._Debug._ShowHiddenObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave;
                patch.name = $"Tile_L{lodIndex}_{patchType}";
                patch.layer = surface.Layer;
                patch.transform.parent = parent;
                var position = offset;
                patch.transform.localPosition = scale * position.XNZ();
                // Scale only horizontally, otherwise culling bounding box will be scaled up in y.
                patch.transform.localScale = new(scale, 1f, scale);

                if (!patch.TryGetComponent<MeshRenderer>(out var mr))
                {
                    mr = patch.AddComponent<MeshRenderer>();
                    // I don't think one would use light probes for a purely specular water surface? (although diffuse
                    // foam shading would benefit).
                    mr.lightProbeUsage = LightProbeUsage.Off;
                }

                var order = -water.LodLevels + (patchType == PatchType.Interior ? -1 : lodIndex);

                if (lodIndex == water.LodLevels - 1)
                {
                    order += 1;
                }

                var chunk = patch.AddComponent<WaterChunkRenderer>();
                var mesh = surface._Meshes[(int)patchType];

                {
                    patch.AddComponent<MeshFilter>().sharedMesh = mesh;

                    chunk._Water = water;
                    chunk._SortingOrder = order;
                    chunk._SiblingIndex = s_SiblingIndex++;

                    chunk.Initialize(lodIndex, mr, mesh);

                    // When custom rendering, we loop over chunks to render, which means these need to
                    // be optimally sorted. We statically sort by LOD. Sub-sort is only done for LOD0,
                    // where interior tiles are placed first. Further sorting must be done dynamically.
                    surface.Chunks.Add(chunk);

                    chunk._DrawRenderBounds = water.Surface._Debug._DrawRendererBounds;
                }

                // Sorting order to stop unity drawing it back to front. Make the innermost four tiles draw first,
                // followed by the rest of the tiles by LOD index.
                if (RenderPipelineHelper.IsHighDefinition)
                {
                    // HDRP has a different rendering priority system:
                    // https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.10/manual/Renderer-And-Material-Priority.html#sorting-by-renderer
                    mr.rendererPriority = order;
                }
                else if (!water.Surface.AllowRenderQueueSorting)
                {
                    // Sorting order to stop unity drawing it back to front. make the innermost 4 tiles draw first, followed by
                    // the rest of the tiles by LOD index. all this happens before layer 0 - the sorting layer takes priority over the
                    // render queue it seems! ( https://cdry.wordpress.com/2017/04/28/unity-render-queues-vs-sorting-layers/ ). This pushes
                    // water rendering way early, so transparent objects will by default render afterwards, which is typical for water rendering.
                    mr.sortingOrder = order;
                }

                mr.shadowCastingMode = water.Surface.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

                // This setting is ignored by Unity for the transparent water shader.
                mr.receiveShadows = false;

                mr.motionVectorGenerationMode = !water.WriteMotionVectors
                    ? MotionVectorGenerationMode.ForceNoMotion
                    : MotionVectorGenerationMode.Object;

                mr.material = water.Surface.Material;

                OnCreateChunkRenderer?.Invoke(mr);

                // Rotate side patches to point the +x side outwards.
                var rotateXOutwards = patchType is PatchType.FatX or PatchType.FatXOuter or PatchType.SlimX or PatchType.SlimXFatZ;
                if (rotateXOutwards)
                {
                    if (MathF.Abs(position.y) >= MathF.Abs(position.x))
                        patch.transform.localEulerAngles = 90f * MathF.Sign(position.y) * -Vector3.up;
                    else
                        patch.transform.localEulerAngles = position.x < 0f ? Vector3.up * 180f : Vector3.zero;
                }

                // Rotate the corner patches so the +x and +z sides point outwards.
                var rotateXZOutwards = patchType is PatchType.FatXZ or PatchType.SlimXZ or PatchType.FatXSlimZ or PatchType.FatXZOuter;
                if (rotateXZOutwards)
                {
                    // XZ direction before rotation
                    var from = new Vector3(1f, 0f, 1f).normalized;
                    // Target XZ direction is outwards vector given by local patch position - assumes this patch is a corner (checked below).
                    var to = patch.transform.localPosition.normalized;
                    if (MathF.Abs(patch.transform.localPosition.x) < 0.0001f || MathF.Abs(MathF.Abs(patch.transform.localPosition.x) - MathF.Abs(patch.transform.localPosition.z)) > 0.001f)
                    {
                        Debug.LogWarning("Crest: Skipped rotating a patch because it isn't a corner, click here to highlight.", patch);
                        return;
                    }

                    // Detect 180 degree rotations as it doesn't always rotate around Y.
                    if (Vector3.Dot(from, to) < -0.99f)
                        patch.transform.localEulerAngles = Vector3.up * 180f;
                    else
                        patch.transform.localRotation = Quaternion.FromToRotation(from, to);
                }

                // Pre-rotate bounds.
                {
                    var bounds = mesh.bounds;
                    bounds = bounds.Rotate(chunk.Transform.rotation);
                    chunk._LocalBounds = bounds;
                    chunk._LocalScale = chunk.Transform.localScale.x;
                }
            }
        }
    }
}
