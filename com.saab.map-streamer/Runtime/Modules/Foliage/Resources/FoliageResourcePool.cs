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

namespace Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Resources
{
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Configuration;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Rendering;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Runtime;
using Saab.Foundation.Unity.MapStreamer.Modules.FoliageRuntime.Terrain;

    internal sealed class FoliageResourcePool : IDisposable
    {
        private readonly long _maximumBytes;
        private readonly Stack<ComputeBuffer> _buffers =
            new Stack<ComputeBuffer>();
        private long _pooledBytes;

        public FoliageResourcePool(long maximumBytes)
        {
            _maximumBytes = Math.Max(0, maximumBytes);
        }

        internal long PooledBytes => _pooledBytes;

        public ComputeBuffer Rent(int count, int stride)
        {
            ComputeBuffer match = null;
            var rejected = new Stack<ComputeBuffer>();
            while (_buffers.Count > 0)
            {
                var candidate = _buffers.Pop();
                _pooledBytes -= (long)candidate.count * candidate.stride;
                if (match == null &&
                    candidate.count == count &&
                    candidate.stride == stride)
                {
                    match = candidate;
                    break;
                }
                rejected.Push(candidate);
            }
            while (rejected.Count > 0)
            {
                var candidate = rejected.Pop();
                _buffers.Push(candidate);
                _pooledBytes +=
                    (long)candidate.count * candidate.stride;
            }
            return match ?? new ComputeBuffer(count, stride);
        }

        public void Return(ComputeBuffer buffer)
        {
            if (buffer == null)
                return;
            var bytes = (long)buffer.count * buffer.stride;
            if (_pooledBytes + bytes > _maximumBytes)
            {
                buffer.Release();
                return;
            }
            _buffers.Push(buffer);
            _pooledBytes += bytes;
        }

        public void Dispose()
        {
            while (_buffers.Count > 0)
                _buffers.Pop().Release();
            _pooledBytes = 0;
        }
    }

}
