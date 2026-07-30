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
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageCullingPass
    {
        private readonly Plane[] _planes = new Plane[6];
        private readonly Vector4[] _shaderPlanes = new Vector4[6];

        public Vector4[] GetFrustum(Camera camera, float drawDistance)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            for (var index = 0; index < _planes.Length; ++index)
            {
                var normal = _planes[index].normal;
                _shaderPlanes[index] = new Vector4(
                    normal.x,
                    normal.y,
                    normal.z,
                    _planes[index].distance);
            }
            _shaderPlanes[5].w = drawDistance;
            return _shaderPlanes;
        }

        public static float CalculateDrawDistance(
            Camera camera,
            float objectHeight,
            float coverage) =>
            objectHeight /
            (2f * Mathf.Tan(
                camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * coverage);
    }

}
