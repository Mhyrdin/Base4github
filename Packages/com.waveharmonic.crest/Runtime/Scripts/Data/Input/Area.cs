// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A local area to override water data.
    /// </summary>
    [@ExecuteDuringEditMode]
    [@AddComponentMenu(Constants.k_MenuPrefixInputs + "Area")]
    public sealed partial class Area : SharedMeshComponent
    {
        private protected override LodInputMode InputMode => LodInputMode.Area;
        private protected override bool AlwaysNeedsCulling => true;

        static readonly List<Vector4> s_Data0 = new(4);
        static readonly Vector4[] s_Data1 = new Vector4[4];
        static readonly Vector4[] s_Data2 = new Vector4[4];
        static readonly Vector4[] s_Data3 = new Vector4[4];

        private protected override void Initialize()
        {
            base.Initialize();

            if (_Mesh == null)
            {
                _Mesh = Instantiate(Helpers.QuadMesh);

                var vertices = _Mesh.vertices;

                var rotation = Quaternion.Euler(90, 0, 0);

                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = rotation * vertices[i];
                }

                _Mesh.vertices = vertices;

                UpdateSharedMesh();

                _Mesh.RecalculateNormals();
                _Mesh.RecalculateBounds();
                _Mesh.RecalculateTangents();
            }
        }

        internal override void RecalculateCulling()
        {
            // TODO: only calculate rect if bounds not needed. Bounds is used for height
            // reporting which means checking if valid level input exists. Another time.
            var scale = Transform.lossyScale.XZ();
            scale = Helpers.RotateAndEncapsulateXZ(scale, Transform.rotation.eulerAngles.y);
            Bounds = new(Transform.position, scale.XNZ());
            Rect = Bounds.RectXZ();
        }

        private protected override void UpdateMesh(WaterRenderer water)
        {
            base.UpdateMesh(water);

            if (!ShouldRenderMaximumLOD)
            {
                return;
            }

            var mesh = _Mesh;
            var data1 = Vector4.zero;
            var data2 = Vector4.zero;
            var data3 = Vector4.zero;

            if (_HasFlow) data1.z = _FlowLodInput.GetData<AreaLodInputData>().Value.XY().magnitude;
            if (_HasAbsorption) data2 = _AbsorptionLodInput.GetData<AreaLodInputData>().Value;
            if (_HasScattering) data3 = _ScatteringLodInput.GetData<AreaLodInputData>().Value;

            // Alpha blending.
            if (_HasAbsorption) data2.w = 1f;
            if (_HasScattering) data3.w = 1f;

            if (_HasFlow)
            {
                var flow = _FlowLodInput.GetData<AreaLodInputData>().Value.XY();
                var direction = flow.normalized;
                data1.z = flow.magnitude;

                mesh.GetUVs(0, s_Data0);

                for (var i = 0; i < 4; i++)
                {
                    var uv = s_Data0[i];
                    uv.z = direction.x;
                    uv.w = direction.y;
                    s_Data0[i] = uv;
                }

                mesh.SetUVs(0, s_Data0);
            }

            System.Array.Fill(s_Data1, data1);
            System.Array.Fill(s_Data2, data2);
            System.Array.Fill(s_Data3, data3);

            mesh.SetUVs(1, s_Data1, 0, 4);
            mesh.SetUVs(2, s_Data2, 0, 4);
            mesh.SetUVs(3, s_Data3, 0, 4);
        }
    }
}
