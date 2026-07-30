using System;
using System.Collections.Generic;
using System.Linq;

using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Utils;

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Runtime
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.TerrainShading.Resources;

    internal sealed class TerrainShadingState : IDisposable
    {
        private readonly MeshRenderer _renderer;
        private readonly Material _original;
        private Material _owned;
        private ComputeBuffer _normalBuffer;

        public TerrainShadingState(
            MeshRenderer renderer,
            Material original,
            Material owned,
            ComputeBuffer normalBuffer)
        {
            _renderer = renderer;
            _original = original;
            _owned = owned;
            _normalBuffer = normalBuffer;
        }

        public void Dispose()
        {
            if (_renderer != null &&
                _renderer.sharedMaterial == _owned)
                _renderer.sharedMaterial = _original;
            _normalBuffer?.Release();
            _normalBuffer = null;
            if (_owned != null)
                UnityEngine.Object.Destroy(_owned);
            _owned = null;
        }
    }

}
