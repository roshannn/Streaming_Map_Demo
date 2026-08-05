using System;
using System.Collections.Generic;
using System.Linq;

using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;

using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer.Runtime;
using Saab.Utility.GfxCaps;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageRenderPass
    {
        public void Draw(
            Material material,
            ComputeBuffer points,
            ComputeBuffer indirect,
            float drawDistance,
            Vector3 boundsCenter,
            int layer,
            bool shadows)
        {
            material.SetBuffer("_PointBuffer", points);
            ComputeBuffer.CopyCount(points, indirect, 0);
            var size = new Vector3(
                drawDistance * 2f,
                drawDistance * 2f,
                drawDistance * 2f);
            Graphics.DrawProceduralIndirect(
                material,
                new Bounds(boundsCenter, size),
                MeshTopology.Points,
                indirect,
                0,
                null,
                null,
                shadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off,
                true,
                layer);
        }
    }

}
