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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageTerrainState : IDisposable
    {
        private readonly List<FoliageFeatureRuntime> _placements;
        private bool _disposed;

        public FoliageTerrainState(
            TerrainModuleIdentity identity,
            List<FoliageFeatureRuntime> placements)
        {
            Identity = identity;
            _placements = placements;
        }

        public TerrainModuleIdentity Identity { get; }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var feature in _placements)
                feature.Placement?.RemoveFoliage(Identity);
            _placements.Clear();
        }
    }

}
