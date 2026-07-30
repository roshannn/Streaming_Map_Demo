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

    internal sealed class FoliageOcclusionPass : IDisposable
    {
        private RenderTexture _depth;

        public RenderTexture Execute(int downscale, Material material)
        {
            if (Shader.GetGlobalTexture("_CameraDepthTexture") == null ||
                material == null)
                return null;
            var width = Mathf.Max(1, Screen.width / downscale);
            var height = Mathf.Max(1, Screen.height / downscale);
            if (_depth == null ||
                _depth.width != width ||
                _depth.height != height)
            {
                Dispose();
                _depth = new RenderTexture(
                    width,
                    height,
                    0,
                    RenderTextureFormat.RFloat)
                {
                    name = "Foliage Occlusion Depth",
                    filterMode = FilterMode.Point,
                    useMipMap = false
                };
                _depth.Create();
            }
            Graphics.Blit(null, _depth, material);
            material.mainTexture = _depth;
            return _depth;
        }

        public void Dispose()
        {
            if (_depth == null)
                return;
            _depth.Release();
            UnityEngine.Object.Destroy(_depth);
            _depth = null;
        }
    }

}
