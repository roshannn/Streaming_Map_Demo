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

    internal sealed class FoliageTerrainRegistry : IDisposable
    {
        private readonly Dictionary<TerrainModuleIdentity, FoliageTerrainState>
            _states =
                new Dictionary<TerrainModuleIdentity, FoliageTerrainState>();
        private readonly List<TerrainModuleIdentity> _order =
            new List<TerrainModuleIdentity>();

        public int Count => _states.Count;
        public bool Contains(TerrainModuleIdentity identity) =>
            _states.ContainsKey(identity);

        public void Add(FoliageTerrainState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            _states.Add(state.Identity, state);
            _order.Add(state.Identity);
        }

        public void Remove(TerrainModuleIdentity identity)
        {
            if (!_states.TryGetValue(identity, out var state))
                return;
            _states.Remove(identity);
            _order.Remove(identity);
            state.Dispose();
        }

        public void Dispose()
        {
            for (var index = _order.Count - 1; index >= 0; --index)
            {
                var identity = _order[index];
                if (_states.TryGetValue(identity, out var state))
                    state.Dispose();
            }
            _states.Clear();
            _order.Clear();
        }
    }

}
