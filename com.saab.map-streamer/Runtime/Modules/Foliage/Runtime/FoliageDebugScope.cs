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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageDebugScope : IDisposable
    {
        private readonly NativeLeakDetectionMode _previous;

        public FoliageDebugScope(bool enabled)
        {
            _previous = UnsafeUtility.GetLeakDetectionMode();
            UnsafeUtility.SetLeakDetectionMode(
                enabled
                    ? NativeLeakDetectionMode.EnabledWithStackTrace
                    : NativeLeakDetectionMode.Disabled);
        }

        public void Dispose() =>
            UnsafeUtility.SetLeakDetectionMode(_previous);
    }

}
